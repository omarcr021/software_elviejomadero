using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    // HU-19: Consultar Reporte de Ventas y Pedidos (Rol: Administrador)
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportViewModel> GetSalesReportAsync(ReportFilterInputModel filter)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.Table)
                .AsQueryable();

            // Filtrado por Estado
            if (!string.IsNullOrWhiteSpace(filter.Status) &&
                !filter.Status.Equals("Todos", StringComparison.OrdinalIgnoreCase) &&
                !filter.Status.Equals("Todos los estados", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Status.Equals("Listo", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(o => o.Status == OrderStatuses.ReadyForDispatch || o.Status == "Listo");
                }
                else if (filter.Status.Equals("Nuevo", StringComparison.OrdinalIgnoreCase) ||
                         filter.Status.Equals("Nuevos", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(o => o.Status == OrderStatuses.New || o.Status == OrderStatuses.InKitchen);
                }
                else
                {
                    query = query.Where(o => o.Status == filter.Status);
                }
            }

            // Filtrado por Tipo de Pedido
            if (!string.IsNullOrWhiteSpace(filter.OrderType) &&
                !filter.OrderType.Equals("Todos", StringComparison.OrdinalIgnoreCase) &&
                !filter.OrderType.Equals("Todos los tipos", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.OrderType.Equals("Salón", StringComparison.OrdinalIgnoreCase) ||
                    filter.OrderType.Equals("Salon", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(o => o.OrderType == OrderTypes.DineIn);
                }
                else if (filter.OrderType.Equals("Delivery", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(o => o.OrderType == OrderTypes.Delivery);
                }
                else if (filter.OrderType.Equals("Para llevar", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(o => o.OrderType == OrderTypes.Takeout);
                }
                else
                {
                    query = query.Where(o => o.OrderType == filter.OrderType);
                }
            }

            // Filtrado por Fecha (CreatedAt)
            if (filter.Date.HasValue)
            {
                var targetDate = filter.Date.Value.Date;
                var nextDate = targetDate.AddDays(1);
                query = query.Where(o => o.CreatedAt >= targetDate && o.CreatedAt < nextDate);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .ToListAsync();

            // Detalle 2: El total de ventas solo considera los pedidos finalizados:
            // "Servido" en salón y "Entregado" en delivery y para llevar.
            bool IsFinalized(Order o) =>
                (o.OrderType == OrderTypes.DineIn && (o.Status == OrderStatuses.Served || o.Status == OrderStatuses.Delivered))
                || (o.OrderType != OrderTypes.DineIn && o.Status == OrderStatuses.Delivered);

            var finalizedOrders = orders.Where(IsFinalized).ToList();
            var totalSales = finalizedOrders.Sum(o => o.TotalAmount);
            var averageTicket = orders.Count > 0 ? Math.Round(totalSales / orders.Count, 2) : 0m;

            // Ventas por método de pago (CA-02)
            var salesByPayment = new SalesByPaymentMethodViewModel
            {
                Yape = finalizedOrders.Where(o => o.PaymentMethod == PaymentMethods.Yape).Sum(o => o.TotalAmount),
                Plin = finalizedOrders.Where(o => o.PaymentMethod == PaymentMethods.Plin).Sum(o => o.TotalAmount),
                Cash = finalizedOrders.Where(o => o.PaymentMethod == PaymentMethods.Cash).Sum(o => o.TotalAmount)
            };

            // Ventas por tipo de pedido (CA-02)
            var salesByType = new List<SalesByOrderTypeItemViewModel>
            {
                new()
                {
                    OrderType = "Salón",
                    OrdersCount = orders.Count(o => o.OrderType == OrderTypes.DineIn),
                    TotalSales = finalizedOrders.Where(o => o.OrderType == OrderTypes.DineIn).Sum(o => o.TotalAmount)
                },
                new()
                {
                    OrderType = "Delivery",
                    OrdersCount = orders.Count(o => o.OrderType == OrderTypes.Delivery),
                    TotalSales = finalizedOrders.Where(o => o.OrderType == OrderTypes.Delivery).Sum(o => o.TotalAmount)
                },
                new()
                {
                    OrderType = "Para llevar",
                    OrdersCount = orders.Count(o => o.OrderType == OrderTypes.Takeout),
                    TotalSales = finalizedOrders.Where(o => o.OrderType == OrderTypes.Takeout).Sum(o => o.TotalAmount)
                }
            };

            // Mapeo de pedidos para la tabla (CA-01)
            var mappedOrders = orders.Select(o => new ReportOrderItemViewModel
            {
                Id = o.Id,
                Code = o.OrderCode.StartsWith("#") ? o.OrderCode : $"#{o.OrderCode}",
                OrderType = o.OrderType,
                TableNumber = o.Table?.Number,
                CustomerName = o.CustomerName,
                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt,
                IsFinalized = IsFinalized(o)
            }).ToList();

            return new ReportViewModel
            {
                Filter = filter,
                FilteredOrdersCount = orders.Count,
                TotalSales = totalSales,
                AverageTicket = averageTicket,
                SalesByPaymentMethod = salesByPayment,
                SalesByOrderType = salesByType,
                Orders = mappedOrders,
                AvailableStatuses = new List<string>
                {
                    "Todos los estados",
                    OrderStatuses.Delivered,
                    OrderStatuses.Served,
                    "Listo",
                    OrderStatuses.InPreparation,
                    "Nuevo"
                },
                AvailableTypes = new List<string>
                {
                    "Todos los tipos",
                    "Salón",
                    "Delivery",
                    "Para llevar"
                }
            };
        }
    }
}
