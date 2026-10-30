using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using software_elviejomadero.Services.Interfaces;

namespace software_elviejomadero.Controllers
{
    // HU-13: avisos de cocina para el repartidor (campana del encabezado).
    [Authorize(Roles = "Repartidor")]
    public class NoticeController : Controller
    {
        private readonly INoticeService _noticeService;

        public NoticeController(INoticeService noticeService)
        {
            _noticeService = noticeService;
        }

        // El navegador lo consulta cada pocos segundos: contador + lista de avisos.
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Summary()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var model = await _noticeService.GetSummaryAsync(userId);
            return Json(model);
        }

        // Se llama al cerrar la ventana "Avisos de cocina".
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int upToId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _noticeService.MarkAsReadAsync(userId, upToId);
            return Json(new { success = true });
        }
    }
}