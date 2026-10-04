using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace software_elviejomadero.Models
{
    public class Dish
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del plato es obligatorio.")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0.01, 99999.99, ErrorMessage = "El precio debe ser mayor que cero.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El tiempo de preparación es obligatorio.")]
        [Range(0, 360, ErrorMessage = "El tiempo de preparación debe ser mayor o igual a 0 minutos.")]
        public int PreparationTimeMinutes { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public int CategoryId { get; set; }

        public Category? Category { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
