using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    // HU-02 (estados de mesa), HU-03 (registrar pedido) y HU-07 (visualizar detalle de mesa).
    // Solo mozo o administrador pueden ver y gestionar las mesas.
    [Authorize(Roles = "Mozo,Administrador")]
    public class SalonOrderController : Controller
    {
        private readonly ISalonOrderService _salon;
        private readonly IDishService _dishes;
        public SalonOrderController(ISalonOrderService salon, IDishService dishes)
        {
            _salon = salon; _dishes = dishes;
        }

        // HU-04: construcción del tablero con soporte de filtro de estado
        private async Task<SalonTablesViewModel> BuildBoardAsync(string? filter = "Todas") => new()
        {
            Tables = await _salon.GetTablesAsync(filter),
            ReadyToServe = await _salon.GetReadyToServeAsync(),
            Attention = await _salon.GetTableAttentionAsync(),
            CurrentFilter = string.IsNullOrWhiteSpace(filter) ? "Todas" : filter
        };

        [HttpGet]
        public async Task<IActionResult> Index(string? filter = "Todas") => View(await BuildBoardAsync(filter));

        // HU-02 (criterio 4) y HU-04 (detalle 4): el navegador consulta este fragmento cada pocos segundos
        // respetando el filtro actual sin recargar la página.
        [HttpGet]
        public async Task<IActionResult> Board(string? filter = "Todas")
        {
            Response.Headers.CacheControl = "no-store";
            return PartialView("_Board", await BuildBoardAsync(filter));
        }

        // HU-02 y HU-07: detalle de mesa con sus pedidos y consumos.
        [HttpGet]
        public async Task<IActionResult> TableDetail(int tableId)
        {
            Response.Headers.CacheControl = "no-store";
            var detail = await _salon.GetTableConsumptionAsync(tableId);
            return detail == null ? NotFound() : PartialView("_TableDetail", detail);
        }

        [HttpGet]
        public async Task<IActionResult> New(int tableId = 0, int? id = null, bool additional = false)
        {
            var effectiveTableId = tableId > 0 ? tableId : (id ?? 0);
            if (effectiveTableId <= 0)
            {
                TempData["ErrorMessage"] = "No se especificó la mesa.";
                return RedirectToAction(nameof(Index));
            }

            var model = await _salon.GetNewOrderForTableAsync(effectiveTableId);
            if (model == null)
            {
                TempData["ErrorMessage"] = "La mesa no existe.";
                return RedirectToAction(nameof(Index));
            }

            if (additional)
            {
                model.IsAdditionalOrder = true;
            }

            return View(await BuildNewOrderModelAsync(model));
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
                return RedirectToAction(nameof(New), new { tableId = request.TableId, additional = request.IsAdditionalOrder });
            }
            return RedirectToAction(nameof(Confirmation), new { id = result.CreatedOrder.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var result = await _salon.GetConfirmationAsync(id);
            return result == null ? NotFound() : View(result);
        }

        // HU-07 y HU-08: Servir pedido a la mesa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ServeOrder(int orderId, int? tableId)
        {
            var result = await _salon.ServeOrderAsync(orderId);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                if (tableId.HasValue)
                {
                    var detail = await _salon.GetTableConsumptionAsync(tableId.Value);
                    return detail == null ? NotFound() : PartialView("_TableDetail", detail);
                }
                return Json(new { succeeded = result.Succeeded, errorMessage = result.ErrorMessage });
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? "El pedido fue marcado como servido en la mesa."
                : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        // HU-07 CA-04: Generar boleta
        [HttpGet]
        public async Task<IActionResult> Bill(int tableId)
        {
            var waiterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bill = await _salon.GenerateBillAsync(tableId, waiterId);
            if (bill == null)
            {
                TempData["ErrorMessage"] = "La mesa no está ocupada o no tiene pedidos para facturar.";
                return RedirectToAction(nameof(Index));
            }
            return View(bill);
        }

        // HU-07 CA-05: Liberar mesa (solo tras generar boleta)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FreeTable(int tableId)
        {
            var result = await _salon.FreeTableAsync(tableId);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { succeeded = result.Succeeded, errorMessage = result.ErrorMessage });
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? "Mesa liberada correctamente."
                : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
    }
}
