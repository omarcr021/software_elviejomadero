using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IOrderService
    {
        /// <summary>Completa la lista de platos disponibles y garantiza al menos 2 líneas en blanco.</summary>
        Task<CreateOrderViewModel> GetCreateFormAsync(CreateOrderViewModel? model = null);

        /// <summary>Registra un pedido en estado "Nuevo" (visible para cocina).</summary>
        Task<(bool Success, string? ErrorMessage, string? Code)> CreateOrderAsync(CreateOrderViewModel model, string? userId);
    }
}
