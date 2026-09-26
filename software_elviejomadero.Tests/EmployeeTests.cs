using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    public class EmployeeTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private Mock<UserManager<ApplicationUser>> GetMockUserManager()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                userStore.Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<IPasswordHasher<ApplicationUser>>().Object,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new Mock<ILookupNormalizer>().Object,
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                new Mock<ILogger<UserManager<ApplicationUser>>>().Object);
        }

        private Mock<RoleManager<IdentityRole>> GetMockRoleManager()
        {
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            return new Mock<RoleManager<IdentityRole>>(
                roleStore.Object,
                Array.Empty<IRoleValidator<IdentityRole>>(),
                new Mock<ILookupNormalizer>().Object,
                new IdentityErrorDescriber(),
                new Mock<ILogger<RoleManager<IdentityRole>>>().Object);
        }

        // 7. Registrar empleado correctamente
        [Fact]
        public async Task RegisterEmployeeAsync_ValidData_CreatesUserAndAssignsRole()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();
            var mockRoleManager = GetMockRoleManager();
            var mockLogger = new Mock<ILogger<EmployeeService>>();

            mockUserManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Mozo"))
                .ReturnsAsync(IdentityResult.Success);

            var service = new EmployeeService(mockUserManager.Object, mockRoleManager.Object, context, mockLogger.Object);

            var model = new CreateEmployeeViewModel
            {
                FullName = "Carlos Mendoza Ruiz",
                DNI = "12345678",
                Phone = "987654321",
                Role = "Mozo",
                InitialPassword = "Password123*",
                IsActive = true
            };

            // Act
            var result = await service.RegisterEmployeeAsync(model);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("cmendoza", result.GeneratedUserName);
            mockUserManager.Verify(m => m.CreateAsync(It.Is<ApplicationUser>(u => u.DNI == "12345678" && u.Phone == "987654321"), "Password123*"), Times.Once);
            mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Mozo"), Times.Once);
        }

        // 8. DNI duplicado rechazado
        [Fact]
        public async Task RegisterEmployeeAsync_DuplicateDNI_ReturnsError()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            context.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "existing",
                FullName = "Existente",
                DNI = "87654321",
                Phone = "911222333"
            });
            await context.SaveChangesAsync();

            var service = new EmployeeService(GetMockUserManager().Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var model = new CreateEmployeeViewModel
            {
                FullName = "Nuevo Empleado",
                DNI = "87654321", // Mismo DNI
                Phone = "999888777",
                Role = "Cocinero",
                InitialPassword = "Password123*"
            };

            // Act
            var result = await service.RegisterEmployeeAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("El DNI ya se encuentra registrado.", result.ErrorMessage);
        }

        // 9. Usuario duplicado genera sufijo único (jperez -> jperez2)
        [Fact]
        public async Task GenerateUniqueUserNameAsync_DuplicateUserName_AppendsNumericSuffix()
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            var context = GetInMemoryDbContext();

            // Simular que 'jperez' ya existe, pero 'jperez2' está libre
            mockUserManager.SetupSequence(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(new ApplicationUser { UserName = "jperez" })
                .ReturnsAsync((ApplicationUser?)null);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            // Act
            var generatedUser = await service.GenerateUniqueUserNameAsync("Juan Pérez García");

            // Assert
            Assert.Equal("jperez2", generatedUser);
        }

        // 10. DNI inválido rechazado (<> 8 dígitos)
        [Theory]
        [InlineData("1234567")]     // 7 dígitos
        [InlineData("123456789")]   // 9 dígitos
        [InlineData("1234567A")]   // Letra
        [InlineData("")]            // Vacío
        public async Task RegisterEmployeeAsync_InvalidDNI_ReturnsError(string invalidDni)
        {
            // Arrange
            var service = new EmployeeService(GetMockUserManager().Object, GetMockRoleManager().Object, GetInMemoryDbContext(), new Mock<ILogger<EmployeeService>>().Object);

            var model = new CreateEmployeeViewModel
            {
                FullName = "Ana Gómez",
                DNI = invalidDni,
                Phone = "987654321",
                Role = "Recepcionista"
            };

            // Act
            var result = await service.RegisterEmployeeAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("El DNI debe tener exactamente 8 dígitos numéricos.", result.ErrorMessage);
        }

        // 11. Teléfono inválido rechazado (<> 9 dígitos)
        [Theory]
        [InlineData("98765432")]    // 8 dígitos
        [InlineData("9876543210")]  // 10 dígitos
        [InlineData("98765432A")]  // Carácter no numérico
        public async Task RegisterEmployeeAsync_InvalidPhone_ReturnsError(string invalidPhone)
        {
            // Arrange
            var service = new EmployeeService(GetMockUserManager().Object, GetMockRoleManager().Object, GetInMemoryDbContext(), new Mock<ILogger<EmployeeService>>().Object);

            var model = new CreateEmployeeViewModel
            {
                FullName = "Pedro Ramos",
                DNI = "11223344",
                Phone = invalidPhone,
                Role = "Repartidor"
            };

            // Act
            var result = await service.RegisterEmployeeAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("El teléfono debe tener exactamente 9 dígitos numéricos.", result.ErrorMessage);
        }

        // 12. Rol obligatorio y válido
        [Fact]
        public async Task RegisterEmployeeAsync_InvalidRole_ReturnsError()
        {
            // Arrange
            var service = new EmployeeService(GetMockUserManager().Object, GetMockRoleManager().Object, GetInMemoryDbContext(), new Mock<ILogger<EmployeeService>>().Object);

            var model = new CreateEmployeeViewModel
            {
                FullName = "Luis Silva",
                DNI = "44556677",
                Phone = "955667788",
                Role = "RolInexistente"
            };

            // Act
            var result = await service.RegisterEmployeeAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Debe seleccionar un rol válido para el colaborador.", result.ErrorMessage);
        }

        // 13. Empleado creado como activo por defecto
        [Fact]
        public void CreateEmployeeViewModel_DefaultsToActive()
        {
            // Arrange & Act
            var model = new CreateEmployeeViewModel();

            // Assert
            Assert.True(model.IsActive);
        }

        // 14. Usuario generado correctamente con normalización y sin tildes
        [Theory]
        [InlineData("Juan Pérez García", "jperez")]
        [InlineData("María López Torres", "mlopez")]
        [InlineData("Óscar Núñez Ávila", "onunez")]
        [InlineData("Raúl D'Onofrio", "rdonofrio")]
        public async Task GenerateUniqueUserNameAsync_NormalizesAccentsAndFormatting(string fullName, string expectedBase)
        {
            // Arrange
            var mockUserManager = GetMockUserManager();
            mockUserManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, GetInMemoryDbContext(), new Mock<ILogger<EmployeeService>>().Object);

            // Act
            var result = await service.GenerateUniqueUserNameAsync(fullName);

            // Assert
            Assert.Equal(expectedBase, result);
        }
    }
}
