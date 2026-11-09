using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using System.Security.Claims;
using Xunit;

namespace software_elviejomadero.Tests
{
    // HU-08: Visualizar pedidos listos para entregar (Rol: Mozo)
    public class Hu08ReadyOrdersTests
    {
        private static ApplicationDbContext Context()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new ApplicationDbContext(options);
            db.Users.Add(new ApplicationUser
            {
                Id = "waiter1",
                UserName = "mozo",
                FullName = "Carlos Ramos",
                DNI = "12345678",
                Phone = "999999991",
                IsActive = true
            });
            db.Categories.Add(new Category { Id = 1, Name = "Especialidades" });
            db.Dishes.AddRange(
                new Dish { Id = 10, Name = "1/4 Pollo Broaster Clásico", Price = 24m, CategoryId = 1, IsActive = true },
                new Dish { Id = 11, Name = "1/2 Pollo Broaster Familiar", Price = 42m, CategoryId = 1, IsActive = true },
                new Dish { Id = 12, Name = "Pollo Broaster Entero", Price = 75m, CategoryId = 1, IsActive = true },
                new Dish { Id = 13, Name = "Combo Tiras Crocantes", Price = 26m, CategoryId = 1, IsActive = true });
            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "1", Status = TableStatuses.Occupied, IsActive = true },
                new RestaurantTable { Id = 2, Number = "2", Status = TableStatuses.Occupied, IsActive = true });
            db.SaveChanges();
            return db;
        }

        [Fact]
        public async Task CA01_GetReadyToServeAsync_OnlyReturnsReadyForDispatchDineInOrders()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            // Orden 1: Lista para servir en salón
            var o1 = new Order
            {
                OrderCode = "D75",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Juan",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-12),
                ReadyAt = DateTime.UtcNow.AddMinutes(-11)
            };
            // Orden 2: En preparación (no debe incluirse)
            var o2 = new Order
            {
                OrderCode = "D76",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.InPreparation,
                TableId = 1,
                CustomerName = "Juan",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            };
            // Orden 3: Ya servida / entregada (no debe incluirse)
            var o3 = new Order
            {
                OrderCode = "D77",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.Delivered,
                TableId = 2,
                CustomerName = "Maria",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                ReadyAt = DateTime.UtcNow.AddMinutes(-18)
            };
            // Orden 4: Para llevar lista para despachar (no es DineIn de salón)
            var o4 = new Order
            {
                OrderCode = "D78",
                OrderType = OrderTypes.Takeout,
                Status = OrderStatuses.ReadyForDispatch,
                CustomerName = "Pedro",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ReadyAt = DateTime.UtcNow.AddMinutes(-3)
            };

            db.Orders.AddRange(o1, o2, o3, o4);
            await db.SaveChangesAsync();

            var readyOrders = await service.GetReadyToServeAsync();

            Assert.Single(readyOrders);
            Assert.Equal("D75", readyOrders[0].Code);
            Assert.Equal("Juan", readyOrders[0].CustomerName);
            Assert.Equal("Mesa 1", readyOrders[0].TableLabel);
        }

        [Fact]
        public async Task CA01_GetReadyToServeAsync_OrdersByWaitTimeOldestFirst()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var now = DateTime.UtcNow;
            // Orden A: lista hace 15 min
            var oA = new Order
            {
                OrderCode = "D81",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Cliente A",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = now.AddMinutes(-20),
                ReadyAt = now.AddMinutes(-15)
            };
            // Orden B: lista hace 5 min
            var oB = new Order
            {
                OrderCode = "D82",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 2,
                CustomerName = "Cliente B",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = now.AddMinutes(-10),
                ReadyAt = now.AddMinutes(-5)
            };
            // Orden C: lista hace 25 min (la más antigua esperando entrega)
            var oC = new Order
            {
                OrderCode = "D83",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Cliente C",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = now.AddMinutes(-30),
                ReadyAt = now.AddMinutes(-25)
            };

            db.Orders.AddRange(oA, oB, oC);
            await db.SaveChangesAsync();

            var list = await service.GetReadyToServeAsync();

            Assert.Equal(3, list.Count);
            // Orden C (hace 25m) primero, luego A (hace 15m), luego B (hace 5m)
            Assert.Equal("D83", list[0].Code);
            Assert.Equal("D81", list[1].Code);
            Assert.Equal("D82", list[2].Code);
        }

        [Fact]
        public async Task CA01_GetReadyToServeAsync_IncludesPlatesAndQuantities()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var order = new Order
            {
                OrderCode = "D75",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Juan",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-12),
                ReadyAt = DateTime.UtcNow.AddMinutes(-11),
                Items = new List<OrderItem>
                {
                    new OrderItem { DishId = 10, Quantity = 1, UnitPrice = 24m },
                    new OrderItem { DishId = 11, Quantity = 1, UnitPrice = 42m },
                    new OrderItem { DishId = 12, Quantity = 1, UnitPrice = 75m },
                    new OrderItem { DishId = 13, Quantity = 1, UnitPrice = 26m }
                }
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var list = await service.GetReadyToServeAsync();

            Assert.Single(list);
            var item = list[0];
            Assert.Equal("D75", item.Code);
            Assert.Equal("Juan", item.CustomerName);
            Assert.Equal("Mesa 1", item.TableLabel);
            Assert.NotNull(item.ReadyAtUtc);
            Assert.Equal(4, item.Items.Count);
            Assert.Equal(1, item.Items[0].Quantity);
            Assert.Equal("1/4 Pollo Broaster Clásico", item.Items[0].DishName);
            Assert.Equal(1, item.Items[1].Quantity);
            Assert.Equal("1/2 Pollo Broaster Familiar", item.Items[1].DishName);
            Assert.Equal(1, item.Items[2].Quantity);
            Assert.Equal("Pollo Broaster Entero", item.Items[2].DishName);
            Assert.Equal(1, item.Items[3].Quantity);
            Assert.Equal("Combo Tiras Crocantes", item.Items[3].DishName);
        }

        [Fact]
        public async Task CA02_BoardCount_MatchesReadyOrdersCount()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var o1 = new Order
            {
                OrderCode = "D75",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Juan",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow,
                ReadyAt = DateTime.UtcNow
            };
            db.Orders.Add(o1);
            await db.SaveChangesAsync();

            var readyList = await service.GetReadyToServeAsync();

            Assert.Single(readyList);
            Assert.Equal("D75", readyList[0].Code);
        }

        [Fact]
        public async Task CA03_ServeOrderAsync_ChangesStatusToServed_AndRemovesFromReadyToServe()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var order = new Order
            {
                OrderCode = "D75",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Juan",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ReadyAt = DateTime.UtcNow.AddMinutes(-8)
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            // Verificar que inicialmente aparece en "Listos para entregar"
            var readyBefore = await service.GetReadyToServeAsync();
            Assert.Single(readyBefore);

            // Servir a la mesa
            var serveResult = await service.ServeOrderAsync(order.Id);
            Assert.True(serveResult.Succeeded);

            // Verificar que el estado del pedido ahora es "Servido" (Served)
            var updatedOrder = await db.Orders.FindAsync(order.Id);
            Assert.NotNull(updatedOrder);
            Assert.Equal(OrderStatuses.Served, updatedOrder.Status);

            // Verificar que ya no aparece en "Listos para entregar"
            var readyAfter = await service.GetReadyToServeAsync();
            Assert.Empty(readyAfter);
        }

        [Fact]
        public async Task CA03_ServeOrder_Ajax_ReturnsJsonSucceeded()
        {
            using var db = Context();
            var salonService = new SalonOrderService(db, new OrderService(db));

            var order = new Order
            {
                OrderCode = "D75",
                OrderType = OrderTypes.DineIn,
                Status = OrderStatuses.ReadyForDispatch,
                TableId = 1,
                CustomerName = "Juan",
                CustomerPhone = "999888777",
                PaymentMethod = PaymentMethods.Cash,
                ReceptionistId = "waiter1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ReadyAt = DateTime.UtcNow.AddMinutes(-8)
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var controller = new SalonOrderController(salonService, null!);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

            var result = await controller.ServeOrder(order.Id, null);

            var jsonResult = Assert.IsType<JsonResult>(result);
            var prop = jsonResult.Value!.GetType().GetProperty("succeeded");
            Assert.NotNull(prop);
            Assert.True((bool)prop.GetValue(jsonResult.Value)!);
        }
    }
}
