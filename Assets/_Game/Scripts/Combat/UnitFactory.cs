using UnityEngine;

namespace Ashfold
{
    public static class UnitFactory
    {
        public static GameObject SpawnHero(Transform parent, Vector3 pos, string heroId, TeamId team, bool player)
        {
            var def = GameContent.GetHero(heroId);
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = (player ? "Player_" : "Hero_") + def.DisplayName;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            ApplySilhouette(go.transform, def.Id, GameContent.HeroColor(def.Id));

            var unit = go.AddComponent<CombatUnit>();
            unit.Team = team;
            unit.MaxHp = def.MaxHp;
            unit.Hp = def.MaxHp;
            unit.Bounty = player ? 0 : 120;
            unit.IsHero = true;
            unit.IsPlayer = player;
            unit.DisplayName = def.DisplayName;
            unit.GroundY = 1.35f;
            unit.DisableOnDeath = false;

            var motor = go.AddComponent<TapMoveMotor>();
            motor.Speed = def.MoveSpeed;
            motor.GroundY = 1.35f;

            var combat = go.AddComponent<HeroCombat>();
            combat.Def = def;
            combat.Unit = unit;
            combat.Motor = motor;
            go.AddComponent<HeroProgression>();

            if (player)
            {
                var cmd = go.AddComponent<PlayerCommander>();
                cmd.Hero = combat;
                cmd.Unit = unit;
            }

            WorldHpBar.Attach(unit);
            go.AddComponent<BrushStealth>().Unit = unit;
            return go;
        }

        public static string PreferredItemFor(string heroId)
        {
            switch (heroId)
            {
                case "bastion": return "stoneplate";
                case "mira": return "lifewell";
                default: return "iron_edge";
            }
        }

        public static GameObject SpawnMinion(Transform parent, Vector3 pos, TeamId team, Vector3 laneGoal, bool localAi = true, bool captain = false)
        {
            var color = team == TeamId.Dawn ? Color.Lerp(GameTheme.Teal, Color.white, 0.25f) : Color.Lerp(GameTheme.Crimson, Color.white, 0.2f);
            var scale = 0.7f * (captain ? CombatBalance.CaptainScale : 1f);
            var gy = captain ? CombatBalance.CaptainGroundY : CombatBalance.MinionGroundY;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = (captain ? "Captain_" : "Minion_") + team;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, gy, pos.z);
            go.transform.localScale = new Vector3(scale, scale, scale);
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = RuntimeMat.Make(color);

            var unit = go.AddComponent<CombatUnit>();
            unit.Team = team;
            unit.MaxHp = captain ? CombatBalance.CaptainHp : CombatBalance.MinionHp;
            unit.Hp = unit.MaxHp;
            unit.Bounty = captain ? CombatBalance.CaptainBounty : CombatBalance.MinionBounty;
            unit.DisplayName = captain ? "Captain" : "Minion";
            unit.GroundY = gy;

            var motor = go.AddComponent<TapMoveMotor>();
            motor.Speed = CombatBalance.MinionSpeed;
            motor.GroundY = gy;

            if (AttachMinionVisual(go.transform, motor, captain))
                rend.enabled = false;

            if (localAi)
            {
                var ai = go.AddComponent<MeleeAi>();
                ai.Unit = unit;
                ai.Motor = motor;
                ai.Damage = captain ? CombatBalance.CaptainDamage : CombatBalance.MinionDamage;
                ai.Range = captain ? 1.9f : 1.7f;
                ai.Aggro = 5.2f;
                ai.LaneGoal = laneGoal;
            }
            else
                motor.enabled = false;

            WorldHpBar.Attach(unit);
            return go;
        }

        public static CombatUnit MakeStructure(GameObject go, TeamId team, float hp, int bounty, string name, bool turret, bool localAi = true)
        {
            var unit = go.AddComponent<CombatUnit>();
            unit.Team = team;
            unit.MaxHp = hp;
            unit.Hp = hp;
            unit.Bounty = bounty;
            unit.IsStructure = true;
            unit.DisableOnDeath = false;
            unit.DisplayName = name;
            if (turret && localAi)
            {
                var ai = go.AddComponent<TurretAi>();
                ai.Unit = unit;
            }
            if (name == "Crystal" && go.GetComponent<CrystalGemMotion>() == null)
                go.AddComponent<CrystalGemMotion>();
            WorldHpBar.Attach(unit);
            return unit;
        }

        public static void MakeCamp(GameObject go)
        {
            var unit = go.AddComponent<CombatUnit>();
            unit.Team = TeamId.Neutral;
            unit.MaxHp = 220f;
            unit.Hp = 220f;
            unit.Bounty = 28;
            unit.DisplayName = "Camp";
            unit.DisableOnDeath = false;
            unit.GroundY = 0.4f;
            var motor = go.AddComponent<TapMoveMotor>();
            motor.Speed = 3.2f;
            motor.GroundY = 0.4f;
            var ai = go.AddComponent<MeleeAi>();
            ai.Unit = unit;
            ai.Motor = motor;
            ai.Damage = 22f;
            ai.Range = 1.8f;
            ai.Aggro = 3.5f;
            ai.RoamLane = false;
            var camp = go.AddComponent<JungleCamp>();
            camp.Unit = unit;
            camp.Bind();
            WorldHpBar.Attach(unit);
        }

        public static GameObject SpawnCamp(Transform parent, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Camp";
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 1.4f;
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(GameTheme.Hex(0x4A6A38));

            var unit = go.AddComponent<CombatUnit>();
            unit.Team = TeamId.Neutral;
            unit.MaxHp = 220f;
            unit.Hp = 220f;
            unit.Bounty = 28;
            unit.DisplayName = "Camp";
            unit.DisableOnDeath = false;
            unit.GroundY = 0.4f;
            var motor = go.AddComponent<TapMoveMotor>();
            motor.Speed = 3.2f;
            motor.GroundY = 0.4f;
            motor.enabled = false;
            WorldHpBar.Attach(unit);
            return go;
        }

        static bool AttachMinionVisual(Transform root, TapMoveMotor motor, bool captain)
        {
            var prefab = Resources.Load<GameObject>(captain ? MapModels.CaptainVisualRes : MapModels.MinionVisualRes);
            if (prefab == null && captain)
                prefab = Resources.Load<GameObject>(MapModels.MinionVisualRes);
            if (prefab == null)
                return false;
            var vis = Object.Instantiate(prefab, root, false);
            vis.name = "Visual";
            vis.transform.localPosition = new Vector3(0f, -1f, 0f);
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;
            var anim = root.GetComponent<UnitAnim>() ?? root.gameObject.AddComponent<UnitAnim>();
            anim.Motor = motor;
            anim.Animator = vis.GetComponentInChildren<Animator>();
            return true;
        }

        static void ApplySilhouette(Transform root, string id, Color color)
        {
            var body = root.GetComponent<Renderer>();
            body.sharedMaterial = RuntimeMat.Make(color);
            switch (id)
            {
                case "bastion":
                    root.localScale = new Vector3(1.45f, 1.4f, 1.45f);
                    Decor(root, PrimitiveType.Cube, new Vector3(-0.7f, 0.35f, 0f), new Vector3(0.55f, 0.35f, 0.7f), color);
                    Decor(root, PrimitiveType.Cube, new Vector3(0.7f, 0.35f, 0f), new Vector3(0.55f, 0.35f, 0.7f), color);
                    break;
                case "vesper":
                    root.localScale = new Vector3(0.85f, 1.55f, 0.85f);
                    Decor(root, PrimitiveType.Cylinder, new Vector3(0.55f, 0.2f, 0.15f), new Vector3(0.12f, 0.9f, 0.12f), GameTheme.Hex(0x2A1A1A));
                    break;
                case "mira":
                    root.localScale = new Vector3(1.05f, 1.25f, 1.05f);
                    Decor(root, PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 0.08f, 1.1f), Color.Lerp(color, Color.white, 0.4f));
                    break;
            }
        }

        static void Decor(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(color);
        }
    }
}
