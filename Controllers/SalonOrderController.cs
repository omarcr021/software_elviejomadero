using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    // HU-02 (estados de mesa) y HU-03 (registrar pedido del salón).
    // Solo mozo o administrador pueden ver las mesas.
    [Authorize(Roles = "Mozo,Administrador")]
    public class SalonOrderController : Controller
    {
        private readonly ISalonOrderService _salon;
        private readonly IDishService _dishes;
        public SalonOrderController(ISalonOrderService salon, IDishService dishes)
        {
            _salon = salon; _dishes = dishes;
        }

        private async Task<SalonTablesViewModel> BuildBoardAsync() => new()
        {
            Tables = await _salon.GetTablesAsync(),
            ReadyToServe = await _salon.GetReadyToServeAsync()
        };

        [HttpGet]
        public async Task<IActionResult> Index() => View(await BuildBoardAsync());

        // HU-02 (criterio 4): el navegador consulta este fragmento cada pocos segundos
        // para reflejar los cambios de otros mozos sin recargar la página.
        [HttpGet]
        public async Task<IActionResult> Board()
        {
            Response.Headers.CacheControl = "no-store";
            return PartialView("_Board", await BuildBoardAsync());
        }

        // HU-02 (criterios 2 y 3): al tocar una mesa libre se ofrece tomar un pedido nuevo;
        // al tocar una ocupada se muestra su consumo con la opción de agregar más platos.
        [HttpGet]
        public async Task<IActionResult> TableDetail(int tableId)
        {
            Response.Headers.CacheControl = "no-store";
            var detail = await _salon.GetTableConsumptionAsync(tableId);
            return detail == null ? NotFound() : PartialView("_TableDetail", detail);
        }

        [HttpGet]
        public async Task<IActionResult> New(int tableId)
        {
            var table = await _salon.GetAvailableTableAsync(tableId);
            if (table == null)
            {
                TempData["ErrorMessage"] = "La mesa está ocupada o no existe.";
                return RedirectToAction(nameof(Index));
            }
            return View(await BuildNewOrderModelAsync(new SalonNewOrderViewModel
            {
                TableId = table.Id, TableNumber = table.Number
            }));
        }

        // HU-02 (criterio 3): agregar más platos al pedido de una mesa ocupada.
        [HttpGet]
        public async Task<IActionResult> AddItems(int tableId)
        {
            var context = await _salon.GetAddItemsContextAsync(tableId);
            if (context == null)
            {
                TempData["ErrorMessage"] = "La mesa no está ocupada o no tiene un pedido activo.";
                return RedirectToAction(nameof(Index));
            }
            return View("New", await BuildNewOrderModelAsync(context));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItems(SalonNewOrderViewModel request)
        {
            var waiterId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _salon.AddItemsAsync(request, waiterId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudieron agregar los platos.";
                return RedirectToAction(nameof(AddItems), new { tableId = request.TableId });
            }
            TempData["SuccessMessage"] = "Platos agregados al pedido y enviados a cocina.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SalonNewOrderViewModel> BuildNewOrderModelAsync(SalonNewOrderViewModel model)
        {
            var menu = await _dishes.GetDishesGroupedByCategoryAsync();
            model.CategoryGroups = menu.CategoryGroups
                .Select(g => new CategoryGroupViewModel
                {
                    CategoryId = g.CategoryId, CategoryName = g.CategoryName,
                    Dishes = g.Dishes.Where(d => d.IsActive).ToList()
                }).Where(g => g.Dishes.Any()).ToList();
            return model;
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
