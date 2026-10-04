using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;

namespace software_elviejomadero.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdministrationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdministrationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalDishes = await _context.Dishes.CountAsync();
            ViewBag.ActiveDishes = await _context.Dishes.CountAsync(d => d.IsActive);
            ViewBag.TotalEmployees = await _context.Users.CountAsync();
            ViewBag.ActiveEmployees = await _context.Users.CountAsync(u => u.IsActive);

            return View();
        }
    }
}
