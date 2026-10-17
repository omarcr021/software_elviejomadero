using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;

namespace software_elviejomadero.Tests
{
    public class KitchenServiceTests
    {
        private static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ApplicationDbContext(options);

            db.Users.AddRange(
                new ApplicationUser { Id = "cook-1", UserName = "cocinero", FullName = "Cocinero Test", DNI = "11112222", Phone = "999888777" },
                new ApplicationUser { Id = "rec-1", UserName = "recepcionista", FullName = "Recep Test", DNI = "33334444", Phone = "999111222" }
            );

            db.RestaurantTables.Add(new RestaurantTable { Id = 1, Number = "M3" });
            db.Categories.Add(new Category { Id = 1, Name = "Combos" });
            db.Dishes.Add(new Dish { Id = 10, Name = "1/4 Pollo Broaster", Price = 24m, CategoryId = 1, IsActive = true });

            db.SaveChanges();
            return db;
        }

        private static Order CreateSampleOrder(string code, string type, string status, int? tableId = null) => new()
        {
            OrderCode = code,
            OrderType = type,
            Status = status,
            CustomerName = "Carlos Cliente",
            CustomerPhone = "987654321",
            PaymentMethod = PaymentMethods.Cash,
            ReceptionistId = "rec-1",
            TableId = tableId,
            TotalAmount = 24m,
            TotalItemsCount = 1,
            Items = new List<OrderItem>
            {
                new() { DishId = 10, DishName = "1/4 Pollo Broaster", Quantity = 1, UnitPrice = 24m, Subtotal = 24m }
            }
        };

        [Fact]
        public async Task GetBoardAsync_DistributesActiveOrdersIntoCorrectColumns()
        {
            using var db = CreateInMemoryContext();

            var o1 = CreateSampleOrder("PED-01", OrderTypes.DineIn, OrderStatuses.New, 1);
            var o2 = CreateSampleOrder("PED-02", OrderTypes.Takeout, OrderStatuses.InKitchen);
            var o3 = CreateSampleOrder("PED-03", OrderTypes.Delivery, OrderStatuses.InPreparation);
            var o4 = CreateSampleOrder("PED-04", OrderTypes.Delivery, OrderStatuses.ReadyForDispatch);
            var o5 = CreateSampleOrder("PED-05", OrderTypes.Delivery, OrderStatuses.OnTheWay);
            var o6 = CreateSampleOrder("PED-06", OrderTypes.Delivery, OrderStatuses.Delivered);

            db.Orders.AddRange(o1, o2, o3, o4, o5, o6);
            await db.SaveChangesAsync();

            var service = new KitchenService(db);
            var board = await service.GetBoardAsync();

            Assert.Equal(2, board.NewOrders.Count);
            Assert.Contains(board.NewOrders, o => o.Code == "PED-01" && o.TableNumber == 3);
            Assert.Contains(board.NewOrders, o => o.Code == "PED-02");

            Assert.Single(board.InPreparationOrders);
            Assert.Equal("PED-03", board.InPreparationOrders[0].Code);

            Assert.Single(board.ReadyOrders);
            Assert.Equal("PED-04", board.ReadyOrders[0].Code);

            // En camino y Entregados no deben aparecer en el tablero de cocina
            Assert.DoesNotContain(board.NewOrders, o => o.Code == "PED-05" || o.Code == "PED-06");
            Assert.DoesNotContain(board.InPreparationOrders, o => o.Code == "PED-05" || o.Code == "PED-06");
            Assert.DoesNotContain(board.ReadyOrders, o => o.Code == "PED-05" || o.Code == "PED-06");
        }

        [Fact]
        public async Task StartPreparationAsync_TransitionsOrderAndRecordsCookAndTimestamp()
        {
            using var db = CreateInMemoryContext();
            var order = CreateSampleOrder("PED-10", OrderTypes.DineIn, OrderStatuses.New, 1);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var service = new KitchenService(db);
            var result = await service.StartPreparationAsync(order.Id, "cook-1");

            Assert.True(result.Success, result.Message);

            var updated = await db.Orders.FindAsync(order.Id);
            Assert.NotNull(updated);
            Assert.Equal(OrderStatuses.InPreparation, updated!.Status);
            Assert.NotNull(updated.PreparationStartedAt);
            Assert.Equal("cook-1", updated.PreparedByUserId);
        }

        [Fact]
        public async Task StartPreparationAsync_RejectsIfAlreadyInPreparationOrReady()
        {
            using var db = CreateInMemoryContext();
            var order = CreateSampleOrder("PED-11", OrderTypes.Delivery, OrderStatuses.InPreparation);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var service = new KitchenService(db);
            var result = await service.StartPreparationAsync(order.Id, "cook-1");

            Assert.False(result.Success);
            Assert.Contains("ya está en preparación", result.Message);
        }

        [Fact]
        public async Task MarkAsReadyAsync_TransitionsFromPreparationToReady_WithAppropriateLabel()
        {
            using var db = CreateInMemoryContext();
            var salonOrder = CreateSampleOrder("PED-20", OrderTypes.DineIn, OrderStatuses.InPreparation, 1);
            var deliveryOrder = CreateSampleOrder("PED-21", OrderTypes.Delivery, OrderStatuses.InPreparation);
            db.Orders.AddRange(salonOrder, deliveryOrder);
            await db.SaveChangesAsync();

            var service = new KitchenService(db);

            var resultSalon = await service.MarkAsReadyAsync(salonOrder.Id);
            Assert.True(resultSalon.Success);
            Assert.Contains("Listo para servir", resultSalon.Message);

            var updatedSalon = await db.Orders.FindAsync(salonOrder.Id);
            Assert.Equal(OrderStatuses.ReadyForDispatch, updatedSalon!.Status);
            Assert.NotNull(updatedSalon.ReadyAt);

            var resultDelivery = await service.MarkAsReadyAsync(deliveryOrder.Id);
            Assert.True(resultDelivery.Success);
            Assert.Contains("Listo para reparto", resultDelivery.Message);

            var updatedDelivery = await db.Orders.FindAsync(deliveryOrder.Id);
            Assert.Equal(OrderStatuses.ReadyForDispatch, updatedDelivery!.Status);
            Assert.NotNull(updatedDelivery.ReadyAt);
        }

        [Fact]
        public async Task MarkAsReadyAsync_RejectsIfOrderIsNotInPreparation()
        {
            using var db = CreateInMemoryContext();
            var newOrder = CreateSampleOrder("PED-30", OrderTypes.Takeout, OrderStatuses.New);
            db.Orders.Add(newOrder);
            await db.SaveChangesAsync();

            var service = new KitchenService(db);
            var result = await service.MarkAsReadyAsync(newOrder.Id);

            Assert.False(result.Success);
            Assert.Contains("Primero debes empezar la preparación", result.Message);

            var notFoundResult = await service.MarkAsReadyAsync(99999);
            Assert.False(notFoundResult.Success);
            Assert.Contains("no existe", notFoundResult.Message);
        }
    }
}
