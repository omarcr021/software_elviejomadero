using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    // HU-19 y HU-20: Módulo de Administración exclusivo para el rol Administrador
    [Authorize(Roles = "Administrador")]
    public class AdministrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportService _reportService;
        private readonly IPdfReportGenerator _pdfReportGenerator;

        public AdministrationController(
            ApplicationDbContext context,
            IReportService reportService,
            IPdfReportGenerator pdfReportGenerator)
        {
            _context = context;
            _reportService = reportService;
            _pdfReportGenerator = pdfReportGenerator;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalDishes = await _context.Dishes.CountAsync();
            ViewBag.ActiveDishes = await _context.Dishes.CountAsync(d => d.IsActive);
            ViewBag.TotalEmployees = await _context.Users.CountAsync();
            ViewBag.ActiveEmployees = await _context.Users.CountAsync(u => u.IsActive);
            ViewBag.ActiveOrders = await _context.Orders.CountAsync();
            var salesSum = await _context.Orders.SumAsync(o => (double?)o.TotalAmount) ?? 0.0;
            ViewBag.TotalSales = (decimal)salesSum;

            return View();
        }

        // HU-19: Consultar Reporte de Ventas y Pedidos
        [HttpGet]
        public async Task<IActionResult> Reports(string? status, string? orderType, DateTime? date)
        {
            var filter = new ReportFilterInputModel
            {
                Status = status,
                OrderType = orderType,
                Date = date
            };

            var model = await _reportService.GetSalesReportAsync(filter);
            return View(model);
        }

        // HU-20: Exportar Reportes en formato PDF
        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? status, string? orderType, DateTime? date)
        {
            var filter = new ReportFilterInputModel
            {
                Status = status,
                OrderType = orderType,
                Date = date
            };

            var model = await _reportService.GetSalesReportAsync(filter);
            var pdfBytes = _pdfReportGenerator.GenerateSalesReportPdf(model);

            var fileName = $"Reporte_Ventas_ElViejoMadero_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
