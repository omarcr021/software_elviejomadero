using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class KitchenService : IKitchenService
    {
        private readonly ApplicationDbContext _context;

        public KitchenService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<KitchenBoardViewModel> GetBoardAsync()
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.Table)
                .Where(o => o.Status != OrderStatuses.Delivered && o.Status != OrderStatuses.OnTheWay)
                .OrderBy(o => o.CreatedAt)
                .ThenBy(o => o.Id)
                .ToListAsync();

            var mappedOrders = orders.Select(o =>
            {
                int? tableNum = null;
                if (o.Table != null && !string.IsNullOrWhiteSpace(o.Table.Number))
                {
                    var cleanNum = o.Table.Number.Replace("M", "", StringComparison.OrdinalIgnoreCase).Trim();
                    if (int.TryParse(cleanNum, out var n))
                    {
                        tableNum = n;
                    }
                }

                var orderType = o.OrderType switch
                {
                    OrderTypes.DineIn => OrderType.Mesa,
                    OrderTypes.Takeout => OrderType.ParaLlevar,
                    _ => OrderType.Delivery
                };

                var orderStatus = o.Status switch
                {
                    OrderStatuses.InPreparation => OrderStatus.EnPreparacion,
                    OrderStatuses.ReadyForDispatch => OrderStatus.Listo,
                    _ => OrderStatus.Nuevo
                };

                return new KitchenOrderViewModel
                {
                    Id = o.Id,
                    Code = o.OrderCode,
                    Type = orderType,
                    TableNumber = tableNum,
                    CustomerName = o.CustomerName,
                    Note = o.AdditionalNote,
                    Status = orderStatus,
                    CreatedAt = o.CreatedAt,
                    Items = o.Items
                        .OrderBy(i => i.Id)
                        .Select(i => new KitchenOrderItemViewModel
                        {
                            DishName = i.DishName,
                            Quantity = i.Quantity
                        })
                        .ToList()
                };
            }).ToList();

            return new KitchenBoardViewModel
            {
                NewOrders = mappedOrders.Where(o => o.Status == OrderStatus.Nuevo).ToList(),
                InPreparationOrders = mappedOrders.Where(o => o.Status == OrderStatus.EnPreparacion).ToList(),
                ReadyOrders = mappedOrders.Where(o => o.Status == OrderStatus.Listo).ToList(),
                ServerNowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public async Task<KitchenActionResult> StartPreparationAsync(int orderId, string? cookUserId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                return KitchenActionResult.Fail("El pedido no existe.");
            }

            if (order.Status == OrderStatuses.InPreparation)
            {
                return KitchenActionResult.Fail("El pedido ya está en preparación.");
            }

            if (order.Status != OrderStatuses.New && order.Status != OrderStatuses.InKitchen)
            {
                return KitchenActionResult.Fail("El pedido ya fue marcado como listo; no se puede retroceder su estado.");
            }

            order.Status = OrderStatuses.InPreparation;
            order.PreparationStartedAt = DateTime.UtcNow;
            order.PreparedByUserId = cookUserId;

            try
            {
                await _context.SaveChangesAsync();
                return KitchenActionResult.Ok("El pedido pasó a \"En preparación\".");
            }
            catch (DbUpdateConcurrencyException)
            {
                return KitchenActionResult.Fail("El pedido fue modificado por otro usuario; actualiza el tablero.");
            }
        }

        public async Task<KitchenActionResult> MarkAsReadyAsync(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                return KitchenActionResult.Fail("El pedido no existe.");
            }

            if (order.Status == OrderStatuses.New || order.Status == OrderStatuses.InKitchen)
            {
                return KitchenActionResult.Fail("Primero debes empezar la preparación; no se puede saltar un estado.");
            }

            if (order.Status != OrderStatuses.InPreparation)
            {
                return KitchenActionResult.Fail("El pedido ya fue marcado como listo.");
            }

            order.Status = OrderStatuses.ReadyForDispatch;
            order.ReadyAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                var label = order.OrderType switch
                {
                    OrderTypes.DineIn => "Listo para servir",
                    OrderTypes.Takeout => "Listo para recoger",
                    OrderTypes.Delivery => "Listo para reparto",
                    _ => "Listo"
                };

                return KitchenActionResult.Ok($"El pedido pasó a \"{label}\".");
            }
            catch (DbUpdateConcurrencyException)
            {
                return KitchenActionResult.Fail("El pedido fue modificado por otro usuario; actualiza el tablero.");
            }
        }
    }
}
