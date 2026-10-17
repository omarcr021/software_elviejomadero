using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Models;

namespace software_elviejomadero.Data
{
    public static class KitchenDemoSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger logger)
        {
            // Usuario cocinero de prueba
            var cook = await userManager.FindByNameAsync("cocinero");
            if (cook == null)
            {
                cook = new ApplicationUser
                {
                    UserName = "cocinero",
                    Email = "cocinero@elviejomadero.com",
                    FullName = "Cocinero de Prueba",
                    DNI = "00000001",
                    Phone = "999999998",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var created = await userManager.CreateAsync(cook, "Cocinero123*!");
                if (created.Succeeded)
                {
                    await userManager.AddToRoleAsync(cook, "Cocinero");
                    logger.LogInformation("Usuario cocinero de prueba creado (cocinero / Cocinero123*!).");
                }
            }

            if (await context.Orders.AnyAsync()) return;

            var dishes = await context.Dishes.ToDictionaryAsync(d => d.Name);
            if (dishes.Count == 0) return;

            var tables = await context.RestaurantTables.ToDictionaryAsync(t => t.Number);

            var receptionist = await userManager.FindByNameAsync("admin") ?? cook;
            var receptionistId = receptionist?.Id ?? "admin";

            var now = DateTime.UtcNow;

            OrderItem? Item(string dishName, int qty)
            {
                if (!dishes.TryGetValue(dishName, out var d)) return null;
                return new OrderItem
                {
                    DishId = d.Id,
                    DishName = d.Name,
                    Quantity = qty,
                    UnitPrice = d.Price,
                    Subtotal = d.Price * qty
                };
            }

            Order Build(string code, string type, string? tableNumber, string? customer, string? note,
                        string status, int minutesAgo, params OrderItem?[] items)
            {
                var created = now.AddMinutes(-minutesAgo);
                var validItems = items.Where(i => i != null).Select(i => i!).ToList();
                int? tableId = null;
                if (!string.IsNullOrWhiteSpace(tableNumber) && tables.TryGetValue(tableNumber, out var t))
                {
                    tableId = t.Id;
                }

                return new Order
                {
                    OrderCode = code,
                    OrderType = type,
                    TableId = tableId,
                    CustomerName = customer ?? "Cliente General",
                    CustomerPhone = "999888777",
                    AdditionalNote = note,
                    Status = status,
                    CreatedAt = created,
                    PreparationStartedAt = status == OrderStatuses.InPreparation ? created.AddMinutes(1) : (status == OrderStatuses.ReadyForDispatch ? created.AddMinutes(1) : null),
                    ReadyAt = status == OrderStatuses.ReadyForDispatch ? created.AddMinutes(14) : null,
                    PreparedByUserId = status != OrderStatuses.New ? cook?.Id : null,
                    PaymentMethod = PaymentMethods.Cash,
                    ReceptionistId = receptionistId,
                    TotalAmount = validItems.Sum(i => i.Subtotal),
                    TotalItemsCount = validItems.Sum(i => i.Quantity),
                    Items = validItems
                };
            }

            var orders = new List<Order>
            {
                Build("PED-0001", OrderTypes.DineIn, "M4", "Carlos Ramos", "Sin ají en las cremas", OrderStatuses.New, 3,
                    Item("1/2 Pollo Broaster Familiar", 1), Item("Porción de Papas Fritas", 2)),
                Build("PED-0002", OrderTypes.Delivery, null, "María Torres", null, OrderStatuses.New, 6,
                    Item("Alitas BBQ Crocantes", 2)),
                Build("PED-0003", OrderTypes.Takeout, null, "Luis Quispe", "Bien crocante, por favor", OrderStatuses.InPreparation, 12,
                    Item("Hamburguesa de Pollo Crispy", 2), Item("Porción de Papas Fritas", 1)),
                Build("PED-0004", OrderTypes.DineIn, "M2", "Ana Flores", null, OrderStatuses.InPreparation, 18,
                    Item("Pollo Broaster Entero", 1), Item("Ensalada Fresca", 2)),
                Build("PED-0005", OrderTypes.DineIn, "M7", "Pedro Ruiz", null, OrderStatuses.ReadyForDispatch, 25,
                    Item("Combo Tiras Crocantes", 1))
            };

            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();
            logger.LogInformation("Pedidos de prueba para cocina sembrados correctamente.");
        }
    }
}