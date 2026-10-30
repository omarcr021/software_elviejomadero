using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Tests
{
    public class Hu05SalonAttentionTests
    {
        private static ApplicationDbContext Context()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new ApplicationDbContext(options);
            db.Users.Add(new ApplicationUser
            {
                Id = "mozo", UserName = "mozo", FullName = "Mozo de prueba",
                DNI = "98712345", Phone = "999999999", IsActive = true
            });
            db.Categories.Add(new Category { Id = 1, Name = "Comidas" });
            db.Dishes.AddRange(
                new Dish { Id = 1, Name = "Pollo", Price = 24m, PreparationTimeMinutes = 15, CategoryId = 1, IsActive = true },
                new Dish { Id = 2, Name = "Papas", Price = 10m, PreparationTimeMinutes = 10, CategoryId = 1, IsActive = true });
            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "M1", Status = TableStatuses.Free },
                new RestaurantTable { Id = 2, Number = "M2", Status = TableStatuses.Free });
            db.SaveChanges();
            return db;
        }

        private static SalonNewOrderViewModel Request(params (int dish, int qty)[] items) => new()
        {
            TableId = 1, CustomerName = "Juan",
            Items = items.Select(x => new CreateOrderItemInputModel
            {
                DishId = x.dish, Quantity = x.qty
            }).ToList()
        };

        [Fact]
        public void Estimator_UsesPreparationTimeTimesQuantity()
        {
            Assert.Equal(40, SalonAttentionEstimator.CalculateMinutes(new[] { (15, 2), (10, 1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SalonAttentionEstimator.CalculateMinutes(new[] { (-1, 1) }));
            Assert.Equal(0, SalonAttentionEstimator.CalculateMinutes(Array.Empty<(int, int)>()));
        }

        [Fact]
        public async Task ConfirmingSalonOrder_StoresEstimateAndUtcSendTime()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            var before = DateTime.UtcNow.AddSeconds(-2);

            var result = await service.CreateAsync(Request((1, 2), (2, 1)), "mozo");

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.NotNull(result.CreatedOrder);
            Assert.Equal(40, result.CreatedOrder.EstimatedServiceMinutes);
            Assert.True(result.CreatedOrder.CreatedAt >= before);
            Assert.Equal(58m, result.CreatedOrder.TotalAmount);

            var statuses = await service.GetTableAttentionAsync();
            var attention = Assert.Single(statuses);
            Assert.Equal(1, attention.TableId);
            Assert.Equal(40, attention.EstimatedServiceMinutes);
            Assert.Equal(58m, attention.TotalConsumption);
            Assert.Equal(DateTimeKind.Utc, attention.SentToKitchenAtUtc.Kind);
        }

        [Fact]
        public async Task NewItems_RefreshEstimateButKeepInitialSendTime()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            var created = await service.CreateAsync(Request((1, 1)), "mozo");
            Assert.True(created.Succeeded);
            var sent = created.CreatedOrder!.CreatedAt;

            var added = await service.AddItemsAsync(Request((2, 2)), "mozo");

            Assert.True(added.Succeeded, added.ErrorMessage);
            Assert.Equal(35, added.UpdatedOrder!.EstimatedServiceMinutes);
            Assert.Equal(sent, added.UpdatedOrder.CreatedAt);
            var status = Assert.Single(await service.GetTableAttentionAsync());
            Assert.Equal(44m, status.TotalConsumption);
            Assert.Equal(35, status.EstimatedServiceMinutes);
        }

        [Fact]
        public async Task FreeTables_AndDeliveredOrders_DoNotHaveAttentionCounter()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));
            Assert.Empty(await service.GetTableAttentionAsync());
            var created = await service.CreateAsync(Request((1, 1)), "mozo");
            created.CreatedOrder!.Status = OrderStatuses.Delivered;
            await db.SaveChangesAsync();
            Assert.Empty(await service.GetTableAttentionAsync());
        }
    }
}
