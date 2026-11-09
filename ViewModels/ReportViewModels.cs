using software_elviejomadero.Models;

namespace software_elviejomadero.ViewModels
{
    public class ReportFilterInputModel
    {
        public string? Status { get; set; }
        public string? OrderType { get; set; }
        public DateTime? Date { get; set; }
    }

    public class ReportOrderItemViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string? TableNumber { get; set; }
        public string TypeDisplay => OrderType == Models.OrderTypes.DineIn && !string.IsNullOrWhiteSpace(TableNumber)
            ? $"Mesa {TableNumber.Replace("M", "", StringComparison.OrdinalIgnoreCase).Trim()}"
            : OrderType;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay => Status == Models.OrderStatuses.ReadyForDispatch ? "Listo" : Status;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentMethodDisplay => PaymentMethods.Allowed.Contains(PaymentMethod) ? PaymentMethod : "—";
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsFinalized { get; set; }
    }

    public class SalesByPaymentMethodViewModel
    {
        public decimal Yape { get; set; }
        public decimal Plin { get; set; }
        public decimal Cash { get; set; }
        public decimal Total => Yape + Plin + Cash;
    }

    public class SalesByOrderTypeItemViewModel
    {
        public string OrderType { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
        public decimal TotalSales { get; set; }
    }

    public class ReportViewModel
    {
        public ReportFilterInputModel Filter { get; set; } = new();

        // Indicadores KPI
        public int FilteredOrdersCount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal AverageTicket { get; set; }

        // Secciones de ventas (CA-02)
        public SalesByPaymentMethodViewModel SalesByPaymentMethod { get; set; } = new();
        public List<SalesByOrderTypeItemViewModel> SalesByOrderType { get; set; } = new();

        // Tabla de pedidos (CA-01)
        public List<ReportOrderItemViewModel> Orders { get; set; } = new();

        // Dropdowns de filtrado
        public List<string> AvailableStatuses { get; set; } = new();
        public List<string> AvailableTypes { get; set; } = new();
    }
}
