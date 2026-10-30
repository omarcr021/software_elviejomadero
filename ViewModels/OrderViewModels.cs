using System.ComponentModel.DataAnnotations;
using software_elviejomadero.Models;

namespace software_elviejomadero.ViewModels
{
    public class OrderLineInput
    {
        public int DishId { get; set; }

        [Range(1, 999, ErrorMessage = "La cantidad debe estar entre 1 y 999.")]
        public int Quantity { get; set; } = 1;
    }

    public class DishOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class CreateOrderViewModel
    {
        [Display(Name = "Tipo de pedido")]
        public OrderType Type { get; set; } = OrderType.Mesa;

        [Range(1, 99, ErrorMessage = "El número de mesa debe estar entre 1 y 99.")]
        [Display(Name = "Mesa")]
        public int? TableNumber { get; set; }

        [StringLength(120, ErrorMessage = "El nombre del cliente no puede superar los 120 caracteres.")]
        [Display(Name = "Cliente")]
        public string? CustomerName { get; set; }

        [StringLength(500, ErrorMessage = "La nota no puede superar los 500 caracteres.")]
        [Display(Name = "Nota para cocina")]
        public string? Note { get; set; }

        public List<OrderLineInput> Lines { get; set; } = new();

        public List<DishOptionViewModel> Dishes { get; set; } = new();
    }
}