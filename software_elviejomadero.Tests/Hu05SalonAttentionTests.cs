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

        [Fact]
        public async Task CA02_MultipleActiveOrders_AccumulateTotalConsumptionAndKeepInitialSendTime()
        {
            using var db = Context();
            var service = new SalonOrderService(db, new OrderService(db));

            // Primer pedido enviado a cocina hace 10 minutos
            var initialSendTime = DateTime.UtcNow.AddMinutes(-10);
            var table = await db.RestaurantTables.FindAsync(1);
            table!.Status = TableStatuses.Occupied;

            var order1 = new Order
            {
                OrderCode = "#M1-01",
                OrderType = OrderTypes.DineIn,
                TableId = 1,
                Status = OrderStatuses.InKitchen,
                TotalAmount = 50m,
                EstimatedServiceMinutes = 20,
                CreatedAt = initialSendTime,
                CustomerName = "Carlos"
            };

            // Segundo pedido enviado a cocina hace 2 minutos
            var order2 = new Order
            {
                OrderCode = "#M1-02",
                OrderType = OrderTypes.DineIn,
                TableId = 1,
                Status = OrderStatuses.InKitchen,
                TotalAmount = 30m,
                EstimatedServiceMinutes = 15,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                CustomerName = "Carlos"
            };

            db.Orders.AddRange(order1, order2);
            await db.SaveChangesAsync();

            var attentions = await service.GetTableAttentionAsync();
            var attention = Assert.Single(attentions);

            Assert.Equal(1, attention.TableId);
            // El consumo total de la mesa suma todos sus pedidos activos: 50 + 30 = 80
            Assert.Equal(80m, attention.TotalConsumption);
            // El tiempo transcurrido cuenta desde el primer envío a cocina
            Assert.Equal(DateTime.SpecifyKind(initialSendTime, DateTimeKind.Utc), attention.SentToKitchenAtUtc);
        }

        [Fact]
        public async Task CA03_SalonOrderController_Board_IncludesAttentionInViewModel()
        {
            using var db = Context();
            var salonService = new SalonOrderService(db, new OrderService(db));
            var dishService = new DishService(db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<DishService>());
            var controller = new Controllers.SalonOrderController(salonService, dishService)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
                }
            };

            var created = await salonService.CreateAsync(Request((1, 2)), "mozo");
            Assert.True(created.Succeeded);

            var actionResult = await controller.Board("Todas");
            var partialView = Assert.IsType<Microsoft.AspNetCore.Mvc.PartialViewResult>(actionResult);
            var model = Assert.IsType<SalonTablesViewModel>(partialView.Model);

            Assert.NotEmpty(model.Attention);
            var tableAttention = model.Attention.FirstOrDefault(a => a.TableId == 1);
            Assert.NotNull(tableAttention);
            Assert.Equal(30, tableAttention.EstimatedServiceMinutes); // 15 min * 2 = 30
            Assert.Equal(48m, tableAttention.TotalConsumption);      // 24 * 2 = 48
        }
    }
}
