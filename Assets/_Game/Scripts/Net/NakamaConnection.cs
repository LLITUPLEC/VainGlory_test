using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Держит Client + Session (DDOL через GameSession).</summary>
    public sealed class NakamaConnection
    {
        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        public bool IsConnected => Session != null && !Session.IsExpired;

        public void EnsureClient()
        {
            if (Client != null)
                return;

            // Как в доке Nakama Unity: UnityWebRequest надёжнее HttpClient в Editor/Player.
            Client = new Client(
                NakamaConfig.Scheme,
                NakamaConfig.Host,
                NakamaConfig.Port,
                NakamaConfig.ServerKey,
                UnityWebRequestAdapter.Instance)
            {
                Timeout = NakamaConfig.TimeoutSeconds
            };
            Debug.Log($"[Ashfold] Nakama client → {NakamaConfig.Scheme}://{NakamaConfig.Host}:{NakamaConfig.Port}");
        }

        public async Task<ISession> AuthenticateDeviceAsync(string deviceId, string username)
        {
            EnsureClient();
            try
            {
                Session = await Client.AuthenticateDeviceAsync(deviceId, username, true);
            }
            catch (Exception e)
            {
                var root = e;
                while (root.InnerException != null)
                    root = root.InnerException;
                Debug.LogError(
                    $"[Ashfold] Auth transport error: {root.GetType().Name}: {root.Message}\n" +
                    $"URL={NakamaConfig.Scheme}://{NakamaConfig.Host}:{NakamaConfig.Port}  " +
                    $"(часто = порт 443/прокси недоступен, не ServerKey)");
                throw;
            }

            Debug.Log($"[Ashfold] Nakama authenticated userId={Session.UserId} username={Session.Username}");
            return Session;
        }

        public async Task<string> RpcAsync(string id, string payload = "{}")
        {
            if (!IsConnected)
                throw new InvalidOperationException("Nakama session missing");
            var result = await Client.RpcAsync(Session, id, payload);
            return result.Payload;
        }

        public void ClearSession()
        {
            Session = null;
        }
    }
}
