using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;

namespace software_elviejomadero.Controllers
{
    // Vista operativa mínima para que cocina reciba los pedidos de HU-03 y
    // pueda preparar deliveries para la HU-14, hasta integrar la HU-09 completa.
    [Authorize(Roles = "Cocinero,Administrador")]
    public class KitchenDispatchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeliveryRouteService _routes;
        public KitchenDispatchController(ApplicationDbContext context, IDeliveryRouteService routes)
        {
            _context = context; _routes = routes;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Table)
                .Where(o => (o.OrderType == OrderTypes.DineIn && o.Status == OrderStatuses.InKitchen) ||
                            (o.OrderType == OrderTypes.Delivery && o.Status == OrderStatuses.New))
                .OrderBy(o => o.CreatedAt).ToListAsync();
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ready(int id)
        {
            var result = await _routes.MarkReadyForDispatchAsync(id);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? "Pedido marcado como Listo para despacho." : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
    }
}
