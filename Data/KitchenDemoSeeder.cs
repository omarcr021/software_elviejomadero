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
            if (await userManager.FindByNameAsync("cocinero") == null)
            {
                var cook = new ApplicationUser
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

            // HU-13: usuario repartidor de prueba (recibe los avisos de cocina)
            if (await userManager.FindByNameAsync("repartidor") == null)
            {
                var courier = new ApplicationUser
                {
                    UserName = "repartidor",
                    Email = "repartidor@elviejomadero.com",
                    FullName = "Repartidor de Prueba",
                    DNI = "00000002",
                    Phone = "999999997",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var created = await userManager.CreateAsync(courier, "Repartidor123*!");
                if (created.Succeeded)
                {
                    await userManager.AddToRoleAsync(courier, "Repartidor");
                    logger.LogInformation("Usuario repartidor de prueba creado (repartidor / Repartidor123*!).");
                }
            }

            if (await context.Orders.AnyAsync()) return;

            var dishes = await context.Dishes.ToDictionaryAsync(d => d.Name);
            if (dishes.Count == 0) return;

            var now = DateTime.UtcNow;

            OrderItem? Item(string dishName, int qty) =>
                dishes.TryGetValue(dishName, out var d)
                    ? new OrderItem { DishId = d.Id, DishName = d.Name, Quantity = qty, UnitPrice = d.Price }
                    : null;

            Order Build(string code, OrderType type, int? table, string? customer, string? note,
                        OrderStatus status, int minutesAgo, params OrderItem?[] items)
            {
                var created = now.AddMinutes(-minutesAgo);
                return new Order
                {
                    Code = code,
                    Type = type,
                    TableNumber = table,
                    CustomerName = customer,
                    Note = note,
                    Status = status,
                    CreatedAt = created,
                    PreparationStartedAt = status >= OrderStatus.EnPreparacion ? created.AddMinutes(1) : null,
                    ReadyAt = status >= OrderStatus.Listo ? created.AddMinutes(14) : null,
                    Items = items.Where(i => i != null).Select(i => i!).ToList()
                };
            }

            var orders = new List<Order>
            {
                Build("PED-0001", OrderType.Mesa, 4, "Carlos Ramos", "Sin ají en las cremas", OrderStatus.Nuevo, 3,
                    Item("1/2 Pollo Broaster Familiar", 1), Item("Porción de Papas Fritas", 2)),
                Build("PED-0002", OrderType.Delivery, null, "María Torres", null, OrderStatus.Nuevo, 6,
                    Item("Alitas BBQ Crocantes", 2)),
                Build("PED-0003", OrderType.ParaLlevar, null, "Luis Quispe", "Bien crocante, por favor", OrderStatus.EnPreparacion, 12,
                    Item("Hamburguesa de Pollo Crispy", 2), Item("Porción de Papas Fritas", 1)),
                Build("PED-0004", OrderType.Mesa, 2, "Ana Flores", null, OrderStatus.EnPreparacion, 18,
                    Item("Pollo Broaster Entero", 1), Item("Ensalada Fresca", 2)),
                Build("PED-0005", OrderType.Mesa, 7, null, null, OrderStatus.Listo, 25,
                    Item("Combo Tiras Crocantes", 1))
            };

            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();
            logger.LogInformation("Pedidos de prueba para cocina sembrados correctamente.");
        }
    }
}