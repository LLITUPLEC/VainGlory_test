using System;
using Nakama;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Слушает комнату матча (DDOL на GameSession). Не отключать до Results.</summary>
    public sealed class NakamaMatchClient : MonoBehaviour
    {
        readonly object _gate = new object();
        ISocket _socket;

        NakamaRosterDto _roster;
        NetSnapshotDto _snapshot;
        string _phase = "waiting";
        float _draftLeft;
        long _lastHitTick = -1;
        int _outSeq;
        readonly System.Collections.Generic.List<NetPingDto> _pings = new System.Collections.Generic.List<NetPingDto>(4);

        public NakamaRosterDto Roster
        {
            get { lock (_gate) return _roster; }
        }

        public NetSnapshotDto Snapshot
        {
            get { lock (_gate) return _snapshot; }
        }

        public string Phase
        {
            get { lock (_gate) return _phase ?? "waiting"; }
        }

        public float DraftLeft
        {
            get { lock (_gate) return _draftLeft; }
        }

        public bool HasSnapshot
        {
            get { lock (_gate) return _snapshot != null; }
        }

        public void Attach(ISocket socket)
        {
            Detach();
            _socket = socket;
            if (_socket == null)
                return;
            _socket.ReceivedMatchState += OnMatchState;
        }

        public void Detach()
        {
            if (_socket != null)
            {
                _socket.ReceivedMatchState -= OnMatchState;
                _socket = null;
            }
            lock (_gate)
            {
                _roster = null;
                _snapshot = null;
                _phase = "waiting";
                _draftLeft = 0f;
                _lastHitTick = -1;
                _pings.Clear();
            }
        }

        public bool ConsumeHits(out NetHitDto[] hits)
        {
            lock (_gate)
            {
                hits = null;
                if (_snapshot == null || _snapshot.hits == null || _snapshot.hits.Length == 0)
                    return false;
                if (_snapshot.tick == _lastHitTick)
                    return false;
                _lastHitTick = _snapshot.tick;
                hits = _snapshot.hits;
                return true;
            }
        }

        public void SendPick(string heroId)
        {
            Send(NakamaConnection.OpDraftPick, JsonUtility.ToJson(new NetHeroPickDto { heroId = heroId }));
        }

        public void SendLock(string heroId)
        {
            Send(NakamaConnection.OpDraftLock, JsonUtility.ToJson(new NetHeroPickDto { heroId = heroId }));
        }

        public void SendMove(float x, float z)
        {
            Send(NakamaConnection.OpInputMove, JsonUtility.ToJson(new NetVecDto { x = x, z = z, seq = NextSeq() }));
        }

        public void SendAttack(int targetId)
        {
            Send(NakamaConnection.OpInputAttack, JsonUtility.ToJson(new NetTargetDto { targetId = targetId, seq = NextSeq() }));
        }

        public void SendSkill(float yaw, int slot = 0)
        {
            Send(NakamaConnection.OpInputSkill, JsonUtility.ToJson(new NetSkillDto { yaw = yaw, seq = NextSeq(), slot = slot }));
        }

        public void SendRecall()
        {
            Send(NakamaConnection.OpInputRecall, JsonUtility.ToJson(new NetSeqDto { seq = NextSeq() }));
        }

        public void SendBuy(string itemId)
        {
            Send(NakamaConnection.OpInputBuy, JsonUtility.ToJson(new NetBuyDto { itemId = itemId, seq = NextSeq() }));
        }

        public void SendMapPing(float x, float z)
        {
            Send(NakamaConnection.OpMapPing, JsonUtility.ToJson(new NetPingDto { x = x, z = z }));
        }

        public bool ConsumePings(out NetPingDto[] pings)
        {
            lock (_gate)
            {
                if (_pings.Count == 0)
                {
                    pings = null;
                    return false;
                }
                pings = _pings.ToArray();
                _pings.Clear();
                return true;
            }
        }

        int NextSeq()
        {
            lock (_gate)
            {
                _outSeq++;
                return _outSeq;
            }
        }

        public void SendSurrender()
        {
            Send(NakamaConnection.OpSurrender, "{}");
        }

        public static void Send(long op, string json)
        {
            var nk = GameSession.I != null ? GameSession.I.Nakama : null;
            if (nk == null || nk.Socket == null || !nk.Socket.IsConnected || nk.CurrentMatch == null)
                return;
            var _ = nk.Socket.SendMatchStateAsync(nk.CurrentMatch.Id, op, json);
        }

        void OnDestroy()
        {
            Detach();
        }

        void OnMatchState(IMatchState state)
        {
            if (state == null || state.State == null)
                return;
            string json;
            try
            {
                json = System.Text.Encoding.UTF8.GetString(state.State);
            }
            catch
            {
                return;
            }

            if (state.OpCode == NakamaConnection.OpRoster)
            {
                try
                {
                    var roster = JsonUtility.FromJson<NakamaRosterDto>(json);
                    lock (_gate)
                    {
                        _roster = roster;
                        if (roster != null)
                        {
                            if (!string.IsNullOrEmpty(roster.phase))
                                _phase = roster.phase;
                            _draftLeft = roster.draftLeft;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ashfold] Roster parse: " + e.Message);
                }
                return;
            }

            if (state.OpCode == NakamaConnection.OpSnapshot)
            {
                try
                {
                    var snap = JsonUtility.FromJson<NetSnapshotDto>(json);
                    lock (_gate)
                    {
                        _snapshot = snap;
                        if (snap != null && !string.IsNullOrEmpty(snap.phase))
                            _phase = snap.phase;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ashfold] Snapshot parse: " + e.Message);
                }
                return;
            }

            if (state.OpCode == NakamaConnection.OpMapPing)
            {
                try
                {
                    var ping = JsonUtility.FromJson<NetPingDto>(json);
                    if (ping == null)
                        return;
                    lock (_gate)
                        _pings.Add(ping);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ashfold] Ping parse: " + e.Message);
                }
            }
        }
    }
}
