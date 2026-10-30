using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Models;

namespace software_elviejomadero.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        if (User.IsInRole("Administrador"))
            return RedirectToAction("Index", "Administration");

        if (User.IsInRole("Cocinero"))
            return RedirectToAction("Index", "Kitchen");

        // HU-13: el repartidor entra a "Despacho y rutas" (aquí está la campana de avisos)
        if (User.IsInRole("Repartidor"))
            return RedirectToAction("Index", "Dispatch");

        // Mozo y Recepcionista registran pedidos
        if (User.IsInRole("Mozo") || User.IsInRole("Recepcionista"))
            return RedirectToAction("Create", "Order");

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

