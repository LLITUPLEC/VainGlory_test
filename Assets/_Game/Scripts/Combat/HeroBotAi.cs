using UnityEngine;

namespace Ashfold
{
    /// <summary>
    /// Бот: пуш мида, защита кристалла.
    /// Отход при &lt;30% HP к союзной турели/кристаллу, затем recall на фонтан.
    /// </summary>
    public sealed class HeroBotAi : MonoBehaviour
    {
        public HeroCombat Combat;
        public CombatUnit Unit;
        public TeamId Team;
        public Vector3 Fountain;
        public Vector3 PushGoal;
        public int Gold = 80;
        public string PreferredItemId = "iron_edge";
        int _laneI = -1;

        const float RetreatHp = 0.30f;
        const float RecoverHp = 0.72f;
        const float NearBaseDist = 14f;
        const float CrystalDefendRadius = 16f;

        float _think;
        bool _respawning;
        float _spawnGrace;
        bool _retreating;

        static readonly string[] BuildCarry =
            { "iron_edge", "storm_charm", "iron_edge", "stoneplate", "stoneplate", "wardcloak" };
        static readonly string[] BuildTank =
            { "stoneplate", "stoneplate", "wardcloak", "wardcloak", "wardcloak", "iron_edge" };
        static readonly string[] BuildSupport =
            { "lifewell", "lifewell", "pulse_beacon", "stoneplate", "stoneplate", "wardcloak" };

        void Start()
        {
            if (Unit != null)
                Unit.Killed += OnDead;
        }

        void OnDestroy()
        {
            if (Unit != null)
                Unit.Killed -= OnDead;
        }

        void Update()
        {
            if (BattleRuntime.I != null && BattleRuntime.I.Frozen)
                return;
            if (Combat == null || Unit == null || _respawning || !Unit.IsAlive)
                return;

            if (FoldMapBuilder.InFountain(transform.position, Team))
                Unit.Heal(Unit.MaxHp * 0.22f * Time.deltaTime);

            if (_spawnGrace > 0f)
                _spawnGrace -= Time.deltaTime;

            if (Combat.Recalling)
                return;

            // Ретрит проверяем каждый кадр — иначе бот успевает умереть под турелью между тиками AI.
            UpdateRetreatState();
            if (_retreating)
            {
                DoRetreat();
                return;
            }

            _think -= Time.deltaTime;
            if (_think > 0f)
                return;
            _think = 0.2f;

            if (TryShop())
                return;

            if (_spawnGrace > 0f)
            {
                var threat = FindCrystalThreat(12f);
                if (threat != null)
                {
                    _spawnGrace = 0f;
                    Combat.CommandAttack(threat);
                    return;
                }
                Combat.CommandMove(FoldMapBuilder.NextCommitted(Team, transform.position, ref _laneI));
                return;
            }

            if (Combat.Progress != null)
                Combat.Progress.AutoSpend();
            TryCastReady();

            var target = FindTarget(Mathf.Max(Combat.Def.AttackRange + 2.5f, 10f));
            if (target != null)
            {
                Combat.CommandAttack(target);
                return;
            }

            var crystalThreat = FindCrystalThreat(CrystalDefendRadius + 8f);
            if (crystalThreat != null)
            {
                Combat.CommandAttack(crystalThreat);
                return;
            }

            // Не заходить под укреплённую турель без крипов.
            var lockedTurret = FindFortifiedEnemyTurret(CombatBalance.TurretRange + 1.2f);
            if (lockedTurret != null)
            {
                var away = transform.position - lockedTurret.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f)
                    away = Team == TeamId.Dawn ? Vector3.left : Vector3.right;
                var safe = lockedTurret.transform.position + away.normalized * (CombatBalance.TurretRange + 2.5f);
                Combat.CommandMove(safe);
                return;
            }

            Combat.CommandMove(FoldMapBuilder.NextCommitted(Team, transform.position, ref _laneI));
        }

        void TryCastReady()
        {
            if (Random.value > 0.55f)
                return;
            if (TryCastSlot(1))
                return;
            if (TryCastSlot(0))
                return;
            if (Random.value < 0.35f)
                TryCastSlot(2);
        }

        bool TryCastSlot(int slot)
        {
            if (!Combat.SlotReady(slot))
                return false;
            var def = Combat.Ability(slot);
            var rank = Combat.Progress != null ? Combat.Progress.RankOf(slot) : 1;
            if (def == null)
                return false;
            var range = def.Rng(Mathf.Max(1, rank));
            // Не тратить умения «в пустоту» на бегу — нужен враг в радиусе.
            var target = FindTarget(Mathf.Max(range, 1.5f));
            if (target == null)
                return false;
            Combat.CommandAttack(target);
            var ground = target.transform.position;
            return Combat.TryCastSkill(slot, target, ground);
        }

        void UpdateRetreatState()
        {
            if (_retreating)
            {
                if (FoldMapBuilder.InFountain(transform.position, Team) && Unit.Hp01 >= RecoverHp)
                    _retreating = false;
                return;
            }

            if (Unit.Hp01 < RetreatHp && !FoldMapBuilder.InFountain(transform.position, Team))
                _retreating = true;
        }

        void DoRetreat()
        {
            if (DistToFountain() <= NearBaseDist)
            {
                Combat.CommandMove(Fountain);
                return;
            }

            var safe = NearestAllyStructure();
            if (safe != null)
            {
                var behind = BehindStructure(safe);
                if (DistFlat(transform.position, behind) > 2.2f)
                {
                    Combat.CommandMove(behind);
                    return;
                }
            }

            Combat.TryRecall();
        }

        /// <summary>Точка ЗА союзной постройкой (со стороны фонтана), а не в её центре.</summary>
        Vector3 BehindStructure(CombatUnit structure)
        {
            var from = structure.transform.position;
            from.y = 0f;
            var toFountain = Fountain;
            toFountain.y = 0f;
            var dir = toFountain - from;
            if (dir.sqrMagnitude < 0.01f)
                dir = Team == TeamId.Dawn ? Vector3.left : Vector3.right;
            dir.Normalize();
            var back = StructureRules.BodyRadius(structure) + 3.8f;
            var p = from + dir * back;
            p.y = transform.position.y;
            return p;
        }

        CombatUnit NearestAllyStructure()
        {
            CombatUnit bestTurret = null;
            CombatUnit bestAny = null;
            var bestTurretSq = float.MaxValue;
            var bestAnySq = float.MaxValue;
            var origin = transform.position;
            foreach (var u in CombatUnit.All)
            {
                if (u == null || !u.IsAlive || u.Team != Team || !u.IsStructure)
                    continue;
                var d = u.transform.position - origin;
                d.y = 0f;
                var sq = d.sqrMagnitude;
                if (sq < bestAnySq)
                {
                    bestAnySq = sq;
                    bestAny = u;
                }
                if (u.IsTurret && sq < bestTurretSq)
                {
                    bestTurretSq = sq;
                    bestTurret = u;
                }
            }
            return bestTurret != null ? bestTurret : bestAny;
        }

        float DistToFountain()
        {
            var a = transform.position;
            a.y = 0f;
            var b = Fountain;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        string[] ShopBuild()
        {
            if (Combat == null || Combat.Def == null)
                return BuildCarry;
            switch (Combat.Def.Role)
            {
                case HeroRole.Tank: return BuildTank;
                case HeroRole.Support: return BuildSupport;
                default: return BuildCarry;
            }
        }

        bool TryShop()
        {
            if (Combat.Items.Count >= HeroCombat.MaxItems)
                return false;
            if (!FoldMapBuilder.InFountain(transform.position, Team))
                return false;

            var build = ShopBuild();
            if (Combat.Items.Count >= build.Length)
                return false;

            var nextId = build[Combat.Items.Count];
            var item = GameContent.GetItem(nextId);
            if (item == null || Gold < item.Cost)
                return false;
            if (!Combat.TryBuyFree(item))
                return false;
            Gold -= item.Cost;
            return true;
        }

        CombatUnit FindTarget(float radius)
        {
            CombatUnit bestHeroThreat = null;
            CombatUnit bestHero = null;
            CombatUnit bestCreep = null;
            CombatUnit bestStruct = null;
            var bestThreatSq = radius * radius;
            var bestHeroSq = radius * radius;
            var bestCreepSq = radius * radius;
            var bestStructSq = radius * radius;
            var origin = transform.position;
            var crystal = AlliedCrystal();

            foreach (var u in CombatUnit.All)
            {
                if (!Unit.IsEnemy(u))
                    continue;
                var d = u.transform.position - origin;
                d.y = 0f;
                var sq = d.sqrMagnitude;
                if (sq > radius * radius)
                    continue;

                if (u.IsHero)
                {
                    var threatensCrystal = crystal != null && DistFlat(u.transform.position, crystal.transform.position) <= CrystalDefendRadius;
                    if (threatensCrystal && sq < bestThreatSq)
                    {
                        bestThreatSq = sq;
                        bestHeroThreat = u;
                    }
                    else if (sq < bestHeroSq)
                    {
                        bestHeroSq = sq;
                        bestHero = u;
                    }
                }
                else if (u.IsStructure)
                {
                    if (!StructureRules.CanHurt(u))
                        continue;
                    // Не лезть под укреплённую турель без крипов.
                    if (u.IsTurret && StructureRules.TurretFortified(u))
                        continue;
                    if (sq < bestStructSq)
                    {
                        bestStructSq = sq;
                        bestStruct = u;
                    }
                }
                else if (sq < bestCreepSq)
                {
                    bestCreepSq = sq;
                    bestCreep = u;
                }
            }

            if (bestHeroThreat != null)
                return bestHeroThreat;
            if (bestHero != null)
                return bestHero;
            if (bestCreep != null)
                return bestCreep;
            return bestStruct;
        }

        CombatUnit FindFortifiedEnemyTurret(float radius)
        {
            CombatUnit best = null;
            var bestSq = radius * radius;
            var origin = transform.position;
            foreach (var u in CombatUnit.All)
            {
                if (u == null || !u.IsAlive || !u.IsTurret || !Unit.IsEnemy(u))
                    continue;
                if (!StructureRules.TurretFortified(u))
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

        CombatUnit FindCrystalThreat(float radiusFromCrystal)
        {
            var crystal = AlliedCrystal();
            if (crystal == null || !crystal.IsAlive)
                return null;

            CombatUnit best = null;
            var bestSq = radiusFromCrystal * radiusFromCrystal;
            foreach (var u in CombatUnit.All)
            {
                if (!u.IsHero || !Unit.IsEnemy(u))
                    continue;
                var d = u.transform.position - crystal.transform.position;
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

        CombatUnit AlliedCrystal()
        {
            if (BattleRuntime.I == null)
                return null;
            return Team == TeamId.Dawn ? BattleRuntime.I.CrystalDawn : BattleRuntime.I.CrystalDusk;
        }

        static float DistFlat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void OnDead(CombatUnit victim, CombatUnit killer)
        {
            if (_respawning)
                return;
            _retreating = false;
            StartCoroutine(Respawn());
        }

        System.Collections.IEnumerator Respawn()
        {
            _respawning = true;
            Combat.BeginDeathLock();

            var wait = RespawnRules.DurationSeconds();
            if (Unit != null)
                Unit.RespawnLeft = wait;
            while (Unit != null && Unit.RespawnLeft > 0f)
            {
                Unit.RespawnLeft -= Time.deltaTime;
                yield return null;
            }

            Combat.ReviveAt(Fountain);
            if (Unit != null)
                Unit.RespawnLeft = 0f;
            _laneI = -1;
            _spawnGrace = 3.5f;
            _retreating = false;
            _respawning = false;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;
            Gold += amount;
            if (Unit != null && MatchStatsTracker.I != null)
                MatchStatsTracker.I.AddGoldEarned(Unit, amount);
        }
    }
}
