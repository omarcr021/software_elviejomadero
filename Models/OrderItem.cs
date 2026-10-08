using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace software_elviejomadero.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order? Order { get; set; }

        public int DishId { get; set; }

        public Dish? Dish { get; set; }

        [Required]
        [MaxLength(150)]
        public string DishName { get; set; } = string.Empty;

        [Range(1, 999)]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal Subtotal { get; set; }
    }
}
