using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IDeliveryRouteService
    {
        Task<DeliveryRouteDashboardViewModel> GetDashboardAsync(string driverId);
        Task<(bool Succeeded, string? ErrorMessage)> StartRouteAsync(int orderId, string driverId);
        Task<(bool Succeeded, string? ErrorMessage)> ConfirmDeliveryAsync(int orderId, string driverId, string? deliveryNote);
        Task<(bool Succeeded, string? ErrorMessage)> MarkReadyForDispatchAsync(int orderId);
    }
}
