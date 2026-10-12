using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Tests
{
    public class Sprint2SalonAndRouteTests
    {
        private static ApplicationDbContext Context()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new ApplicationDbContext(options);
            db.Users.AddRange(
                new ApplicationUser { Id = "waiter", UserName = "mozo", FullName = "Mozo", DNI = "12345678", Phone = "999999991" },
                new ApplicationUser { Id = "driver", UserName = "repartidor", FullName = "Repartidor", DNI = "87654321", Phone = "999999992" });
            db.Categories.Add(new Category { Id = 1, Name = "Especialidades" });
            db.Dishes.Add(new Dish { Id = 10, Name = "Alitas", Price = 28m, CategoryId = 1, IsActive = true });
            db.RestaurantTables.Add(new RestaurantTable { Id = 1, Number = "M1" });
            db.SaveChanges();
            return db;
        }

        private static SalonNewOrderViewModel SalonRequest() => new()
        {
            TableId = 1, CustomerName = "Juan",
            Items = new() { new() { DishId = 10, Quantity = 2 } }
        };

        [Fact]
        public async Task Salon_RegistersOrderAndOccupiesTable()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            var result = await service.CreateAsync(SalonRequest(), "waiter");
            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(OrderTypes.DineIn, result.CreatedOrder!.OrderType);
            Assert.Equal(OrderStatuses.InKitchen, result.CreatedOrder.Status);
            Assert.Equal("#S", result.CreatedOrder.OrderCode[..2]);
            Assert.Equal(56m, result.CreatedOrder.TotalAmount);
            Assert.Equal(2, result.CreatedOrder.TotalItemsCount);
            Assert.Equal("waiter", result.CreatedOrder.ReceptionistId);
            Assert.Equal(TableStatuses.Occupied, (await db.RestaurantTables.FindAsync(1))!.Status);
        }

        [Fact]
        public async Task Salon_RejectsOccupiedTableAndEmptyCart()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            var empty = SalonRequest(); empty.Items.Clear();
            Assert.False((await service.CreateAsync(empty, "waiter")).Succeeded);
            Assert.True((await service.CreateAsync(SalonRequest(), "waiter")).Succeeded);
            Assert.False((await service.CreateAsync(SalonRequest(), "waiter")).Succeeded);
            Assert.Single(db.Orders);
        }

        private static Order Delivery(string status) => new()
        {
            OrderCode = "#D15", OrderType = OrderTypes.Delivery, Status = status,
            CustomerName = "María", CustomerPhone = "987654321", DeliveryAddress = "Av. Javier Prado 1256",
            PaymentMethod = PaymentMethods.Cash, ReceptionistId = "waiter",
            TotalAmount = 28m, TotalItemsCount = 1,
            Items = new List<OrderItem> { new() { DishId = 10, DishName = "Alitas", Quantity = 1, UnitPrice = 28m, Subtotal = 28m } }
        };

        [Fact]
        public async Task Route_OnlyReadyDeliveryCanStartAndRecordsDriverAndTime()
        {
            using var db = Context();
            var order = Delivery(OrderStatuses.New);
            db.Orders.Add(order); await db.SaveChangesAsync();
            var route = new DeliveryRouteService(db);
            Assert.False((await route.StartRouteAsync(order.Id, "driver")).Succeeded);
            Assert.True((await route.MarkReadyForDispatchAsync(order.Id)).Succeeded);
            Assert.True((await route.StartRouteAsync(order.Id, "driver")).Succeeded);
            Assert.Equal(OrderStatuses.OnTheWay, order.Status);
            Assert.Equal("driver", order.DeliveryDriverId);
            Assert.NotNull(order.RouteStartedAt);
            Assert.False((await route.StartRouteAsync(order.Id, "driver")).Succeeded);
            Assert.False((await route.MarkReadyForDispatchAsync(order.Id)).Succeeded);
            var board = await route.GetDashboardAsync("driver");
            Assert.Empty(board.Ready);
            Assert.Single(board.InRoute);
            Assert.Equal("María", board.InRoute[0].CustomerName);
            Assert.Equal(28m, board.InRoute[0].Total);
            Assert.Single(board.InRoute[0].Items);
        }

        [Fact]
        public async Task Route_DoesNotIncludeSalonOrOtherDriversOrders()
        {
            using var db = Context();
            var order = Delivery(OrderStatuses.ReadyForDispatch);
            db.Orders.Add(order); await db.SaveChangesAsync();
            var service = new DeliveryRouteService(db);
            Assert.True((await service.StartRouteAsync(order.Id, "driver")).Succeeded);
            Assert.Empty((await service.GetDashboardAsync("waiter")).InRoute);
            Assert.Single((await service.GetDashboardAsync("driver")).InRoute);
        }

        [Fact]
        public async Task Route_ConfirmDelivery_SucceedsAndMovesToHistoryWithNoteAndDuration()
        {
            using var db = Context();
            var order = Delivery(OrderStatuses.OnTheWay);
            order.DeliveryDriverId = "driver";
            order.RouteStartedAt = DateTime.UtcNow.AddMinutes(-25);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var service = new DeliveryRouteService(db);
            var result = await service.ConfirmDeliveryAsync(order.Id, "driver", "Entregado conforme en recepción");

            Assert.True(result.Succeeded, result.ErrorMessage);
            var updatedOrder = await db.Orders.FindAsync(order.Id);
            Assert.NotNull(updatedOrder);
            Assert.Equal(OrderStatuses.Delivered, updatedOrder!.Status);
            Assert.NotNull(updatedOrder.DeliveredAt);
            Assert.Equal("Entregado conforme en recepción", updatedOrder.DeliveryNote);

            var dashboard = await service.GetDashboardAsync("driver");
            Assert.Empty(dashboard.InRoute);
            Assert.Single(dashboard.History);
            var historyItem = dashboard.History[0];
            Assert.Equal(order.OrderCode, historyItem.Code);
            Assert.Equal("Entregado conforme en recepción", historyItem.DeliveryNote);
            Assert.NotNull(historyItem.DeliveryDurationMinutes);
            Assert.True(historyItem.DeliveryDurationMinutes >= 24);
        }

        [Fact]
        public async Task Route_ConfirmDelivery_RejectsIfNotInRouteOrDifferentDriver()
        {
            using var db = Context();
            var orderReady = Delivery(OrderStatuses.ReadyForDispatch);
            var orderInRoute = Delivery(OrderStatuses.OnTheWay);
            orderInRoute.OrderCode = "#D16";
            orderInRoute.DeliveryDriverId = "driver";
            orderInRoute.RouteStartedAt = DateTime.UtcNow.AddMinutes(-10);
            db.Orders.AddRange(orderReady, orderInRoute);
            await db.SaveChangesAsync();

            var service = new DeliveryRouteService(db);

            // Rejects if not on the way (e.g. still ReadyForDispatch)
            var resultNotOnTheWay = await service.ConfirmDeliveryAsync(orderReady.Id, "driver", "Nota");
            Assert.False(resultNotOnTheWay.Succeeded);

            // Rejects if different driver
            var resultWrongDriver = await service.ConfirmDeliveryAsync(orderInRoute.Id, "waiter", "Nota");
            Assert.False(resultWrongDriver.Succeeded);
            Assert.Contains("repartidor que aceptó el pedido", resultWrongDriver.ErrorMessage ?? "");

            // Order remains unchanged
            var notChanged = await db.Orders.FindAsync(orderInRoute.Id);
            Assert.Equal(OrderStatuses.OnTheWay, notChanged!.Status);
            Assert.Null(notChanged.DeliveredAt);
        }

        [Fact]
        public async Task Route_ConfirmDelivery_CannotModifyAlreadyDeliveredOrder()
        {
            using var db = Context();
            var order = Delivery(OrderStatuses.OnTheWay);
            order.DeliveryDriverId = "driver";
            order.RouteStartedAt = DateTime.UtcNow.AddMinutes(-15);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var service = new DeliveryRouteService(db);
            var firstConfirm = await service.ConfirmDeliveryAsync(order.Id, "driver", "Primera entrega");
            Assert.True(firstConfirm.Succeeded);

            // Second confirmation must be rejected
            var secondConfirm = await service.ConfirmDeliveryAsync(order.Id, "driver", "Intento duplicado");
            Assert.False(secondConfirm.Succeeded);

            // Cannot re-start route either
            var reStart = await service.StartRouteAsync(order.Id, "driver");
            Assert.False(reStart.Succeeded);
        }
    }
}
