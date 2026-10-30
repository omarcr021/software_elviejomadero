using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private const int MaxAttempts = 3;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(ApplicationDbContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CreateOrderViewModel> GetCreateFormAsync(CreateOrderViewModel? model = null)
        {
            model ??= new CreateOrderViewModel();

            model.Dishes = await _context.Dishes
                .AsNoTracking()
                .Where(d => d.IsActive && d.Category != null && d.Category.IsActive)
                .OrderBy(d => d.Category!.Name)
                .ThenBy(d => d.Name)
                .Select(d => new DishOptionViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Price = d.Price,
                    CategoryName = d.Category!.Name
                })
                .ToListAsync();

            while (model.Lines.Count < 2)
                model.Lines.Add(new OrderLineInput());

            return model;
        }

        public async Task<(bool Success, string? ErrorMessage, string? Code)> CreateOrderAsync(CreateOrderViewModel model, string? userId)
        {
            if (!Enum.IsDefined(model.Type))
                return (false, "El tipo de pedido no es válido.", null);

            var customer = string.IsNullOrWhiteSpace(model.CustomerName) ? null : model.CustomerName.Trim();
            var note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();

            if (model.Type == OrderType.Mesa)
            {
                if (model.TableNumber is null or < 1)
                    return (false, "Indica el número de mesa.", null);
            }
            else if (customer == null)
            {
                // Sin nombre, el aviso del repartidor (HU-13) no podría identificar el pedido.
                return (false, "El nombre del cliente es obligatorio para pedidos para llevar y delivery.", null);
            }

            // Se ignoran las líneas sin plato y se suman las cantidades del mismo plato.
            var lines = (model.Lines ?? new())
                .Where(l => l.DishId > 0)
                .GroupBy(l => l.DishId)
                .Select(g => new { DishId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            if (lines.Count == 0)
                return (false, "Agrega al menos un plato al pedido.", null);

            if (lines.Any(l => l.Quantity < 1 || l.Quantity > 999))
                return (false, "La cantidad de cada plato debe estar entre 1 y 999.", null);

            var ids = lines.Select(l => l.DishId).ToList();
            var dishes = await _context.Dishes
                .AsNoTracking()
                .Where(d => ids.Contains(d.Id) && d.IsActive)
                .ToDictionaryAsync(d => d.Id);

            if (dishes.Count != ids.Count)
                return (false, "Uno de los platos seleccionados ya no está disponible.", null);

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var order = new Order
                {
                    Code = await NextCodeAsync(),
                    Type = model.Type,
                    TableNumber = model.Type == OrderType.Mesa ? model.TableNumber : null,
                    CustomerName = customer,
                    Note = note,
                    Status = OrderStatus.Nuevo,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId,
                    Items = lines.Select(l => new OrderItem
                    {
                        DishId = l.DishId,
                        DishName = dishes[l.DishId].Name,   // copia del nombre al momento del pedido
                        Quantity = l.Quantity,
                        UnitPrice = dishes[l.DishId].Price
                    }).ToList()
                };

                _context.Orders.Add(order);

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Pedido {Code} ({Type}) registrado.", order.Code, order.Type);
                    return (true, null, order.Code);
                }
                catch (DbUpdateException ex)
                {
                    // Dos pedidos simultáneos pudieron generar el mismo código (índice único): se reintenta.
                    _logger.LogWarning(ex, "Conflicto al registrar el pedido (intento {Attempt}).", attempt);
                    _context.ChangeTracker.Clear();
                }
            }

            return (false, "No se pudo registrar el pedido. Inténtalo de nuevo.", null);
        }

        private async Task<string> NextCodeAsync()
        {
            var last = await _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.Id)
                .Select(o => o.Code)
                .FirstOrDefaultAsync();

            var number = 0;
            if (last != null)
            {
                var digits = new string(last.Where(char.IsDigit).ToArray());
                int.TryParse(digits, out number);
            }

            return $"PED-{number + 1:0000}";
        }
    }
}
