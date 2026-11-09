using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;
using Xunit;

namespace software_elviejomadero.Tests
{
    public class Hu04FilterTablesTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new ApplicationDbContext(options);

            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "M1", Status = TableStatuses.Free, IsActive = true },
                new RestaurantTable { Id = 2, Number = "M2", Status = TableStatuses.Occupied, IsActive = true },
                new RestaurantTable { Id = 3, Number = "M3", Status = TableStatuses.Free, IsActive = true },
                new RestaurantTable { Id = 4, Number = "M4", Status = TableStatuses.Occupied, IsActive = true },
                new RestaurantTable { Id = 5, Number = "M5", Status = TableStatuses.Occupied, IsActive = true },
                new RestaurantTable { Id = 6, Number = "M6", Status = TableStatuses.Free, IsActive = true }
            );
            db.SaveChanges();
            return db;
        }

        // CA-01: Filtro "Todas" retorna todas las mesas del salón
        [Theory]
        [InlineData("Todas")]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetTablesAsync_WithFilterTodas_ReturnsAllTables(string? filter)
        {
            using var db = CreateContext();
            var service = new SalonOrderService(db, new OrderService(db));

            var tables = await service.GetTablesAsync(filter);

            Assert.Equal(6, tables.Count);
            Assert.Contains(tables, t => t.Status == TableStatuses.Free);
            Assert.Contains(tables, t => t.Status == TableStatuses.Occupied);
        }

        // CA-02: Filtro "Libres" muestra únicamente las mesas en estado Libre
        [Theory]
        [InlineData("Libres")]
        [InlineData("libres")]
        [InlineData("Libre")]
        public async Task GetTablesAsync_WithFilterLibres_ReturnsOnlyFreeTables(string filter)
        {
            using var db = CreateContext();
            var service = new SalonOrderService(db, new OrderService(db));

            var tables = await service.GetTablesAsync(filter);

            Assert.Equal(3, tables.Count);
            Assert.All(tables, t => Assert.Equal(TableStatuses.Free, t.Status));
            Assert.Equal(new[] { "M1", "M3", "M6" }, tables.Select(t => t.Number));
        }

        // CA-03: Filtro "Ocupadas" muestra únicamente las mesas en estado Ocupada
        [Theory]
        [InlineData("Ocupadas")]
        [InlineData("ocupadas")]
        [InlineData("Ocupada")]
        public async Task GetTablesAsync_WithFilterOcupadas_ReturnsOnlyOccupiedTables(string filter)
        {
            using var db = CreateContext();
            var service = new SalonOrderService(db, new OrderService(db));

            var tables = await service.GetTablesAsync(filter);

            Assert.Equal(3, tables.Count);
            Assert.All(tables, t => Assert.Equal(TableStatuses.Occupied, t.Status));
            Assert.Equal(new[] { "M2", "M4", "M5" }, tables.Select(t => t.Number));
        }

        // Detalle 1 y 2: SalonTablesViewModel conserva el filtro activo seleccionado
        [Fact]
        public void SalonTablesViewModel_DefaultFilter_IsTodas()
        {
            var vm = new SalonTablesViewModel();
            Assert.Equal("Todas", vm.CurrentFilter);
        }

        // Controller: Index y Board aceptan el filtro y lo propagan al ViewModel
        [Fact]
        public async Task SalonOrderController_Board_PassesFilterToViewModel()
        {
            using var db = CreateContext();
            var salonService = new SalonOrderService(db, new OrderService(db));
            var dishService = new DishService(db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<DishService>());
            var controller = new SalonOrderController(salonService, dishService);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };

            var result = await controller.Board("Libres");

            var partialViewResult = Assert.IsType<PartialViewResult>(result);
            var model = Assert.IsType<SalonTablesViewModel>(partialViewResult.Model);
            Assert.Equal("Libres", model.CurrentFilter);
            Assert.Equal(3, model.Tables.Count);
            Assert.All(model.Tables, t => Assert.Equal(TableStatuses.Free, t.Status));
        }
    }
}
