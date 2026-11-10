using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    // HU-10: Priorizar pedidos (Rol: Cocinero)
    public class Hu10PrioritizeOrdersTests
    {
        private static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ApplicationDbContext(options);

            db.Users.AddRange(
                new ApplicationUser { Id = "cook-1", UserName = "cocinero", FullName = "Cocinero Test", DNI = "11112222", Phone = "999888777" },
                new ApplicationUser { Id = "waiter-1", UserName = "mozo", FullName = "Mozo Test", DNI = "33334444", Phone = "999111222" }
            );

            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "M1", Status = TableStatuses.Occupied },
                new RestaurantTable { Id = 2, Number = "M2", Status = TableStatuses.Occupied }
            );

            db.Categories.Add(new Category { Id = 1, Name = "Especialidades" });
            db.Dishes.AddRange(
                new Dish { Id = 10, Name = "1/4 Pollo Broaster Clásico", Price = 24m, CategoryId = 1, IsActive = true },
                new Dish { Id = 11, Name = "1/2 Pollo Broaster Familiar", Price = 42m, CategoryId = 1, IsActive = true }
            );

            db.SaveChanges();
            return db;
        }

        private static Order CreateSampleOrder(int id, string code, string status, DateTime createdAt, int? tableId = 1) => new()
        {
            Id = id,
            OrderCode = code,
            OrderType = OrderTypes.DineIn,
            Status = status,
            CustomerName = "Juan Perez",
            CustomerPhone = "987654321",
            PaymentMethod = PaymentMethods.Cash,
            ReceptionistId = "waiter-1",
            TableId = tableId,
            CreatedAt = createdAt,
            TotalAmount = 66m,
            TotalItemsCount = 2,
            Items = new List<OrderItem>
            {
                new() { DishId = 10, DishName = "1/4 Pollo Broaster Clásico", Quantity = 1, UnitPrice = 24m, Subtotal = 24m },
                new() { DishId = 11, DishName = "1/2 Pollo Broaster Familiar", Quantity = 1, UnitPrice = 42m, Subtotal = 42m }
            }
        };

        [Fact]
        public async Task CA01_NewOrder_AppearsInNuevos_WithCodeTableItemsAndRecien()
        {
            using var db = CreateInMemoryContext();
            var service = new KitchenService(db);

            // Pedido tomado y enviado a cocina hace 10 segundos
            var now = DateTime.UtcNow;
            var order = CreateSampleOrder(1, "#S24", OrderStatuses.InKitchen, now.AddSeconds(-10), tableId: 1);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var board = await service.GetBoardAsync();

            var newOrder = Assert.Single(board.NewOrders);
            Assert.Equal("#S24", newOrder.Code);
            Assert.Equal(1, newOrder.TableNumber);
            Assert.Equal("Mesa 1", newOrder.OriginLabel);
            Assert.Equal("Juan Perez", newOrder.CustomerName);
            Assert.Equal(2, newOrder.Items.Count);
            Assert.Equal("1/4 Pollo Broaster Clásico", newOrder.Items[0].DishName);
            Assert.Equal(1, newOrder.Items[0].Quantity);
            // Tiempo transcurrido "recién" (< 1 minuto)
            Assert.Equal("recién", newOrder.DisplayElapsedText);
        }

        [Fact]
        public async Task CA02_MultipleOrdersInNuevos_OrderedByWaitTimeDescending_EarliestCreatedAtFirst()
        {
            using var db = CreateInMemoryContext();
            var service = new KitchenService(db);

            var now = DateTime.UtcNow;

            // Pedido A: enviado hace 15 minutos (mayor tiempo de espera)
            var orderA = CreateSampleOrder(10, "#D75", OrderStatuses.InKitchen, now.AddMinutes(-15));
            // Pedido B: enviado hace 5 minutos
            var orderB = CreateSampleOrder(20, "#R52", OrderStatuses.InKitchen, now.AddMinutes(-5));
            // Pedido C: enviado hace 20 segundos ("recién", menor tiempo de espera)
            var orderC = CreateSampleOrder(30, "#L15", OrderStatuses.InKitchen, now.AddSeconds(-20));

            // Se agregan en orden desordenado
            db.Orders.AddRange(orderC, orderA, orderB);
            await db.SaveChangesAsync();

            var board = await service.GetBoardAsync();

            Assert.Equal(3, board.NewOrders.Count);
            // Primero el de mayor tiempo de espera (#D75, hace 15 min), luego #R52 (5 min), luego #L15 (recién)
            Assert.Equal("#D75", board.NewOrders[0].Code);
            Assert.Equal("#R52", board.NewOrders[1].Code);
            Assert.Equal("#L15", board.NewOrders[2].Code);
        }

        [Fact]
        public async Task CA03_OrdersWithSameWaitTime_OrderedBySendTimeOrRegistrationOrder()
        {
            using var db = CreateInMemoryContext();
            var service = new KitchenService(db);

            var exactSameTime = DateTime.UtcNow.AddMinutes(-8);

            // Dos pedidos enviados exactamente al mismo tiempo
            var orderFirst = CreateSampleOrder(100, "#F71", OrderStatuses.InKitchen, exactSameTime);
            var orderSecond = CreateSampleOrder(101, "#F72", OrderStatuses.InKitchen, exactSameTime);

            db.Orders.AddRange(orderSecond, orderFirst);
            await db.SaveChangesAsync();

            var board = await service.GetBoardAsync();

            Assert.Equal(2, board.NewOrders.Count);
            // Desempate: primero el que se registró/envió antes (Id menor)
            Assert.Equal("#F71", board.NewOrders[0].Code);
            Assert.Equal("#F72", board.NewOrders[1].Code);
        }

        [Fact]
        public void CA04_FormatElapsed_ReturnsCorrectTexts_AndMaintainsPriority()
        {
            // Menos de 1 minuto -> "recién"
            Assert.Equal("recién", KitchenOrderViewModel.FormatElapsed(0));
            Assert.Equal("recién", KitchenOrderViewModel.FormatElapsed(30 * 1000));
            Assert.Equal("recién", KitchenOrderViewModel.FormatElapsed(59 * 1000));

            // Transcurre 1 minuto -> "hace 1 min"
            Assert.Equal("hace 1 min", KitchenOrderViewModel.FormatElapsed(60 * 1000));

            // Transcurren 6 minutos -> "hace 6 min" (ejemplo explícito de CA-04)
            Assert.Equal("hace 6 min", KitchenOrderViewModel.FormatElapsed(6 * 60 * 1000));

            // 19 minutos -> "hace 19 min"
            Assert.Equal("hace 19 min", KitchenOrderViewModel.FormatElapsed(19 * 60 * 1000));

            // 20 minutos (alerta de demora HU-11)
            var orderDelayed = new KitchenOrderViewModel
            {
                Status = OrderStatus.Nuevo,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            };
            Assert.True(orderDelayed.IsDelayed);
            Assert.Equal("Demora: 20 min", orderDelayed.DisplayElapsedText);
        }

        [Fact]
        public async Task Detail03_InPreparationOrders_MaintainsSameWaitTimePriorityOrder()
        {
            using var db = CreateInMemoryContext();
            var service = new KitchenService(db);

            var now = DateTime.UtcNow;

            // Pedido en preparación que lleva 25 minutos
            var prepOld = CreateSampleOrder(201, "#P01", OrderStatuses.InPreparation, now.AddMinutes(-25));
            // Pedido en preparación que lleva 10 minutos
            var prepNew = CreateSampleOrder(202, "#P02", OrderStatuses.InPreparation, now.AddMinutes(-10));

            db.Orders.AddRange(prepNew, prepOld);
            await db.SaveChangesAsync();

            var board = await service.GetBoardAsync();

            Assert.Equal(2, board.InPreparationOrders.Count);
            // Primero el de mayor tiempo de espera
            Assert.Equal("#P01", board.InPreparationOrders[0].Code);
            Assert.Equal("#P02", board.InPreparationOrders[1].Code);
        }
    }
}
