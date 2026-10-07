using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Models;

namespace software_elviejomadero.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            // 1. Aplicar migraciones pendientes automáticamente
            await context.Database.MigrateAsync();

            // Asegurar creación idempotente de tablas Orders y OrderItems en SQLite (Sprint 2 - HU-06)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Orders"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Orders"" PRIMARY KEY AUTOINCREMENT,
                    ""OrderCode"" TEXT NOT NULL,
                    ""OrderType"" TEXT NOT NULL,
                    ""Status"" TEXT NOT NULL,
                    ""CustomerName"" TEXT NOT NULL,
                    ""CustomerPhone"" TEXT NOT NULL,
                    ""DeliveryAddress"" TEXT NULL,
                    ""Latitude"" REAL NULL,
                    ""Longitude"" REAL NULL,
                    ""PaymentMethod"" TEXT NOT NULL,
                    ""AdditionalNote"" TEXT NULL,
                    ""TotalAmount"" decimal(10, 2) NOT NULL,
                    ""TotalItemsCount"" INTEGER NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""ReceptionistId"" TEXT NOT NULL,
                    CONSTRAINT ""FK_Orders_AspNetUsers_ReceptionistId"" FOREIGN KEY (""ReceptionistId"") REFERENCES ""AspNetUsers"" (""Id"") ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Orders_OrderCode"" ON ""Orders"" (""OrderCode"");
                CREATE INDEX IF NOT EXISTS ""IX_Orders_ReceptionistId"" ON ""Orders"" (""ReceptionistId"");

                CREATE TABLE IF NOT EXISTS ""OrderItems"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_OrderItems"" PRIMARY KEY AUTOINCREMENT,
                    ""OrderId"" INTEGER NOT NULL,
                    ""DishId"" INTEGER NOT NULL,
                    ""DishName"" TEXT NOT NULL,
                    ""Quantity"" INTEGER NOT NULL,
                    ""UnitPrice"" decimal(10, 2) NOT NULL,
                    ""Subtotal"" decimal(10, 2) NOT NULL,
                    CONSTRAINT ""FK_OrderItems_Dishes_DishId"" FOREIGN KEY (""DishId"") REFERENCES ""Dishes"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_OrderItems_Orders_OrderId"" FOREIGN KEY (""OrderId"") REFERENCES ""Orders"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_OrderItems_DishId"" ON ""OrderItems"" (""DishId"");
                CREATE INDEX IF NOT EXISTS ""IX_OrderItems_OrderId"" ON ""OrderItems"" (""OrderId"");
            ");

            // 2. Sembrar Roles requeridos para El Viejo Madero
            string[] roles = ["Administrador", "Mozo", "Cocinero", "Recepcionista", "Repartidor"];
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.LogInformation("Rol creado: {Role}", roleName);
                }
            }

            // 3. Sembrar Usuario Administrador Inicial
            var adminUser = await userManager.FindByNameAsync("admin");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@elviejomadero.com",
                    FullName = "Administrador El Viejo Madero",
                    DNI = "00000000",
                    Phone = "999999999",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(adminUser, "Admin123*!");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrador");
                    logger.LogInformation("Usuario Administrador inicial creado con éxito (admin / Admin123*!).");
                }
                else
                {
                    logger.LogError("Error al crear usuario administrador inicial: {Errors}",
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }

            // 3b. Sembrar Usuario Recepcionista Inicial del Prototipo (Lucía Vega - lvega / Recepcion123*!)
            var recepUser = await userManager.FindByNameAsync("lvega");
            if (recepUser == null && !await context.Users.AnyAsync(u => u.DNI == "71234567"))
            {
                recepUser = new ApplicationUser
                {
                    UserName = "lvega",
                    Email = "lvega@elviejomadero.com",
                    FullName = "Lucía Vega",
                    DNI = "71234567",
                    Phone = "987654321",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createRecepResult = await userManager.CreateAsync(recepUser, "Recepcion123*!");
                if (createRecepResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(recepUser, "Recepcionista");
                    logger.LogInformation("Usuario Recepcionista inicial creado con éxito (lvega / Recepcion123*!).");
                }
            }

            // 4. Sembrar Categorías si no existen
            if (!await context.Categories.AnyAsync())
            {
                var catBroaster = new Category { Name = "Combos Broaster", IsActive = true };
                var catEspecialidades = new Category { Name = "Especialidades", IsActive = true };
                var catAcompanamientos = new Category { Name = "Acompañamientos", IsActive = true };

                await context.Categories.AddRangeAsync(catBroaster, catEspecialidades, catAcompanamientos);
                await context.SaveChangesAsync();

                // 5. Sembrar Platos iniciales del prototipo
                var dishes = new List<Dish>
                {
                    new()
                    {
                        Name = "1/4 Pollo Broaster Clásico",
                        Description = "1/4 pollo broaster con papas fritas crocantes y ensalada fresca clásica.",
                        Price = 24.00m,
                        PreparationTimeMinutes = 15,
                        CategoryId = catBroaster.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "1/2 Pollo Broaster Familiar",
                        Description = "1/2 pollo broaster crujiente acompañado con papas familiares y cremas de la casa.",
                        Price = 42.00m,
                        PreparationTimeMinutes = 20,
                        CategoryId = catBroaster.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Pollo Broaster Entero",
                        Description = "Pollo broaster entero dorado y crocante, incluye porción familiar de papas y ensaladas.",
                        Price = 78.00m,
                        PreparationTimeMinutes = 25,
                        CategoryId = catBroaster.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Combo Tiras Crocantes",
                        Description = "6 tiras tiernas de pechuga crocante con papas fritas y salsas especiales.",
                        Price = 26.00m,
                        PreparationTimeMinutes = 12,
                        CategoryId = catBroaster.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Alitas BBQ Crocantes",
                        Description = "8 piezas de alitas bañadas en salsa BBQ artesanal acompañadas de papas fritas.",
                        Price = 28.00m,
                        PreparationTimeMinutes = 18,
                        CategoryId = catEspecialidades.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Hamburguesa de Pollo Crispy",
                        Description = "Filete de pechuga extra crocante, lechuga fresca, tomate y salsa especial en pan artesanal.",
                        Price = 20.00m,
                        PreparationTimeMinutes = 12,
                        CategoryId = catEspecialidades.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Porción de Papas Fritas",
                        Description = "Papas fritas artesanales cortadas y sazonadas con sal de maras.",
                        Price = 10.00m,
                        PreparationTimeMinutes = 10,
                        CategoryId = catAcompanamientos.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Ensalada Fresca",
                        Description = "Selección de lechugas, tomate, pepino y vinagreta de la casa.",
                        Price = 8.00m,
                        PreparationTimeMinutes = 8,
                        CategoryId = catAcompanamientos.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await context.Dishes.AddRangeAsync(dishes);
                await context.SaveChangesAsync();
                logger.LogInformation("Categorías y platos de muestra sembrados correctamente.");
            }
        }
    }
}
