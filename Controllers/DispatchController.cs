using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;

namespace software_elviejomadero.Controllers
{
    // Pantalla "Despacho y rutas" del repartidor (prototipo de HU-13).
    // La campana de "Avisos de cocina" vive en el encabezado de esta vista.
    [Authorize(Roles = "Repartidor")]
    public class DispatchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DispatchController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var ready = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Type == OrderType.Delivery && o.Status == OrderStatus.Listo)
                .OrderBy(o => o.ReadyAt)
                .ThenBy(o => o.Id)
                .ToListAsync();

            ViewBag.HistoryCount = await _context.Orders
                .CountAsync(o => o.Type == OrderType.Delivery && o.Status == OrderStatus.Entregado);

            return View(ready);
        }
    }
}
