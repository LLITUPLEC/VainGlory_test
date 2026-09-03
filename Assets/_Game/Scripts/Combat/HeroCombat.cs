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
        public readonly float[] SkillCd = new float[HeroRules.SlotCount];
        public readonly List<string> Items = new List<string>(6);
        public bool Recalling;
        public bool ServerAuth;
        public float RecallT;
        public const float RecallDuration = 2.5f;
        public const int MaxItems = 6;
        float _buyLockUntil;

        public HeroProgression Progress
        {
            get { return GetComponent<HeroProgression>(); }
        }

        public Vector3 FountainPos;
        public float ExtraDamage;
        public float ExtraAs;
        public float ExtraHeal;
        public float ExtraMove;

        public bool SlotReady(int slot)
        {
            if (slot < 0 || slot >= HeroRules.SlotCount)
                return false;
            var rank = Progress != null ? Progress.RankOf(slot) : 0;
            if (rank < 1)
                return false;
            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
                return false;
            return SkillCd[slot] <= 0f
                   && Unit != null && Unit.IsAlive && !Unit.Stunned && !Recalling;
        }

        public bool SkillReady => SlotReady(0);
        public float AttackDamage => Def.Damage + ExtraDamage;
        public float AttackInterval => Def.AttackInterval / Mathf.Max(0.4f, 1f + ExtraAs);

        public AbilityDef Ability(int slot)
        {
            return Def != null ? Def.Ability(slot) : null;
        }

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

            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
            {
                if (Motor != null)
                    Motor.Stop();
                AttackTarget = null;
                return;
            }

            if (ServerAuth)
            {
                PredictLocal();
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
            TickSkills();
            if (ExtraHeal > 0f)
                Unit.Heal(6f * ExtraHeal * Time.deltaTime);

            if (AttackTarget != null && !AttackTarget.IsAlive)
                AttackTarget = null;

            if (AttackTarget == null)
                return;

            if (!StructureRules.InAttackRange(Unit, AttackTarget, Def.AttackRange))
            {
                Motor.MoveTo(StructureRules.ApproachPoint(Unit, AttackTarget, Def.AttackRange));
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
            if (Motor != null)
                Motor.Stop();
            if (ServerAuth && GameSession.I != null && GameSession.I.MatchClient != null)
                GameSession.I.MatchClient.SendRecall();
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
            for (var i = 0; i < SkillCd.Length; i++)
                SkillCd[i] = 0f;
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
                Motor.SnapToGround();
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
        }

        public void EnsureControlIfAlive()
        {
            if (Unit == null || !Unit.IsAlive || Motor == null || !Motor.Locked)
                return;
            Motor.Locked = false;
            Motor.StunUntil = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
        }

        void PredictLocal()
        {
            EnsureControlIfAlive();
            TickSkills();
            if (Unit.Stunned)
            {
                if (Motor != null)
                {
                    Motor.StunUntil = Unit.StunUntil;
                    Motor.Stop();
                }
                Recalling = false;
                return;
            }
            if (Recalling)
            {
                if (Motor != null)
                    Motor.Stop();
                RecallT += Time.deltaTime;
                if (RecallT >= RecallDuration)
                    CancelRecall();
                return;
            }
            if (Motor == null)
                return;
            if (AttackTarget != null && !AttackTarget.IsAlive)
                AttackTarget = null;
            if (AttackTarget == null)
                return;
            var dist = Motor.DistTo(AttackTarget.transform.position);
            if (dist > Def.AttackRange)
                Motor.MoveTo(AttackTarget.transform.position);
            else
            {
                Motor.Stop();
                Motor.Face(AttackTarget.transform.position);
            }
        }

        void TickSkills()
        {
            for (var i = 0; i < SkillCd.Length; i++)
                SkillCd[i] -= Time.deltaTime;
        }

        public bool TryCastSkill()
        {
            return TryCastSkill(0, AttackTarget, transform.position);
        }

        public bool TryCastSkill(int slot, CombatUnit target, Vector3 ground)
        {
            if (!SlotReady(slot))
                return false;
            var def = Ability(slot);
            var rank = Progress != null ? Progress.RankOf(slot) : 0;
            if (def == null || rank < 1)
                return false;

            if (def.Targeting == AbilityTargeting.NeedTarget)
            {
                if (target == null || !target.IsAlive || DistFlat(target.transform.position) > def.Rng(rank))
                    target = NearestEnemy(def.Rng(rank));
                if (target == null)
                    return false;
            }

            if (def.Targeting == AbilityTargeting.Ground)
                ground = ClampGround(ground, def.Rng(rank));

            CancelRecall();
            SkillCd[slot] = def.Cd(rank);
            FaceForSkill(def, target, ground);

            if (ServerAuth)
            {
                AbilityCaster.PlayFx(this, def, ground);
                if (GameSession.I != null && GameSession.I.MatchClient != null)
                    GameSession.I.MatchClient.SendSkill(transform.eulerAngles.y, slot);
            }
            else
                AbilityCaster.Execute(this, def, rank, target, ground);

            return true;
        }

        public bool TryUpgrade(int slot)
        {
            return Progress != null && Progress.TryUpgrade(slot);
        }

        void FaceForSkill(AbilityDef def, CombatUnit target, Vector3 ground)
        {
            Vector3 to;
            if (def.Targeting == AbilityTargeting.Ground)
                to = ground - transform.position;
            else if (target != null && target.IsAlive)
                to = target.transform.position - transform.position;
            else
                return;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(to);
        }

        Vector3 ClampGround(Vector3 ground, float range)
        {
            var to = ground - transform.position;
            to.y = 0f;
            if (to.magnitude > range && range > 0.1f)
                ground = transform.position + to.normalized * range;
            ground.y = 0f;
            return ground;
        }

        float DistFlat(Vector3 world)
        {
            var d = world - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        CombatUnit NearestEnemy(float radius)
        {
            CombatUnit best = null;
            var bestSq = radius * radius;
            var origin = transform.position;
            foreach (var u in CombatUnit.All)
            {
                if (u == null || Unit == null || !Unit.IsEnemy(u))
                    continue;
                var d = u.transform.position - origin;
                d.y = 0f;
                var sq = d.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = u;
                }
            }
            return best;
        }

        public void DebugResetCds()
        {
            for (var i = 0; i < SkillCd.Length; i++)
                SkillCd[i] = 0f;
        }

        public bool TryBuy(ItemDef item)
        {
            if (item == null || BattleRuntime.I == null || Unit == null)
                return false;
            if (Items.Count >= MaxItems)
                return false;
            if (!FoldMapBuilder.InFountain(transform.position, Unit.Team))
                return false;
            if (BattleRuntime.I.Gold < item.Cost)
                return false;
            if (ServerAuth && Time.unscaledTime < _buyLockUntil)
                return false;

            Items.Add(item.Id);
            BattleRuntime.I.Gold -= item.Cost;
            ApplyItems();
            if (ServerAuth)
            {
                _buyLockUntil = Time.unscaledTime + 0.45f;
                if (GameSession.I != null && GameSession.I.MatchClient != null)
                    GameSession.I.MatchClient.SendBuy(item.Id);
            }
            return true;
        }

        public void ApplyItemsCsv(string csv)
        {
            if (ServerAuth && Time.unscaledTime < _buyLockUntil)
                return;
            var next = ParseItemCsv(csv);
            if (SameItems(next))
                return;
            Items.Clear();
            Items.AddRange(next);
            ApplyItems();
        }

        static List<string> ParseItemCsv(string csv)
        {
            var list = new List<string>(6);
            if (string.IsNullOrEmpty(csv))
                return list;
            var parts = csv.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var id = parts[i].Trim();
                if (id.Length > 0)
                    list.Add(id);
            }
            return list;
        }

        bool SameItems(List<string> other)
        {
            if (other == null || other.Count != Items.Count)
                return false;
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i] != other[i])
                    return false;
            }
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
    }
}
