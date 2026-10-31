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
        // HU-02
        Task<List<ReadyToServeViewModel>> GetReadyToServeAsync();
        Task<TableConsumptionViewModel?> GetTableConsumptionAsync(int tableId);
        Task<SalonNewOrderViewModel?> GetAddItemsContextAsync(int tableId);
        Task<(bool Succeeded, string? ErrorMessage, Order? UpdatedOrder)> AddItemsAsync(SalonNewOrderViewModel request, string waiterId);

        // HU-07: Visualizar detalle de mesa (Sprint 3)
        Task<(bool Succeeded, string? ErrorMessage)> ServeOrderAsync(int orderId);
        Task<SalonBillViewModel?> GenerateBillAsync(int tableId, string? waiterId = null);
        Task<(bool Succeeded, string? ErrorMessage)> FreeTableAsync(int tableId);
        Task<SalonNewOrderViewModel?> GetNewOrderForTableAsync(int tableId);
    }
}
