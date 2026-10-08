using System.ComponentModel.DataAnnotations;
using software_elviejomadero.Models;

namespace software_elviejomadero.ViewModels
{
    public class SalonTablesViewModel
    {
        public List<RestaurantTable> Tables { get; set; } = new();
    }

    public class SalonNewOrderViewModel
    {
        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        [Required(ErrorMessage = "Ingresa el nombre del cliente.")]
        [MaxLength(120)]
        public string CustomerName { get; set; } = string.Empty;
        public List<CreateOrderItemInputModel> Items { get; set; } = new();
        public List<CategoryGroupViewModel> CategoryGroups { get; set; } = new();
    }

    public class SalonOrderConfirmationViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }
}
