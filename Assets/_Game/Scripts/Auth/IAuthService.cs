using System.Threading.Tasks;

namespace Ashfold
{
    public interface IAuthService
    {
        bool SupportsEmail { get; }
        string DeviceId { get; }
        Task<PlayerProfile> TryRestoreAsync();
        Task<PlayerProfile> SignInGuestAsync(string preferredName);
        Task<PlayerProfile> SignInEmailAsync(string email, string password);
        Task<PlayerProfile> LinkEmailAsync(string email, string password);
        Task SaveProgressAsync(PlayerProfile profile);
        void SignOutLocal();
    }
}
