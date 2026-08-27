using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Держит Client + Session (DDOL через GameSession).</summary>
    public sealed class NakamaConnection
    {
        public const string KeyAuthToken = "ashfold.nakama.authToken";
        public const string KeyRefreshToken = "ashfold.nakama.refreshToken";
        public const string KeyForceLogin = "ashfold.nakama.forceLogin";

        public ISocket Socket { get; private set; }
        public IMatch CurrentMatch { get; private set; }
        public string MatchmakerTicket { get; private set; }
        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        public bool IsConnected => Session != null && !Session.HasExpired(DateTime.UtcNow);

        public const long OpRoster = 11;

        public static bool HasStoredSession =>
            !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyAuthToken, string.Empty));

        public void EnsureClient()
        {
            if (Client != null)
                return;

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

        public void PersistSession()
        {
            if (Session == null)
                return;
            PlayerPrefs.SetString(KeyAuthToken, Session.AuthToken);
            PlayerPrefs.SetString(KeyRefreshToken, Session.RefreshToken ?? string.Empty);
            PlayerPrefs.DeleteKey(KeyForceLogin);
            PlayerPrefs.Save();
        }

        public void ClearStoredSession()
        {
            PlayerPrefs.DeleteKey(KeyAuthToken);
            PlayerPrefs.DeleteKey(KeyRefreshToken);
            PlayerPrefs.SetInt(KeyForceLogin, 1);
            PlayerPrefs.Save();
            var _ = DisconnectRealtimeAsync();
            Session = null;
        }

        public async Task<ISession> TryRestoreSessionAsync()
        {
            if (PlayerPrefs.GetInt(KeyForceLogin, 0) == 1)
                return null;

            EnsureClient();
            var auth = PlayerPrefs.GetString(KeyAuthToken, string.Empty);
            if (string.IsNullOrEmpty(auth))
                return null;

            try
            {
                var session = Nakama.Session.Restore(auth, PlayerPrefs.GetString(KeyRefreshToken, string.Empty));
                if (session.HasExpired(DateTime.UtcNow.AddMinutes(5)))
                {
                    if (string.IsNullOrEmpty(session.RefreshToken))
                        return null;
                    session = await Client.SessionRefreshAsync(session);
                }

                Session = session;
                PersistSession();
                Debug.Log($"[Ashfold] Session restored userId={Session.UserId}");
                return Session;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Session restore failed: " + e.Message);
                return null;
            }
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
                LogTransport(e);
                throw;
            }

            PersistSession();
            Debug.Log($"[Ashfold] Nakama authenticated userId={Session.UserId} username={Session.Username}");
            return Session;
        }

        public async Task<ISession> AuthenticateEmailAsync(string email, string password, bool create)
        {
            EnsureClient();
            try
            {
                Session = await Client.AuthenticateEmailAsync(email, password, null, create);
            }
            catch (Exception e)
            {
                LogTransport(e);
                throw;
            }

            PersistSession();
            Debug.Log($"[Ashfold] Email auth userId={Session.UserId} create={create}");
            return Session;
        }

        public async Task LinkEmailAsync(string email, string password)
        {
            if (Session == null)
                throw new InvalidOperationException("Nakama session missing");
            await Client.LinkEmailAsync(Session, email, password);
        }

        public async Task LinkDeviceAsync(string deviceId)
        {
            if (Session == null)
                throw new InvalidOperationException("Nakama session missing");
            try
            {
                await Client.LinkDeviceAsync(Session, deviceId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] LinkDevice skipped: " + e.Message);
            }
        }

        public async Task<IApiAccount> GetAccountAsync()
        {
            if (Session == null)
                throw new InvalidOperationException("Nakama session missing");
            return await Client.GetAccountAsync(Session);
        }

        public async Task<string> RpcAsync(string id, string payload = "{}")
        {
            if (!IsConnected)
                throw new InvalidOperationException("Nakama session missing");
            var result = await Client.RpcAsync(Session, id, payload);
            return result.Payload;
        }

        public async Task ConnectRealtimeAsync()
        {
            EnsureClient();
            if (Session == null)
                throw new InvalidOperationException("Nakama session missing");
            if (Socket != null && Socket.IsConnected)
                return;
            await DisconnectRealtimeAsync();
            Socket = Client.NewSocket();
            await Socket.ConnectAsync(Session, true);
            Debug.Log("[Ashfold] Realtime socket connected");
        }

        public async Task AddMatchmakerAsync()
        {
            if (Socket == null || !Socket.IsConnected)
                await ConnectRealtimeAsync();
            var name = GameSession.I != null && GameSession.I.Profile != null
                ? GameSession.I.Profile.DisplayName
                : "Player";
            var ticket = await Socket.AddMatchmakerAsync(
                "properties.mode:casual_3v3",
                2,
                2,
                new Dictionary<string, string>
                {
                    { "mode", "casual_3v3" },
                    { "name", name }
                });
            MatchmakerTicket = ticket.Ticket;
            Debug.Log("[Ashfold] Matchmaker ticket " + MatchmakerTicket);
        }

        public async Task JoinMatchedAsync(IMatchmakerMatched matched)
        {
            if (Socket == null || !Socket.IsConnected)
                throw new InvalidOperationException("Socket not connected");
            var name = GameSession.I != null && GameSession.I.Profile != null
                ? GameSession.I.Profile.DisplayName
                : "Player";
            var meta = new Dictionary<string, string> { { "name", name } };
            if (!string.IsNullOrEmpty(matched.MatchId))
                CurrentMatch = await Socket.JoinMatchAsync(matched.MatchId, meta);
            else
                CurrentMatch = await Socket.JoinMatchAsync(matched);
            MatchmakerTicket = null;
            Debug.Log("[Ashfold] Joined match " + CurrentMatch.Id);
        }

        public async Task DisconnectRealtimeAsync()
        {
            var ticket = MatchmakerTicket;
            var match = CurrentMatch;
            var socket = Socket;
            MatchmakerTicket = null;
            CurrentMatch = null;
            Socket = null;
            if (socket == null)
                return;
            try
            {
                if (socket.IsConnected)
                {
                    if (!string.IsNullOrEmpty(ticket))
                        await socket.RemoveMatchmakerAsync(ticket);
                    if (match != null)
                        await socket.LeaveMatchAsync(match.Id);
                    await socket.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Realtime close: " + e.Message);
            }
        }

        public void ClearSession()
        {
            var _ = DisconnectRealtimeAsync();
            Session = null;
        }

        static void LogTransport(Exception e)
        {
            var root = e;
            while (root.InnerException != null)
                root = root.InnerException;
            Debug.LogError(
                $"[Ashfold] Auth transport error: {root.GetType().Name}: {root.Message}\n" +
                $"URL={NakamaConfig.Scheme}://{NakamaConfig.Host}:{NakamaConfig.Port}  " +
                $"(часто = порт 443/прокси недоступен, не ServerKey)");
        }
    }
}
