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

        /// <summary>Copia del nombre del plato al momento del pedido (no cambia si luego se edita la carta).</summary>
        [Required]
        [MaxLength(150)]
        public string DishName { get; set; } = string.Empty;

        [Range(1, 999)]
        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10, 2)")]
        public decimal UnitPrice { get; set; }
    }
}