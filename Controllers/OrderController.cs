using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    // Registro de pedidos (vista del Mozo / Recepcionista). Los pedidos nacen en estado "Nuevo"
    // y aparecen de inmediato en la columna "Nuevos" del tablero del cocinero.
    [Authorize(Roles = "Mozo,Recepcionista,Administrador")]
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            return View(await _orderService.GetCreateFormAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _orderService.CreateOrderAsync(model, userId);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Pedido {result.Code} enviado a cocina.";
                    return RedirectToAction(nameof(Create));
                }

                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Error al registrar el pedido.");
            }

            // Se conserva lo que el usuario escribió
            return View(await _orderService.GetCreateFormAsync(model));
        }
    }
}
