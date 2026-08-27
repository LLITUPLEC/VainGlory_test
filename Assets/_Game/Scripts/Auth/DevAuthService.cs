using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Локальный гость (этап 0.3). Email недоступен без Nakama.</summary>
    public sealed class DevAuthService : IAuthService
    {
        public const string KeyId = "ashfold.userId";
        public const string KeyName = "ashfold.displayName";

        public static bool HasSavedGuest => PlayerPrefs.HasKey(KeyId);

        public bool SupportsEmail => false;

        public string DeviceId => PlayerPrefs.GetString(KeyId, string.Empty);

        public async Task<PlayerProfile> TryRestoreAsync()
        {
            if (!HasSavedGuest)
                return null;
            return await SignInGuestAsync(PlayerPrefs.GetString(KeyName, string.Empty));
        }

        public async Task<PlayerProfile> SignInGuestAsync(string preferredName)
        {
            await Task.Yield();

            var id = PlayerPrefs.GetString(KeyId, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = "dev_" + SystemInfo.deviceUniqueIdentifier;
                if (id.Length > 24)
                    id = id.Substring(0, 24);
                PlayerPrefs.SetString(KeyId, id);
            }

            var name = string.IsNullOrWhiteSpace(preferredName)
                ? PlayerPrefs.GetString(KeyName, string.Empty)
                : preferredName.Trim();

            if (string.IsNullOrEmpty(name))
                name = "Warrior_" + id.Substring(id.Length - 4).ToUpperInvariant();

            PlayerPrefs.SetString(KeyName, name);
            PlayerPrefs.Save();

            var profile = new PlayerProfile
            {
                UserId = id,
                DisplayName = name,
                Level = 1,
                Essence = 0,
                AuthProvider = "dev-guest"
            };
            await NakamaProgress.HydrateAsync(null, profile);
            return profile;
        }

        public Task<PlayerProfile> SignInEmailAsync(string email, string password)
        {
            throw new InvalidOperationException("Email login needs NakamaConfig.UseServer=true");
        }

        public Task<PlayerProfile> LinkEmailAsync(string email, string password)
        {
            throw new InvalidOperationException("Email link needs NakamaConfig.UseServer=true");
        }

        public async Task SaveProgressAsync(PlayerProfile profile)
        {
            await NakamaProgress.PushAsync(null, profile);
        }

        public void SignOutLocal()
        {
            PlayerPrefs.DeleteKey(KeyId);
            PlayerPrefs.Save();
        }
    }
}
