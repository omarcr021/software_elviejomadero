using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services
{
    public class KitchenService : IKitchenService
    {
        private readonly ApplicationDbContext _context;
        private readonly INoticeService _noticeService;
        private readonly ILogger<KitchenService> _logger;

        public KitchenService(
            ApplicationDbContext context,
            INoticeService noticeService,
            ILogger<KitchenService> logger)
        {
            _context = context;
            _noticeService = noticeService;
            _logger = logger;
        }

        public async Task<KitchenBoardViewModel> GetBoardAsync()
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Entregado)
                .OrderBy(o => o.CreatedAt)          // del más antiguo al más reciente
                .ThenBy(o => o.Id)
                .Select(o => new KitchenOrderViewModel
                {
                    Id = o.Id,
                    Code = o.Code,
                    Type = o.Type,
                    TableNumber = o.TableNumber,
                    CustomerName = o.CustomerName,
                    Note = o.Note,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    Items = o.Items
                        .OrderBy(i => i.Id)
                        .Select(i => new KitchenOrderItemViewModel
                        {
                            DishName = i.DishName,
                            Quantity = i.Quantity
                        })
                        .ToList()
                })
                .ToListAsync();

            return new KitchenBoardViewModel
            {
                NewOrders = orders.Where(o => o.Status == OrderStatus.Nuevo).ToList(),
                InPreparationOrders = orders.Where(o => o.Status == OrderStatus.EnPreparacion).ToList(),
                ReadyOrders = orders.Where(o => o.Status == OrderStatus.Listo).ToList(),
                ServerNowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public async Task<KitchenActionResult> StartPreparationAsync(int orderId, string? cookUserId)
        {
            var now = DateTime.UtcNow;

            // UPDATE atómico condicionado al estado actual: si dos cocineros pulsan a la vez, solo uno avanza.
            var rows = await _context.Orders
                .Where(o => o.Id == orderId && o.Status == OrderStatus.Nuevo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.EnPreparacion)
                    .SetProperty(o => o.PreparationStartedAt, now)
                    .SetProperty(o => o.PreparedByUserId, cookUserId));

            if (rows == 1)
            {
                // HU-13: si es un pedido de delivery, se avisa al repartidor.
                // Un fallo al generar el aviso no debe impedir que cocina avance el pedido.
                try
                {
                    await _noticeService.NotifyDeliveryInPreparationAsync(orderId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo generar el aviso de delivery del pedido {OrderId}.", orderId);
                }

                return KitchenActionResult.Ok("El pedido pasó a \"En preparación\".");
            }

            var current = await GetCurrentStatusAsync(orderId);
            return current switch
            {
                null => KitchenActionResult.Fail("El pedido no existe."),
                OrderStatus.EnPreparacion => KitchenActionResult.Fail("El pedido ya está en preparación."),
                _ => KitchenActionResult.Fail("El pedido ya fue marcado como listo; no se puede retroceder su estado.")
            };
        }

        public async Task<KitchenActionResult> MarkAsReadyAsync(int orderId)
        {
            var now = DateTime.UtcNow;

            var rows = await _context.Orders
                .Where(o => o.Id == orderId && o.Status == OrderStatus.EnPreparacion)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Listo)
                    .SetProperty(o => o.ReadyAt, now));

            if (rows == 1)
            {
                var type = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.Id == orderId)
                    .Select(o => o.Type)
                    .FirstAsync();

                var label = type switch
                {
                    OrderType.Mesa => "Listo para servir",
                    OrderType.ParaLlevar => "Listo para recoger",
                    OrderType.Delivery => "Listo para reparto",
                    _ => "Listo"
                };

                return KitchenActionResult.Ok($"El pedido pasó a \"{label}\".");
            }

            var current = await GetCurrentStatusAsync(orderId);
            return current switch
            {
                null => KitchenActionResult.Fail("El pedido no existe."),
                OrderStatus.Nuevo => KitchenActionResult.Fail("Primero debes empezar la preparación; no se puede saltar un estado."),
                _ => KitchenActionResult.Fail("El pedido ya fue marcado como listo.")
            };
        }

        private async Task<OrderStatus?> GetCurrentStatusAsync(int orderId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync();
        }
    }
}