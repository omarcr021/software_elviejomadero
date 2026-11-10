using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.Models
{
    // HU-13: Avisos en tiempo real para repartidores cuando cocina inicia preparación de un pedido de delivery
    public class DeliveryKitchenNotice
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        [MaxLength(20)]
        public string OrderCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }
    }
}
