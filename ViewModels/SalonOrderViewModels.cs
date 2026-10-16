using System.ComponentModel.DataAnnotations;
using software_elviejomadero.Models;

namespace software_elviejomadero.ViewModels
{
    public class SalonTablesViewModel
    {
        public List<RestaurantTable> Tables { get; set; } = new();
        // HU-02: pedidos del salón que cocina ya dejó listos para entregar.
        public List<ReadyToServeViewModel> ReadyToServe { get; set; } = new();
    }

    // HU-02: etiqueta visible de la mesa ("M3" -> "Mesa 3").
    public static class TableLabels
    {
        public static string Format(string number)
        {
            var n = (number ?? string.Empty).Trim();
            if (n.Length > 1 && (n[0] == 'M' || n[0] == 'm') && n.Skip(1).All(char.IsDigit))
                n = n[1..];
            return $"Mesa {n}";
        }
    }

    public class ReadyToServeViewModel
    {
        public int OrderId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string TableLabel { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int ItemsCount { get; set; }
    }

    // HU-02: consumo de una mesa (se muestra al tocarla).
    public class TableConsumptionViewModel
    {
        public int TableId { get; set; }
        public string TableLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public string? OrderCode { get; set; }
        public string? CustomerName { get; set; }
        public string? OrderStatus { get; set; }
        public decimal Total { get; set; }
        public List<DispatchItemViewModel> Items { get; set; } = new();
    }

    public class SalonNewOrderViewModel
    {
        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        [Required(ErrorMessage = "Ingresa el nombre del cliente.")]
        [MaxLength(120)]
        public string CustomerName { get; set; } = string.Empty;
        // HU-02: true cuando se agregan platos al pedido activo de una mesa ocupada.
        public bool AddToExisting { get; set; }
        public string? OrderCode { get; set; }
        public List<CreateOrderItemInputModel> Items { get; set; } = new();
        public List<CategoryGroupViewModel> CategoryGroups { get; set; } = new();
    }

    public class SalonOrderConfirmationViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }
}
