using System.Threading.Tasks;

namespace Ashfold
{
    public interface IAuthService
    {
        Task<PlayerProfile> SignInGuestAsync(string preferredName);
    }
}
