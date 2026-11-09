using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IEmployeeService
    {
        Task<EmployeeListViewModel> GetEmployeesAsync(string? roleFilter = null);
        Task<(bool Success, string? ErrorMessage, string? GeneratedUserName)> RegisterEmployeeAsync(CreateEmployeeViewModel model);
        Task<EditEmployeeViewModel?> GetEmployeeForEditAsync(string id);
        Task<(bool Success, string? ErrorMessage)> UpdateEmployeeAsync(EditEmployeeViewModel model, string currentAdminId);
        Task<(bool Success, string? ErrorMessage, bool NewStatus)> ToggleEmployeeStatusAsync(string id, string currentAdminId);
        Task<(bool Success, string? ErrorMessage)> DeleteEmployeeAsync(string id, string currentAdminId);
        Task<string> GenerateUniqueUserNameAsync(string fullName);
        string[] GetAvailableRoles();
    }
}
