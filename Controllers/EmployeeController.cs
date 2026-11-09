using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _employeeService;

        public EmployeeController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? role = null)
        {
            var model = await _employeeService.GetEmployeesAsync(role);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateEmployeeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = errors;
                return RedirectToAction(nameof(Index));
            }

            var result = await _employeeService.RegisterEmployeeAsync(model);

            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Empleado registrado correctamente con el usuario: {result.GeneratedUserName}.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al registrar el empleado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GenerateUsername(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Json(new { username = string.Empty });
            }

            var username = await _employeeService.GenerateUniqueUserNameAsync(fullName);
            return Json(new { username });
        }

        // HU-18 CA-01: Obtener datos para precargar modal de edición
        [HttpGet]
        public async Task<IActionResult> GetForEdit(string id)
        {
            var employee = await _employeeService.GetEmployeeForEditAsync(id);
            if (employee == null)
            {
                return NotFound(new { errorMessage = "El colaborador no existe." });
            }
            return Json(employee);
        }

        // HU-18 CA-02 & CA-03: Guardar cambios del empleado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditEmployeeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = errors;
                return RedirectToAction(nameof(Index));
            }

            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _employeeService.UpdateEmployeeAsync(model, currentAdminId);

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Datos del colaborador actualizados correctamente.";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al actualizar el colaborador.";
            }

            return RedirectToAction(nameof(Index));
        }

        // HU-18 CA-04: Desactivar o Activar empleado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _employeeService.ToggleEmployeeStatusAsync(id, currentAdminId);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.NewStatus
                    ? "Colaborador activado correctamente."
                    : "Colaborador desactivado correctamente.";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al cambiar el estado del colaborador.";
            }

            return RedirectToAction(nameof(Index));
        }

        // HU-18 CA-05: Eliminar empleado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _employeeService.DeleteEmployeeAsync(id, currentAdminId);

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Colaborador eliminado correctamente del restaurante.";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Error al eliminar el colaborador.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
