using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;

namespace software_elviejomadero.Controllers
{
    [Authorize(Roles = "Repartidor,Administrador")]
    public class DeliveryRouteController : Controller
    {
        private readonly IDeliveryRouteService _routes;
        public DeliveryRouteController(IDeliveryRouteService routes) => _routes = routes;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var driverId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return View(await _routes.GetDashboardAsync(driverId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var driverId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _routes.StartRouteAsync(id, driverId);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? "Ruta iniciada: el pedido ahora está En camino."
                : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(int id, string? deliveryNote)
        {
            var driverId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _routes.ConfirmDeliveryAsync(id, driverId, deliveryNote);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? "Entrega confirmada y registrada en el historial."
                : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        // HU-13 CA-01 y Detalle 2: Polling en tiempo real de avisos de cocina para pedidos de delivery
        [HttpGet]
        public async Task<IActionResult> GetNoticesSummary()
        {
            Response.Headers.CacheControl = "no-store";
            var unreadCount = await _routes.GetUnreadNoticesCountAsync();
            var notices = await _routes.GetKitchenNoticesAsync();
            return Json(new { unreadCount, notices });
        }

        // HU-13 CA-03: Al cerrar la ventana, se marcan como leídos y se retira el contador
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNoticesAsRead()
        {
            await _routes.MarkNoticesAsReadAsync();
            return Json(new { success = true, succeeded = true });
        }
    }
}
