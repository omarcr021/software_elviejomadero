using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.Models
{
    public class Order
    {
        public int Id { get; set; }

        /// <summary>Código visible del pedido, por ejemplo PED-0001.</summary>
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public OrderType Type { get; set; } = OrderType.Mesa;

        /// <summary>Solo aplica cuando Type == Mesa.</summary>
        public int? TableNumber { get; set; }

        [MaxLength(120)]
        public string? CustomerName { get; set; }

        /// <summary>Nota adicional para cocina (ej. "sin ají").</summary>
        [MaxLength(500)]
        public string? Note { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Nuevo;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PreparationStartedAt { get; set; }

        public DateTime? ReadyAt { get; set; }

        /// <summary>Mozo o recepcionista que envió el pedido a cocina.</summary>
        public string? CreatedByUserId { get; set; }

        /// <summary>Cocinero que empezó la preparación.</summary>
        public string? PreparedByUserId { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}