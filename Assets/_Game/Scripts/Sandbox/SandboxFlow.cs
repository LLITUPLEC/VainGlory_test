using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Полигон умений на той же карте 3v3. Без волн, без отсчёта, без победы по кристаллу.</summary>
    public sealed class SandboxFlow : MonoBehaviour
    {
        string _heroId = "bastion";
        string _dummyHeroId = "vesper";
        SandboxDummyKind _kind = SandboxDummyKind.Post;
        SandboxDummyAct _act = SandboxDummyAct.Idle;
        bool _immortal = true;
        bool _rings = true;

        FoldMap _map;
        HeroCombat _hero;
        CombatUnit _dummy;
        SandboxDummy _brain;
        BattleHud _hud;
        Text _status;
        readonly GameObject[] _ring = new GameObject[HeroRules.SlotCount];
        Vector3 _dummyHome;

        void Awake()
        {
            AppUi.PurgeBattleLeftovers();
            AppUi.EnsureEventSystem();
            gameObject.AddComponent<BattleRuntime>();
        }

        void Start()
        {
            if (Application.isMobilePlatform)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.antiAliasing = 0;
            }

            _map = FoldMapBuilder.Build(transform);
            var desert = _map.Root != null && _map.Root.GetComponent<FoldMapAuthoring>() != null;
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

            if (_map.Camps != null)
            {
                foreach (var camp in _map.Camps)
                {
                    if (camp != null)
                        UnitFactory.MakeCamp(camp);
                }
            }
            UnitFactory.MakeStructure(_map.CrystalDawn, TeamId.Dawn, CombatBalance.CrystalHp, 0, "Crystal", false, false);
            UnitFactory.MakeStructure(_map.CrystalDusk, TeamId.Dusk, CombatBalance.CrystalHp, 200, "Crystal", false, false);
            BindTurrets(_map.TurretsDawn, TeamId.Dawn);
            BindTurrets(_map.TurretsDusk, TeamId.Dusk);
            SilenceMapAi();

            _dummyHome = _map.DawnSpawn + FoldMapBuilder.LaneDir(TeamId.Dawn) * 10f;
            SpawnPlayer();
            SpawnDummy();
            BuildPanel();

            if (BattleRuntime.I != null)
                BattleRuntime.I.Gold = 9999;
        }

        static void BindTurrets(GameObject[] turrets, TeamId team)
        {
            if (turrets == null)
                return;
            for (var i = 0; i < turrets.Length; i++)
                UnitFactory.MakeStructure(turrets[i], team, CombatBalance.TurretHp, i == 0 ? 0 : 120, "Turret", true, false);
        }

        void SilenceMapAi()
        {
            foreach (var ai in GetComponentsInChildren<MeleeAi>(true))
                ai.enabled = false;
            foreach (var ai in GetComponentsInChildren<TurretAi>(true))
                ai.enabled = false;
            foreach (var bot in GetComponentsInChildren<HeroBotAi>(true))
                bot.enabled = false;
        }

        void SpawnPlayer()
        {
            if (_hero != null)
            {
                if (_hero.Unit != null)
                    _hero.Unit.Killed -= OnPlayerDown;
                Destroy(_hero.gameObject);
            }
            if (_hud != null)
                Destroy(_hud.gameObject);

            var go = UnitFactory.SpawnHero(transform, _map.DawnSpawn, _heroId, TeamId.Dawn, true);
            _hero = go.GetComponent<HeroCombat>();
            _hero.FountainPos = _map.DawnSpawn;
            _hero.Unit.Killed += OnPlayerDown;
            var prog = _hero.Progress;
            if (prog != null)
                prog.DebugMaxOut();

            _hud = BattleHud.Create(_hero.Unit, _hero);
            _hud.SetSurrender(() => SceneManager.LoadScene(AppScenes.Hall));

            var cam = Camera.main;
            if (cam != null)
            {
                var old = cam.GetComponents<IsoFollowCamera>();
                for (var i = 0; i < old.Length; i++)
                    DestroyImmediate(old[i]);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = _map.Root != null && _map.Root.GetComponent<FoldMapAuthoring>() != null
                    ? new Color(0.82f, 0.68f, 0.45f)
                    : GameTheme.Hex(0x0A1412);
                var follow = cam.gameObject.AddComponent<IsoFollowCamera>();
                follow.Target = _hero.transform;
                cam.transform.position = _hero.transform.position + follow.Offset;
                cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            }

            EnsureRings();
            BindDummyPlayer();
        }

        void SpawnDummy()
        {
            if (_dummy != null)
                Destroy(_dummy.gameObject);

            GameObject go;
            switch (_kind)
            {
                case SandboxDummyKind.Minion:
                    go = UnitFactory.SpawnMinion(transform, _dummyHome, TeamId.Dusk, _dummyHome, false);
                    go.GetComponent<TapMoveMotor>().enabled = true;
                    break;
                case SandboxDummyKind.Jungle:
                    go = UnitFactory.SpawnCamp(transform, _dummyHome);
                    go.GetComponent<TapMoveMotor>().enabled = true;
                    go.name = "Dummy_Jungle";
                    break;
                case SandboxDummyKind.Hero:
                    go = UnitFactory.SpawnHero(transform, _dummyHome, _dummyHeroId, TeamId.Dusk, false);
                    go.GetComponent<HeroCombat>().FountainPos = _map.DuskSpawn;
                    break;
                case SandboxDummyKind.Kraken:
                    go = MakeKraken(_dummyHome);
                    break;
                default:
                    go = MakePost(_dummyHome);
                    break;
            }

            _dummy = go.GetComponent<CombatUnit>();
            _dummy.DisableOnDeath = false;
            _brain = go.GetComponent<SandboxDummy>() ?? go.AddComponent<SandboxDummy>();
            _brain.Act = _act;
            _brain.Immortal = _immortal;
            BindDummyPlayer();
            SilenceMapAi();
        }

        void BindDummyPlayer()
        {
            if (_brain != null && _hero != null)
                _brain.Player = _hero.Unit;
        }

        GameObject MakePost(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dummy_Post";
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.4f, 1.1f, 1.4f);
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(GameTheme.Hex(0x8A6A4A));
            var unit = go.AddComponent<CombatUnit>();
            unit.Team = TeamId.Dusk;
            unit.MaxHp = 8000f;
            unit.Hp = 8000f;
            unit.DisplayName = Loc.T("sandbox.kind.post");
            unit.GroundY = 1.1f;
            var motor = go.AddComponent<TapMoveMotor>();
            motor.Speed = 5.5f;
            motor.Hover = 1.1f;
            motor.SnapToGround();
            WorldHpBar.Attach(unit);
            return go;
        }

        GameObject MakeKraken(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dummy_Kraken";
            go.transform.SetParent(transform, false);
            go.transform.position = pos + Vector3.up * 0.6f;
            go.transform.localScale = Vector3.one * 3.2f;
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(GameTheme.Hex(0x3A2A58));
            var unit = go.AddComponent<CombatUnit>();
            unit.Team = TeamId.Neutral;
            unit.MaxHp = 6000f;
            unit.Hp = 6000f;
            unit.DisplayName = Loc.T("sandbox.kind.kraken");
            unit.GroundY = 1.6f;
            var motor = go.AddComponent<TapMoveMotor>();
            motor.Speed = 2.4f;
            motor.Hover = 1.6f;
            motor.SnapToGround();
            WorldHpBar.Attach(unit);
            var dummy = go.AddComponent<SandboxDummy>();
            dummy.MeleeDamage = 90f;
            dummy.MeleeRange = 2.8f;
            return go;
        }

        void OnPlayerDown(CombatUnit victim, CombatUnit killer)
        {
            if (_hero != null)
                _hero.ReviveAt(_map.DawnSpawn);
            if (_hud != null)
                _hud.ClearDeathTimer();
        }

        void BuildPanel()
        {
            var canvas = UiFactory.CreateCanvas("SandboxPanel");
            canvas.sortingOrder = 28;
            var root = canvas.transform;
            var sheet = UiFactory.Box(root, new Vector2(0.01f, 0.16f), new Vector2(0.24f, 0.84f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Panel");
            UiFactory.Label(
                UiFactory.Box(sheet.transform, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.99f), Vector2.zero, Vector2.zero, Color.clear, "T").transform,
                Loc.T("sandbox.title"), 16, GameTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

            _status = UiFactory.Label(
                UiFactory.Box(sheet.transform, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.91f), Vector2.zero, Vector2.zero, Color.clear, "S").transform,
                "", 13, GameTheme.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, true);

            Row(sheet.transform, 0.68f, Loc.T("sandbox.hero"), CycleHero, NextHeroLabel);
            Row(sheet.transform, 0.58f, Loc.T("sandbox.target"), CycleKind, KindLabel);
            Row(sheet.transform, 0.48f, Loc.T("sandbox.act"), CycleAct, ActLabel);
            Row(sheet.transform, 0.38f, Loc.T("sandbox.dummy_hero"), CycleDummyHero, DummyHeroLabel);

            Tiny(sheet.transform, 0.28f, 0.04f, 0.48f, Loc.T("sandbox.max"), MaxSkills);
            Tiny(sheet.transform, 0.28f, 0.52f, 0.96f, Loc.T("sandbox.fresh"), FreshSkills);
            Tiny(sheet.transform, 0.18f, 0.04f, 0.48f, Loc.T("sandbox.heal"), HealBoth);
            Tiny(sheet.transform, 0.18f, 0.52f, 0.96f, Loc.T("sandbox.cds"), ResetCds);
            Tiny(sheet.transform, 0.08f, 0.04f, 0.48f, Loc.T("sandbox.immortal"), ToggleImmortal);
            Tiny(sheet.transform, 0.08f, 0.52f, 0.96f, Loc.T("sandbox.rings"), ToggleRings);
        }

        void Row(Transform parent, float y, string caption, UnityEngine.Events.UnityAction action, System.Func<string> label)
        {
            var btn = UiFactory.Button(parent, caption + "\n" + label(), action, GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(0.04f, y), new Vector2(0.96f, y + 0.09f), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 13;
            btn.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
            btn.onClick.AddListener(() =>
            {
                var t = btn.GetComponentInChildren<Text>();
                if (t != null)
                    t.text = caption + "\n" + label();
            });
        }

        void Tiny(Transform parent, float y, float x0, float x1, string caption, UnityEngine.Events.UnityAction action)
        {
            var btn = UiFactory.Button(parent, caption, action, GameTheme.BgPanelSoft, GameTheme.Gold);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x0, y), new Vector2(x1, y + 0.09f), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 12;
        }

        void CycleHero()
        {
            _heroId = NextId(_heroId);
            SpawnPlayer();
        }

        void CycleDummyHero()
        {
            _dummyHeroId = NextId(_dummyHeroId);
            if (_kind == SandboxDummyKind.Hero)
                SpawnDummy();
        }

        void CycleKind()
        {
            _kind = (SandboxDummyKind)(((int)_kind + 1) % 5);
            SpawnDummy();
        }

        void CycleAct()
        {
            _act = (SandboxDummyAct)(((int)_act + 1) % 3);
            if (_brain != null)
                _brain.Act = _act;
        }

        static string NextId(string id)
        {
            if (id == "bastion")
                return "vesper";
            if (id == "vesper")
                return "mira";
            return "bastion";
        }

        string NextHeroLabel() => GameContent.GetHero(_heroId).DisplayName;
        string DummyHeroLabel() => GameContent.GetHero(_dummyHeroId).DisplayName;
        string KindLabel() => Loc.T("sandbox.kind." + _kind.ToString().ToLowerInvariant());
        string ActLabel() => Loc.T("sandbox.act." + _act.ToString().ToLowerInvariant());

        void MaxSkills()
        {
            if (_hero != null && _hero.Progress != null)
                _hero.Progress.DebugMaxOut();
        }

        void FreshSkills()
        {
            if (_hero != null && _hero.Progress != null)
                _hero.Progress.DebugFresh();
        }

        void HealBoth()
        {
            if (_hero != null && _hero.Unit != null)
                _hero.Unit.Hp = _hero.Unit.MaxHp;
            if (_dummy != null)
                _dummy.Hp = _dummy.MaxHp;
            if (_hero != null)
                _hero.DebugResetCds();
        }

        void ResetCds()
        {
            if (_hero != null)
                _hero.DebugResetCds();
        }

        void ToggleImmortal()
        {
            _immortal = !_immortal;
            if (_brain != null)
                _brain.Immortal = _immortal;
        }

        void ToggleRings()
        {
            _rings = !_rings;
        }

        void EnsureRings()
        {
            var colors = new[] { GameTheme.Gold, GameTheme.Teal, GameTheme.Crimson };
            for (var i = 0; i < _ring.Length; i++)
            {
                if (_ring[i] != null)
                    continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var col = go.GetComponent<Collider>();
                if (col != null)
                    DestroyImmediate(col);
                go.name = "RangeRing_" + i;
                go.layer = 2;
                var c = colors[i];
                go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(new Color(c.r, c.g, c.b, 0.18f));
                _ring[i] = go;
            }
        }

        void LateUpdate()
        {
            if (_status != null)
            {
                var dist = 0f;
                var hp = "—";
                if (_hero != null && _dummy != null)
                {
                    var d = _dummy.transform.position - _hero.transform.position;
                    d.y = 0f;
                    dist = d.magnitude;
                    hp = Mathf.CeilToInt(_dummy.Hp) + "/" + Mathf.CeilToInt(_dummy.MaxHp);
                }
                _status.text = Loc.T("sandbox.status",
                    GameContent.GetHero(_heroId).DisplayName,
                    KindLabel(),
                    ActLabel(),
                    hp,
                    dist.ToString("0.0"),
                    _immortal ? Loc.T("sandbox.on") : Loc.T("sandbox.off"));
            }

            if (_hero == null)
                return;
            for (var i = 0; i < _ring.Length; i++)
            {
                var ring = _ring[i];
                if (ring == null)
                    continue;
                var def = _hero.Ability(i);
                var rank = _hero.Progress != null ? _hero.Progress.RankOf(i) : 0;
                var show = _rings && def != null && rank > 0 && def.Rng(rank) > 0.2f;
                ring.SetActive(show);
                if (!show)
                    continue;
                var r = def.Rng(rank);
                ring.transform.position = _hero.transform.position + Vector3.up * (0.05f + i * 0.03f);
                ring.transform.localScale = new Vector3(r * 2f, 0.06f, r * 2f);
            }
        }

        void OnDestroy()
        {
            if (_hero != null && _hero.Unit != null)
                _hero.Unit.Killed -= OnPlayerDown;
            for (var i = 0; i < _ring.Length; i++)
            {
                if (_ring[i] != null)
                    Destroy(_ring[i]);
            }
        }
    }
}
