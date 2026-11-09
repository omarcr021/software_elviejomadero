using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class EmployeeService : IEmployeeService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmployeeService> _logger;

        private static readonly string[] ValidRoles =
        [
            "Administrador",
            "Mozo",
            "Cocinero",
            "Recepcionista",
            "Repartidor"
        ];

        public EmployeeService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ILogger<EmployeeService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        public string[] GetAvailableRoles() => ValidRoles;

        public async Task<EmployeeListViewModel> GetEmployeesAsync(string? roleFilter = null)
        {
            var query = _userManager.Users.AsNoTracking();

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var employeeItems = new List<EmployeeItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var primaryRole = roles.FirstOrDefault() ?? "Sin Rol";

                if (string.IsNullOrEmpty(roleFilter) || primaryRole.Equals(roleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    employeeItems.Add(new EmployeeItemViewModel
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        DNI = user.DNI,
                        Phone = user.Phone,
                        UserName = user.UserName ?? string.Empty,
                        Role = primaryRole,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt,
                        LastLoginAt = user.LastLoginAt
                    });
                }
            }

            return new EmployeeListViewModel
            {
                Employees = employeeItems,
                TotalCount = users.Count,
                SelectedRole = roleFilter,
                AvailableRoles = ValidRoles.ToList()
            };
        }

        public async Task<(bool Success, string? ErrorMessage, string? GeneratedUserName)> RegisterEmployeeAsync(CreateEmployeeViewModel model)
        {
            // 1. Validar campos requeridos
            if (string.IsNullOrWhiteSpace(model.FullName))
                return (false, "El nombre completo es obligatorio.", null);

            if (string.IsNullOrWhiteSpace(model.DNI) || !Regex.IsMatch(model.DNI.Trim(), @"^\d{8}$"))
                return (false, "El DNI debe tener exactamente 8 dígitos numéricos.", null);

            if (string.IsNullOrWhiteSpace(model.Phone) || !Regex.IsMatch(model.Phone.Trim(), @"^\d{9}$"))
                return (false, "El teléfono debe tener exactamente 9 dígitos numéricos.", null);

            if (string.IsNullOrWhiteSpace(model.Role) || !ValidRoles.Contains(model.Role))
                return (false, "Debe seleccionar un rol válido para el colaborador.", null);

            // 2. Validar unicidad de DNI
            var dniTrimmed = model.DNI.Trim();
            var dniExists = await _context.Users.AnyAsync(u => u.DNI == dniTrimmed);
            if (dniExists)
            {
                return (false, "El DNI ya se encuentra registrado.", null);
            }

            // 3. Generar nombre de usuario único automático
            var generatedUserName = await GenerateUniqueUserNameAsync(model.FullName);

            // 4. Crear entidad ApplicationUser
            var user = new ApplicationUser
            {
                UserName = generatedUserName,
                FullName = model.FullName.Trim(),
                DNI = dniTrimmed,
                Phone = model.Phone.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var password = string.IsNullOrWhiteSpace(model.InitialPassword) ? "Madero2026*" : model.InitialPassword;
            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                var error = createResult.Errors.FirstOrDefault()?.Description ?? "Error al registrar el empleado.";
                _logger.LogError("Error al crear usuario Identity: {Error}", error);
                return (false, error, null);
            }

            // 5. Asignar rol único de Identity
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Error al asignar rol {Role} al usuario {User}", model.Role, user.UserName);
            }

            _logger.LogInformation("Empleado {FullName} ({UserName}) registrado con rol {Role}",
                user.FullName, user.UserName, model.Role);

            return (true, null, generatedUserName);
        }

        // HU-18 CA-01: Obtener datos del empleado para edición
        public async Task<EditEmployeeViewModel?> GetEmployeeForEditAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? string.Empty;

            return new EditEmployeeViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                DNI = user.DNI,
                Phone = user.Phone,
                UserName = user.UserName ?? string.Empty,
                Role = primaryRole,
                IsActive = user.IsActive
            };
        }

        // HU-18 CA-02 & CA-03: Actualizar datos y rol del empleado
        public async Task<(bool Success, string? ErrorMessage)> UpdateEmployeeAsync(EditEmployeeViewModel model, string currentAdminId)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
                return (false, "Identificador de empleado no válido.");

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return (false, "El empleado no fue encontrado.");

            // Detalle 5: El administrador no puede desactivarse a sí mismo
            if (!string.IsNullOrEmpty(currentAdminId) && user.Id == currentAdminId && !model.IsActive)
                return (false, "El administrador no puede desactivarse a sí mismo.");

            // 1. Validaciones de formato (HU-17)
            if (string.IsNullOrWhiteSpace(model.FullName))
                return (false, "El nombre completo es obligatorio.");

            if (string.IsNullOrWhiteSpace(model.DNI) || !Regex.IsMatch(model.DNI.Trim(), @"^\d{8}$"))
                return (false, "El DNI debe tener exactamente 8 dígitos numéricos.");

            if (string.IsNullOrWhiteSpace(model.Phone) || !Regex.IsMatch(model.Phone.Trim(), @"^\d{9}$"))
                return (false, "El teléfono debe tener exactamente 9 dígitos numéricos.");

            if (string.IsNullOrWhiteSpace(model.Role) || !ValidRoles.Contains(model.Role))
                return (false, "Debe seleccionar un rol válido para el colaborador.");

            // 2. Validar que DNI no pertenezca a otro usuario
            var dniTrimmed = model.DNI.Trim();
            var dniExists = await _context.Users.AnyAsync(u => u.DNI == dniTrimmed && u.Id != user.Id);
            if (dniExists)
                return (false, "El DNI ya se encuentra registrado por otro colaborador.");

            // 3. Actualizar datos básicos
            user.FullName = model.FullName.Trim();
            user.DNI = dniTrimmed;
            user.Phone = model.Phone.Trim();
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var error = updateResult.Errors.FirstOrDefault()?.Description ?? "Error al actualizar los datos del empleado.";
                _logger.LogError("Error al actualizar usuario {Id}: {Error}", user.Id, error);
                return (false, error);
            }

            // 4. Cambiar rol si es necesario (CA-03)
            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault();
            if (currentRole != model.Role)
            {
                if (currentRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }
                var addRoleResult = await _userManager.AddToRoleAsync(user, model.Role);
                if (!addRoleResult.Succeeded)
                {
                    _logger.LogWarning("No se pudo asignar el nuevo rol {Role} al usuario {Id}", model.Role, user.Id);
                }
            }

            // 5. Actualizar contraseña solo si se proporcionó una nueva (Detalle 3)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                if (model.NewPassword.Length < 6)
                    return (false, "La nueva contraseña debe tener al menos 6 caracteres.");

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
                if (!passResult.Succeeded)
                {
                    var passError = passResult.Errors.FirstOrDefault()?.Description ?? "Error al actualizar la contraseña.";
                    return (false, passError);
                }
            }

            _logger.LogInformation("Empleado {FullName} ({UserName}) actualizado correctamente por admin {AdminId}.",
                user.FullName, user.UserName, currentAdminId);

            return (true, null);
        }

        // HU-18 CA-04: Desactivar o activar empleado
        public async Task<(bool Success, string? ErrorMessage, bool NewStatus)> ToggleEmployeeStatusAsync(string id, string currentAdminId)
        {
            if (string.IsNullOrWhiteSpace(id))
                return (false, "Identificador no válido.", false);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return (false, "El empleado no fue encontrado.", false);

            // Detalle 5: El administrador no puede desactivarse a sí mismo
            if (!string.IsNullOrEmpty(currentAdminId) && user.Id == currentAdminId)
                return (false, "El administrador no puede desactivarse a sí mismo.", user.IsActive);

            user.IsActive = !user.IsActive;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, "No se pudo cambiar el estado del empleado.", !user.IsActive);
            }

            _logger.LogInformation("Estado del empleado {UserName} cambiado a {Status} por admin {AdminId}",
                user.UserName, user.IsActive ? "Activo" : "Inactivo", currentAdminId);

            return (true, null, user.IsActive);
        }

        // HU-18 CA-05: Eliminar empleado (preservando pedidos históricos según Detalle 4)
        public async Task<(bool Success, string? ErrorMessage)> DeleteEmployeeAsync(string id, string currentAdminId)
        {
            if (string.IsNullOrWhiteSpace(id))
                return (false, "Identificador no válido.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return (false, "El empleado no fue encontrado.");

            // Detalle 5: El administrador no puede eliminarse a sí mismo
            if (!string.IsNullOrEmpty(currentAdminId) && user.Id == currentAdminId)
                return (false, "El administrador no puede eliminarse a sí mismo.");

            // Detalle 4: Un empleado eliminado no afecta a los pedidos que registró.
            // Si el usuario tiene pedidos donde figure como DeliveryDriverId o PreparedByUserId, desvincularlos si aplica.
            // Si figura como ReceptionistId (requerido), deshabilitamos temporalmente o eliminamos directamente.
            try
            {
                // Limpiar llaves foráneas opcionales si las hubiera
                var driverOrders = await _context.Orders.Where(o => o.DeliveryDriverId == user.Id).ToListAsync();
                foreach (var ord in driverOrders) ord.DeliveryDriverId = null;

                var preparedOrders = await _context.Orders.Where(o => o.PreparedByUserId == user.Id).ToListAsync();
                foreach (var ord in preparedOrders) ord.PreparedByUserId = null;

                if (driverOrders.Any() || preparedOrders.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Si tiene pedidos como ReceptionistId (FK requerida en EF), reasignar a un usuario del sistema o eliminar usuario sin borrar pedidos
                var receptionistOrders = await _context.Orders.Where(o => o.ReceptionistId == user.Id).ToListAsync();
                if (receptionistOrders.Any())
                {
                    // Reasignar el ReceptionistId al admin actual para preservar íntegro el historial de pedidos
                    if (!string.IsNullOrEmpty(currentAdminId))
                    {
                        foreach (var ro in receptionistOrders)
                        {
                            ro.ReceptionistId = currentAdminId;
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    var error = deleteResult.Errors.FirstOrDefault()?.Description ?? "Error al eliminar el empleado.";
                    _logger.LogError("Error al eliminar usuario {Id}: {Error}", user.Id, error);
                    return (false, error);
                }

                _logger.LogInformation("Empleado {FullName} ({UserName}) eliminado por admin {AdminId}.",
                    user.FullName, user.UserName, currentAdminId);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al eliminar el colaborador {Id}", user.Id);
                return (false, "No se pudo eliminar el empleado debido a un error del sistema.");
            }
        }

        public async Task<string> GenerateUniqueUserNameAsync(string fullName)
        {
            var baseUserName = BuildBaseUserName(fullName);
            var candidate = baseUserName;
            var counter = 2;

            while (await _userManager.FindByNameAsync(candidate) != null)
            {
                candidate = $"{baseUserName}{counter}";
                counter++;
            }

            return candidate;
        }

        private static string BuildBaseUserName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "usuario";

            // Limpiar y separar palabras
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "usuario";

            // Primera letra del primer nombre + primer apellido
            var firstName = RemoveDiacritics(parts[0].ToLowerInvariant());
            var firstLetter = firstName.Length > 0 ? firstName[0].ToString() : "u";

            if (parts.Length > 1)
            {
                var lastName = RemoveDiacritics(parts[1].ToLowerInvariant());
                // Remover cualquier carácter no alfanumérico
                lastName = Regex.Replace(lastName, @"[^a-z0-9]", "");
                if (!string.IsNullOrEmpty(lastName))
                {
                    return $"{firstLetter}{lastName}";
                }
            }

            // Si solo puso una sola palabra
            firstName = Regex.Replace(firstName, @"[^a-z0-9]", "");
            return !string.IsNullOrEmpty(firstName) ? firstName : "usuario";
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC)
                .Replace("ñ", "n")
                .Replace("Ñ", "N");
        }
    }
}
