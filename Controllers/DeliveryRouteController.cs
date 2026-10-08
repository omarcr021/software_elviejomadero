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
    }
}
