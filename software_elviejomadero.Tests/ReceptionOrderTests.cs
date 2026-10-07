using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Tests
{
    public class ReceptionOrderTests
    {
        private static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new ApplicationDbContext(options);

            var receptionist = new ApplicationUser
            {
                Id = "recep-01",
                UserName = "lvega",
                FullName = "Lucía Vega",
                DNI = "71234567",
                Phone = "987654321",
                IsActive = true
            };

            var category = new Category { Id = 1, Name = "Combos Broaster", IsActive = true };

            var dish1 = new Dish
            {
                Id = 1,
                Name = "1/4 Pollo Broaster Clásico",
                Price = 24.00m,
                PreparationTimeMinutes = 15,
                CategoryId = 1,
                IsActive = true
            };

            var dish2 = new Dish
            {
                Id = 2,
                Name = "Pollo Broaster Entero",
                Price = 78.00m,
                PreparationTimeMinutes = 25,
                CategoryId = 1,
                IsActive = true
            };

            context.Users.Add(receptionist);
            context.Categories.Add(category);
            context.Dishes.AddRange(dish1, dish2);
            context.SaveChanges();

            return context;
        }

        [Fact]
        public async Task CreateReceptionOrderAsync_ValidDeliveryOrder_RegistersWithCodeAndStatusNew()
        {
            using var context = CreateInMemoryContext();
            var service = new OrderService(context);

            var model = new CreateReceptionOrderViewModel
            {
                OrderType = OrderTypes.Delivery,
                CustomerName = "Maria Gomez",
                CustomerPhone = "947832487",
                DeliveryAddress = "Av. Javier Prado 1256",
                Latitude = -12.0864,
                Longitude = -77.0015,
                PaymentMethod = PaymentMethods.Yape,
                AdditionalNote = "Sin ají, todas las cremas",
                Items = new List<CreateOrderItemInputModel>
                {
                    new() { DishId = 1, Quantity = 2 }, // 48.00
                    new() { DishId = 2, Quantity = 1 }  // 78.00
                }
            };

            var (succeeded, errorMessage, createdOrder) = await service.CreateReceptionOrderAsync(model, "recep-01");

            Assert.True(succeeded);
            Assert.Null(errorMessage);
            Assert.NotNull(createdOrder);
            Assert.StartsWith("#D", createdOrder!.OrderCode);
            Assert.Equal(OrderStatuses.New, createdOrder.Status);
            Assert.Equal(OrderTypes.Delivery, createdOrder.OrderType);
            Assert.Equal(3, createdOrder.TotalItemsCount);
            Assert.Equal(126.00m, createdOrder.TotalAmount);
            Assert.Equal("recep-01", createdOrder.ReceptionistId);
        }

        [Fact]
        public async Task CreateReceptionOrderAsync_ValidTakeoutOrder_RegistersAsParaLlevar()
        {
            using var context = CreateInMemoryContext();
            var service = new OrderService(context);

            var model = new CreateReceptionOrderViewModel
            {
                OrderType = OrderTypes.Takeout,
                CustomerName = "Genesis",
                CustomerPhone = "957494343",
                PaymentMethod = PaymentMethods.Plin,
                AdditionalNote = "Sin Ensalada",
                Items = new List<CreateOrderItemInputModel>
                {
                    new() { DishId = 1, Quantity = 1 }, // 24.00
                    new() { DishId = 2, Quantity = 1 }  // 78.00
                }
            };

            var (succeeded, errorMessage, createdOrder) = await service.CreateReceptionOrderAsync(model, "recep-01");

            Assert.True(succeeded);
            Assert.Null(errorMessage);
            Assert.NotNull(createdOrder);
            Assert.StartsWith("#T", createdOrder!.OrderCode);
            Assert.Equal(OrderTypes.Takeout, createdOrder.OrderType);
            Assert.Equal(OrderTypes.Takeout, createdOrder.DeliveryAddress);
            Assert.Equal(OrderStatuses.New, createdOrder.Status);
            Assert.Equal(102.00m, createdOrder.TotalAmount);
        }

        [Fact]
        public async Task CreateReceptionOrderAsync_DeliveryWithoutAddressOrPin_ReturnsFailure()
        {
            using var context = CreateInMemoryContext();
            var service = new OrderService(context);

            var modelNoPin = new CreateReceptionOrderViewModel
            {
                OrderType = OrderTypes.Delivery,
                CustomerName = "Juan Lopez",
                CustomerPhone = "987654321",
                DeliveryAddress = "Av. Javier Prado 6463",
                Latitude = null,
                Longitude = null,
                PaymentMethod = PaymentMethods.Cash,
                Items = new List<CreateOrderItemInputModel>
                {
                    new() { DishId = 1, Quantity = 1 }
                }
            };

            var result = await service.CreateReceptionOrderAsync(modelNoPin, "recep-01");

            Assert.False(result.Succeeded);
            Assert.Contains("mapa", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateReceptionOrderAsync_WithoutDishes_ReturnsFailure()
        {
            using var context = CreateInMemoryContext();
            var service = new OrderService(context);

            var modelEmptyItems = new CreateReceptionOrderViewModel
            {
                OrderType = OrderTypes.Takeout,
                CustomerName = "Genesis",
                CustomerPhone = "957494343",
                PaymentMethod = PaymentMethods.Yape,
                Items = new List<CreateOrderItemInputModel>()
            };

            var result = await service.CreateReceptionOrderAsync(modelEmptyItems, "recep-01");

            Assert.False(result.Succeeded);
            Assert.Contains("al menos un plato", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateReceptionOrderAsync_InvalidPaymentMethod_ReturnsFailure()
        {
            using var context = CreateInMemoryContext();
            var service = new OrderService(context);

            var modelInvalidPay = new CreateReceptionOrderViewModel
            {
                OrderType = OrderTypes.Takeout,
                CustomerName = "Genesis",
                CustomerPhone = "957494343",
                PaymentMethod = "Bitcoin",
                Items = new List<CreateOrderItemInputModel>
                {
                    new() { DishId = 1, Quantity = 1 }
                }
            };

            var result = await service.CreateReceptionOrderAsync(modelInvalidPay, "recep-01");

            Assert.False(result.Succeeded);
            Assert.Contains("Yape, Plin o Efectivo", result.ErrorMessage!);
        }

        [Fact]
        public async Task GetReceptionOrdersAsync_ReturnsRegisteredOrdersForReceptionScreen()
        {
            using var context = CreateInMemoryContext();
            var service = new OrderService(context);

            await service.CreateReceptionOrderAsync(new CreateReceptionOrderViewModel
            {
                OrderType = OrderTypes.Takeout,
                CustomerName = "Genesis",
                CustomerPhone = "957494343",
                PaymentMethod = PaymentMethods.Yape,
                Items = new List<CreateOrderItemInputModel> { new() { DishId = 1, Quantity = 2 } }
            }, "recep-01");

            var orders = await service.GetReceptionOrdersAsync();

            Assert.Single(orders);
            Assert.Equal("Genesis", orders[0].CustomerName);
            Assert.Equal("Cliente en local · Para llevar", orders[0].DisplayAddress);
            Assert.Equal(2, orders[0].TotalItemsCount);
            Assert.Equal(48.00m, orders[0].TotalAmount);
            Assert.Equal("Nuevo", orders[0].Status);
        }
    }
}
