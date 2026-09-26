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
    }
}
