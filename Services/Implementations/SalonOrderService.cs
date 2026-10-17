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

        public async Task<List<RestaurantTable>> GetTablesAsync()
        {
            var tables = await _context.RestaurantTables
                .AsNoTracking()
                .Where(t => t.IsActive)
                .ToListAsync();

            return tables.OrderBy(t => ExtractTableNumber(t.Number)).ThenBy(t => t.Id).ToList();
        }

        private static int ExtractTableNumber(string? number)
        {
            if (string.IsNullOrWhiteSpace(number)) return int.MaxValue;
            var digits = new string(number.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var val) ? val : int.MaxValue;
        }

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

        // ---------------- HU-02: estados de mesa ----------------

        // Pedido activo de una mesa ocupada: el más reciente del salón que no se entregó.
        private IQueryable<Order> ActiveOrderOf(int tableId) => _context.Orders
            .Where(o => o.TableId == tableId && o.OrderType == OrderTypes.DineIn && o.Status != OrderStatuses.Delivered)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);

        public async Task<List<ReadyToServeViewModel>> GetReadyToServeAsync()
        {
            // El salón usa el mismo estado "Listo para despacho" cuando cocina termina el pedido (HU-09).
            var orders = await _context.Orders.AsNoTracking().Include(o => o.Table)
                .Where(o => o.OrderType == OrderTypes.DineIn && o.Status == OrderStatuses.ReadyForDispatch)
                .OrderBy(o => o.ReadyAt ?? o.CreatedAt).ToListAsync();
            return orders.Select(o => new ReadyToServeViewModel
            {
                OrderId = o.Id, Code = o.OrderCode, CustomerName = o.CustomerName,
                TableLabel = TableLabels.Format(o.Table?.Number ?? string.Empty),
                ItemsCount = o.TotalItemsCount
            }).ToList();
        }

        public async Task<TableConsumptionViewModel?> GetTableConsumptionAsync(int tableId)
        {
            var table = await _context.RestaurantTables.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId && t.IsActive);
            if (table == null) return null;

            var result = new TableConsumptionViewModel
            {
                TableId = table.Id, TableLabel = TableLabels.Format(table.Number),
                Status = table.Status, IsFree = table.Status == TableStatuses.Free
            };
            if (result.IsFree) return result;

            var order = await ActiveOrderOf(table.Id).AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync();
            if (order != null)
            {
                result.OrderCode = order.OrderCode;
                result.CustomerName = order.CustomerName;
                result.OrderStatus = order.Status;
                result.Total = order.TotalAmount;
                result.Items = order.Items.OrderBy(i => i.Id).Select(i => new DispatchItemViewModel
                {
                    Name = i.DishName, Quantity = i.Quantity, UnitPrice = i.UnitPrice
                }).ToList();
            }
            return result;
        }

        public async Task<SalonNewOrderViewModel?> GetAddItemsContextAsync(int tableId)
        {
            var table = await _context.RestaurantTables.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId && t.IsActive && t.Status == TableStatuses.Occupied);
            if (table == null) return null;
            var order = await ActiveOrderOf(table.Id).AsNoTracking().FirstOrDefaultAsync();
            if (order == null) return null;
            return new SalonNewOrderViewModel
            {
                TableId = table.Id, TableNumber = table.Number, CustomerName = order.CustomerName,
                AddToExisting = true, OrderCode = order.OrderCode
            };
        }

        public async Task<(bool Succeeded, string? ErrorMessage, Order? UpdatedOrder)> AddItemsAsync(SalonNewOrderViewModel request, string waiterId)
        {
            if (string.IsNullOrWhiteSpace(waiterId) || !await _context.Users.AnyAsync(u => u.Id == waiterId && u.IsActive))
                return (false, "No se pudo identificar al mozo activo.", null);

            var quantities = request.Items?.Where(i => i.Quantity > 0)
                .GroupBy(i => i.DishId)
                .Select(g => new { DishId = g.Key, Quantity = g.Sum(x => (long)x.Quantity) })
                .ToList();
            if (quantities == null || quantities.Count == 0)
                return (false, "Agrega al menos un plato.", null);
            if (quantities.Any(i => i.Quantity > 999 || i.DishId <= 0))
                return (false, "La cantidad máxima por plato es 999.", null);

            var dishIds = quantities.Select(i => i.DishId).ToArray();
            var dishes = await _context.Dishes.Where(d => dishIds.Contains(d.Id) && d.IsActive)
                .ToDictionaryAsync(d => d.Id);
            if (dishes.Count != dishIds.Length)
                return (false, "Uno o más platos ya no están disponibles.", null);

            try
            {
                var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == request.TableId);
                if (table == null || !table.IsActive || table.Status != TableStatuses.Occupied)
                    return (false, "La mesa ya no está ocupada. Actualiza la vista.", null);
                var order = await ActiveOrderOf(table.Id).Include(o => o.Items).FirstOrDefaultAsync();
                if (order == null)
                    return (false, "La mesa no tiene un pedido activo.", null);

                foreach (var q in quantities)
                {
                    var dish = dishes[q.DishId];
                    var line = order.Items.FirstOrDefault(i => i.DishId == dish.Id && i.UnitPrice == dish.Price);
                    if (line == null)
                    {
                        order.Items.Add(new OrderItem
                        {
                            DishId = dish.Id, DishName = dish.Name, Quantity = (int)q.Quantity,
                            UnitPrice = dish.Price, Subtotal = dish.Price * q.Quantity
                        });
                    }
                    else
                    {
                        if (line.Quantity + q.Quantity > 999)
                            return (false, "La cantidad máxima por plato es 999.", null);
                        line.Quantity += (int)q.Quantity;
                        line.Subtotal = line.UnitPrice * line.Quantity;
                    }
                }
                order.TotalAmount = order.Items.Sum(i => i.Subtotal);
                order.TotalItemsCount = order.Items.Sum(i => i.Quantity);
                // Los platos nuevos deben pasar otra vez por cocina.
                order.Status = OrderStatuses.InKitchen;
                await _context.SaveChangesAsync();
                return (true, null, order);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "El pedido cambió mientras lo editabas. Actualiza la vista.", null);
            }
            catch (DbUpdateException)
            {
                return (false, "No se pudieron agregar los platos. Inténtalo de nuevo.", null);
            }
        }
    }
}
