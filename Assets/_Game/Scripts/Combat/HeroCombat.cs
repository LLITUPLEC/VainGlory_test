using System.Collections.Generic;
using UnityEngine;

namespace Ashfold
{
    public sealed class HeroCombat : MonoBehaviour
    {
        public HeroDef Def;
        public CombatUnit Unit;
        public TapMoveMotor Motor;
        public CombatUnit AttackTarget;
        public float AttackCd;
        public float SkillCd;
        public readonly List<string> Items = new List<string>(6);
        public bool Recalling;
        public float RecallT;
        public const float RecallDuration = 2.5f;
        public const int MaxItems = 6;

        public Vector3 FountainPos;
        public float ExtraDamage;
        public float ExtraAs;
        public float ExtraHeal;
        public float ExtraMove;

        public bool SkillReady => SkillCd <= 0f && Unit != null && Unit.IsAlive && !Unit.Stunned && !Recalling;
        public float AttackDamage => Def.Damage + ExtraDamage;
        public float AttackInterval => Def.AttackInterval / Mathf.Max(0.4f, 1f + ExtraAs);
        public float SkillPower => Def.SkillPower * (1f + ExtraHeal);

        void Start()
        {
            if (Unit != null)
                Unit.Damaged += CancelRecall;
        }

        void OnDestroy()
        {
            if (Unit != null)
                Unit.Damaged -= CancelRecall;
        }

        void Update()
        {
            if (Unit == null || !Unit.IsAlive)
            {
                Motor.Stop();
                Recalling = false;
                return;
            }

            if (Unit.Stunned)
            {
                Motor.Stop();
                Motor.StunUntil = Unit.StunUntil;
                Recalling = false;
                return;
            }

            if (Recalling)
            {
                Motor.Stop();
                RecallT += Time.deltaTime;
                if (RecallT >= RecallDuration)
                {
                    Recalling = false;
                    transform.position = FountainPos;
                    Motor.Stop();
                    AttackTarget = null;
                }
                return;
            }

            // Фонтанный реген для игрока тоже.
            if (Unit != null && Unit.IsAlive && FoldMapBuilder.InFountain(transform.position, Unit.Team))
                Unit.Heal(Unit.MaxHp * 0.22f * Time.deltaTime);

            AttackCd -= Time.deltaTime;
            SkillCd -= Time.deltaTime;
            if (ExtraHeal > 0f)
                Unit.Heal(6f * ExtraHeal * Time.deltaTime);

            if (AttackTarget != null && !AttackTarget.IsAlive)
                AttackTarget = null;

            if (AttackTarget == null)
                return;

            var dist = Motor.DistTo(AttackTarget.transform.position);
            if (dist > Def.AttackRange)
            {
                Motor.MoveTo(AttackTarget.transform.position);
                return;
            }

            Motor.Stop();
            Motor.Face(AttackTarget.transform.position);
            if (AttackCd > 0f)
                return;

            AttackCd = AttackInterval;
            if (Def.Ranged)
                Projectile.Spawn(Unit, AttackTarget, AttackDamage, GameContent.HeroColor(Def.Id));
            else
                AttackTarget.ApplyDamage(AttackDamage, Unit);
        }

        public void CommandAttack(CombatUnit target)
        {
            if (target == null || !Unit.IsEnemy(target))
                return;
            CancelRecall();
            AttackTarget = target;
        }

        public void CommandMove(Vector3 point)
        {
            CancelRecall();
            AttackTarget = null;
            Motor.MoveTo(point);
        }

        public void TryRecall()
        {
            if (Unit == null || !Unit.IsAlive || Recalling)
                return;
            if (FoldMapBuilder.InFountain(transform.position, Unit.Team))
                return;
            Recalling = true;
            RecallT = 0f;
            AttackTarget = null;
            Motor.Stop();
        }

        public void CancelRecall()
        {
            Recalling = false;
            RecallT = 0f;
        }

        public void BeginDeathLock()
        {
            CancelRecall();
            AttackTarget = null;
            if (Motor != null)
            {
                Motor.Locked = true;
                Motor.Stop();
            }
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        public void ReviveAt(Vector3 pos)
        {
            CancelRecall();
            AttackTarget = null;
            AttackCd = 0f;
            if (Unit != null)
            {
                Unit.Hp = Unit.MaxHp;
                Unit.StunUntil = 0f;
            }
            transform.position = pos;
            if (Motor != null)
            {
                Motor.Locked = false;
                Motor.StunUntil = 0f;
                Motor.Stop();
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
        }

        public bool TryCastSkill()
        {
            if (!SkillReady)
                return false;
            CancelRecall();
            SkillCd = Def.SkillCooldown;

            if (AttackTarget != null && AttackTarget.IsAlive)
                Motor.Face(AttackTarget.transform.position);

            switch (Def.Id)
            {
                case "bastion":
                    CastCone();
                    break;
                case "vesper":
                    Projectile.SpawnSkillshot(Unit, transform.forward, SkillPower, 22f, 0.7f, GameTheme.Crimson);
                    break;
                case "mira":
                    CastMendNova();
                    break;
            }

            return true;
        }

        public bool TryBuy(ItemDef item)
        {
            if (BattleRuntime.I == null)
                return false;
            if (BattleRuntime.I.Gold < item.Cost)
                return false;
            if (!TryBuyFree(item))
                return false;
            BattleRuntime.I.Gold -= item.Cost;
            return true;
        }

        /// <summary>Покупка без списания из BattleRuntime (для бот-кошелька).</summary>
        public bool TryBuyFree(ItemDef item)
        {
            if (item == null || Items.Count >= MaxItems || Unit == null)
                return false;
            if (!FoldMapBuilder.InFountain(transform.position, Unit.Team))
                return false;
            Items.Add(item.Id);
            ApplyItems();
            return true;
        }

        public void ApplyItems()
        {
            ExtraDamage = 0f;
            ExtraAs = 0f;
            ExtraHeal = 0f;
            ExtraMove = 0f;
            var hp = 0f;
            var resist = 0f;
            foreach (var id in Items)
            {
                var item = GameContent.GetItem(id);
                if (item == null)
                    continue;
                ExtraDamage += item.BonusDamage;
                ExtraAs += item.BonusAttackSpeed;
                ExtraHeal += item.BonusHealPower;
                ExtraMove += item.BonusMoveSpeed;
                hp += item.BonusHp;
                resist += item.BonusResist;
            }

            var newMax = Def.MaxHp + hp;
            if (newMax > Unit.MaxHp)
                Unit.Hp += newMax - Unit.MaxHp;
            Unit.MaxHp = newMax;
            Unit.Resist = Mathf.Clamp01(resist);
            Motor.Speed = Def.MoveSpeed * (1f + ExtraMove);
        }

        void CastMendNova()
        {
            var units = new List<CombatUnit>(CombatUnit.All);
            foreach (var u in units)
            {
                if (u == null || !u.IsAlive)
                    continue;
                var d = u.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude > Def.SkillRange)
                    continue;
                if (u.Team == Unit.Team)
                    u.Heal(SkillPower);
                else if (Unit.IsEnemy(u))
                    u.ApplyDamage(SkillPower * 0.55f, Unit);
            }
            Flash(GameTheme.Teal);
        }

        void CastCone()
        {
            var units = new List<CombatUnit>(CombatUnit.All);
            foreach (var u in units)
            {
                if (u == null || !Unit.IsEnemy(u))
                    continue;
                var to = u.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > Def.SkillRange)
                    continue;
                if (Vector3.Angle(transform.forward, to) > 55f)
                    continue;
                u.ApplyDamage(SkillPower, Unit);
                u.Stun(0.85f);
            }
            Flash(GameTheme.Gold);
        }

        void Flash(Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = transform.position + Vector3.up;
            go.transform.localScale = Vector3.one * (Def.Id == "mira" ? Def.SkillRange * 2f : 2.2f);
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(new Color(color.r, color.g, color.b, 0.35f));
            Object.Destroy(go, 0.25f);
        }
    }
}
