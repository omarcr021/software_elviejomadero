using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    public class Hu18EmployeeEditTests
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

        // 1. GetEmployeeForEditAsync retorna datos correctos
        [Fact]
        public async Task GetEmployeeForEditAsync_ReturnsCorrectData()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();
            var mockRoleManager = GetMockRoleManager();

            var user = new ApplicationUser
            {
                Id = "emp-1",
                FullName = "Carlos Mendoza",
                DNI = "12345678",
                Phone = "987654321",
                UserName = "cmendoza",
                IsActive = true
            };

            mockUserManager.Setup(m => m.FindByIdAsync("emp-1")).ReturnsAsync(user);
            mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Mozo" });

            var service = new EmployeeService(mockUserManager.Object, mockRoleManager.Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var result = await service.GetEmployeeForEditAsync("emp-1");

            Assert.NotNull(result);
            Assert.Equal("emp-1", result.Id);
            Assert.Equal("Carlos Mendoza", result.FullName);
            Assert.Equal("12345678", result.DNI);
            Assert.Equal("987654321", result.Phone);
            Assert.Equal("cmendoza", result.UserName);
            Assert.Equal("Mozo", result.Role);
            Assert.True(result.IsActive);
        }

        // 2. UpdateEmployeeAsync actualiza datos y rol correctamente
        [Fact]
        public async Task UpdateEmployeeAsync_ValidData_UpdatesUserAndRole()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();
            var mockRoleManager = GetMockRoleManager();

            var user = new ApplicationUser
            {
                Id = "emp-2",
                FullName = "Carlos Mendoza",
                DNI = "12345678",
                Phone = "987654321",
                UserName = "cmendoza",
                IsActive = true
            };

            mockUserManager.Setup(m => m.FindByIdAsync("emp-2")).ReturnsAsync(user);
            mockUserManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Mozo" });
            mockUserManager.Setup(m => m.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
            mockUserManager.Setup(m => m.AddToRoleAsync(user, "Recepcionista")).ReturnsAsync(IdentityResult.Success);

            var service = new EmployeeService(mockUserManager.Object, mockRoleManager.Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var model = new EditEmployeeViewModel
            {
                Id = "emp-2",
                FullName = "Carlos Mendoza Actualizado",
                DNI = "87654321",
                Phone = "911222333",
                Role = "Recepcionista",
                IsActive = true,
                NewPassword = null
            };

            var result = await service.UpdateEmployeeAsync(model, "admin-id");

            Assert.True(result.Success);
            Assert.Equal("Carlos Mendoza Actualizado", user.FullName);
            Assert.Equal("87654321", user.DNI);
            Assert.Equal("911222333", user.Phone);
            mockUserManager.Verify(m => m.AddToRoleAsync(user, "Recepcionista"), Times.Once);
        }

        // 3. UpdateEmployeeAsync con contraseña vacía conserva la actual
        [Fact]
        public async Task UpdateEmployeeAsync_EmptyPassword_DoesNotResetPassword()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();

            var user = new ApplicationUser { Id = "emp-3", FullName = "Mario Diaz", DNI = "11223344", Phone = "987654321" };
            mockUserManager.Setup(m => m.FindByIdAsync("emp-3")).ReturnsAsync(user);
            mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Repartidor" });

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var model = new EditEmployeeViewModel
            {
                Id = "emp-3",
                FullName = "Mario Diaz",
                DNI = "11223344",
                Phone = "987654321",
                Role = "Repartidor",
                NewPassword = "" // Contraseña vacía
            };

            var result = await service.UpdateEmployeeAsync(model, "admin-id");

            Assert.True(result.Success);
            mockUserManager.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // 4. UpdateEmployeeAsync con nueva contraseña actualiza mediante ResetPasswordAsync
        [Fact]
        public async Task UpdateEmployeeAsync_NewPasswordProvided_UpdatesPassword()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();

            var user = new ApplicationUser { Id = "emp-4", FullName = "Mario Diaz", DNI = "11223344", Phone = "987654321" };
            mockUserManager.Setup(m => m.FindByIdAsync("emp-4")).ReturnsAsync(user);
            mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Repartidor" });
            mockUserManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token123");
            mockUserManager.Setup(m => m.ResetPasswordAsync(user, "token123", "NuevaPass2026*")).ReturnsAsync(IdentityResult.Success);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var model = new EditEmployeeViewModel
            {
                Id = "emp-4",
                FullName = "Mario Diaz",
                DNI = "11223344",
                Phone = "987654321",
                Role = "Repartidor",
                NewPassword = "NuevaPass2026*"
            };

            var result = await service.UpdateEmployeeAsync(model, "admin-id");

            Assert.True(result.Success);
            mockUserManager.Verify(m => m.ResetPasswordAsync(user, "token123", "NuevaPass2026*"), Times.Once);
        }

        // 5. UpdateEmployeeAsync rechaza DNI duplicado de otro usuario
        [Fact]
        public async Task UpdateEmployeeAsync_DuplicateDNI_ReturnsError()
        {
            var context = GetInMemoryDbContext();
            context.Users.Add(new ApplicationUser { Id = "emp-other", FullName = "Otro", DNI = "99887766", Phone = "987654321" });
            await context.SaveChangesAsync();

            var mockUserManager = GetMockUserManager();
            var user = new ApplicationUser { Id = "emp-5", FullName = "Mario Diaz", DNI = "11223344", Phone = "987654321" };
            mockUserManager.Setup(m => m.FindByIdAsync("emp-5")).ReturnsAsync(user);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var model = new EditEmployeeViewModel
            {
                Id = "emp-5",
                FullName = "Mario Diaz",
                DNI = "99887766", // DNI ya existente en emp-other
                Phone = "987654321",
                Role = "Mozo"
            };

            var result = await service.UpdateEmployeeAsync(model, "admin-id");

            Assert.False(result.Success);
            Assert.Equal("El DNI ya se encuentra registrado por otro colaborador.", result.ErrorMessage);
        }

        // 6. UpdateEmployeeAsync impide que el admin se desactive a sí mismo (Detalle 5)
        [Fact]
        public async Task UpdateEmployeeAsync_AdminSelfDeactivation_ReturnsError()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();

            var adminUser = new ApplicationUser { Id = "admin-1", FullName = "Admin General", DNI = "11112222", Phone = "987654321", IsActive = true };
            mockUserManager.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(adminUser);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var model = new EditEmployeeViewModel
            {
                Id = "admin-1",
                FullName = "Admin General",
                DNI = "11112222",
                Phone = "987654321",
                Role = "Administrador",
                IsActive = false // Intenta desactivarse
            };

            var result = await service.UpdateEmployeeAsync(model, "admin-1");

            Assert.False(result.Success);
            Assert.Equal("El administrador no puede desactivarse a sí mismo.", result.ErrorMessage);
        }

        // 7. ToggleEmployeeStatusAsync alterna el estado activo/inactivo (CA-04)
        [Fact]
        public async Task ToggleEmployeeStatusAsync_InvertsIsActive()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();

            var user = new ApplicationUser { Id = "emp-7", FullName = "Pepe", DNI = "11223344", Phone = "987654321", IsActive = true };
            mockUserManager.Setup(m => m.FindByIdAsync("emp-7")).ReturnsAsync(user);
            mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            // Primer toggle: pasa a false (inactivo)
            var result1 = await service.ToggleEmployeeStatusAsync("emp-7", "admin-id");
            Assert.True(result1.Success);
            Assert.False(result1.NewStatus);
            Assert.False(user.IsActive);

            // Segundo toggle: reactiva a true (activo)
            var result2 = await service.ToggleEmployeeStatusAsync("emp-7", "admin-id");
            Assert.True(result2.Success);
            Assert.True(result2.NewStatus);
            Assert.True(user.IsActive);
        }

        // 8. ToggleEmployeeStatusAsync impide que el admin se desactive a sí mismo
        [Fact]
        public async Task ToggleEmployeeStatusAsync_AdminSelf_ReturnsError()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();

            var adminUser = new ApplicationUser { Id = "admin-8", FullName = "Admin", DNI = "11223344", Phone = "987654321", IsActive = true };
            mockUserManager.Setup(m => m.FindByIdAsync("admin-8")).ReturnsAsync(adminUser);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var result = await service.ToggleEmployeeStatusAsync("admin-8", "admin-8");

            Assert.False(result.Success);
            Assert.Equal("El administrador no puede desactivarse a sí mismo.", result.ErrorMessage);
            Assert.True(result.NewStatus);
        }

        // 9. DeleteEmployeeAsync impide que el admin se elimine a sí mismo
        [Fact]
        public async Task DeleteEmployeeAsync_AdminSelf_ReturnsError()
        {
            var context = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();

            var adminUser = new ApplicationUser { Id = "admin-9", FullName = "Admin", DNI = "11223344", Phone = "987654321" };
            mockUserManager.Setup(m => m.FindByIdAsync("admin-9")).ReturnsAsync(adminUser);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var result = await service.DeleteEmployeeAsync("admin-9", "admin-9");

            Assert.False(result.Success);
            Assert.Equal("El administrador no puede eliminarse a sí mismo.", result.ErrorMessage);
        }

        // 10. DeleteEmployeeAsync elimina correctamente y preserva integridad de pedidos (CA-05 & Detalle 4)
        [Fact]
        public async Task DeleteEmployeeAsync_ValidEmployeeWithOrders_DeletesAndPreservesOrders()
        {
            var context = GetInMemoryDbContext();
            var empUser = new ApplicationUser { Id = "emp-10", FullName = "Recepcionista 1", DNI = "11223344", Phone = "987654321" };
            var adminUser = new ApplicationUser { Id = "admin-10", FullName = "Admin", DNI = "99887766", Phone = "987654321" };
            context.Users.AddRange(empUser, adminUser);

            var order = new Order
            {
                Id = 1,
                OrderCode = "#D01",
                OrderType = OrderTypes.Delivery,
                CustomerName = "Cliente Test",
                CustomerPhone = "999888777",
                PaymentMethod = "Efectivo",
                TotalAmount = 50m,
                ReceptionistId = "emp-10",
                DeliveryDriverId = "emp-10",
                PreparedByUserId = "emp-10"
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var mockUserManager = GetMockUserManager();
            mockUserManager.Setup(m => m.FindByIdAsync("emp-10")).ReturnsAsync(empUser);
            mockUserManager.Setup(m => m.DeleteAsync(empUser)).ReturnsAsync(IdentityResult.Success);

            var service = new EmployeeService(mockUserManager.Object, GetMockRoleManager().Object, context, new Mock<ILogger<EmployeeService>>().Object);

            var result = await service.DeleteEmployeeAsync("emp-10", "admin-10");

            Assert.True(result.Success);
            mockUserManager.Verify(m => m.DeleteAsync(empUser), Times.Once);

            // Verificar que el pedido no se eliminó y conserva integridad
            var persistedOrder = await context.Orders.FindAsync(1);
            Assert.NotNull(persistedOrder);
            Assert.Equal("admin-10", persistedOrder.ReceptionistId);
            Assert.Null(persistedOrder.DeliveryDriverId);
            Assert.Null(persistedOrder.PreparedByUserId);
        }

        // 11. Controller endpoints test
        [Fact]
        public async Task EmployeeController_GetForEdit_ReturnsJson()
        {
            var mockService = new Mock<IEmployeeService>();
            mockService.Setup(s => s.GetEmployeeForEditAsync("emp-1"))
                .ReturnsAsync(new EditEmployeeViewModel { Id = "emp-1", FullName = "Pedro" });

            var controller = new EmployeeController(mockService.Object);

            var result = await controller.GetForEdit("emp-1");

            var jsonResult = Assert.IsType<JsonResult>(result);
            var model = Assert.IsType<EditEmployeeViewModel>(jsonResult.Value);
            Assert.Equal("Pedro", model.FullName);
        }
    }
}
