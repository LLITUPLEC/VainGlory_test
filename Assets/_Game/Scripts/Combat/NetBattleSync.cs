using System.Collections.Generic;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Снапшоты: ремоуты интерполируются, локальный герой предиктится мотором и сверяется с сервером.</summary>
    public sealed class NetBattleSync : MonoBehaviour
    {
        const float InterpDelay = 0.12f;
        const float HardCorrect = 2.8f;
        const float SoftCorrect = 0.85f;

        readonly Dictionary<int, CombatUnit> _units = new Dictionary<int, CombatUnit>(32);
        readonly HashSet<int> _dead = new HashSet<int>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _prune = new List<int>(8);
        readonly List<SnapFrame> _frames = new List<SnapFrame>(12);
        long _lastTick = -1;
        bool _ended;

        struct SnapFrame
        {
            public float Time;
            public Dictionary<int, NetEntityDto> Ents;
        }

        public void Register(int netId, CombatUnit unit)
        {
            if (unit == null)
                return;
            unit.NetId = netId;
            _units[netId] = unit;
        }

        void Update()
        {
            var client = GameSession.I != null ? GameSession.I.MatchClient : null;
            if (client == null)
                return;
            var snap = client.Snapshot;
            if (snap == null || snap.entities == null)
                return;

            if (snap.tick != _lastTick)
            {
                _lastTick = snap.tick;
                PushFrame(snap);
                if (client.ConsumeHits(out var hits))
                    PlayHits(hits);
            }

            _seen.Clear();
            foreach (var e in snap.entities)
            {
                _seen.Add(e.id);
                if (!_units.ContainsKey(e.id) || _units[e.id] == null)
                    SpawnMissing(e);
            }

            _prune.Clear();
            foreach (var kv in _units)
            {
                if (kv.Key >= 100 && !_seen.Contains(kv.Key))
                    _prune.Add(kv.Key);
            }
            for (var i = 0; i < _prune.Count; i++)
            {
                var id = _prune[i];
                if (_units.TryGetValue(id, out var u) && u != null)
                    Destroy(u.gameObject);
                _units.Remove(id);
                _dead.Remove(id);
            }

            foreach (var e in snap.entities)
                ApplyTruth(e);

            InterpolateRemotes();

            if (!_ended && snap.phase == "ended")
            {
                _ended = true;
                var flow = GetComponent<BattleFlow>();
                if (flow != null)
                    flow.FinishFromServer(snap.winnerTeam, snap.surrendered);
            }
        }

        void PushFrame(NetSnapshotDto snap)
        {
            var ents = new Dictionary<int, NetEntityDto>(snap.entities.Length);
            foreach (var e in snap.entities)
                ents[e.id] = e;
            _frames.Add(new SnapFrame { Time = Time.time, Ents = ents });
            while (_frames.Count > 10)
                _frames.RemoveAt(0);
        }

        void ApplyTruth(NetEntityDto e)
        {
            if (!_units.TryGetValue(e.id, out var unit) || unit == null)
                return;

            var prevHp = unit.Hp;
            unit.MaxHp = e.maxHp;
            unit.Hp = Mathf.Max(0f, e.hp);
            if (e.stunLeft > 0.05f)
                unit.StunUntil = Mathf.Max(unit.StunUntil, Time.time + e.stunLeft);

            if (!string.IsNullOrEmpty(e.heroId) && (e.kind == "hero" || string.IsNullOrEmpty(e.kind)))
                unit.DisplayName = GameContent.GetHero(e.heroId).DisplayName;

            if (BattleRuntime.I != null && GameSession.I.MatchClient.Snapshot != null)
                BattleRuntime.I.MatchTime = GameSession.I.MatchClient.Snapshot.matchTime;

            if (MatchStatsTracker.I != null && MatchStatsTracker.I.ByUnit.TryGetValue(unit, out var row))
            {
                row.Kills = e.kills;
                row.Deaths = e.deaths;
                row.Gold = e.gold;
            }

            if (unit.IsPlayer && BattleRuntime.I != null)
            {
                BattleRuntime.I.Gold = e.gold;
                BattleRuntime.I.Kills = e.kills;
                BattleRuntime.I.Deaths = e.deaths;
            }

            var combat = unit.GetComponent<HeroCombat>();
            var snapPos = new Vector3(e.x, unit.GroundY, e.z);
            if (unit.IsPlayer && combat != null && e.hp < prevHp - 0.4f)
                combat.CancelRecall();

            if (!e.alive)
            {
                unit.Hp = 0f;
                if (_dead.Add(e.id))
                {
                    if (combat != null)
                        combat.BeginDeathLock();
                    else
                        SetRenderers(unit, false);
                }
                if (unit.IsPlayer)
                {
                    var flow = GetComponent<BattleFlow>();
                    if (flow != null)
                        flow.ShowNetDeath(e.respawn);
                }
                return;
            }

            if (_dead.Remove(e.id))
            {
                if (combat != null)
                    combat.ReviveAt(snapPos);
                else
                {
                    SetRenderers(unit, true);
                    unit.transform.position = snapPos;
                    unit.Hp = Mathf.Max(1f, e.hp);
                }
                if (unit.IsPlayer)
                {
                    var flow = GetComponent<BattleFlow>();
                    if (flow != null)
                        flow.ClearNetDeath();
                }
            }

            if (unit.IsPlayer)
                ReconcileLocal(unit, snapPos);
        }

        static void SetRenderers(CombatUnit unit, bool on)
        {
            foreach (var r in unit.GetComponentsInChildren<Renderer>(true))
                r.enabled = on;
            var col = unit.GetComponent<Collider>();
            if (col != null)
                col.enabled = on;
        }

        void ReconcileLocal(CombatUnit unit, Vector3 serverPos)
        {
            var motor = unit.GetComponent<TapMoveMotor>();
            var delta = unit.transform.position - serverPos;
            delta.y = 0f;
            var err = delta.magnitude;
            if (err < SoftCorrect)
                return;

            if (motor != null && motor.HasOrder && AlongPath(serverPos, unit.transform.position, motor.Destination))
            {
                if (err > HardCorrect)
                    unit.transform.position = Vector3.Lerp(unit.transform.position, serverPos, 0.35f);
                return;
            }

            if (err > HardCorrect)
                unit.transform.position = serverPos;
            else
                unit.transform.position = Vector3.Lerp(unit.transform.position, serverPos, 0.2f);
        }

        static bool AlongPath(Vector3 server, Vector3 local, Vector3 dest)
        {
            var path = dest - server;
            path.y = 0f;
            var ahead = local - server;
            ahead.y = 0f;
            if (path.sqrMagnitude < 0.04f)
                return ahead.sqrMagnitude < 1.2f;
            var lat = ahead - Vector3.Project(ahead, path.normalized);
            lat.y = 0f;
            return lat.sqrMagnitude < 1.6f && Vector3.Dot(ahead, path) >= -0.8f;
        }

        void InterpolateRemotes()
        {
            if (_frames.Count == 0)
                return;
            var renderAt = Time.time - InterpDelay;
            SnapFrame a, b;
            if (!FindSpan(renderAt, out a, out b))
            {
                a = _frames[_frames.Count - 1];
                b = a;
            }

            var span = Mathf.Max(0.001f, b.Time - a.Time);
            var t = Mathf.Clamp01((renderAt - a.Time) / span);

            foreach (var kv in _units)
            {
                var unit = kv.Value;
                if (unit == null || unit.IsPlayer || unit.IsStructure)
                    continue;
                if (_dead.Contains(kv.Key))
                    continue;

                NetEntityDto ea, eb;
                if (!a.Ents.TryGetValue(kv.Key, out ea))
                    continue;
                if (!b.Ents.TryGetValue(kv.Key, out eb))
                    eb = ea;

                var from = new Vector3(ea.x, unit.GroundY, ea.z);
                var to = new Vector3(eb.x, unit.GroundY, eb.z);
                unit.transform.position = Vector3.Lerp(from, to, t);
                var yaw = Mathf.LerpAngle(ea.yaw, eb.yaw, t);
                unit.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        bool FindSpan(float time, out SnapFrame a, out SnapFrame b)
        {
            a = default;
            b = default;
            if (_frames.Count == 1)
            {
                a = _frames[0];
                b = _frames[0];
                return true;
            }
            for (var i = 0; i < _frames.Count - 1; i++)
            {
                if (time >= _frames[i].Time && time <= _frames[i + 1].Time)
                {
                    a = _frames[i];
                    b = _frames[i + 1];
                    return true;
                }
            }
            if (time > _frames[_frames.Count - 1].Time)
            {
                a = _frames[_frames.Count - 1];
                b = a;
                return true;
            }
            a = _frames[0];
            b = _frames[0];
            return true;
        }

        CombatUnit SpawnMissing(NetEntityDto e)
        {
            if (e.kind == "camp")
            {
                var pos = new Vector3(e.x, 0.4f, e.z);
                var go = UnitFactory.SpawnCamp(transform, pos);
                var camp = go.GetComponent<CombatUnit>();
                Register(e.id, camp);
                return camp;
            }
            if (e.kind != "minion")
                return null;
            var team = MapTeam(e.team, e.kind);
            var posM = new Vector3(e.x, 0.7f, e.z);
            var goal = team == TeamId.Dawn ? new Vector3(40f, 0.7f, 0f) : new Vector3(-40f, 0.7f, 0f);
            var minionGo = UnitFactory.SpawnMinion(transform, posM, team, goal, false);
            var unit = minionGo.GetComponent<CombatUnit>();
            Register(e.id, unit);
            return unit;
        }

        static TeamId MapTeam(int serverTeam, string kind)
        {
            if (kind == "camp" || serverTeam == 2)
                return TeamId.Neutral;
            return serverTeam == 1 ? TeamId.Dusk : TeamId.Dawn;
        }

        void PlayHits(NetHitDto[] hits)
        {
            foreach (var hit in hits)
            {
                if (!_units.TryGetValue(hit.src, out var src) || src == null)
                    continue;
                if (!_units.TryGetValue(hit.dst, out var dst) || dst == null)
                    continue;
                var combat = src.GetComponent<HeroCombat>();
                var ranged = src.IsStructure || (combat != null && combat.Def != null && combat.Def.Ranged);
                if (ranged)
                {
                    var color = src.Team == TeamId.Dawn ? GameTheme.Teal : GameTheme.Crimson;
                    if (combat != null && combat.Def != null)
                        color = GameContent.HeroColor(combat.Def.Id);
                    Projectile.Spawn(src, dst, 0f, color, true);
                }
            }
        }
    }
}
