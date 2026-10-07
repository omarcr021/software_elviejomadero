using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    [Authorize(Roles = "Recepcionista,Administrador")]
    public class ReceptionOrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IDishService _dishService;

        public ReceptionOrderController(IOrderService orderService, IDishService dishService)
        {
            _orderService = orderService;
            _dishService = dishService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _orderService.GetReceptionOrdersAsync();
            var menu = await _dishService.GetDishesGroupedByCategoryAsync();

            var activeGroups = menu.CategoryGroups
                .Select(g => new CategoryGroupViewModel
                {
                    CategoryId = g.CategoryId,
                    CategoryName = g.CategoryName,
                    Dishes = g.Dishes.Where(d => d.IsActive).ToList()
                })
                .Where(g => g.Dishes.Any())
                .ToList();

            var viewModel = new ReceptionOrderIndexViewModel
            {
                Orders = orders,
                AvailableCategoryGroups = activeGroups
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReceptionOrderViewModel model)
        {
            var receptionistId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var (succeeded, errorMessage, createdOrder) = await _orderService.CreateReceptionOrderAsync(model, receptionistId);

            if (!succeeded || createdOrder == null)
            {
                TempData["ErrorMessage"] = errorMessage ?? "No se pudo registrar el pedido.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = $"Pedido {createdOrder.OrderCode} registrado y enviado a cocina con estado '{createdOrder.Status}'.";
            return RedirectToAction(nameof(Index));
        }
    }
}
