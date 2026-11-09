using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Controllers;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Implementations;
using software_elviejomadero.ViewModels;
using System.Reflection;
using System.Text;
using Xunit;

namespace software_elviejomadero.Tests
{
    // HU-20: Exportar Reportes en formato PDF (Rol: Administrador)
    public class Hu20ExportReportTests
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
        public async Task CA01_ExportPdf_ReturnsFileResultWithPdfMimeTypeAndFileName()
        {
            using var db = CreateInMemoryContext();
            var reportService = new ReportService(db);
            var pdfGenerator = new PdfReportGenerator();
            var controller = new AdministrationController(db, reportService, pdfGenerator);

            var result = await controller.ExportPdf(null, null, null);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", fileResult.ContentType);
            Assert.StartsWith("Reporte_Ventas_ElViejoMadero_", fileResult.FileDownloadName);
            Assert.EndsWith(".pdf", fileResult.FileDownloadName);
            Assert.NotNull(fileResult.FileContents);
            Assert.NotEmpty(fileResult.FileContents);
        }

        [Fact]
        public async Task CA01_ExportPdf_GeneratesValidPdfBinaryHeaderAndFooter()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("L15", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria Gomez", PaymentMethods.Yape, 131.00m, now),
                CreateSampleOrder("D75", OrderTypes.DineIn, OrderStatuses.Served, "Juan", "Pendiente", 166.00m, now, 1)
            );
            await db.SaveChangesAsync();

            var reportService = new ReportService(db);
            var pdfGenerator = new PdfReportGenerator();
            var controller = new AdministrationController(db, reportService, pdfGenerator);

            var result = await controller.ExportPdf(null, null, null);
            var fileResult = Assert.IsType<FileContentResult>(result);

            // Validar cabecera PDF (%PDF-1.4) y terminador (%%EOF)
            var rawText = Encoding.ASCII.GetString(fileResult.FileContents);
            Assert.StartsWith("%PDF-1.4", rawText);
            Assert.Contains("%%EOF", rawText);
        }

        [Fact]
        public async Task CA02_ExportPdf_ContainsRestaurantNameAndMetadata()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.Add(CreateSampleOrder("L15", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria Gomez", PaymentMethods.Yape, 131.00m, now));
            await db.SaveChangesAsync();

            var reportService = new ReportService(db);
            var pdfGenerator = new PdfReportGenerator();
            var controller = new AdministrationController(db, reportService, pdfGenerator);

            var result = await controller.ExportPdf("Entregado", "Delivery", now.Date);
            var fileResult = Assert.IsType<FileContentResult>(result);

            // Decodificar el stream del PDF y validar metadatos
            var rawContent = Encoding.Latin1.GetString(fileResult.FileContents);

            // 1. Nombre del restaurante
            Assert.Contains("EL VIEJO MADERO", rawContent);
            // 2. Título del reporte
            Assert.Contains("Reporte de Ventas y Pedidos", rawContent);
            // 3. Fecha y hora de generación
            Assert.Contains("Fecha y hora de generaci", rawContent); // "generación"
            // 4. Rango de fechas
            Assert.Contains("Rango de fechas", rawContent);
            // 5. Filtros aplicados
            Assert.Contains("Filtros aplicados", rawContent);
            Assert.Contains("Entregado", rawContent);
            Assert.Contains("Delivery", rawContent);
        }

        [Fact]
        public async Task CA02_ExportPdf_ContainsKpisAndFinancialBreakdown()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("L15", OrderTypes.Delivery, OrderStatuses.Delivered, "Maria Gomez", PaymentMethods.Yape, 131.00m, now),
                CreateSampleOrder("D64", OrderTypes.Delivery, OrderStatuses.Delivered, "Genesis Tafur", PaymentMethods.Cash, 52.00m, now)
            );
            await db.SaveChangesAsync();

            var reportService = new ReportService(db);
            var pdfGenerator = new PdfReportGenerator();
            var controller = new AdministrationController(db, reportService, pdfGenerator);

            var result = await controller.ExportPdf(null, null, null);
            var fileResult = Assert.IsType<FileContentResult>(result);
            var rawContent = Encoding.Latin1.GetString(fileResult.FileContents);

            // Indicadores
            Assert.Contains("Pedidos filtrados", rawContent);
            Assert.Contains("Ventas", rawContent);
            Assert.Contains("Ticket promedio", rawContent);

            // Métodos de pago
            Assert.Contains("Yape", rawContent);
            Assert.Contains("Plin", rawContent);
            Assert.Contains("Efectivo", rawContent);

            // Tipos de pedido
            Assert.Contains("Sal", rawContent); // Salón
            Assert.Contains("Delivery", rawContent);
            Assert.Contains("Para llevar", rawContent);
        }

        [Fact]
        public async Task CA03_ExportPdf_RespectsAppliedFilters()
        {
            using var db = CreateInMemoryContext();
            var now = DateTime.UtcNow;

            db.Orders.AddRange(
                CreateSampleOrder("DELIV-01", OrderTypes.Delivery, OrderStatuses.Delivered, "Cliente Delivery", PaymentMethods.Yape, 100m, now),
                CreateSampleOrder("SALON-02", OrderTypes.DineIn, OrderStatuses.Served, "Cliente Salon", "Pendiente", 200m, now, 1)
            );
            await db.SaveChangesAsync();

            var reportService = new ReportService(db);
            var pdfGenerator = new PdfReportGenerator();
            var controller = new AdministrationController(db, reportService, pdfGenerator);

            // Filtrar solo por Delivery
            var result = await controller.ExportPdf(null, "Delivery", null);
            var fileResult = Assert.IsType<FileContentResult>(result);
            var rawContent = Encoding.Latin1.GetString(fileResult.FileContents);

            Assert.Contains("DELIV-01", rawContent);
            Assert.DoesNotContain("SALON-02", rawContent);
        }

        [Fact]
        public void Detalle1_ExportPdf_RequiresAdministratorRole()
        {
            var authAttr = typeof(AdministrationController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Administrador", authAttr.Roles);
        }
    }
}
