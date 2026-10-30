using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface INoticeService
    {
        /// <summary>
        /// HU-13: genera el aviso "en preparación" si el pedido es de delivery y está en preparación.
        /// Para cualquier otro tipo de pedido no hace nada. Es idempotente (un aviso por pedido).
        /// </summary>
        Task NotifyDeliveryInPreparationAsync(int orderId);

        Task<NoticeSummaryViewModel> GetSummaryAsync(string userId);

        /// <summary>Marca como leídos los avisos del usuario con Id menor o igual a <paramref name="upToId"/>.</summary>
        Task MarkAsReadAsync(string userId, int upToId);
    }
}