using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ashfold
{
    public sealed class BattleFlow : MonoBehaviour
    {
        BattleRuntime _runtime;
        MatchStatsTracker _stats;
        HeroCombat _hero;
        Vector3 _spawn;
        BattleHud _hud;
        bool _playerDead;
        float _respawnLeft;

        void Awake()
        {
            AppUi.EnsureEventSystem();
            _runtime = gameObject.AddComponent<BattleRuntime>();
            _stats = gameObject.AddComponent<MatchStatsTracker>();
        }

        void Start()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.28f, 0.30f);
            var sun = new GameObject("Sun");
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(0.92f, 0.95f, 0.98f);

            var map = FoldMapBuilder.Build(transform);
            _spawn = map.DawnSpawn;

            foreach (var camp in map.Camps)
                UnitFactory.MakeCamp(camp);

            _runtime.CrystalDawn = UnitFactory.MakeStructure(map.CrystalDawn, TeamId.Dawn, 1400f, 0, "Crystal", false);
            _runtime.CrystalDusk = UnitFactory.MakeStructure(map.CrystalDusk, TeamId.Dusk, 1400f, 200, "Crystal", false);
            UnitFactory.MakeStructure(map.TurretDawn, TeamId.Dawn, 1100f, 0, "Turret", true);
            UnitFactory.MakeStructure(map.TurretDusk, TeamId.Dusk, 1100f, 120, "Turret", true);
            _runtime.CrystalDawn.Killed += OnCrystalDown;
            _runtime.CrystalDusk.Killed += OnCrystalDown;

            SpawnRoster(map);

            var waves = gameObject.AddComponent<WaveSpawner>();
            waves.Parent = transform;

            var cam = Camera.main;
            if (cam != null && _hero != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = GameTheme.Hex(0x0A1412);
                var follow = cam.gameObject.AddComponent<IsoFollowCamera>();
                follow.Target = _hero.transform;
                cam.transform.position = _hero.transform.position + follow.Offset;
                cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            }

            _hud = BattleHud.Create(_hero.Unit, _hero);
            _hud.SetSurrender(Surrender);
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

            var dawnIndex = 0;
            var duskIndex = 0;
            foreach (var p in match.Players)
            {
                var team = p.Team == 0 ? TeamId.Dawn : TeamId.Dusk;
                var basePos = team == TeamId.Dawn ? map.DawnSpawn : map.DuskSpawn;
                var lane = team == TeamId.Dawn ? dawnIndex++ : duskIndex++;
                var offset = new Vector3(team == TeamId.Dawn ? 2f : -2f, 0f, (lane - 1) * 2f);
                var heroId = string.IsNullOrEmpty(p.HeroId) ? "bastion" : p.HeroId;
                if (p.IsLocal)
                    SpawnHero(p.Name, heroId, team, basePos, true);
                else
                    SpawnBot(p.Name, heroId, team, basePos + offset);
            }
        }

        HeroCombat SpawnHero(string name, string heroId, TeamId team, Vector3 pos, bool player)
        {
            var go = UnitFactory.SpawnHero(transform, pos, heroId, team, player);
            var combat = go.GetComponent<HeroCombat>();
            combat.FountainPos = team == TeamId.Dawn
                ? new Vector3(-FoldMapBuilder.HalfLength, 1.35f, 0f)
                : new Vector3(FoldMapBuilder.HalfLength, 1.35f, 0f);

            _stats.Register(combat.Unit, name, heroId, team == TeamId.Dawn ? 0 : 1, player, !player);

            if (player)
            {
                _hero = combat;
                _spawn = combat.FountainPos;
                _runtime.Player = combat.Unit;
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
            if (!_playerDead || _hud == null)
                return;
            _respawnLeft -= Time.deltaTime;
            _hud.SetDeathTimer(Mathf.Max(0f, _respawnLeft));
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

            // Всегда снимаем lock, даже если матч уже кончился.
            _hero.ReviveAt(_spawn);
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
            FinishMatch(false, true);
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
