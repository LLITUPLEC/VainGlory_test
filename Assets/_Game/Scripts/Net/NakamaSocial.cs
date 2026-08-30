using System;
using System.Collections.Generic;
using System.Text;
using Nakama;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Друзья, пати и чат пати. Сокет Hall/мета, не боевой match loop.</summary>
    public sealed class NakamaSocial : MonoBehaviour
    {
        public const long OpChat = 1;
        public const long OpQueue = 2;
        public const int PartyMax = 3;
        const int NotifyFriendRequest = -2;
        const int NotifyFriendAccept = -3;

        readonly object _gate = new object();
        readonly List<IApiFriend> _friends = new List<IApiFriend>(16);
        readonly List<string> _chat = new List<string>(24);
        ISocket _socket;
        IParty _party;
        string _pendingInvite;
        string _leaderId;
        bool _dirty;
        IMatchmakerMatched _queuedMatch;
        int _queueCue;
        string _status = "";

        public event Action Changed;
        public event Action<IMatchmakerMatched> Matched;
        public event Action QueueStarted;
        public event Action QueueStopped;

        public IReadOnlyList<IApiFriend> Friends
        {
            get { lock (_gate) return _friends.ToArray(); }
        }

        public IReadOnlyList<string> ChatLines
        {
            get { lock (_gate) return _chat.ToArray(); }
        }

        public string PartyId
        {
            get { lock (_gate) return _party != null ? _party.Id : ""; }
        }

        public bool InParty
        {
            get { lock (_gate) return _party != null; }
        }

        public bool IsLeader
        {
            get
            {
                var me = LocalId();
                lock (_gate)
                    return !string.IsNullOrEmpty(_leaderId) && _leaderId == me;
            }
        }

        public string LeaderId
        {
            get { lock (_gate) return _leaderId ?? ""; }
        }

        public int PartySize
        {
            get
            {
                lock (_gate)
                {
                    if (_party == null)
                        return 0;
                    var n = 0;
                    if (_party.Presences != null)
                    {
                        foreach (var _ in _party.Presences)
                            n++;
                    }
                    return Mathf.Max(1, n);
                }
            }
        }

        public string PendingInvite
        {
            get { lock (_gate) return _pendingInvite ?? ""; }
        }

        public string Status
        {
            get { lock (_gate) return _status ?? ""; }
        }

        public int IncomingFriendCount
        {
            get
            {
                lock (_gate)
                {
                    var n = 0;
                    for (var i = 0; i < _friends.Count; i++)
                    {
                        if (_friends[i] != null && _friends[i].State == 2)
                            n++;
                    }
                    return n;
                }
            }
        }

        public void Bind(ISocket socket)
        {
            Unbind();
            _socket = socket;
            if (_socket == null)
                return;
            _socket.ReceivedParty += OnParty;
            _socket.ReceivedPartyPresence += OnPartyPresence;
            _socket.ReceivedPartyData += OnPartyData;
            _socket.ReceivedPartyClose += OnPartyClose;
            _socket.ReceivedPartyLeader += OnPartyLeader;
            _socket.ReceivedChannelMessage += OnChannelMessage;
            _socket.ReceivedMatchmakerMatched += OnMatched;
            _socket.ReceivedNotification += OnNotification;
            var _ = RefreshFriendsAsync();
        }

        public void Unbind()
        {
            if (_socket == null)
                return;
            _socket.ReceivedParty -= OnParty;
            _socket.ReceivedPartyPresence -= OnPartyPresence;
            _socket.ReceivedPartyData -= OnPartyData;
            _socket.ReceivedPartyClose -= OnPartyClose;
            _socket.ReceivedPartyLeader -= OnPartyLeader;
            _socket.ReceivedChannelMessage -= OnChannelMessage;
            _socket.ReceivedMatchmakerMatched -= OnMatched;
            _socket.ReceivedNotification -= OnNotification;
            _socket = null;
        }

        public void ResetLocal()
        {
            Unbind();
            lock (_gate)
            {
                _friends.Clear();
                _chat.Clear();
                _party = null;
                _pendingInvite = "";
                _leaderId = "";
                _status = "";
                _queuedMatch = null;
                _queueCue = 0;
                _dirty = true;
            }
        }

        public async System.Threading.Tasks.Task RefreshFriendsAsync()
        {
            var nk = Conn();
            if (nk == null || nk.Session == null)
                return;
            if (!await nk.EnsureSessionAsync())
                return;
            try
            {
                var list = await nk.Client.ListFriendsAsync(nk.Session, null, 50);
                lock (_gate)
                {
                    _friends.Clear();
                    if (list != null && list.Friends != null)
                    {
                        foreach (var f in list.Friends)
                            _friends.Add(f);
                    }
                    _status = "";
                }
                Fire();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task AddByUsernameAsync(string username)
        {
            var nk = Conn();
            if (nk == null || string.IsNullOrWhiteSpace(username))
                return;
            var query = username.Trim();
            try
            {
                if (!await nk.EnsureSessionAsync())
                    return;
                var userId = await ResolveUserIdAsync(nk, query);
                if (string.IsNullOrEmpty(userId))
                {
                    SetStatus(Loc.T("social.not_found"));
                    return;
                }
                if (userId == LocalId())
                {
                    SetStatus(Loc.T("social.self"));
                    return;
                }
                await nk.Client.AddFriendsAsync(nk.Session, new[] { userId }, null);
                await PingFriendAsync(userId);
                await RefreshFriendsAsync();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task AcceptAsync(string userId)
        {
            var nk = Conn();
            if (nk == null || string.IsNullOrEmpty(userId))
                return;
            try
            {
                if (!await nk.EnsureSessionAsync())
                    return;
                await nk.Client.AddFriendsAsync(nk.Session, new[] { userId }, null);
                await PingFriendAsync(userId);
                await RefreshFriendsAsync();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task RemoveAsync(string userId)
        {
            var nk = Conn();
            if (nk == null || string.IsNullOrEmpty(userId))
                return;
            try
            {
                if (!await nk.EnsureSessionAsync())
                    return;
                await nk.Client.DeleteFriendsAsync(nk.Session, new[] { userId }, null);
                await RefreshFriendsAsync();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task CreatePartyAsync()
        {
            var socket = Sock();
            if (socket == null)
                return;
            try
            {
                if (InParty)
                    await LeavePartyAsync();
                var party = await socket.CreatePartyAsync(true, PartyMax);
                lock (_gate)
                {
                    _party = party;
                    CaptureLeader();
                    _status = "";
                }
                Fire();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task JoinPartyAsync(string partyId)
        {
            var socket = Sock();
            if (socket == null || string.IsNullOrWhiteSpace(partyId))
                return;
            try
            {
                if (InParty)
                    await LeavePartyAsync();
                await socket.JoinPartyAsync(partyId.Trim());
                lock (_gate)
                    _pendingInvite = "";
                Fire();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task LeavePartyAsync()
        {
            var socket = Sock();
            string id;
            lock (_gate)
                id = _party != null ? _party.Id : "";
            if (socket != null && !string.IsNullOrEmpty(id))
            {
                try
                {
                    await socket.LeavePartyAsync(id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ashfold] Leave party: " + e.Message);
                }
            }
            lock (_gate)
            {
                _party = null;
                _leaderId = "";
                _chat.Clear();
            }
            Fire();
        }

        public async System.Threading.Tasks.Task KickAsync(IUserPresence member)
        {
            var socket = Sock();
            if (socket == null || member == null || !IsLeader)
                return;
            try
            {
                await socket.RemovePartyMemberAsync(PartyId, member);
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task SendChatAsync(string text)
        {
            var socket = Sock();
            if (socket == null || !InParty || string.IsNullOrWhiteSpace(text))
                return;
            var dto = new PartyChatDto
            {
                n = LocalName(),
                m = text.Trim()
            };
            try
            {
                await socket.SendPartyDataAsync(PartyId, OpChat, JsonUtility.ToJson(dto));
                PushChat(dto.n, dto.m);
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        public async System.Threading.Tasks.Task NotifyQueueAsync(bool searching)
        {
            var socket = Sock();
            if (socket == null || !InParty)
                return;
            try
            {
                var json = JsonUtility.ToJson(new QueueDto { s = searching ? 1 : 0 });
                await socket.SendPartyDataAsync(PartyId, OpQueue, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Party queue cue: " + e.Message);
            }
        }

        public async System.Threading.Tasks.Task InviteFriendAsync(string userId)
        {
            var socket = Sock();
            if (socket == null || string.IsNullOrEmpty(userId) || !InParty)
                return;
            try
            {
                var channel = await socket.JoinChatAsync(userId, ChannelType.DirectMessage, false, true);
                var json = JsonUtility.ToJson(new PartyInviteDto { partyId = PartyId });
                await socket.WriteChatMessageAsync(channel, json);
                lock (_gate)
                    _status = "";
                Fire();
            }
            catch (Exception e)
            {
                SetStatus(e.Message);
            }
        }

        async System.Threading.Tasks.Task PingFriendAsync(string userId)
        {
            var socket = Sock();
            if (socket == null || string.IsNullOrEmpty(userId))
                return;
            try
            {
                var channel = await socket.JoinChatAsync(userId, ChannelType.DirectMessage, false, true);
                await socket.WriteChatMessageAsync(channel, JsonUtility.ToJson(new FriendPingDto { friendReq = 1 }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Friend ping: " + e.Message);
            }
        }

        public void DismissInvite()
        {
            lock (_gate)
                _pendingInvite = "";
            Fire();
        }

        public IUserPresence[] MembersSnapshot()
        {
            lock (_gate)
            {
                if (_party == null || _party.Presences == null)
                    return new IUserPresence[0];
                var list = new List<IUserPresence>(PartyMax);
                foreach (var p in _party.Presences)
                    list.Add(p);
                return list.ToArray();
            }
        }

        public static string FriendLabel(IApiFriend f)
        {
            if (f == null || f.User == null)
                return "?";
            var name = !string.IsNullOrEmpty(f.User.DisplayName) ? f.User.DisplayName : f.User.Username;
            var online = f.User.Online ? "● " : "○ ";
            return online + name;
        }

        public static string FriendStateKey(IApiFriend f)
        {
            if (f == null)
                return "";
            if (f.State == 1)
                return "social.sent";
            if (f.State == 2)
                return "social.recv";
            if (f.State == 3)
                return "social.blocked";
            return "";
        }

        void Update()
        {
            IMatchmakerMatched matched = null;
            var cue = 0;
            var dirty = false;
            lock (_gate)
            {
                dirty = _dirty;
                _dirty = false;
                matched = _queuedMatch;
                _queuedMatch = null;
                cue = _queueCue;
                _queueCue = 0;
            }
            if (matched != null && !MatchPrepFlow.Queuing)
                Matched?.Invoke(matched);
            if (cue > 0)
                QueueStarted?.Invoke();
            if (cue < 0)
                QueueStopped?.Invoke();
            if (dirty)
                Changed?.Invoke();
        }

        void OnParty(IParty party)
        {
            lock (_gate)
            {
                _party = party;
                CaptureLeader();
            }
            Fire();
        }

        void OnPartyPresence(IPartyPresenceEvent ev)
        {
            lock (_gate)
            {
                if (_party != null && ev != null)
                    _party.UpdatePresences(ev);
            }
            Fire();
        }

        void OnPartyLeader(IPartyLeader leader)
        {
            lock (_gate)
            {
                if (leader != null && leader.Presence != null)
                    _leaderId = leader.Presence.UserId;
            }
            Fire();
        }

        void OnPartyClose(IPartyClose close)
        {
            lock (_gate)
            {
                _party = null;
                _leaderId = "";
                _chat.Clear();
            }
            Fire();
        }

        void OnPartyData(IPartyData data)
        {
            if (data == null || data.Data == null)
                return;
            string json;
            try
            {
                json = Encoding.UTF8.GetString(data.Data);
            }
            catch
            {
                return;
            }

            if (data.OpCode == OpQueue)
            {
                if (data.Presence != null && data.Presence.UserId == LocalId())
                    return;
                var cue = JsonUtility.FromJson<QueueDto>(json);
                lock (_gate)
                    _queueCue = cue != null && cue.s > 0 ? 1 : -1;
                return;
            }

            if (data.OpCode != OpChat)
                return;
            var dto = JsonUtility.FromJson<PartyChatDto>(json);
            if (dto == null || string.IsNullOrEmpty(dto.m))
                return;
            var name = !string.IsNullOrEmpty(dto.n) ? dto.n : (data.Presence != null ? data.Presence.Username : "?");
            if (data.Presence != null && data.Presence.UserId == LocalId())
                return;
            PushChat(name, dto.m);
        }

        void OnChannelMessage(IApiChannelMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.Content))
                return;
            if (msg.SenderId == LocalId())
                return;

            FriendPingDto ping = null;
            try
            {
                ping = JsonUtility.FromJson<FriendPingDto>(msg.Content);
            }
            catch
            {
                ping = null;
            }
            if (ping != null && ping.friendReq > 0)
            {
                var _ = RefreshFriendsAsync();
                return;
            }

            PartyInviteDto invite = null;
            try
            {
                invite = JsonUtility.FromJson<PartyInviteDto>(msg.Content);
            }
            catch
            {
                return;
            }
            if (invite == null || string.IsNullOrEmpty(invite.partyId))
                return;
            lock (_gate)
                _pendingInvite = invite.partyId;
            Fire();
        }

        void OnMatched(IMatchmakerMatched matched)
        {
            lock (_gate)
                _queuedMatch = matched;
        }

        void OnNotification(IApiNotification notification)
        {
            if (notification == null)
                return;
            if (notification.Code != NotifyFriendRequest && notification.Code != NotifyFriendAccept)
                return;
            var _ = RefreshFriendsAsync();
        }

        async System.Threading.Tasks.Task<string> ResolveUserIdAsync(NakamaConnection nk, string username)
        {
            try
            {
                var users = await nk.Client.GetUsersAsync(nk.Session, new string[0], new[] { username });
                var id = FirstUserId(users);
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] GetUsers: " + e.Message);
            }

            try
            {
                var payload = await nk.RpcAsync("ashfold_find_user", JsonUtility.ToJson(new FindUserDto { username = username }));
                var found = JsonUtility.FromJson<FoundUserDto>(payload);
                if (found != null && !string.IsNullOrEmpty(found.id))
                    return found.id;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Find user RPC: " + e.Message);
            }

            return "";
        }

        static string FirstUserId(IApiUsers users)
        {
            if (users == null || users.Users == null)
                return "";
            foreach (var user in users.Users)
            {
                if (user != null && !string.IsNullOrEmpty(user.Id))
                    return user.Id;
            }
            return "";
        }

        void PushChat(string name, string text)
        {
            lock (_gate)
            {
                _chat.Add(name + ": " + text);
                while (_chat.Count > 20)
                    _chat.RemoveAt(0);
            }
            Fire();
        }

        void CaptureLeader()
        {
            if (_party != null && _party.Leader != null)
                _leaderId = _party.Leader.UserId;
        }

        void SetStatus(string msg)
        {
            lock (_gate)
                _status = msg ?? "";
            Fire();
        }

        void Fire()
        {
            lock (_gate)
                _dirty = true;
        }

        ISocket Sock()
        {
            var nk = Conn();
            return nk != null ? nk.Socket : _socket;
        }

        static NakamaConnection Conn()
        {
            return GameSession.I != null ? GameSession.I.Nakama : null;
        }

        static string LocalId()
        {
            if (GameSession.I != null && GameSession.I.Nakama != null && GameSession.I.Nakama.Session != null)
                return GameSession.I.Nakama.Session.UserId;
            return "";
        }

        static string LocalName()
        {
            if (GameSession.I != null && GameSession.I.Profile != null)
                return GameSession.I.Profile.DisplayName;
            return "Player";
        }

        void OnDestroy()
        {
            Unbind();
        }

        [Serializable]
        sealed class PartyChatDto
        {
            public string n;
            public string m;
        }

        [Serializable]
        sealed class PartyInviteDto
        {
            public string partyId;
        }

        [Serializable]
        sealed class FriendPingDto
        {
            public int friendReq;
        }

        [Serializable]
        sealed class QueueDto
        {
            public int s;
        }

        [Serializable]
        sealed class FindUserDto
        {
            public string username;
        }

        [Serializable]
        sealed class FoundUserDto
        {
            public string id;
            public string username;
        }
    }
}
