using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public class KitchenActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static KitchenActionResult Ok(string message) => new() { Success = true, Message = message };
        public static KitchenActionResult Fail(string message) => new() { Success = false, Message = message };
    }

    public interface IKitchenService
    {
        /// <summary>Pedidos activos agrupados en Nuevos / En preparación / Listos, del más antiguo al más reciente.</summary>
        Task<KitchenBoardViewModel> GetBoardAsync();

        /// <summary>Nuevo -> En preparación.</summary>
        Task<KitchenActionResult> StartPreparationAsync(int orderId, string? cookUserId);

        /// <summary>En preparación -> Listo.</summary>
        Task<KitchenActionResult> MarkAsReadyAsync(int orderId);
    }
}