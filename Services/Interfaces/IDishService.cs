using Microsoft.AspNetCore.Mvc.Rendering;
using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IDishService
    {
        Task<DishListViewModel> GetDishesGroupedByCategoryAsync();
        Task<EditDishViewModel?> GetDishForEditAsync(int id);
        Task<(bool Success, string? ErrorMessage)> CreateDishAsync(CreateDishViewModel model);
        Task<(bool Success, string? ErrorMessage)> UpdateDishAsync(EditDishViewModel model);
        Task<(bool Success, string? ErrorMessage, bool NewStatus)> ToggleActiveAsync(int id);
        Task<(bool Success, string? ErrorMessage)> DeleteDishAsync(int id);
        Task<IEnumerable<SelectListItem>> GetCategoriesSelectListAsync();
    }
}
