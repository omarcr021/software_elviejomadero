using System.ComponentModel.DataAnnotations;

namespace software_elviejomadero.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<Dish> Dishes { get; set; } = new List<Dish>();
    }
}
