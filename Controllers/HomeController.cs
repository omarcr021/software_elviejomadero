using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Models;

using Microsoft.AspNetCore.Authorization;

namespace software_elviejomadero.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        if (User.IsInRole("Administrador"))
        {
            return RedirectToAction("Index", "Administration");
        }

        if (User.IsInRole("Recepcionista"))
        {
            return RedirectToAction("Index", "ReceptionOrder");
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
