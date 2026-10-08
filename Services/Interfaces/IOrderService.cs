using software_elviejomadero.Models;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IOrderService
    {
        Task<List<ReceptionOrderCardViewModel>> GetReceptionOrdersAsync();
        Task<string> GenerateUniqueOrderCodeAsync(string orderType);
        Task<(bool Succeeded, string? ErrorMessage, Order? CreatedOrder)> CreateReceptionOrderAsync(
            CreateReceptionOrderViewModel model,
            string receptionistId);
    }
}
