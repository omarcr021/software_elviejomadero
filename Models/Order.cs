using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace software_elviejomadero.Models
{
    public static class OrderTypes
    {
        public const string Delivery = "Delivery";
        public const string Takeout = "Para llevar";
    }

    public static class OrderStatuses
    {
        public const string New = "Nuevo";
    }

    public static class PaymentMethods
    {
        public const string Yape = "Yape";
        public const string Plin = "Plin";
        public const string Cash = "Efectivo";

        public static readonly string[] Allowed = [Yape, Plin, Cash];
    }

    public class Order
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(15)]
        public string OrderCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string OrderType { get; set; } = OrderTypes.Delivery;

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = OrderStatuses.New;

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
        [MaxLength(30)]
        public string PaymentMethod { get; set; } = PaymentMethods.Yape;

        [MaxLength(500)]
        public string? AdditionalNote { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal TotalAmount { get; set; }

        public int TotalItemsCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string ReceptionistId { get; set; } = string.Empty;

        public ApplicationUser? Receptionist { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
