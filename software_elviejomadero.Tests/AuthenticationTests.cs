using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using software_elviejomadero.Controllers;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using AppAuthService = software_elviejomadero.Services.Implementations.AuthenticationService;
using Xunit;

namespace software_elviejomadero.Tests
{
    public class AuthenticationTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<ILogger<AppAuthService>> _mockLogger;
        private readonly AppAuthService _authService;

        public AuthenticationTests()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<IPasswordHasher<ApplicationUser>>().Object,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new Mock<ILookupNormalizer>().Object,
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                _mockUserManager.Object,
                contextAccessor.Object,
                claimsFactory.Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
                new Mock<IAuthenticationSchemeProvider>().Object,
                new Mock<IUserConfirmation<ApplicationUser>>().Object);

            _mockLogger = new Mock<ILogger<AppAuthService>>();

            _authService = new AppAuthService(
                _mockUserManager.Object,
                _mockSignInManager.Object,
                _mockLogger.Object);
        }

        // 1. Login exitoso
        [Fact]
        public async Task LoginAsync_ValidCredentialsAndActiveUser_ReturnsSuccessAndUpdatesLastLogin()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "admin",
                FullName = "Administrador General",
                IsActive = true
            };

            _mockUserManager.Setup(m => m.FindByNameAsync("admin"))
                .ReturnsAsync(user);

            _mockSignInManager.Setup(m => m.CheckPasswordSignInAsync(user, "Password123*", false))
                .ReturnsAsync(SignInResult.Success);

            _mockUserManager.Setup(m => m.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Administrador" });

            // Act
            var result = await _authService.LoginAsync("admin", "Password123*", rememberMe: false);

            // Assert
            Assert.Equal(LoginResultStatus.Success, result.Status);
            Assert.NotNull(result.User);
            Assert.NotNull(result.User.LastLoginAt);
            Assert.Contains("Administrador", result.Roles);
            _mockSignInManager.Verify(m => m.SignInAsync(user, false, null), Times.Once);
        }

        // 2. Login con contraseña incorrecta
        [Fact]
        public async Task LoginAsync_IncorrectPassword_ReturnsInvalidCredentialsGenericMessage()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "admin",
                IsActive = true
            };

            _mockUserManager.Setup(m => m.FindByNameAsync("admin"))
                .ReturnsAsync(user);

            _mockSignInManager.Setup(m => m.CheckPasswordSignInAsync(user, "WrongPassword", false))
                .ReturnsAsync(SignInResult.Failed);

            // Act
            var result = await _authService.LoginAsync("admin", "WrongPassword", false);

            // Assert
            Assert.Equal(LoginResultStatus.InvalidCredentials, result.Status);
            Assert.Equal("Usuario o contraseña incorrectos.", result.ErrorMessage);
            _mockSignInManager.Verify(m => m.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), null), Times.Never);
        }

        // 3. Login con usuario inexistente
        [Fact]
        public async Task LoginAsync_NonExistentUser_ReturnsInvalidCredentialsGenericMessage()
        {
            // Arrange
            _mockUserManager.Setup(m => m.FindByNameAsync("nonexistent"))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _authService.LoginAsync("nonexistent", "SomePass123", false);

            // Assert
            Assert.Equal(LoginResultStatus.InvalidCredentials, result.Status);
            Assert.Equal("Usuario o contraseña incorrectos.", result.ErrorMessage);
        }

        // 4. Usuario inactivo no puede iniciar sesión
        [Fact]
        public async Task LoginAsync_InactiveUser_BlocksAccessWithInactiveMessage()
        {
            // Arrange
            var inactiveUser = new ApplicationUser
            {
                UserName = "mlopez",
                IsActive = false
            };

            _mockUserManager.Setup(m => m.FindByNameAsync("mlopez"))
                .ReturnsAsync(inactiveUser);

            // Act
            var result = await _authService.LoginAsync("mlopez", "Password123*", false);

            // Assert
            Assert.Equal(LoginResultStatus.InactiveUser, result.Status);
            Assert.Equal("El usuario no está activo.", result.ErrorMessage);
            _mockSignInManager.Verify(m => m.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), null), Times.Never);
        }

        // 5. Usuario autenticado obtiene su rol
        [Fact]
        public async Task LoginAsync_AuthenticatedUser_RetrievesCorrectAssignedRole()
        {
            // Arrange
            var user = new ApplicationUser
            {
                UserName = "jgarcia",
                IsActive = true
            };

            _mockUserManager.Setup(m => m.FindByNameAsync("jgarcia"))
                .ReturnsAsync(user);

            _mockSignInManager.Setup(m => m.CheckPasswordSignInAsync(user, "Password123*", false))
                .ReturnsAsync(SignInResult.Success);

            _mockUserManager.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Mozo" });

            // Act
            var result = await _authService.LoginAsync("jgarcia", "Password123*", false);

            // Assert
            Assert.Equal(LoginResultStatus.Success, result.Status);
            Assert.Single(result.Roles);
            Assert.Equal("Mozo", result.Roles.First());
        }

        // 6. Usuario no administrador no puede acceder a Administración (Protección con Authorize)
        [Fact]
        public void AdministrationController_RequiresAdministradorRoleAttribute()
        {
            // Act
            var authorizeAttribute = typeof(AdministrationController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .FirstOrDefault() as AuthorizeAttribute;

            // Assert
            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Administrador", authorizeAttribute.Roles);
        }
    }
}
