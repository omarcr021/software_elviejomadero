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
