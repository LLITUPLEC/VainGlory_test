using System.Collections;
using Nakama;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ashfold
{
    public sealed class BattleFlow : MonoBehaviour
    {
        BattleRuntime _runtime;
        MatchStatsTracker _stats;
        NetBattleSync _net;
        HeroCombat _hero;
        Vector3 _spawn;
        BattleHud _hud;
        bool _playerDead;
        float _respawnLeft;
        bool _networked;
        ISocket _hooked;
        volatile bool _socketDrop;
        bool _reconnecting;

        void Awake()
        {
            AppUi.PurgeBattleLeftovers();
            AppUi.EnsureEventSystem();
            _runtime = gameObject.AddComponent<BattleRuntime>();
            _stats = gameObject.AddComponent<MatchStatsTracker>();
        }

        void Start()
        {
            _networked = GameSession.I != null && GameSession.I.Match != null && GameSession.I.Match.IsNetworked;
            if (Application.isMobilePlatform)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.antiAliasing = 0;
            }
            var map = FoldMapBuilder.Build(transform);
            var desert = map.Root != null && map.Root.GetComponent<FoldMapAuthoring>() != null;
            if (!desert)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.22f, 0.28f, 0.30f);
            }
            var sun = new GameObject("Sun");
            sun.transform.rotation = Quaternion.Euler(desert ? 42f : 50f, desert ? 25f : -35f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = desert ? 1.35f : 1.15f;
            light.color = desert ? new Color(1f, 0.93f, 0.78f) : new Color(0.92f, 0.95f, 0.98f);
            light.shadows = Application.isMobilePlatform ? LightShadows.None : LightShadows.Soft;
            _spawn = map.DawnSpawn;

            if (_networked)
            {
                _net = gameObject.AddComponent<NetBattleSync>();
                _runtime.CrystalDawn = UnitFactory.MakeStructure(map.CrystalDawn, TeamId.Dawn, 1400f, 0, "Crystal", false, false);
                _runtime.CrystalDusk = UnitFactory.MakeStructure(map.CrystalDusk, TeamId.Dusk, 1400f, 200, "Crystal", false, false);
                var turretDawn = UnitFactory.MakeStructure(map.TurretDawn, TeamId.Dawn, 1100f, 0, "Turret", true, false);
                var turretDusk = UnitFactory.MakeStructure(map.TurretDusk, TeamId.Dusk, 1100f, 120, "Turret", true, false);
                _net.Register(10, turretDawn);
                _net.Register(11, turretDusk);
                _net.Register(12, _runtime.CrystalDawn);
                _net.Register(13, _runtime.CrystalDusk);
                if (map.Camps != null)
                {
                    foreach (var camp in map.Camps)
                    {
                        if (camp != null)
                            camp.SetActive(false);
                    }
                }
            }
            else
            {
                foreach (var camp in map.Camps)
                    UnitFactory.MakeCamp(camp);

                _runtime.CrystalDawn = UnitFactory.MakeStructure(map.CrystalDawn, TeamId.Dawn, 1400f, 0, "Crystal", false);
                _runtime.CrystalDusk = UnitFactory.MakeStructure(map.CrystalDusk, TeamId.Dusk, 1400f, 200, "Crystal", false);
                UnitFactory.MakeStructure(map.TurretDawn, TeamId.Dawn, 1100f, 0, "Turret", true);
                UnitFactory.MakeStructure(map.TurretDusk, TeamId.Dusk, 1100f, 120, "Turret", true);
                _runtime.CrystalDawn.Killed += OnCrystalDown;
                _runtime.CrystalDusk.Killed += OnCrystalDown;
            }

            SpawnRoster(map);

            if (!_networked)
            {
                var waves = gameObject.AddComponent<WaveSpawner>();
                waves.Parent = transform;
            }

            var cam = Camera.main;
            if (cam != null && _hero != null)
            {
                var oldFollow = cam.GetComponents<IsoFollowCamera>();
                for (var i = 0; i < oldFollow.Length; i++)
                    Object.DestroyImmediate(oldFollow[i]);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = map.Root != null && map.Root.GetComponent<FoldMapAuthoring>() != null
                    ? new Color(0.82f, 0.68f, 0.45f)
                    : GameTheme.Hex(0x0A1412);
                var follow = cam.gameObject.AddComponent<IsoFollowCamera>();
                follow.Target = _hero.transform;
                cam.transform.position = _hero.transform.position + follow.Offset;
                cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            }

            if (_hero != null)
            {
                _hud = BattleHud.Create(_hero.Unit, _hero);
                _hud.SetSurrender(Surrender);
                if (_networked)
                    _hud.SetHint(Loc.T("hud.hint_net"));
                TutorialCoach.TryStartBattle();
            }

            _runtime.BeginCountdown(BattleRuntime.CountdownSeconds);

            if (_networked)
                HookSocket();
        }

        void SpawnRoster(FoldMap map)
        {
            var match = GameSession.I != null ? GameSession.I.Match : null;
            if (match == null || match.Players.Count == 0)
            {
                SpawnHero("Player", "bastion", TeamId.Dawn, map.DawnSpawn, true);
                SpawnBot("Ally_A", "mira", TeamId.Dawn, map.DawnSpawn + new Vector3(2f, 0f, 2f));
                SpawnBot("Ally_B", "vesper", TeamId.Dawn, map.DawnSpawn + new Vector3(2f, 0f, -2f));
                SpawnBot("Enemy_A", "bastion", TeamId.Dusk, map.DuskSpawn + new Vector3(-2f, 0f, 2f));
                SpawnBot("Enemy_B", "vesper", TeamId.Dusk, map.DuskSpawn + new Vector3(-2f, 0f, 0f));
                SpawnBot("Enemy_C", "mira", TeamId.Dusk, map.DuskSpawn + new Vector3(-2f, 0f, -2f));
                return;
            }

            foreach (var p in match.Players)
            {
                var team = p.Team == 0 ? TeamId.Dawn : TeamId.Dusk;
                var basePos = team == TeamId.Dawn ? map.DawnSpawn : map.DuskSpawn;
                var offset = new Vector3(team == TeamId.Dawn ? 2f : -2f, 0f, (p.Slot - 1) * 2f);
                var heroId = string.IsNullOrEmpty(p.HeroId) ? "bastion" : p.HeroId;
                var netId = p.Team * 3 + p.Slot + 1;
                if (p.IsLocal)
                    SpawnHero(p.Name, heroId, team, basePos, true, p.IsBot, netId);
                else if (_networked)
                    SpawnHero(p.Name, heroId, team, basePos + offset, false, p.IsBot, netId);
                else
                    SpawnBot(p.Name, heroId, team, basePos + offset);
            }
        }

        HeroCombat SpawnHero(string name, string heroId, TeamId team, Vector3 pos, bool player, bool bot = true, int netId = 0)
        {
            var go = UnitFactory.SpawnHero(transform, pos, heroId, team, player);
            var combat = go.GetComponent<HeroCombat>();
            combat.FountainPos = team == TeamId.Dawn
                ? new Vector3(-FoldMapBuilder.HalfLength, 1.35f, 0f)
                : new Vector3(FoldMapBuilder.HalfLength, 1.35f, 0f);

            if (_networked)
            {
                combat.ServerAuth = true;
                combat.Unit.NetId = netId;
                if (!player && combat.Motor != null)
                    combat.Motor.enabled = false;
                if (_net != null && netId > 0)
                    _net.Register(netId, combat.Unit);
            }

            _stats.Register(combat.Unit, name, heroId, team == TeamId.Dawn ? 0 : 1, player, bot && !player);

            if (player)
            {
                _hero = combat;
                _spawn = combat.FountainPos;
                _runtime.Player = combat.Unit;
                if (!_networked)
                    combat.Unit.Killed += OnPlayerDown;
            }

            return combat;
        }

        void SpawnBot(string name, string heroId, TeamId team, Vector3 pos)
        {
            var combat = SpawnHero(name, heroId, team, pos, false);
            var ai = combat.gameObject.AddComponent<HeroBotAi>();
            ai.Combat = combat;
            ai.Unit = combat.Unit;
            ai.Team = team;
            ai.Fountain = combat.FountainPos;
            ai.PushGoal = team == TeamId.Dawn
                ? new Vector3(FoldMapBuilder.HalfLength + 2f, 1.35f, 0f)
                : new Vector3(-FoldMapBuilder.HalfLength - 2f, 1.35f, 0f);
            ai.PreferredItemId = UnitFactory.PreferredItemFor(heroId);
        }

        void Update()
        {
            if (_networked && _socketDrop && !_reconnecting && _runtime != null && !_runtime.MatchOver)
                StartCoroutine(ReconnectRoutine());

            if (_networked || !_playerDead || _hud == null)
                return;
            _respawnLeft -= Time.deltaTime;
            _hud.SetDeathTimer(Mathf.Max(0f, _respawnLeft));
        }

        void HookSocket()
        {
            UnhookSocket();
            var nk = GameSession.I != null ? GameSession.I.Nakama : null;
            if (nk == null || nk.Socket == null)
                return;
            _hooked = nk.Socket;
            _hooked.Closed += OnSocketClosed;
            _hooked.ReceivedError += OnSocketError;
        }

        void UnhookSocket()
        {
            if (_hooked == null)
                return;
            _hooked.Closed -= OnSocketClosed;
            _hooked.ReceivedError -= OnSocketError;
            _hooked = null;
        }

        void OnSocketClosed()
        {
            if (!_reconnecting)
                _socketDrop = true;
        }

        void OnSocketError(System.Exception _)
        {
            if (!_reconnecting)
                _socketDrop = true;
        }

        IEnumerator ReconnectRoutine()
        {
            _reconnecting = true;
            _socketDrop = false;
            UnhookSocket();
            var nk = GameSession.I.Nakama;
            var matchId = nk != null && !string.IsNullOrEmpty(nk.MatchId)
                ? nk.MatchId
                : (GameSession.I.Match != null ? GameSession.I.Match.NakamaMatchId : "");
            var left = 30f;
            while (left > 0f && _runtime != null && !_runtime.MatchOver)
            {
                if (_hud != null)
                    _hud.SetNetStatus(Loc.T("hud.reconnecting", Mathf.CeilToInt(left)), true);

                if (nk != null)
                {
                    var close = nk.CloseSocketKeepMatchAsync();
                    while (!close.IsCompleted)
                        yield return null;

                    var join = nk.JoinMatchByIdAsync(matchId);
                    while (!join.IsCompleted)
                        yield return null;
                    if (!join.IsFaulted && nk.CurrentMatch != null)
                    {
                        HookSocket();
                        _socketDrop = false;
                        if (_hud != null)
                            _hud.SetNetStatus("", false);
                        _reconnecting = false;
                        Debug.Log("[Ashfold] Reconnected to " + matchId);
                        yield break;
                    }
                }

                yield return new WaitForSeconds(2f);
                left -= 2f;
            }

            if (_hud != null)
                _hud.SetNetStatus(Loc.T("hud.rejoin_fail"), true);
            _reconnecting = false;
            if (nk != null)
            {
                var leave = nk.DisconnectRealtimeAsync();
                while (!leave.IsCompleted)
                    yield return null;
            }
            if (_runtime != null && !_runtime.MatchOver)
                FinishMatch(false, false);
        }

        void OnDestroy()
        {
            UnhookSocket();
        }

        void OnPlayerDown(CombatUnit victim, CombatUnit killer)
        {
            if (_playerDead || _runtime.MatchOver)
                return;
            _runtime.Deaths++;
            StartCoroutine(RespawnPlayer());
        }

        IEnumerator RespawnPlayer()
        {
            _playerDead = true;
            var wait = RespawnRules.DurationSeconds();
            _respawnLeft = wait;
            _hero.BeginDeathLock();

            if (_hud != null)
                _hud.SetDeathTimer(_respawnLeft);

            yield return new WaitForSeconds(wait);

            _hero.ReviveAt(_spawn);
            _playerDead = false;
            if (_hud != null)
                _hud.ClearDeathTimer();
        }

        public void ShowNetDeath(float seconds)
        {
            _playerDead = true;
            if (_hud != null)
                _hud.SetDeathTimer(Mathf.Max(0f, seconds));
        }

        public void ClearNetDeath()
        {
            _playerDead = false;
            if (_hud != null)
                _hud.ClearDeathTimer();
        }

        void OnCrystalDown(CombatUnit crystal, CombatUnit killer)
        {
            if (_runtime.MatchOver)
                return;
            FinishMatch(crystal.Team == TeamId.Dusk, false);
        }

        void Surrender()
        {
            if (_runtime.MatchOver)
                return;
            if (_networked && GameSession.I != null && GameSession.I.MatchClient != null)
            {
                GameSession.I.MatchClient.SendSurrender();
                return;
            }
            FinishMatch(false, true);
        }

        public void FinishFromServer(int winnerTeam, bool surrendered)
        {
            if (_runtime.MatchOver)
                return;
            var localTeam = 0;
            if (GameSession.I != null && GameSession.I.Match != null && GameSession.I.Match.Local != null)
                localTeam = GameSession.I.Match.Local.Team;
            FinishMatch(winnerTeam == localTeam, surrendered);
        }

        void FinishMatch(bool victory, bool surrendered)
        {
            _runtime.MatchOver = true;
            if (GameSession.I != null)
                GameSession.I.LastResult = _stats.BuildResult(victory, surrendered);
            SceneManager.LoadScene(AppScenes.Results);
        }
    }
}
