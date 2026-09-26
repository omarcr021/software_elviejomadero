using software_elviejomadero.ViewModels;

namespace software_elviejomadero.Services.Interfaces
{
    public interface IEmployeeService
    {
        Task<EmployeeListViewModel> GetEmployeesAsync(string? roleFilter = null);
        Task<(bool Success, string? ErrorMessage, string? GeneratedUserName)> RegisterEmployeeAsync(CreateEmployeeViewModel model);
        Task<string> GenerateUniqueUserNameAsync(string fullName);
        string[] GetAvailableRoles();
    }
}
