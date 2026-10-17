using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;

namespace software_elviejomadero.Controllers
{
    // HU-09: pantalla "Cocina - comandas" para el rol Cocinero (el Administrador puede supervisarla).
    [Authorize(Roles = "Cocinero,Administrador")]
    public class KitchenController : Controller
    {
        private readonly IKitchenService _kitchenService;

        public KitchenController(IKitchenService kitchenService)
        {
            _kitchenService = kitchenService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _kitchenService.GetBoardAsync();
            return View(model);
        }

        // Fragmento HTML del tablero; el navegador lo consulta cada pocos segundos
        // para que los pedidos nuevos aparezcan solos en "Nuevos".
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Board()
        {
            var model = await _kitchenService.GetBoardAsync();
            return PartialView("_Board", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPreparation(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _kitchenService.StartPreparationAsync(id, userId);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsReady(int id)
        {
            var result = await _kitchenService.MarkAsReadyAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
    }
}