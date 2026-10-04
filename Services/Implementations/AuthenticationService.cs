using Microsoft.AspNetCore.Identity;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;

namespace software_elviejomadero.Services.Implementations
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AuthenticationService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<LoginServiceResult> LoginAsync(string userName, string password, bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginServiceResult
                {
                    Status = LoginResultStatus.InvalidCredentials,
                    ErrorMessage = "Usuario o contraseña incorrectos."
                };
            }

            var user = await _userManager.FindByNameAsync(userName.Trim());

            // 1. Si el usuario no existe, retornar error genérico para no filtrar existencia
            if (user == null)
            {
                _logger.LogWarning("Intento de login con usuario inexistente: {UserName}", userName);
                return new LoginServiceResult
                {
                    Status = LoginResultStatus.InvalidCredentials,
                    ErrorMessage = "Usuario o contraseña incorrectos."
                };
            }

            // 2. Si el usuario no está activo, bloquear acceso de inmediato
            if (!user.IsActive)
            {
                _logger.LogWarning("Intento de login con usuario inactivo: {UserName}", userName);
                return new LoginServiceResult
                {
                    Status = LoginResultStatus.InactiveUser,
                    ErrorMessage = "El usuario no está activo."
                };
            }

            // 3. Validar contraseña mediante SignInManager
            var checkPassword = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
            if (!checkPassword.Succeeded)
            {
                _logger.LogWarning("Contraseña incorrecta para el usuario: {UserName}", userName);
                return new LoginServiceResult
                {
                    Status = LoginResultStatus.InvalidCredentials,
                    ErrorMessage = "Usuario o contraseña incorrectos."
                };
            }

            // 4. Iniciar sesión por Cookies
            await _signInManager.SignInAsync(user, isPersistent: rememberMe);

            // 5. Registrar fecha y hora de último acceso
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogInformation("Usuario autenticado exitosamente: {UserName} con roles: {Roles}",
                user.UserName, string.Join(", ", roles));

            return new LoginServiceResult
            {
                Status = LoginResultStatus.Success,
                User = user,
                Roles = roles
            };
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}
