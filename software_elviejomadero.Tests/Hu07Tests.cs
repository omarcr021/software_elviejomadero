using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Tests
{
    // HU-07: Visualizar detalle de mesa (Sprint 3)
    public class Hu07Tests
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
                new Dish { Id = 12, Name = "Alitas BBQ Crocantes", Price = 23m, CategoryId = 1, IsActive = true },
                new Dish { Id = 13, Name = "Hamburguesa de Pollo Crispy", Price = 21m, CategoryId = 1, IsActive = true },
                new Dish { Id = 14, Name = "Chicha Morada de la Casa", Price = 7m, CategoryId = 1, IsActive = true });
            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "M1", Status = TableStatuses.Free, IsActive = true, IsBilled = false },
                new RestaurantTable { Id = 2, Number = "M2", Status = TableStatuses.Free, IsActive = true, IsBilled = false });
            db.SaveChanges();
            return db;
        }

        private static SalonNewOrderViewModel CreateOrderRequest(int tableId, string customer, bool isAdditional, params (int dish, int qty)[] items) => new()
        {
            TableId = tableId,
            CustomerName = customer,
            IsAdditionalOrder = isAdditional,
            Items = items.Select(i => new CreateOrderItemInputModel { DishId = i.dish, Quantity = i.qty }).ToList()
        };

        [Fact]
        public async Task CA01_TableDetail_ShowsTotalConsumptionAndEachOrderDetails()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            // Pedido 1 (#F71): 24 + 42 + 23 + 21 + 7 = S/ 117 (como en el prototipo)
            var req1 = CreateOrderRequest(2, "Mesa 2 Cliente", false, (10, 1), (11, 1), (12, 1), (13, 1), (14, 1));
            var res1 = await service.CreateAsync(req1, "waiter1");
            Assert.True(res1.Succeeded);

            // Consultar detalle inicial
            var detail1 = await service.GetTableConsumptionAsync(2);
            Assert.NotNull(detail1);
            Assert.False(detail1!.IsFree);
            Assert.Single(detail1.Orders);
            Assert.Equal(117m, detail1.Total);
            Assert.Equal(117m, detail1.Orders[0].Subtotal);
            Assert.Equal(5, detail1.Orders[0].Items.Count);

            // CA-03: Cliente pide más platos -> Se agrega como un nuevo pedido (#F72): 2x Chicha (S/ 14)
            var req2 = CreateOrderRequest(2, "Mesa 2 Cliente", true, (14, 2));
            var res2 = await service.CreateAsync(req2, "waiter1");
            Assert.True(res2.Succeeded);

            // Detalle 1: consumo total es la suma de ambos pedidos (117 + 14 = 131)
            var detail2 = await service.GetTableConsumptionAsync(2);
            Assert.NotNull(detail2);
            Assert.Equal(2, detail2!.Orders.Count);
            Assert.Equal(131m, detail2.Total);
            Assert.Equal(117m, detail2.Orders[0].Subtotal);
            Assert.Equal(14m, detail2.Orders[1].Subtotal);
        }

        [Fact]
        public async Task CA02_ServeOrder_OnlyWhenReadyToServe_ChangesToServed()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var req = CreateOrderRequest(1, "Ana", false, (10, 1));
            var res = await service.CreateAsync(req, "waiter1");
            Assert.True(res.Succeeded);
            var orderId = res.CreatedOrder!.Id;

            // Detalle 3: No se puede servir si sigue en cocina
            var failServe = await service.ServeOrderAsync(orderId);
            Assert.False(failServe.Succeeded);
            Assert.Contains("Listo para servir", failServe.ErrorMessage);

            // Cocina marca como listo para servir (ReadyForDispatch)
            res.CreatedOrder.Status = OrderStatuses.ReadyForDispatch;
            await db.SaveChangesAsync();

            // Mozo presiona "Servir a la mesa"
            var okServe = await service.ServeOrderAsync(orderId);
            Assert.True(okServe.Succeeded);

            var updatedOrder = await db.Orders.FindAsync(orderId);
            Assert.Equal(OrderStatuses.Served, updatedOrder!.Status);

            var detail = await service.GetTableConsumptionAsync(1);
            Assert.False(detail!.Orders[0].IsReadyToServe);
            Assert.Equal("Servido", detail.Orders[0].StatusDisplay);
        }

        [Fact]
        public async Task CA04_GenerateBill_EmitsBillAndMarksTableIsBilled()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var req = CreateOrderRequest(1, "Pedro", false, (10, 2), (14, 1)); // 2x24 + 1x7 = S/ 55
            await service.CreateAsync(req, "waiter1");

            var bill = await service.GenerateBillAsync(1, "waiter1");
            Assert.NotNull(bill);
            Assert.Equal(55m, bill!.Total);
            Assert.Equal("Mesa 1", bill.TableLabel);
            Assert.Equal("Pedro", bill.CustomerName);
            Assert.Equal("Carlos Ramos", bill.WaiterName);
            Assert.Equal(2, bill.ConsolidatedItems.Count);

            var table = await db.RestaurantTables.FindAsync(1);
            Assert.True(table!.IsBilled);
        }

        [Fact]
        public async Task CA05_FreeTable_RequiresBillFirst_ThenFreesTableAndClosesOrders()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            var req = CreateOrderRequest(1, "Lucia", false, (10, 1));
            var created = await service.CreateAsync(req, "waiter1");
            var orderId = created.CreatedOrder!.Id;

            // Detalle 4: Solo se puede liberar la mesa después de generar la boleta
            var failFree = await service.FreeTableAsync(1);
            Assert.False(failFree.Succeeded);
            Assert.Equal("Solo se puede liberar la mesa después de generar la boleta.", failFree.ErrorMessage);

            // Generar boleta
            await service.GenerateBillAsync(1, "waiter1");

            // Ahora sí se puede liberar la mesa
            var okFree = await service.FreeTableAsync(1);
            Assert.True(okFree.Succeeded);

            var table = await db.RestaurantTables.FindAsync(1);
            Assert.Equal(TableStatuses.Free, table!.Status);
            Assert.False(table.IsBilled);

            var order = await db.Orders.FindAsync(orderId);
            Assert.Equal(OrderStatuses.Delivered, order!.Status);

            // Ahora la mesa figura como Libre en el salón
            var detail = await service.GetTableConsumptionAsync(1);
            Assert.True(detail!.IsFree);
        }
    }
}
