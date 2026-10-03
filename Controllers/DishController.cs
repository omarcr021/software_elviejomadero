using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class DishController : Controller
    {
        private readonly IDishService _dishService;

        public DishController(IDishService dishService)
        {
            _dishService = dishService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _dishService.GetDishesGroupedByCategoryAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateDishViewModel
            {
                Categories = await _dishService.GetCategoriesSelectListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDishViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = errors;
                return RedirectToAction(nameof(Index));
            }

            var result = await _dishService.CreateDishAsync(model);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Plato registrado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al registrar el plato.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _dishService.GetDishForEditAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "El plato no existe.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditDishViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = errors;
                return RedirectToAction(nameof(Index));
            }

            var result = await _dishService.UpdateDishAsync(model);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Plato actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al actualizar el plato.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var result = await _dishService.ToggleActiveAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.NewStatus
                    ? "Plato activado correctamente."
                    : "Plato ocultado correctamente.";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al cambiar la visibilidad del plato.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _dishService.DeleteDishAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Plato eliminado correctamente.";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al eliminar el plato.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
