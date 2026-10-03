using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace software_elviejomadero.ViewModels
{
    public class CreateDishViewModel
    {
        [Required(ErrorMessage = "El nombre del plato es obligatorio.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres.")]
        [Display(Name = "Nombre del plato")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, 9999.99, ErrorMessage = "El precio debe ser mayor que cero.")]
        [Display(Name = "Precio (S/)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El tiempo de preparación es obligatorio.")]
        [Range(0, 360, ErrorMessage = "El tiempo de preparación debe ser mayor o igual a 0 minutos.")]
        [Display(Name = "Tiempo de preparación (minutos)")]
        public int PreparationTimeMinutes { get; set; } = 15;

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoría válida.")]
        [Display(Name = "Categoría")]
        public int CategoryId { get; set; }

        [Display(Name = "Disponible en la carta")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }

    public class EditDishViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del plato es obligatorio.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres.")]
        [Display(Name = "Nombre del plato")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, 9999.99, ErrorMessage = "El precio debe ser mayor que cero.")]
        [Display(Name = "Precio (S/)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El tiempo de preparación es obligatorio.")]
        [Range(0, 360, ErrorMessage = "El tiempo de preparación debe ser mayor o igual a 0 minutos.")]
        [Display(Name = "Tiempo de preparación (minutos)")]
        public int PreparationTimeMinutes { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoría válida.")]
        [Display(Name = "Categoría")]
        public int CategoryId { get; set; }

        [Display(Name = "Plato activo")]
        public bool IsActive { get; set; }

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }

    public class DishItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int PreparationTimeMinutes { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CategoryGroupViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<DishItemViewModel> Dishes { get; set; } = new();
    }

    public class DishListViewModel
    {
        public List<CategoryGroupViewModel> CategoryGroups { get; set; } = new();
        public int TotalDishesCount { get; set; }
        public int ActiveDishesCount { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}
