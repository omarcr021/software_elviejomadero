using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class SalonOrderService : ISalonOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orders;

        public SalonOrderService(ApplicationDbContext context, IOrderService orders)
        {
            _context = context;
            _orders = orders;
        }

        public Task<List<RestaurantTable>> GetTablesAsync() => _context.RestaurantTables
            .AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Id).ToListAsync();

        public Task<RestaurantTable?> GetAvailableTableAsync(int tableId) => _context.RestaurantTables
            .AsNoTracking().FirstOrDefaultAsync(t => t.Id == tableId && t.IsActive && t.Status == TableStatuses.Free);

        public async Task<(bool Succeeded, string? ErrorMessage, Order? CreatedOrder)> CreateAsync(SalonNewOrderViewModel request, string waiterId)
        {
            if (string.IsNullOrWhiteSpace(waiterId) || !await _context.Users.AnyAsync(u => u.Id == waiterId && u.IsActive))
                return (false, "No se pudo identificar al mozo activo.", null);
            if (string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Trim().Length > 120)
                return (false, "Ingresa un nombre de cliente válido (máximo 120 caracteres).", null);

            var quantities = request.Items?.Where(i => i.Quantity > 0)
                .GroupBy(i => i.DishId)
                .Select(g => new { DishId = g.Key, Quantity = g.Sum(x => (long)x.Quantity) })
                .ToList();
            if (quantities == null || quantities.Count == 0)
                return (false, "El pedido debe tener al menos un plato.", null);
            if (quantities.Any(i => i.Quantity > 999 || i.DishId <= 0))
                return (false, "La cantidad máxima por plato es 999.", null);

            var dishIds = quantities.Select(i => i.DishId).ToArray();
            var dishes = await _context.Dishes.Where(d => dishIds.Contains(d.Id) && d.IsActive)
                .ToDictionaryAsync(d => d.Id);
            if (dishes.Count != dishIds.Length)
                return (false, "Uno o más platos ya no están disponibles.", null);

            // Transacción + concurrencia optimista: dos mozos no pueden ocupar la misma mesa.
            await using var tx = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == request.TableId);
                if (table == null || !table.IsActive || table.Status != TableStatuses.Free)
                    return (false, "La mesa ya está ocupada o no está disponible.", null);

                table.Status = TableStatuses.Occupied;
                var items = quantities.Select(i =>
                {
                    var dish = dishes[i.DishId];
                    return new OrderItem
                    {
                        DishId = dish.Id, DishName = dish.Name, Quantity = (int)i.Quantity,
                        UnitPrice = dish.Price, Subtotal = dish.Price * i.Quantity
                    };
                }).ToList();
                var order = new Order
                {
                    OrderCode = await _orders.GenerateUniqueOrderCodeAsync(OrderTypes.DineIn),
                    OrderType = OrderTypes.DineIn,
                    Status = OrderStatuses.InKitchen,
                    TableId = table.Id,
                    CustomerName = request.CustomerName.Trim(),
                    CustomerPhone = "No aplica",
                    PaymentMethod = "Pendiente",
                    TotalAmount = items.Sum(i => i.Subtotal),
                    TotalItemsCount = items.Sum(i => i.Quantity),
                    CreatedAt = DateTime.UtcNow,
                    ReceptionistId = waiterId,
                    Items = items
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();
                return (true, null, order);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "Otro mozo acaba de ocupar la mesa. Actualiza la vista.", null);
            }
            catch (DbUpdateException)
            {
                return (false, "No se pudo guardar el pedido. Actualiza la vista e inténtalo de nuevo.", null);
            }
        }

        public async Task<SalonOrderConfirmationViewModel?> GetConfirmationAsync(int orderId)
        {
            var order = await _context.Orders.AsNoTracking().Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.OrderType == OrderTypes.DineIn);
            return order == null ? null : new SalonOrderConfirmationViewModel
            {
                OrderCode = order.OrderCode,
                TableNumber = order.Table?.Number ?? "",
                CustomerName = order.CustomerName
            };
        }
    }
}
