using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using software_elviejomadero.Data;
using software_elviejomadero.Models;
using software_elviejomadero.Services.Interfaces;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Implementations
{
    public class DishService : IDishService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DishService> _logger;

        public DishService(ApplicationDbContext context, ILogger<DishService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DishListViewModel> GetDishesGroupedByCategoryAsync()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .Include(c => c.Dishes)
                .OrderBy(c => c.Id)
                .AsNoTracking()
                .ToListAsync();

            var groups = new List<CategoryGroupViewModel>();
            var totalDishes = 0;
            var activeDishes = 0;

            foreach (var cat in categories)
            {
                var dishItems = cat.Dishes
                    .OrderByDescending(d => d.IsActive)
                    .ThenBy(d => d.Name)
                    .Select(d => new DishItemViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        Price = d.Price,
                        PreparationTimeMinutes = d.PreparationTimeMinutes,
                        CategoryId = d.CategoryId,
                        CategoryName = cat.Name,
                        IsActive = d.IsActive
                    })
                    .ToList();

                totalDishes += dishItems.Count;
                activeDishes += dishItems.Count(d => d.IsActive);

                groups.Add(new CategoryGroupViewModel
                {
                    CategoryId = cat.Id,
                    CategoryName = cat.Name,
                    Dishes = dishItems
                });
            }

            var selectCategories = categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return new DishListViewModel
            {
                CategoryGroups = groups,
                TotalDishesCount = totalDishes,
                ActiveDishesCount = activeDishes,
                Categories = selectCategories
            };
        }

        public async Task<EditDishViewModel?> GetDishForEditAsync(int id)
        {
            var dish = await _context.Dishes.FindAsync(id);
            if (dish == null)
                return null;

            var categories = await GetCategoriesSelectListAsync();

            return new EditDishViewModel
            {
                Id = dish.Id,
                Name = dish.Name,
                Description = dish.Description,
                Price = dish.Price,
                PreparationTimeMinutes = dish.PreparationTimeMinutes,
                CategoryId = dish.CategoryId,
                IsActive = dish.IsActive,
                Categories = categories
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> CreateDishAsync(CreateDishViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return (false, "El nombre del plato es obligatorio.");

            if (model.Price <= 0)
                return (false, "El precio debe ser mayor que cero.");

            if (model.PreparationTimeMinutes < 0)
                return (false, "El tiempo de preparación no puede ser negativo.");

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == model.CategoryId && c.IsActive);
            if (!categoryExists)
                return (false, "La categoría seleccionada no existe o no está activa.");

            var dish = new Dish
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                Price = model.Price,
                PreparationTimeMinutes = model.PreparationTimeMinutes,
                CategoryId = model.CategoryId,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Dishes.Add(dish);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Plato creado exitosamente: {Name} (ID: {Id})", dish.Name, dish.Id);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateDishAsync(EditDishViewModel model)
        {
            var dish = await _context.Dishes.FindAsync(model.Id);
            if (dish == null)
                return (false, "El plato no fue encontrado.");

            if (string.IsNullOrWhiteSpace(model.Name))
                return (false, "El nombre del plato es obligatorio.");

            if (model.Price <= 0)
                return (false, "El precio debe ser mayor que cero.");

            if (model.PreparationTimeMinutes < 0)
                return (false, "El tiempo de preparación no puede ser negativo.");

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == model.CategoryId && c.IsActive);
            if (!categoryExists)
                return (false, "La categoría seleccionada no existe.");

            dish.Name = model.Name.Trim();
            dish.Description = model.Description?.Trim();
            dish.Price = model.Price;
            dish.PreparationTimeMinutes = model.PreparationTimeMinutes;
            dish.CategoryId = model.CategoryId;
            dish.IsActive = model.IsActive;
            dish.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Plato actualizado exitosamente: {Name} (ID: {Id})", dish.Name, dish.Id);

            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage, bool NewStatus)> ToggleActiveAsync(int id)
        {
            var dish = await _context.Dishes.FindAsync(id);
            if (dish == null)
                return (false, "El plato no fue encontrado.", false);

            dish.IsActive = !dish.IsActive;
            dish.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Estado del plato {Name} cambiado a: {Status}", dish.Name, dish.IsActive ? "Activo" : "Inactivo");
            return (true, null, dish.IsActive);
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteDishAsync(int id)
        {
            var dish = await _context.Dishes.FindAsync(id);
            if (dish == null)
                return (false, "El plato no fue encontrado.");

            _context.Dishes.Remove(dish);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Plato eliminado: {Name} (ID: {Id})", dish.Name, id);
            return (true, null);
        }

        public async Task<IEnumerable<SelectListItem>> GetCategoriesSelectListAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
        }
    }
}
