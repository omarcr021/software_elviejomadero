using software_elviejomadero.Models;

namespace software_elviejomadero.Services.Interfaces
{
    public enum LoginResultStatus
    {
        Success,
        InvalidCredentials,
        InactiveUser
    }

    public class LoginServiceResult
    {
        public LoginResultStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public ApplicationUser? User { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public interface IAuthenticationService
    {
        Task<LoginServiceResult> LoginAsync(string userName, string password, bool rememberMe);
        Task LogoutAsync();
    }
}
