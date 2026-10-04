using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    public class DishTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        // 15. Crear plato correctamente
        [Fact]
        public async Task CreateDishAsync_ValidModel_PersistsInDatabase()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var category = new Category { Id = 1, Name = "Combos Broaster", IsActive = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);
            var model = new CreateDishViewModel
            {
                Name = "1/4 Pollo Broaster Clásico",
                Description = "Con papas y ensalada",
                Price = 24.00m,
                PreparationTimeMinutes = 15,
                CategoryId = 1
            };

            // Act
            var result = await service.CreateDishAsync(model);

            // Assert
            Assert.True(result.Success);
            var createdDish = await context.Dishes.FirstOrDefaultAsync(d => d.Name == "1/4 Pollo Broaster Clásico");
            Assert.NotNull(createdDish);
            Assert.Equal(24.00m, createdDish.Price);
            Assert.True(createdDish.IsActive);
        }

        // 16. No permitir precio <= 0
        [Theory]
        [InlineData(0)]
        [InlineData(-5.50)]
        public async Task CreateDishAsync_PriceZeroOrNegative_ReturnsError(decimal invalidPrice)
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var category = new Category { Id = 1, Name = "Combos", IsActive = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);
            var model = new CreateDishViewModel
            {
                Name = "Plato Inválido",
                Price = invalidPrice,
                PreparationTimeMinutes = 10,
                CategoryId = 1
            };

            // Act
            var result = await service.CreateDishAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("El precio debe ser mayor que cero.", result.ErrorMessage);
        }

        // 17. No permitir plato sin nombre
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task CreateDishAsync_EmptyName_ReturnsError(string? invalidName)
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);
            var model = new CreateDishViewModel
            {
                Name = invalidName!,
                Price = 15.00m,
                CategoryId = 1
            };

            // Act
            var result = await service.CreateDishAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("El nombre del plato es obligatorio.", result.ErrorMessage);
        }

        // 18. No permitir plato sin categoría existente
        [Fact]
        public async Task CreateDishAsync_NonExistentCategory_ReturnsError()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);
            var model = new CreateDishViewModel
            {
                Name = "Plato Huérfano",
                Price = 20.00m,
                CategoryId = 999 // Categoría no existe
            };

            // Act
            var result = await service.CreateDishAsync(model);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("La categoría seleccionada no existe o no está activa.", result.ErrorMessage);
        }

        // 19. Editar plato
        [Fact]
        public async Task UpdateDishAsync_ValidChanges_UpdatesDatabaseValues()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var cat = new Category { Id = 1, Name = "Especialidades", IsActive = true };
            var dish = new Dish
            {
                Id = 10,
                Name = "Alitas Original",
                Price = 20.00m,
                PreparationTimeMinutes = 12,
                CategoryId = 1,
                IsActive = true
            };
            context.Categories.Add(cat);
            context.Dishes.Add(dish);
            await context.SaveChangesAsync();

            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);
            var editModel = new EditDishViewModel
            {
                Id = 10,
                Name = "Alitas BBQ Picantes",
                Description = "Nueva receta BBQ",
                Price = 28.00m,
                PreparationTimeMinutes = 18,
                CategoryId = 1,
                IsActive = true
            };

            // Act
            var result = await service.UpdateDishAsync(editModel);

            // Assert
            Assert.True(result.Success);
            var updated = await context.Dishes.FindAsync(10);
            Assert.NotNull(updated);
            Assert.Equal("Alitas BBQ Picantes", updated.Name);
            Assert.Equal(28.00m, updated.Price);
            Assert.Equal(18, updated.PreparationTimeMinutes);
            Assert.NotNull(updated.UpdatedAt);
        }

        // 20. Ocultar plato (IsActive = false)
        [Fact]
        public async Task ToggleActiveAsync_ActiveDish_TogglesToInactive()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var dish = new Dish { Id = 20, Name = "Plato Visible", Price = 15m, CategoryId = 1, IsActive = true };
            context.Dishes.Add(dish);
            await context.SaveChangesAsync();

            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);

            // Act
            var result = await service.ToggleActiveAsync(20);

            // Assert
            Assert.True(result.Success);
            Assert.False(result.NewStatus);
            var updated = await context.Dishes.FindAsync(20);
            Assert.False(updated!.IsActive);
        }

        // 21. Reactivar plato (IsActive = true)
        [Fact]
        public async Task ToggleActiveAsync_InactiveDish_TogglesToActive()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var dish = new Dish { Id = 21, Name = "Plato Oculto", Price = 15m, CategoryId = 1, IsActive = false };
            context.Dishes.Add(dish);
            await context.SaveChangesAsync();

            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);

            // Act
            var result = await service.ToggleActiveAsync(21);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.NewStatus);
            var updated = await context.Dishes.FindAsync(21);
            Assert.True(updated!.IsActive);
        }

        // 22. Eliminar plato
        [Fact]
        public async Task DeleteDishAsync_ExistingDish_RemovesFromDatabase()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var dish = new Dish { Id = 30, Name = "Plato a Eliminar", Price = 10m, CategoryId = 1, IsActive = true };
            context.Dishes.Add(dish);
            await context.SaveChangesAsync();

            var service = new DishService(context, new Mock<ILogger<DishService>>().Object);

            // Act
            var result = await service.DeleteDishAsync(30);

            // Assert
            Assert.True(result.Success);
            var deleted = await context.Dishes.FindAsync(30);
            Assert.Null(deleted);
        }

        // 23. Solo Administrador puede gestionar carta (verificar Authorize)
        [Fact]
        public void DishController_RequiresAdministradorRoleAttribute()
        {
            // Act
            var authorizeAttribute = typeof(DishController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .FirstOrDefault() as AuthorizeAttribute;

            // Assert
            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Administrador", authorizeAttribute.Roles);
        }
    }
}
