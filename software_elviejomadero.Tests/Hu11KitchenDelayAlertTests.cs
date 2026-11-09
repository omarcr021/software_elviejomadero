using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    // HU-11: Alertar retraso del pedido (Rol: Cocinero)
    public class Hu11KitchenDelayAlertTests
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

            db.RestaurantTables.Add(new RestaurantTable { Id = 1, Number = "M2" });
            db.Categories.Add(new Category { Id = 1, Name = "Especialidades" });
            db.Dishes.Add(new Dish { Id = 10, Name = "1/4 Pollo Broaster Clásico", Price = 24m, CategoryId = 1, IsActive = true });

            db.SaveChanges();
            return db;
        }

        private static Order CreateSampleOrder(string code, string status, DateTime createdAt, int? tableId = 1) => new()
        {
            OrderCode = code,
            OrderType = OrderTypes.DineIn,
            Status = status,
            CustomerName = "Cindy",
            CustomerPhone = "987654321",
            PaymentMethod = PaymentMethods.Cash,
            ReceptionistId = "rec-1",
            TableId = tableId,
            CreatedAt = createdAt,
            TotalAmount = 24m,
            TotalItemsCount = 1,
            Items = new List<OrderItem>
            {
                new() { DishId = 10, DishName = "1/4 Pollo Broaster Clásico", Quantity = 1, UnitPrice = 24m, Subtotal = 24m }
            }
        };

        [Fact]
        public void CA01_OrderInNuevos_Over20Minutes_IsMarkedAsDelayed_AndHasAlertMessage()
        {
            var order = new KitchenOrderViewModel
            {
                Id = 1,
                Code = "F71",
                Status = OrderStatus.Nuevo,
                CreatedAt = DateTime.UtcNow.AddMinutes(-21)
            };

            Assert.True(order.IsDelayed);
            Assert.Equal("Demora: 21 min", order.DisplayElapsedText);
            Assert.Equal("Esta comanda lleva más de 20 minutos en Nuevos. Inicia la preparación.", KitchenOrderViewModel.DelayAlertMessage);
        }

        [Fact]
        public void CA01_OrderInNuevos_Exactly20Minutes_IsMarkedAsDelayed()
        {
            var order = new KitchenOrderViewModel
            {
                Id = 2,
                Code = "R52",
                Status = OrderStatus.Nuevo,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            };

            Assert.True(order.IsDelayed);
            Assert.Equal("Demora: 20 min", order.DisplayElapsedText);
        }

        [Fact]
        public void CA01_OrderInNuevos_Under20Minutes_IsNotDelayed()
        {
            var order = new KitchenOrderViewModel
            {
                Id = 3,
                Code = "A10",
                Status = OrderStatus.Nuevo,
                CreatedAt = DateTime.UtcNow.AddMinutes(-12)
            };

            Assert.False(order.IsDelayed);
            Assert.Equal("hace 12 min", order.DisplayElapsedText);
        }

        [Fact]
        public void CA02_DemoraText_ShowsAccumulatedMinutes()
        {
            var order = new KitchenOrderViewModel
            {
                Id = 4,
                Code = "B22",
                Status = OrderStatus.Nuevo,
                CreatedAt = DateTime.UtcNow.AddMinutes(-45)
            };

            Assert.True(order.IsDelayed);
            Assert.Equal(45, order.ElapsedMinutes);
            Assert.Equal("Demora: 45 min", order.DisplayElapsedText);
        }

        [Fact]
        public void CA03_OrderInPreparation_Over20Minutes_IsNotDelayed()
        {
            // Detalle 3: La alerta solo se aplica a los pedidos de la columna "Nuevos"
            var order = new KitchenOrderViewModel
            {
                Id = 5,
                Code = "C33",
                Status = OrderStatus.EnPreparacion,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            Assert.False(order.IsDelayed);
            Assert.DoesNotContain("Demora:", order.DisplayElapsedText);
            Assert.Contains("hace 30 min", order.DisplayElapsedText);
        }

        [Fact]
        public async Task CA03_StartPreparation_TransitionsOrderToInPreparation_RemovingAlert()
        {
            using var db = CreateInMemoryContext();

            // Pedido que lleva 25 minutos en Nuevos (retrasado)
            var order = CreateSampleOrder("F71", OrderStatuses.New, DateTime.UtcNow.AddMinutes(-25));
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var service = new KitchenService(db);

            // 1. Verificar que inicialmente en el tablero está en Nuevos y retrasado
            var boardBefore = await service.GetBoardAsync();
            Assert.Single(boardBefore.NewOrders);
            Assert.True(boardBefore.NewOrders[0].IsDelayed);
            Assert.Contains("Demora:", boardBefore.NewOrders[0].DisplayElapsedText);

            // 2. Cocinero selecciona "Empezar preparación"
            var startResult = await service.StartPreparationAsync(order.Id, "cook-1");
            Assert.True(startResult.Success);

            // 3. Verificar que pasa a "En preparación" y la alerta de demora se retira
            var boardAfter = await service.GetBoardAsync();
            Assert.Empty(boardAfter.NewOrders);
            Assert.Single(boardAfter.InPreparationOrders);

            var prepOrder = boardAfter.InPreparationOrders[0];
            Assert.Equal("F71", prepOrder.Code);
            Assert.Equal(OrderStatus.EnPreparacion, prepOrder.Status);
            Assert.False(prepOrder.IsDelayed);
            Assert.DoesNotContain("Demora:", prepOrder.DisplayElapsedText);
        }
    }
}
