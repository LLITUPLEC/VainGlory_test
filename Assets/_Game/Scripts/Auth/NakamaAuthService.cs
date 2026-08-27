using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Device auth через Nakama (этап 1.3). Fallback имени — как у Dev.</summary>
    public sealed class NakamaAuthService : IAuthService
    {
        public const string KeyDevice = "ashfold.nakama.deviceId";
        public const string KeyName = "ashfold.displayName";

        readonly NakamaConnection _conn;

        public NakamaAuthService(NakamaConnection conn)
        {
            _conn = conn;
        }

        public async Task<PlayerProfile> SignInGuestAsync(string preferredName)
        {
            var deviceId = PlayerPrefs.GetString(KeyDevice, string.Empty);
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = "af_" + SystemInfo.deviceUniqueIdentifier;
                if (deviceId.Length > 128)
                    deviceId = deviceId.Substring(0, 128);
                PlayerPrefs.SetString(KeyDevice, deviceId);
            }

            var name = string.IsNullOrWhiteSpace(preferredName)
                ? PlayerPrefs.GetString(KeyName, string.Empty)
                : preferredName.Trim();
            if (string.IsNullOrEmpty(name))
                name = "Warrior_" + UnityEngine.Random.Range(1000, 9999);

            // username в Nakama: [a-zA-Z0-9._]{1,128}
            var username = SanitizeUsername(name);

            try
            {
                var session = await _conn.AuthenticateDeviceAsync(deviceId, username);
                try
                {
                    await _conn.Client.UpdateAccountAsync(session, username, name, null, null, null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ashfold] UpdateAccount skipped: " + e.Message);
                }

                PlayerPrefs.SetString(KeyName, name);
                PlayerPrefs.SetString(DevAuthService.KeyName, name);
                PlayerPrefs.Save();

                string health = null;
                try
                {
                    health = await _conn.RpcAsync("ashfold_health");
                    Debug.Log("[Ashfold] ashfold_health → " + health);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ashfold] health RPC (модуль ещё не задеплоен?): " + e.Message);
                }

                return new PlayerProfile
                {
                    UserId = session.UserId,
                    DisplayName = name,
                    Level = 1,
                    Essence = 0,
                    AuthProvider = "nakama-device"
                };
            }
            catch (Exception e)
            {
                Debug.LogError("[Ashfold] Nakama auth failed: " + e.Message);
                throw;
            }
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
