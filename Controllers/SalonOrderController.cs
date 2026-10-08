using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    [Authorize(Roles = "Mozo,Administrador")]
    public class SalonOrderController : Controller
    {
        private readonly ISalonOrderService _salon;
        private readonly IDishService _dishes;
        public SalonOrderController(ISalonOrderService salon, IDishService dishes)
        {
            _salon = salon; _dishes = dishes;
        }

        [HttpGet]
        public async Task<IActionResult> Index() => View(new SalonTablesViewModel { Tables = await _salon.GetTablesAsync() });

        [HttpGet]
        public async Task<IActionResult> New(int tableId)
        {
            var table = await _salon.GetAvailableTableAsync(tableId);
            if (table == null)
            {
                TempData["ErrorMessage"] = "La mesa está ocupada o no existe.";
                return RedirectToAction(nameof(Index));
            }
            var menu = await _dishes.GetDishesGroupedByCategoryAsync();
            return View(new SalonNewOrderViewModel
            {
                TableId = table.Id, TableNumber = table.Number,
                CategoryGroups = menu.CategoryGroups
                    .Select(g => new CategoryGroupViewModel
                    {
                        CategoryId = g.CategoryId, CategoryName = g.CategoryName,
                        Dishes = g.Dishes.Where(d => d.IsActive).ToList()
                    }).Where(g => g.Dishes.Any()).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalonNewOrderViewModel request)
        {
            var waiterId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _salon.CreateAsync(request, waiterId);
            if (!result.Succeeded || result.CreatedOrder == null)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo registrar el pedido.";
                return RedirectToAction(nameof(New), new { tableId = request.TableId });
            }
            return RedirectToAction(nameof(Confirmation), new { id = result.CreatedOrder.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var result = await _salon.GetConfirmationAsync(id);
            return result == null ? NotFound() : View(result);
        }
    }
}
