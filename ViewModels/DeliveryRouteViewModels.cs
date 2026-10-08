namespace software_elviejomadero.ViewModels
{
    public class DispatchItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
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
        public List<DispatchItemViewModel> Items { get; set; } = new();
    }

    public class DeliveryRouteDashboardViewModel
    {
        public List<DispatchOrderViewModel> Ready { get; set; } = new();
        public List<DispatchOrderViewModel> InRoute { get; set; } = new();
    }
}
