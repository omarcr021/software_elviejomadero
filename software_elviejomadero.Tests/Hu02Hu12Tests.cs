using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Tests
{
    // HU-02 (estados de mesa) y HU-12 (consultar detalle de la entrega).
    public class Hu02Hu12Tests
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
            db.Dishes.AddRange(
                new Dish { Id = 10, Name = "Alitas", Price = 28m, CategoryId = 1, IsActive = true },
                new Dish { Id = 11, Name = "Chicha", Price = 8m, CategoryId = 1, IsActive = true });
            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "M1" },
                new RestaurantTable { Id = 2, Number = "M2" });
            db.SaveChanges();
            return db;
        }

        private static SalonNewOrderViewModel Request(int tableId, params (int dish, int qty)[] items) => new()
        {
            TableId = tableId, CustomerName = "Juan",
            Items = items.Select(i => new CreateOrderItemInputModel { DishId = i.dish, Quantity = i.qty }).ToList()
        };

        [Theory]
        [InlineData("M1", "Mesa 1")]
        [InlineData("M12", "Mesa 12")]
        [InlineData("Terraza", "Mesa Terraza")]
        public void TableLabel_FormatsNumber(string number, string expected) =>
            Assert.Equal(expected, TableLabels.Format(number));

        [Fact]
        public async Task GetTablesAsync_ReturnsTablesInNaturalNumericOrder()
        {
            using var db = Context();
            // Agregar mesas desordenadas M10, M3, M12
            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 10, Number = "M10" },
                new RestaurantTable { Id = 3, Number = "M3" },
                new RestaurantTable { Id = 12, Number = "M12" });
            await db.SaveChangesAsync();

            var service = new SalonOrderService(db, new OrderService(db));
            var tables = await service.GetTablesAsync();

            var numbers = tables.Select(t => t.Number).ToList();
            Assert.Equal(new[] { "M1", "M2", "M3", "M10", "M12" }, numbers);
        }

        [Fact]
        public async Task Table_FreeShowsNoOrder_OccupiedShowsConsumption()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var free = await service.GetTableConsumptionAsync(1);
            Assert.True(free!.IsFree);
            Assert.Equal(TableStatuses.Free, free.Status);
            Assert.Empty(free.Items);

            Assert.True((await service.CreateAsync(Request(1, (10, 2)), "waiter")).Succeeded);
            var occupied = await service.GetTableConsumptionAsync(1);
            Assert.False(occupied!.IsFree);
            Assert.Equal(TableStatuses.Occupied, occupied.Status);
            Assert.Single(occupied.Items);
            Assert.Equal(56m, occupied.Total);
            Assert.Equal(56m, occupied.Items[0].LineTotal);
            Assert.Null(await service.GetTableConsumptionAsync(999));
        }

        [Fact]
        public async Task AddItems_MergesIntoActiveOrderAndSendsBackToKitchen()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            var created = (await service.CreateAsync(Request(1, (10, 1)), "waiter")).CreatedOrder!;
            created.Status = OrderStatuses.ReadyForDispatch;
            await db.SaveChangesAsync();

            var result = await service.AddItemsAsync(Request(1, (10, 2), (11, 1)), "waiter");

            Assert.True(result.Succeeded, result.ErrorMessage);
            var order = await db.Orders.Include(o => o.Items).SingleAsync();
            Assert.Equal(2, order.Items.Count);
            Assert.Equal(3, order.Items.Single(i => i.DishId == 10).Quantity);
            Assert.Equal(92m, order.TotalAmount);          // 3×28 + 1×8
            Assert.Equal(4, order.TotalItemsCount);
            Assert.Equal(OrderStatuses.InKitchen, order.Status);
        }

        [Fact]
        public async Task AddItems_RejectsFreeTableOrEmptyRequest()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            Assert.False((await service.AddItemsAsync(Request(1, (10, 1)), "waiter")).Succeeded);   // mesa libre
            Assert.Null(await service.GetAddItemsContextAsync(1));

            await service.CreateAsync(Request(1, (10, 1)), "waiter");
            Assert.False((await service.AddItemsAsync(Request(1), "waiter")).Succeeded);            // sin platos
            Assert.NotNull(await service.GetAddItemsContextAsync(1));
        }

        [Fact]
        public async Task ReadyToServe_ListsOnlySalonOrdersReadyFromKitchen()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            Assert.Empty(await service.GetReadyToServeAsync());

            var order = (await service.CreateAsync(Request(1, (10, 1)), "waiter")).CreatedOrder!;
            Assert.Empty(await service.GetReadyToServeAsync());            // sigue "En cocina"

            order.Status = OrderStatuses.ReadyForDispatch;
            await db.SaveChangesAsync();
            var ready = await service.GetReadyToServeAsync();
            Assert.Single(ready);
            Assert.Equal("Mesa 1", ready[0].TableLabel);
        }

        private static Order Delivery(string status) => new()
        {
            OrderCode = "#L15", OrderType = OrderTypes.Delivery, Status = status,
            CustomerName = "Maria Gomez", CustomerPhone = "947832467", DeliveryAddress = "Av. Javier Prado 1256",
            PaymentMethod = PaymentMethods.Yape, AdditionalNote = "Sin Aji Todas las cremas", ReceptionistId = "waiter",
            Latitude = -12.0912, Longitude = -77.0301,
            TotalAmount = 131m, TotalItemsCount = 5,
            Items = new List<OrderItem>
            {
                new() { DishId = 10, DishName = "1/4 Pollo Broaster Clásico", Quantity = 1, UnitPrice = 24m, Subtotal = 24m },
                new() { DishId = 11, DishName = "Combo Tiras Crocantes", Quantity = 2, UnitPrice = 22m, Subtotal = 44m }
            }
        };

        [Fact]
        public async Task Route_MarkReady_StoresKitchenExitTime()
        {
            using var db = Context();
            var order = Delivery(OrderStatuses.New);
            db.Orders.Add(order); await db.SaveChangesAsync();
            var service = new DeliveryRouteService(db);

            Assert.True((await service.MarkReadyForDispatchAsync(order.Id)).Succeeded);

            Assert.NotNull(order.ReadyAt);
            Assert.True((DateTime.UtcNow - order.ReadyAt!.Value).TotalMinutes < 1);
        }

        [Fact]
        public async Task Route_ActiveListHasReadyAndOnTheWay_WithDetailData_AndDoesNotChangeStatus()
        {
            using var db = Context();
            var ready = Delivery(OrderStatuses.ReadyForDispatch);
            var onTheWay = Delivery(OrderStatuses.OnTheWay);
            onTheWay.OrderCode = "#L16"; onTheWay.DeliveryDriverId = "driver"; onTheWay.Latitude = null; onTheWay.Longitude = null;
            var newOrder = Delivery(OrderStatuses.New); newOrder.OrderCode = "#L17";
            var salon = Delivery(OrderStatuses.ReadyForDispatch); salon.OrderCode = "#S1"; salon.OrderType = OrderTypes.DineIn;
            db.Orders.AddRange(ready, onTheWay, newOrder, salon);
            await db.SaveChangesAsync();

            var board = await new DeliveryRouteService(db).GetDashboardAsync("driver");

            Assert.Equal(2, board.Active.Count());                       // solo Listo para despacho + En camino de delivery
            Assert.DoesNotContain(board.Active, o => o.Code is "#L17" or "#S1");
            var card = board.Ready.Single();
            Assert.Equal("Yape", card.PaymentMethod);
            Assert.Equal("Sin Aji Todas las cremas", card.AdditionalNote);
            Assert.Equal(131m, card.Total);
            Assert.True(card.HasLocation);
            Assert.Equal(new[] { 24m, 44m }, card.Items.Select(i => i.LineTotal).ToArray());   // precio × cantidad
            Assert.False(board.InRoute.Single().HasLocation);            // sin pin -> mensaje "No hay coordenadas…"
            Assert.Equal(OrderStatuses.ReadyForDispatch, (await db.Orders.FindAsync(ready.Id))!.Status); // consultar no modifica
        }
    }
}
