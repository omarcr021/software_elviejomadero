namespace software_elviejomadero.ViewModels
{
    public class DispatchItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        // HU-12: importe de la línea = precio × cantidad.
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class DispatchOrderViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int ItemsCount { get; set; }
        public decimal Total { get; set; }
        public DateTime? RouteStartedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string? DeliveryNote { get; set; }
        public int? DeliveryDurationMinutes { get; set; }
        // HU-12: hora de salida de cocina, método de pago, nota y ubicación del pin.
        public DateTime? ReadyAt { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? AdditionalNote { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
        public List<DispatchItemViewModel> Items { get; set; } = new();
    }

    public class DeliveryRouteDashboardViewModel
    {
        public List<DispatchOrderViewModel> Ready { get; set; } = new();
        public List<DispatchOrderViewModel> InRoute { get; set; } = new();
        public List<DispatchOrderViewModel> History { get; set; } = new();
        // HU-12: "Despacho activo" = Listo para despacho + En camino.
        public IEnumerable<DispatchOrderViewModel> Active => Ready.Concat(InRoute);
    }

    public class ConfirmDeliveryInputModel
    {
        public int OrderId { get; set; }
        public string? DeliveryNote { get; set; }
    }
}
