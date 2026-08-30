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
        public string MatchId { get; private set; }
        public string MatchmakerTicket { get; private set; }
        public string PartyMatchmakerTicket { get; private set; }
        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        public bool IsConnected => Session != null && !Session.HasExpired(DateTime.UtcNow);

        Task _ensureTask;
        bool _refreshWarned;

        public const long OpRoster = 11;
        public const long OpSnapshot = 20;
        public const long OpInputMove = 30;
        public const long OpInputSkill = 31;
        public const long OpDraftPick = 32;
        public const long OpDraftLock = 33;
        public const long OpInputAttack = 34;
        public const long OpInputRecall = 36;
        public const long OpInputBuy = 37;
        public const long OpSurrender = 40;
        public const long OpMapPing = 50;

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
                UnityWebRequestAdapter.Instance,
                false)
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
            _refreshWarned = false;
            _ensureTask = null;
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
            _refreshWarned = false;
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
            _refreshWarned = false;
            Debug.Log($"[Ashfold] Email auth userId={Session.UserId} create={create}");
            return Session;
        }

        public async Task<bool> EnsureSessionAsync()
        {
            if (Session == null)
                return false;
            EnsureClient();
            Task waiter;
            lock (this)
            {
                if (_ensureTask != null && !_ensureTask.IsCompleted)
                    waiter = _ensureTask;
                else
                {
                    _ensureTask = EnsureSessionCoreAsync();
                    waiter = _ensureTask;
                }
            }

            try
            {
                await waiter;
            }
            catch (Exception e)
            {
                WarnRefreshOnce(e.Message);
                return false;
            }

            return Session != null && !Session.HasExpired(DateTime.UtcNow);
        }

        async Task EnsureSessionCoreAsync()
        {
            if (Session == null)
                return;
            if (!Session.HasExpired(DateTime.UtcNow.AddMinutes(6)))
                return;
            if (string.IsNullOrEmpty(Session.RefreshToken) || RefreshTokenExpired(Session))
            {
                WarnRefreshOnce("Refresh token invalid or expired.");
                return;
            }

            Session = await Client.SessionRefreshAsync(Session);
            PersistSession();
            _refreshWarned = false;
            Debug.Log("[Ashfold] Nakama session refreshed");
        }

        static bool RefreshTokenExpired(ISession session)
        {
            if (session == null || session.RefreshExpireTime <= 0)
                return false;
            try
            {
                var exp = DateTimeOffset.FromUnixTimeSeconds(session.RefreshExpireTime).UtcDateTime;
                return DateTime.UtcNow >= exp.AddMinutes(-1);
            }
            catch
            {
                return false;
            }
        }

        void WarnRefreshOnce(string message)
        {
            if (_refreshWarned)
                return;
            _refreshWarned = true;
            Debug.LogWarning("[Ashfold] Session refresh: " + message);
        }

        public async Task LinkEmailAsync(string email, string password)
        {
            if (!await EnsureSessionAsync())
                throw new InvalidOperationException("Nakama session missing");
            await Client.LinkEmailAsync(Session, email, password);
        }

        public async Task LinkDeviceAsync(string deviceId)
        {
            if (!await EnsureSessionAsync())
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
            if (!await EnsureSessionAsync())
                throw new InvalidOperationException("Nakama session missing");
            return await Client.GetAccountAsync(Session);
        }

        public async Task<string> RpcAsync(string id, string payload = "{}")
        {
            if (!await EnsureSessionAsync())
                throw new InvalidOperationException("Nakama session missing");
            var result = await Client.RpcAsync(Session, id, payload);
            return result.Payload;
        }

        public async Task ConnectRealtimeAsync()
        {
            EnsureClient();
            if (!await EnsureSessionAsync())
                throw new InvalidOperationException("Nakama session missing");
            if (Socket != null && Socket.IsConnected)
            {
                BindRealtime();
                return;
            }
            await CloseSocketKeepMatchAsync();
            Socket = Client.NewSocket();
            await Socket.ConnectAsync(Session, true);
            BindRealtime();
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
            if (GameSession.I != null && GameSession.I.Social != null && GameSession.I.Social.InParty)
                meta["party"] = GameSession.I.Social.PartyId;
            if (!string.IsNullOrEmpty(matched.MatchId))
                CurrentMatch = await Socket.JoinMatchAsync(matched.MatchId, meta);
            else
                CurrentMatch = await Socket.JoinMatchAsync(matched);
            MatchmakerTicket = null;
            PartyMatchmakerTicket = null;
            Debug.Log("[Ashfold] Joined match " + CurrentMatch.Id);
            MatchId = CurrentMatch.Id;
        }

        public async Task JoinMatchByIdAsync(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
                throw new InvalidOperationException("Match id missing");
            if (Socket == null || !Socket.IsConnected)
                await ConnectRealtimeAsync();
            var name = GameSession.I != null && GameSession.I.Profile != null
                ? GameSession.I.Profile.DisplayName
                : "Player";
            var meta = new Dictionary<string, string> { { "name", name } };
            if (GameSession.I != null && GameSession.I.Social != null && GameSession.I.Social.InParty)
                meta["party"] = GameSession.I.Social.PartyId;
            CurrentMatch = await Socket.JoinMatchAsync(matchId, meta);
            MatchId = CurrentMatch.Id;
            Debug.Log("[Ashfold] Rejoined match " + CurrentMatch.Id);
        }

        public async Task AddMatchmakerPartyAsync(string partyId, int partySize)
        {
            if (Socket == null || !Socket.IsConnected)
                await ConnectRealtimeAsync();
            var name = GameSession.I != null && GameSession.I.Profile != null
                ? GameSession.I.Profile.DisplayName
                : "Player";
            var n = Mathf.Clamp(partySize, 2, NakamaSocial.PartyMax);
            var ticket = await Socket.AddMatchmakerPartyAsync(
                partyId,
                "properties.mode:casual_3v3",
                n,
                n,
                new Dictionary<string, string>
                {
                    { "mode", "casual_3v3" },
                    { "name", name }
                });
            PartyMatchmakerTicket = ticket.Ticket;
            MatchmakerTicket = null;
            Debug.Log("[Ashfold] Party matchmaker ticket " + PartyMatchmakerTicket + " size=" + n);
        }

        public async Task CancelMatchmakerAsync()
        {
            var socket = Socket;
            var ticket = MatchmakerTicket;
            var partyTicket = PartyMatchmakerTicket;
            var partyId = GameSession.I != null && GameSession.I.Social != null
                ? GameSession.I.Social.PartyId
                : "";
            MatchmakerTicket = null;
            PartyMatchmakerTicket = null;
            if (socket == null || !socket.IsConnected)
                return;
            try
            {
                if (!string.IsNullOrEmpty(partyTicket) && !string.IsNullOrEmpty(partyId))
                    await socket.RemoveMatchmakerPartyAsync(partyId, partyTicket);
                else if (!string.IsNullOrEmpty(ticket))
                    await socket.RemoveMatchmakerAsync(ticket);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Cancel matchmaker: " + e.Message);
            }
        }

        public async Task LeaveMatchKeepSocketAsync()
        {
            if (GameSession.I != null && GameSession.I.MatchClient != null)
                GameSession.I.MatchClient.Detach();
            var match = CurrentMatch;
            var matchId = match != null ? match.Id : MatchId;
            var socket = Socket;
            CurrentMatch = null;
            MatchId = null;
            await CancelMatchmakerAsync();
            if (socket == null || !socket.IsConnected || string.IsNullOrEmpty(matchId))
                return;
            try
            {
                await socket.LeaveMatchAsync(matchId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Leave match keep socket: " + e.Message);
            }
        }

        public async Task CloseSocketKeepMatchAsync()
        {
            UnbindSocial();
            if (GameSession.I != null && GameSession.I.MatchClient != null)
                GameSession.I.MatchClient.Detach();
            var socket = Socket;
            Socket = null;
            if (socket == null)
                return;
            try
            {
                if (socket.IsConnected)
                    await socket.CloseAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Socket drop: " + e.Message);
            }
        }

        public async Task DisconnectRealtimeAsync()
        {
            UnbindSocial();
            if (GameSession.I != null && GameSession.I.MatchClient != null)
                GameSession.I.MatchClient.Detach();
            var ticket = MatchmakerTicket;
            var partyTicket = PartyMatchmakerTicket;
            var partyId = GameSession.I != null && GameSession.I.Social != null
                ? GameSession.I.Social.PartyId
                : "";
            var match = CurrentMatch;
            var matchId = match != null ? match.Id : MatchId;
            var socket = Socket;
            MatchmakerTicket = null;
            PartyMatchmakerTicket = null;
            CurrentMatch = null;
            MatchId = null;
            Socket = null;
            if (socket == null)
                return;
            try
            {
                if (socket.IsConnected)
                {
                    if (!string.IsNullOrEmpty(partyTicket) && !string.IsNullOrEmpty(partyId))
                        await socket.RemoveMatchmakerPartyAsync(partyId, partyTicket);
                    else if (!string.IsNullOrEmpty(ticket))
                        await socket.RemoveMatchmakerAsync(ticket);
                    if (!string.IsNullOrEmpty(matchId))
                        await socket.LeaveMatchAsync(matchId);
                    await socket.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Realtime close: " + e.Message);
            }
        }

        void BindRealtime()
        {
            if (GameSession.I != null && GameSession.I.MatchClient != null)
                GameSession.I.MatchClient.Attach(Socket);
            if (GameSession.I != null && GameSession.I.Social != null)
                GameSession.I.Social.Bind(Socket);
        }

        static void UnbindSocial()
        {
            if (GameSession.I != null && GameSession.I.Social != null)
                GameSession.I.Social.Unbind();
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
