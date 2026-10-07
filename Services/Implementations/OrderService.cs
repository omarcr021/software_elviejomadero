using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;

        public OrderService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReceptionOrderCardViewModel>> GetReceptionOrdersAsync()
        {
            var orders = await _context.Orders
                .Include(o => o.Receptionist)
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return orders.Select(o => new ReceptionOrderCardViewModel
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                OrderType = o.OrderType,
                Status = o.Status,
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                DisplayAddress = o.OrderType == OrderTypes.Takeout
                    ? "Cliente en local · Para llevar"
                    : (o.DeliveryAddress ?? string.Empty),
                TotalItemsCount = o.TotalItemsCount,
                PaymentMethod = o.PaymentMethod,
                AdditionalNote = o.AdditionalNote,
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt,
                ReceptionistName = o.Receptionist?.FullName ?? string.Empty
            }).ToList();
        }

        public async Task<string> GenerateUniqueOrderCodeAsync(string orderType)
        {
            string prefix = orderType == OrderTypes.Takeout ? "#T" : "#D";

            for (int attempt = 0; attempt < 100; attempt++)
            {
                int number = Random.Shared.Next(10, 100);
                string candidate = $"{prefix}{number}";

                bool exists = await _context.Orders.AnyAsync(o => o.OrderCode == candidate);
                if (!exists)
                {
                    return candidate;
                }
            }

            // Fallback de 3 dígitos si los de 2 dígitos están ocupados
            while (true)
            {
                int number = Random.Shared.Next(100, 1000);
                string candidate = $"{prefix}{number}";
                if (!await _context.Orders.AnyAsync(o => o.OrderCode == candidate))
                {
                    return candidate;
                }
            }
        }

        public async Task<(bool Succeeded, string? ErrorMessage, Order? CreatedOrder)> CreateReceptionOrderAsync(
            CreateReceptionOrderViewModel model,
            string receptionistId)
        {
            if (string.IsNullOrWhiteSpace(receptionistId))
            {
                return (false, "No se pudo identificar a la recepcionista que registra el pedido.", null);
            }

            // 1. Validar tipo de pedido (Delivery o Para llevar)
            if (model.OrderType != OrderTypes.Delivery && model.OrderType != OrderTypes.Takeout)
            {
                return (false, "El tipo de pedido debe ser 'Delivery' o 'Para llevar'.", null);
            }

            // 2. Validar datos del cliente
            if (string.IsNullOrWhiteSpace(model.CustomerName))
            {
                return (false, "El nombre del cliente es obligatorio.", null);
            }

            if (string.IsNullOrWhiteSpace(model.CustomerPhone))
            {
                return (false, "El teléfono del cliente es obligatorio.", null);
            }

            // 3. Validar dirección y ubicación en el mapa para Delivery, o asignar "Para llevar"
            string finalAddress;
            double? finalLat = null;
            double? finalLng = null;

            if (model.OrderType == OrderTypes.Delivery)
            {
                if (string.IsNullOrWhiteSpace(model.DeliveryAddress))
                {
                    return (false, "El pedido de delivery requiere dirección de entrega.", null);
                }

                if (!model.Latitude.HasValue || !model.Longitude.HasValue)
                {
                    return (false, "El pedido de delivery requiere ubicar el pin en el mapa.", null);
                }

                finalAddress = model.DeliveryAddress.Trim();
                finalLat = model.Latitude.Value;
                finalLng = model.Longitude.Value;
            }
            else
            {
                finalAddress = OrderTypes.Takeout;
            }

            // 4. Validar que el pedido tenga al menos un plato
            var validInputItems = model.Items?
                .Where(i => i.Quantity > 0)
                .GroupBy(i => i.DishId)
                .Select(g => new CreateOrderItemInputModel
                {
                    DishId = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList() ?? new List<CreateOrderItemInputModel>();

            if (!validInputItems.Any())
            {
                return (false, "Un pedido debe tener al menos un plato para poder enviarse a cocina.", null);
            }

            // 5. Validar método de pago permitido (Yape, Plin, Efectivo)
            if (string.IsNullOrWhiteSpace(model.PaymentMethod) ||
                !PaymentMethods.Allowed.Contains(model.PaymentMethod))
            {
                return (false, "El método de pago debe ser Yape, Plin o Efectivo.", null);
            }

            // 6. Consultar platos activos en la base de datos y calcular subtotales y total
            var dishIds = validInputItems.Select(i => i.DishId).ToList();
            var dishesInDb = await _context.Dishes
                .Where(d => dishIds.Contains(d.Id) && d.IsActive)
                .ToDictionaryAsync(d => d.Id);

            if (dishesInDb.Count != validInputItems.Count)
            {
                return (false, "Uno o más platos seleccionados no existen o no están disponibles en la carta.", null);
            }

            var orderItems = new List<OrderItem>();
            decimal totalAmount = 0m;
            int totalItemsCount = 0;

            foreach (var itemInput in validInputItems)
            {
                var dish = dishesInDb[itemInput.DishId];
                decimal subtotal = dish.Price * itemInput.Quantity;

                orderItems.Add(new OrderItem
                {
                    DishId = dish.Id,
                    DishName = dish.Name,
                    Quantity = itemInput.Quantity,
                    UnitPrice = dish.Price,
                    Subtotal = subtotal
                });

                totalAmount += subtotal;
                totalItemsCount += itemInput.Quantity;
            }

            // 7. Generar código único y registrar el pedido con estado "Nuevo"
            string uniqueCode = await GenerateUniqueOrderCodeAsync(model.OrderType);

            var order = new Order
            {
                OrderCode = uniqueCode,
                OrderType = model.OrderType,
                Status = OrderStatuses.New,
                CustomerName = model.CustomerName.Trim(),
                CustomerPhone = model.CustomerPhone.Trim(),
                DeliveryAddress = finalAddress,
                Latitude = finalLat,
                Longitude = finalLng,
                PaymentMethod = model.PaymentMethod,
                AdditionalNote = string.IsNullOrWhiteSpace(model.AdditionalNote)
                    ? null
                    : model.AdditionalNote.Trim(),
                TotalAmount = totalAmount,
                TotalItemsCount = totalItemsCount,
                CreatedAt = DateTime.UtcNow,
                ReceptionistId = receptionistId,
                Items = orderItems
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return (true, null, order);
        }
    }
}
