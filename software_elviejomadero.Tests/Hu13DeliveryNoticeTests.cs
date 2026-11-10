using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    // HU-13: Notificar pedido Preparación (Rol: Repartidor)
    public class Hu13DeliveryNoticeTests
    {
        private static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ApplicationDbContext(options);

            db.Users.AddRange(
                new ApplicationUser { Id = "cook-1", UserName = "cocinero", FullName = "Cocinero Test", DNI = "11112222", Phone = "999888777" },
                new ApplicationUser { Id = "driver-1", UserName = "repartidor", FullName = "Diego Salas", DNI = "33334444", Phone = "999111222" },
                new ApplicationUser { Id = "waiter-1", UserName = "mozo", FullName = "Mozo Test", DNI = "55556666", Phone = "999222333" }
            );

            db.Categories.Add(new Category { Id = 1, Name = "Especialidades" });
            db.Dishes.AddRange(
                new Dish { Id = 10, Name = "1/4 Pollo Broaster Clásico", Price = 24m, CategoryId = 1, IsActive = true },
                new Dish { Id = 11, Name = "1/2 Pollo Broaster Familiar", Price = 42m, CategoryId = 1, IsActive = true }
            );

            db.SaveChanges();
            return db;
        }

        private static Order CreateSampleOrder(int id, string code, string orderType, string customerName = "Juan Lopez") => new()
        {
            Id = id,
            OrderCode = code,
            OrderType = orderType,
            Status = OrderStatuses.InKitchen,
            CustomerName = customerName,
            CustomerPhone = "987654321",
            DeliveryAddress = orderType == OrderTypes.Delivery ? "Av. Las Palmeras 123" : null,
            PaymentMethod = PaymentMethods.Cash,
            ReceptionistId = "driver-1",
            CreatedAt = DateTime.UtcNow,
            TotalAmount = 66m,
            TotalItemsCount = 2,
            Items = new List<OrderItem>
            {
                new() { DishId = 10, DishName = "1/4 Pollo Broaster Clásico", Quantity = 1, UnitPrice = 24m, Subtotal = 24m },
                new() { DishId = 11, DishName = "1/2 Pollo Broaster Familiar", Quantity = 1, UnitPrice = 42m, Subtotal = 42m }
            }
        };

        [Fact]
        public async Task CA01_DeliveryOrder_InPreparation_GeneratesUnreadNotice()
        {
            // Arrange
            using var db = CreateInMemoryContext();
            var deliveryOrder = CreateSampleOrder(1, "F61", OrderTypes.Delivery, "Juan Lopez");
            db.Orders.Add(deliveryOrder);
            await db.SaveChangesAsync();

            var kitchenService = new KitchenService(db);
            var deliveryService = new DeliveryRouteService(db);

            // Act: Cocina inicia preparación
            var result = await kitchenService.StartPreparationAsync(deliveryOrder.Id, "cook-1");

            // Assert
            Assert.True(result.Success);
            var updatedOrder = await db.Orders.FindAsync(deliveryOrder.Id);
            Assert.NotNull(updatedOrder);
            Assert.Equal(OrderStatuses.InPreparation, updatedOrder.Status);

            var unreadCount = await deliveryService.GetUnreadNoticesCountAsync();
            var notices = await deliveryService.GetKitchenNoticesAsync();

            Assert.Equal(1, unreadCount);
            Assert.Single(notices);
            Assert.False(notices[0].IsRead);
            Assert.Equal("F61", notices[0].OrderCode);
            Assert.Equal("Juan Lopez", notices[0].CustomerName);
        }

        [Fact]
        public async Task Detail01_DineInAndTakeout_DoNotGenerateNotice()
        {
            // Arrange
            using var db = CreateInMemoryContext();
            var dineInOrder = CreateSampleOrder(1, "S10", OrderTypes.DineIn, "Maria Ramos");
            var takeoutOrder = CreateSampleOrder(2, "L20", OrderTypes.Takeout, "Carlos Diaz");
            db.Orders.AddRange(dineInOrder, takeoutOrder);
            await db.SaveChangesAsync();

            var kitchenService = new KitchenService(db);
            var deliveryService = new DeliveryRouteService(db);

            // Act: Cocina inicia preparación de pedidos Salón y Para Llevar
            await kitchenService.StartPreparationAsync(dineInOrder.Id, "cook-1");
            await kitchenService.StartPreparationAsync(takeoutOrder.Id, "cook-1");

            // Assert: NO deben generar avisos para repartidor
            var unreadCount = await deliveryService.GetUnreadNoticesCountAsync();
            var notices = await deliveryService.GetKitchenNoticesAsync();

            Assert.Equal(0, unreadCount);
            Assert.Empty(notices);
        }

        [Fact]
        public async Task CA02_NoticeContents_MatchRequiredFormat()
        {
            // Arrange
            using var db = CreateInMemoryContext();
            var deliveryOrder = CreateSampleOrder(5, "F61", OrderTypes.Delivery, "Juan Lopez");
            db.Orders.Add(deliveryOrder);
            await db.SaveChangesAsync();

            var kitchenService = new KitchenService(db);
            var deliveryService = new DeliveryRouteService(db);

            // Act
            await kitchenService.StartPreparationAsync(deliveryOrder.Id, "cook-1");
            var notices = await deliveryService.GetKitchenNoticesAsync();

            // Assert format: "Pedido #F61 en preparación", "Cocina comenzó a preparar el delivery de Juan Lopez."
            var notice = Assert.Single(notices);
            Assert.Equal("Pedido #F61 en preparación", notice.Title);
            Assert.Equal("Cocina comenzó a preparar el delivery de Juan Lopez.", notice.Message);
        }

        [Fact]
        public async Task CA03_MarkNoticesAsRead_SetsIsReadTrue_AndUnreadCountZero()
        {
            // Arrange
            using var db = CreateInMemoryContext();
            var order1 = CreateSampleOrder(1, "F61", OrderTypes.Delivery, "Juan Lopez");
            var order2 = CreateSampleOrder(2, "F62", OrderTypes.Delivery, "Ana Torres");
            db.Orders.AddRange(order1, order2);
            await db.SaveChangesAsync();

            var kitchenService = new KitchenService(db);
            var deliveryService = new DeliveryRouteService(db);

            await kitchenService.StartPreparationAsync(order1.Id, "cook-1");
            await kitchenService.StartPreparationAsync(order2.Id, "cook-1");

            Assert.Equal(2, await deliveryService.GetUnreadNoticesCountAsync());

            // Act: El repartidor abre el popover y se marcan como leídos
            await deliveryService.MarkNoticesAsReadAsync();

            // Assert
            Assert.Equal(0, await deliveryService.GetUnreadNoticesCountAsync());

            var notices = await deliveryService.GetKitchenNoticesAsync();
            Assert.Equal(2, notices.Count);
            Assert.All(notices, n => Assert.True(n.IsRead));
        }

        [Fact]
        public async Task Detail04_Notices_OrderedFromNewestToOldest()
        {
            // Arrange
            using var db = CreateInMemoryContext();
            var order1 = CreateSampleOrder(1, "F01", OrderTypes.Delivery, "Cliente 1");
            var order2 = CreateSampleOrder(2, "F02", OrderTypes.Delivery, "Cliente 2");
            var order3 = CreateSampleOrder(3, "F03", OrderTypes.Delivery, "Cliente 3");
            db.Orders.AddRange(order1, order2, order3);
            await db.SaveChangesAsync();

            var kitchenService = new KitchenService(db);
            var deliveryService = new DeliveryRouteService(db);

            // Simular momentos de inicio
            await kitchenService.StartPreparationAsync(order1.Id, "cook-1");
            await Task.Delay(15);
            await kitchenService.StartPreparationAsync(order2.Id, "cook-1");
            await Task.Delay(15);
            await kitchenService.StartPreparationAsync(order3.Id, "cook-1");

            // Act
            var notices = await deliveryService.GetKitchenNoticesAsync();

            // Assert: Más reciente primero
            Assert.Equal(3, notices.Count);
            Assert.Equal("F03", notices[0].OrderCode);
            Assert.Equal("F02", notices[1].OrderCode);
            Assert.Equal("F01", notices[2].OrderCode);
        }

        [Fact]
        public async Task DeliveryRouteController_GetNoticesSummaryAndMarkRead_EndpointsWork()
        {
            // Arrange
            using var db = CreateInMemoryContext();
            var deliveryOrder = CreateSampleOrder(1, "F61", OrderTypes.Delivery, "Juan Lopez");
            db.Orders.Add(deliveryOrder);
            await db.SaveChangesAsync();

            var kitchenService = new KitchenService(db);
            var deliveryService = new DeliveryRouteService(db);

            await kitchenService.StartPreparationAsync(deliveryOrder.Id, "cook-1");

            var controller = new DeliveryRouteController(deliveryService);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "driver-1"),
                new Claim(ClaimTypes.Name, "repartidor")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Act 1: GetNoticesSummary
            var getResult = await controller.GetNoticesSummary() as JsonResult;
            Assert.NotNull(getResult);

            var unreadProp = getResult.Value?.GetType().GetProperty("unreadCount")?.GetValue(getResult.Value, null);
            Assert.Equal(1, unreadProp);

            // Act 2: MarkNoticesAsRead
            var markResult = await controller.MarkNoticesAsRead() as JsonResult;
            Assert.NotNull(markResult);
            var successProp = markResult.Value?.GetType().GetProperty("success")?.GetValue(markResult.Value, null);
            Assert.Equal(true, successProp);

            // Verificar que ahora unreadCount es 0
            var getResult2 = await controller.GetNoticesSummary() as JsonResult;
            var unreadProp2 = getResult2?.Value?.GetType().GetProperty("unreadCount")?.GetValue(getResult2.Value, null);
            Assert.Equal(0, unreadProp2);
        }
    }
}
