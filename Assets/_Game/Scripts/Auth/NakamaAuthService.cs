using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Device + email (Nakama). Прогресс в Storage, сессия на диск.</summary>
    public sealed class NakamaAuthService : IAuthService
    {
        public const string KeyDevice = "ashfold.nakama.deviceId";
        public const string KeyName = "ashfold.displayName";

        readonly NakamaConnection _conn;

        public bool SupportsEmail => true;

        public string DeviceId => PlayerPrefs.GetString(KeyDevice, string.Empty);

        public NakamaAuthService(NakamaConnection conn)
        {
            _conn = conn;
        }

        public async Task<PlayerProfile> TryRestoreAsync()
        {
            var session = await _conn.TryRestoreSessionAsync();
            if (session != null)
                return await FinishAsync();

            if (PlayerPrefs.GetInt(NakamaConnection.KeyForceLogin, 0) == 1)
                return null;

            if (PlayerPrefs.HasKey(KeyDevice))
                return await SignInGuestAsync(PlayerPrefs.GetString(KeyName, string.Empty));

            return null;
        }

        public async Task<PlayerProfile> SignInGuestAsync(string preferredName)
        {
            var deviceId = EnsureDeviceId();
            var name = ResolveName(preferredName);
            var username = SanitizeUsername(name);
            await _conn.AuthenticateDeviceAsync(deviceId, username);
            try
            {
                if (await _conn.EnsureSessionAsync())
                    await _conn.Client.UpdateAccountAsync(_conn.Session, username, name, null, null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] UpdateAccount skipped: " + e.Message);
            }

            return await FinishAsync();
        }

        public async Task<PlayerProfile> SignInEmailAsync(string email, string password)
        {
            ValidateEmailPassword(email, password);
            await _conn.AuthenticateEmailAsync(email.Trim(), password, false);
            await _conn.LinkDeviceAsync(EnsureDeviceId());
            return await FinishAsync();
        }

        public async Task<PlayerProfile> LinkEmailAsync(string email, string password)
        {
            ValidateEmailPassword(email, password);
            if (_conn.Session == null)
                throw new InvalidOperationException("Sign in first, then link email");
            await _conn.LinkEmailAsync(email.Trim(), password);
            return await FinishAsync();
        }

        public async Task SaveProgressAsync(PlayerProfile profile)
        {
            await NakamaProgress.PushAsync(_conn, profile);
        }

        public void SignOutLocal()
        {
            _conn.ClearStoredSession();
            PlayerPrefs.SetString(KeyDevice, NewDeviceId());
            PlayerPrefs.Save();
        }

        async Task<PlayerProfile> FinishAsync()
        {
            var session = _conn.Session;
            var name = PlayerPrefs.GetString(KeyName, session.Username);
            var username = session.Username ?? "";
            var email = "";
            var provider = "nakama-device";

            try
            {
                var account = await _conn.GetAccountAsync();
                if (account?.User != null)
                {
                    if (!string.IsNullOrEmpty(account.User.DisplayName))
                        name = account.User.DisplayName;
                    else if (!string.IsNullOrEmpty(account.User.Username))
                        name = account.User.Username;
                    if (!string.IsNullOrEmpty(account.User.Username))
                        username = account.User.Username;
                }
                if (!string.IsNullOrEmpty(account?.Email))
                {
                    email = account.Email;
                    provider = "nakama-email";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] GetAccount skipped: " + e.Message);
            }

            PlayerPrefs.SetString(KeyName, name);
            PlayerPrefs.SetString(DevAuthService.KeyName, name);
            PlayerPrefs.Save();

            var profile = new PlayerProfile
            {
                UserId = session.UserId,
                DisplayName = name,
                Username = username,
                Level = 1,
                Essence = 0,
                AuthProvider = provider,
                Email = email
            };

            await NakamaProgress.HydrateAsync(_conn, profile);

            await NakamaSessionClaim.PublishAsync(_conn, EnsureDeviceId());

            try
            {
                var health = await _conn.RpcAsync("ashfold_health");
                Debug.Log("[Ashfold] ashfold_health → " + health);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] health RPC: " + e.Message);
            }

            return profile;
        }

        static string EnsureDeviceId()
        {
            var deviceId = PlayerPrefs.GetString(KeyDevice, string.Empty);
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = NewDeviceId();
                PlayerPrefs.SetString(KeyDevice, deviceId);
                PlayerPrefs.Save();
            }
            return deviceId;
        }

        static string NewDeviceId()
        {
            return "af_" + Guid.NewGuid().ToString("N");
        }

        static string ResolveName(string preferredName)
        {
            var name = string.IsNullOrWhiteSpace(preferredName)
                ? PlayerPrefs.GetString(KeyName, string.Empty)
                : preferredName.Trim();
            if (string.IsNullOrEmpty(name))
                name = "Warrior_" + UnityEngine.Random.Range(1000, 9999);
            return name;
        }

        static void ValidateEmailPassword(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') < 1)
                throw new ArgumentException("Enter a valid email");
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters");
        }

        static string SanitizeUsername(string name)
        {
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
                    chars[i] = '_';
            }
            var u = new string(chars);
            if (u.Length < 1)
                u = "player";
            if (u.Length > 128)
                u = u.Substring(0, 128);
            return u;
        }
    }
}
