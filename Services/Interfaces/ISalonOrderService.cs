using software_elviejomadero.Models;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface ISalonOrderService
    {
        Task<List<RestaurantTable>> GetTablesAsync();
        Task<RestaurantTable?> GetAvailableTableAsync(int tableId);
        Task<(bool Succeeded, string? ErrorMessage, Order? CreatedOrder)> CreateAsync(SalonNewOrderViewModel request, string waiterId);
        Task<SalonOrderConfirmationViewModel?> GetConfirmationAsync(int orderId);
    }
}
