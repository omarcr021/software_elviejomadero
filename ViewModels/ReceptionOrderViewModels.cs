using System.ComponentModel.DataAnnotations;
using software_elviejomadero.Models;

namespace software_elviejomadero.ViewModels
{
    public class CreateOrderItemInputModel
    {
        [Required]
        public int DishId { get; set; }

        [Range(1, 999, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Quantity { get; set; }
    }

    public class CreateReceptionOrderViewModel
    {
        [Required(ErrorMessage = "El tipo de pedido es obligatorio.")]
        public string OrderType { get; set; } = OrderTypes.Delivery;

        [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
        [MaxLength(120)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono del cliente es obligatorio.")]
        [MaxLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? DeliveryAddress { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        public string PaymentMethod { get; set; } = PaymentMethods.Yape;

        [MaxLength(500)]
        public string? AdditionalNote { get; set; }

        public List<CreateOrderItemInputModel> Items { get; set; } = new();
    }

    public class ReceptionOrderCardViewModel
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DisplayAddress { get; set; } = string.Empty;
        public int TotalItemsCount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? AdditionalNote { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ReceptionistName { get; set; } = string.Empty;
    }

    public class ReceptionOrderIndexViewModel
    {
        public List<ReceptionOrderCardViewModel> Orders { get; set; } = new();
        public List<CategoryGroupViewModel> AvailableCategoryGroups { get; set; } = new();
    }
}
