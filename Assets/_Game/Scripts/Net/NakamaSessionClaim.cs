using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Один активный клиент на аккаунт: новый вход вытесняет старое устройство.</summary>
    public static class NakamaSessionClaim
    {
        public const string Collection = "account";
        public const string Key = "session_claim";
        public const string PrefsKick = "ashfold.kickReason";

        [Serializable]
        public sealed class Blob
        {
            public string deviceId;
            public string at;
        }

        public static async Task PublishAsync(NakamaConnection conn, string deviceId)
        {
            if (conn == null || conn.Session == null || string.IsNullOrEmpty(deviceId))
                return;
            if (!await conn.EnsureSessionAsync())
                return;
            try
            {
                var json = JsonUtility.ToJson(new Blob
                {
                    deviceId = deviceId,
                    at = DateTime.UtcNow.Ticks.ToString()
                });
                await conn.Client.WriteStorageObjectsAsync(conn.Session, new IApiWriteStorageObject[]
                {
                    new WriteStorageObject
                    {
                        Collection = Collection,
                        Key = Key,
                        Value = json,
                        PermissionRead = 1,
                        PermissionWrite = 1
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Session claim write: " + e.Message);
            }
        }

        /// <returns>true if another device took the account.</returns>
        public static async Task<bool> TakenOverAsync(NakamaConnection conn, string deviceId)
        {
            if (conn == null || conn.Session == null || string.IsNullOrEmpty(deviceId))
                return false;
            if (!await conn.EnsureSessionAsync())
                return false;
            try
            {
                var result = await conn.Client.ReadStorageObjectsAsync(conn.Session, new IApiReadStorageObjectId[]
                {
                    new StorageObjectId
                    {
                        Collection = Collection,
                        Key = Key,
                        UserId = conn.Session.UserId
                    }
                });
                if (result?.Objects == null)
                    return false;
                foreach (var obj in result.Objects)
                {
                    if (string.IsNullOrEmpty(obj.Value))
                        continue;
                    var blob = JsonUtility.FromJson<Blob>(obj.Value);
                    if (blob == null || string.IsNullOrEmpty(blob.deviceId))
                        return false;
                    return blob.deviceId != deviceId;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Session claim read: " + e.Message);
            }

            return false;
        }

        public static void MarkKicked()
        {
            PlayerPrefs.SetString(PrefsKick, "other_device");
            PlayerPrefs.Save();
        }

        public static string ConsumeKickMessage()
        {
            var reason = PlayerPrefs.GetString(PrefsKick, string.Empty);
            if (string.IsNullOrEmpty(reason))
                return null;
            PlayerPrefs.DeleteKey(PrefsKick);
            PlayerPrefs.Save();
            return Loc.T("boot.kicked_device");
        }
    }
}
