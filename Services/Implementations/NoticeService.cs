using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class NoticeService : INoticeService
    {
        // Solo se consideran avisos recientes, para que un repartidor nuevo no vea el historial completo.
        private static readonly TimeSpan Window = TimeSpan.FromHours(24);
        private const int MaxListed = 50;

        private readonly ApplicationDbContext _context;

        public NoticeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task NotifyDeliveryInPreparationAsync(int orderId)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);

            // HU-13 (detalle 1): solo delivery y solo cuando ya está "En preparación"
            if (order == null || order.Type != OrderType.Delivery || order.Status != OrderStatus.EnPreparacion)
                return;

            if (await _context.KitchenNotices.AnyAsync(n => n.OrderId == orderId))
                return;

            var customer = string.IsNullOrWhiteSpace(order.CustomerName) ? null : order.CustomerName.Trim();

            // Formato del criterio 2: "Pedido #F61 en preparación. Cocina comenzó a preparar el delivery de Juan Lopez"
            var message = customer == null
                ? $"Pedido #{order.Code} en preparación. Cocina comenzó a preparar el delivery"
                : $"Pedido #{order.Code} en preparación. Cocina comenzó a preparar el delivery de {customer}";

            _context.KitchenNotices.Add(new KitchenNotice
            {
                OrderId = orderId,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Otro request ya creó el aviso de este pedido (índice único por OrderId).
            }
        }

        public async Task<NoticeSummaryViewModel> GetSummaryAsync(string userId)
        {
            var since = DateTime.UtcNow - Window;
            var recent = _context.KitchenNotices.AsNoTracking().Where(n => n.CreatedAt >= since);

            var unread = await recent.CountAsync(n => !n.Reads.Any(r => r.UserId == userId));

            // HU-13 (detalle 4): del más reciente al más antiguo
            var rows = await recent
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Take(MaxListed)
                .Select(n => new
                {
                    n.Id,
                    n.Message,
                    n.CreatedAt,
                    IsRead = n.Reads.Any(r => r.UserId == userId)
                })
                .ToListAsync();

            return new NoticeSummaryViewModel
            {
                UnreadCount = unread,
                Notices = rows.Select(r => new NoticeItemViewModel
                {
                    Id = r.Id,
                    Message = r.Message,
                    IsRead = r.IsRead,
                    CreatedAtUnixMs = new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc))
                        .ToUnixTimeMilliseconds()
                }).ToList(),
                ServerNowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public async Task MarkAsReadAsync(string userId, int upToId)
        {
            var since = DateTime.UtcNow - Window;

            var ids = await _context.KitchenNotices
                .Where(n => n.Id <= upToId && n.CreatedAt >= since && !n.Reads.Any(r => r.UserId == userId))
                .Select(n => n.Id)
                .ToListAsync();

            if (ids.Count == 0) return;

            var now = DateTime.UtcNow;
            _context.KitchenNoticeReads.AddRange(ids.Select(id => new KitchenNoticeRead
            {
                NoticeId = id,
                UserId = userId,
                ReadAt = now
            }));

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Envío duplicado: esos avisos ya estaban marcados como leídos.
            }
        }
    }
}
