using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class DeliveryRouteService : IDeliveryRouteService
    {
        private readonly ApplicationDbContext _context;
        public DeliveryRouteService(ApplicationDbContext context) => _context = context;

        public async Task<DeliveryRouteDashboardViewModel> GetDashboardAsync(string driverId)
        {
            var orders = await _context.Orders.AsNoTracking().Include(o => o.Items)
                .Where(o => o.OrderType == OrderTypes.Delivery &&
                    (o.Status == OrderStatuses.ReadyForDispatch ||
                     (o.Status == OrderStatuses.OnTheWay && o.DeliveryDriverId == driverId) ||
                     (o.Status == OrderStatuses.Delivered && o.DeliveryDriverId == driverId)))
                .OrderByDescending(o => o.CreatedAt).ToListAsync();

            DispatchOrderViewModel ToCard(Order o) => new()
            {
                Id = o.Id, Code = o.OrderCode, Status = o.Status,
                CustomerName = o.CustomerName, Address = o.DeliveryAddress ?? string.Empty,
                Phone = o.CustomerPhone, ItemsCount = o.TotalItemsCount,
                Total = o.TotalAmount, RouteStartedAt = o.RouteStartedAt,
                DeliveredAt = o.DeliveredAt,
                DeliveryNote = o.DeliveryNote,
                ReadyAt = o.ReadyAt ?? o.CreatedAt,
                PaymentMethod = o.PaymentMethod,
                AdditionalNote = o.AdditionalNote,
                Latitude = o.Latitude, Longitude = o.Longitude,
                DeliveryDurationMinutes = (o.RouteStartedAt.HasValue && o.DeliveredAt.HasValue)
                    ? (int?)Math.Max(1, (int)Math.Round((o.DeliveredAt.Value - o.RouteStartedAt.Value).TotalMinutes))
                    : null,
                Items = o.Items.OrderBy(i => i.Id).Select(i => new DispatchItemViewModel
                {
                    Name = i.DishName, Quantity = i.Quantity, UnitPrice = i.UnitPrice
                }).ToList()
            };

            return new DeliveryRouteDashboardViewModel
            {
                Ready = orders.Where(o => o.Status == OrderStatuses.ReadyForDispatch).Select(ToCard).ToList(),
                InRoute = orders.Where(o => o.Status == OrderStatuses.OnTheWay).Select(ToCard).ToList(),
                History = orders.Where(o => o.Status == OrderStatuses.Delivered)
                    .OrderByDescending(o => o.DeliveredAt ?? o.CreatedAt)
                    .Select(ToCard).ToList()
            };
        }

        public async Task<(bool Succeeded, string? ErrorMessage)> StartRouteAsync(int orderId, string driverId)
        {
            if (string.IsNullOrWhiteSpace(driverId) || !await _context.Users.AnyAsync(u => u.Id == driverId && u.IsActive))
                return (false, "El repartidor no está disponible.");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.OrderType != OrderTypes.Delivery || order.Status != OrderStatuses.ReadyForDispatch)
                return (false, "Solo puedes iniciar la ruta de pedidos de delivery listos para despacho.");
            if (!string.IsNullOrEmpty(order.DeliveryDriverId))
                return (false, "Este pedido ya fue asignado a otro repartidor.");
            if (string.IsNullOrWhiteSpace(order.DeliveryAddress) || string.IsNullOrWhiteSpace(order.CustomerPhone))
                return (false, "El pedido no contiene la dirección y el teléfono necesarios.");

            order.Status = OrderStatuses.OnTheWay;
            order.DeliveryDriverId = driverId;
            order.RouteStartedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "El pedido fue aceptado o actualizado por otra persona. Recarga la pantalla.");
            }
        }

        public async Task<(bool Succeeded, string? ErrorMessage)> ConfirmDeliveryAsync(int orderId, string driverId, string? deliveryNote)
        {
            if (string.IsNullOrWhiteSpace(driverId) || !await _context.Users.AnyAsync(u => u.Id == driverId && u.IsActive))
                return (false, "El repartidor no está disponible o no se encuentra autenticado.");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.OrderType != OrderTypes.Delivery)
                return (false, "El pedido de delivery no existe.");

            if (order.Status == OrderStatuses.Delivered)
                return (false, "Una vez confirmada la entrega, el pedido no puede modificarse.");

            if (order.Status != OrderStatuses.OnTheWay)
                return (false, "Solo se puede confirmar la entrega de los pedidos en estado 'En camino'.");

            if (order.DeliveryDriverId != driverId)
                return (false, "Solo el repartidor que aceptó el pedido puede confirmar su entrega.");

            order.Status = OrderStatuses.Delivered;
            order.DeliveredAt = DateTime.UtcNow;
            order.DeliveryNote = string.IsNullOrWhiteSpace(deliveryNote) ? null : deliveryNote.Trim();

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "El pedido fue actualizado por otro proceso. Recarga la pantalla.");
            }
        }

        // Puente mínimo para la dependencia HU-09 (marcar un delivery preparado).
        public async Task<(bool Succeeded, string? ErrorMessage)> MarkReadyForDispatchAsync(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.OrderType != OrderTypes.Delivery || order.Status != OrderStatuses.New)
                return (false, "Solo puedes marcar como listos los deliveries nuevos.");
            order.Status = OrderStatuses.ReadyForDispatch;
            order.ReadyAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "Este pedido ya cambió de estado. Actualiza la pantalla.");
            }
        }
    }
}
