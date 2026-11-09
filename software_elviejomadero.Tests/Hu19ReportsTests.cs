using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using System.Reflection;
using Xunit;

namespace software_elviejomadero.Tests
{
    // HU-19: Consultar Reporte de Ventas y Pedidos (Rol: Administrador)
    public class Hu19ReportsTests
    {
        private static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ApplicationDbContext(options);

            db.Users.AddRange(
                new ApplicationUser { Id = "admin-1", UserName = "admin", FullName = "Administrador Test", DNI = "00000001", Phone = "999888777" },
                new ApplicationUser { Id = "rec-1", UserName = "recepcionista", FullName = "Recep Test", DNI = "00000002", Phone = "999111222" }
            );

            db.RestaurantTables.AddRange(
                new RestaurantTable { Id = 1, Number = "1", Status = TableStatuses.Free },
                new RestaurantTable { Id = 2, Number = "2", Status = TableStatuses.Occupied }
            );

            db.SaveChanges();
            return db;
        }

        private static Order CreateSampleOrder(
            string code,
            string type,
            string status,
            string customer,
            string payment,
            decimal amount,
            DateTime createdAt,
            int? tableId = null) => new()
        {
            OrderCode = code,
            OrderType = type,
            Status = status,
            CustomerName = customer,
            CustomerPhone = "987654321",
            PaymentMethod = payment,
            ReceptionistId = "rec-1",
            TableId = tableId,
            TotalAmount = amount,
            TotalItemsCount = 1,
            CreatedAt = createdAt
        };

        [Fact]
        public async Task CA01_Reports_ShowsKpisAndOrdersTable()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("L15", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria Gomez", PaymentMethods.Yape, 131.00m, now.AddMinutes(-50)),
                CreateSampleOrder("D75", OrderTypes.DineIn, OrderStatuses.Served, "Juan", "Pendiente", 166.00m, now.AddMinutes(-40), 1),
                CreateSampleOrder("F14", OrderTypes.Takeout, OrderStatuses.Delivered, "David", "Pendiente", 136.00m, now.AddMinutes(-30))
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var report = await service.GetSalesReportAsync(new ReportFilterInputModel());

            Assert.Equal(3, report.FilteredOrdersCount);
            Assert.Equal(433.00m, report.TotalSales); // 131 + 166 + 136
            Assert.Equal(144.33m, report.AverageTicket); // 433 / 3

            Assert.Equal(3, report.Orders.Count);
            Assert.Contains(report.Orders, o => o.Code == "#L15" && o.CustomerName == "Maria Gomez" && o.PaymentMethodDisplay == "Yape");
            Assert.Contains(report.Orders, o => o.Code == "#D75" && o.TypeDisplay == "Mesa 1" && o.PaymentMethodDisplay == "—");
            Assert.Contains(report.Orders, o => o.Code == "#F14" && o.TypeDisplay == "Para llevar");
        }

        [Fact]
        public async Task CA02_Reports_CalculatesSalesByPaymentMethod()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("L15", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria", PaymentMethods.Yape, 131.00m, now),
                CreateSampleOrder("R65", OrderTypes.Delivery, OrderStatuses.Delivered, "Juan", PaymentMethods.Yape, 64.00m, now),
                CreateSampleOrder("M54", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria", PaymentMethods.Yape, 108.00m, now),
                CreateSampleOrder("B64", OrderTypes.Delivery, OrderStatuses.Delivered, "Nicol", PaymentMethods.Yape, 122.00m, now), // Yape total: 425
                CreateSampleOrder("D64", OrderTypes.Delivery, OrderStatuses.Delivered, "Genesis", PaymentMethods.Cash, 52.00m, now), // Efectivo: 52
                CreateSampleOrder("P10", OrderTypes.Delivery, OrderStatuses.Delivered, "Carlos", PaymentMethods.Plin, 25.00m, now)    // Plin: 25
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var report = await service.GetSalesReportAsync(new ReportFilterInputModel());

            Assert.Equal(425.00m, report.SalesByPaymentMethod.Yape);
            Assert.Equal(25.00m, report.SalesByPaymentMethod.Plin);
            Assert.Equal(52.00m, report.SalesByPaymentMethod.Cash);
        }

        [Fact]
        public async Task CA02_Reports_CalculatesSalesByOrderType()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("N42", OrderTypes.DineIn, OrderStatuses.Served, "Genesis", "Pendiente", 60.00m, now, 2),
                CreateSampleOrder("K21", OrderTypes.DineIn, OrderStatuses.Served, "Nicol", "Pendiente", 70.00m, now, 1),
                CreateSampleOrder("L15", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria", PaymentMethods.Yape, 131.00m, now),
                CreateSampleOrder("Q86", OrderTypes.Takeout, OrderStatuses.Delivered, "Kevin", "Pendiente", 64.00m, now)
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var report = await service.GetSalesReportAsync(new ReportFilterInputModel());

            var salon = report.SalesByOrderType.FirstOrDefault(t => t.OrderType == "Salón");
            var delivery = report.SalesByOrderType.FirstOrDefault(t => t.OrderType == "Delivery");
            var takeout = report.SalesByOrderType.FirstOrDefault(t => t.OrderType == "Para llevar");

            Assert.NotNull(salon);
            Assert.Equal(2, salon.OrdersCount);
            Assert.Equal(130.00m, salon.TotalSales); // 60 + 70

            Assert.NotNull(delivery);
            Assert.Equal(1, delivery.OrdersCount);
            Assert.Equal(131.00m, delivery.TotalSales);

            Assert.NotNull(takeout);
            Assert.Equal(1, takeout.OrdersCount);
            Assert.Equal(64.00m, takeout.TotalSales);
        }

        [Fact]
        public async Task CA03_Reports_FiltersByStatus()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("O1", OrderTypes.Delivery, OrderStatuses.Delivered, "Cliente 1", PaymentMethods.Yape, 50m, now),
                CreateSampleOrder("O2", OrderTypes.Delivery, OrderStatuses.Delivered, "Cliente 2", PaymentMethods.Yape, 60m, now),
                CreateSampleOrder("O3", OrderTypes.DineIn, OrderStatuses.InPreparation, "Cliente 3", "Pendiente", 40m, now, 1)
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var filter = new ReportFilterInputModel { Status = OrderStatuses.Delivered };
            var report = await service.GetSalesReportAsync(filter);

            Assert.Equal(2, report.FilteredOrdersCount);
            Assert.All(report.Orders, o => Assert.Equal(OrderStatuses.Delivered, o.Status));
        }

        [Fact]
        public async Task CA03_Reports_FiltersByOrderType()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("O1", OrderTypes.Delivery, OrderStatuses.Delivered, "Cliente 1", PaymentMethods.Yape, 50m, now),
                CreateSampleOrder("O2", OrderTypes.DineIn, OrderStatuses.Served, "Cliente 2", "Pendiente", 60m, now, 1),
                CreateSampleOrder("O3", OrderTypes.Takeout, OrderStatuses.Delivered, "Cliente 3", "Pendiente", 70m, now)
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var filter = new ReportFilterInputModel { OrderType = "Delivery" };
            var report = await service.GetSalesReportAsync(filter);

            Assert.Single(report.Orders);
            Assert.Equal("O1", report.Orders[0].Code.Replace("#", ""));
        }

        [Fact]
        public async Task CA03_Reports_FiltersByDate()
        {
            using var db = CreateInMemoryContext();
            var today = DateTime.UtcNow.Date.AddHours(12);
            var yesterday = today.AddDays(-1);

            db.Orders.AddRange(
                CreateSampleOrder("O-Today", OrderTypes.Delivery, OrderStatuses.Delivered, "Cliente Hoy", PaymentMethods.Yape, 50m, today),
                CreateSampleOrder("O-Yesterday", OrderTypes.Delivery, OrderStatuses.Delivered, "Cliente Ayer", PaymentMethods.Yape, 60m, yesterday)
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var filter = new ReportFilterInputModel { Date = today.Date };
            var report = await service.GetSalesReportAsync(filter);

            Assert.Single(report.Orders);
            Assert.Equal("O-Today", report.Orders[0].Code.Replace("#", ""));
        }

        [Fact]
        public void Detalle1_Reports_RequiresAdministratorRole()
        {
            var authAttr = typeof(AdministrationController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Administrador", authAttr.Roles);
        }

        [Fact]
        public async Task Detalle2_Reports_OnlyFinalizedOrdersCountTowardsSales()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                // Finalizados:
                CreateSampleOrder("O-Served", OrderTypes.DineIn, OrderStatuses.Served, "Mesa Finalizada", "Pendiente", 70.00m, now, 1),
                CreateSampleOrder("O-Delivered", OrderTypes.Delivery, OrderStatuses.Delivered, "Delivery Entregado", PaymentMethods.Yape, 64.00m, now),
                // No finalizados:
                CreateSampleOrder("O-Ready", OrderTypes.DineIn, OrderStatuses.ReadyForDispatch, "Mesa Lista", "Pendiente", 166.00m, now, 2),
                CreateSampleOrder("O-Prep", OrderTypes.Delivery, OrderStatuses.InPreparation, "Delivery Preparando", PaymentMethods.Cash, 52.00m, now),
                CreateSampleOrder("O-New", OrderTypes.Takeout, OrderStatuses.New, "Llevar Nuevo", "Pendiente", 30.00m, now)
            );
            await db.SaveChangesAsync();

            var service = new ReportService(db);
            var report = await service.GetSalesReportAsync(new ReportFilterInputModel());

            // Los 5 pedidos aparecen en la lista de pedidos filtrados
            Assert.Equal(5, report.FilteredOrdersCount);

            // Pero las ventas SOLO consideran los finalizados (70 + 64 = 134)
            Assert.Equal(134.00m, report.TotalSales);
        }

        [Fact]
        public async Task Controller_Reports_ReturnsViewWithReportViewModel()
        {
            using var db = CreateInMemoryContext();
            var service = new ReportService(db);
            var pdfGenerator = new PdfReportGenerator();
            var controller = new AdministrationController(db, service, pdfGenerator);

            var result = await controller.Reports(null, null, null);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<ReportViewModel>(viewResult.Model);
        }
    }
}
