using UnityEngine;

namespace Ashfold
{
    /// <summary>
    /// Бот: пуш мида, защита кристалла (герои важнее построек),
    /// отход: рядом с базой — пешком; далеко — в куст и recall.
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

        const float RetreatHp = 0.32f;
        const float RecoverHp = 0.72f;
        const float NearBaseDist = 14f;
        const float CrystalDefendRadius = 16f;

        float _think;
        bool _bought;
        bool _respawning;
        float _spawnGrace;
        bool _retreating;
        Vector3 _recallSpot;
        bool _goingToBrush;

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

            // Фонтанный реген (иначе крутятся у базы с низким HP).
            if (FoldMapBuilder.InFountain(transform.position, Team))
                Unit.Heal(Unit.MaxHp * 0.22f * Time.deltaTime);

            if (_spawnGrace > 0f)
                _spawnGrace -= Time.deltaTime;

            // Recall крутится сам в HeroCombat — не сбиваем.
            if (Combat.Recalling)
                return;

            _think -= Time.deltaTime;
            if (_think > 0f)
                return;
            _think = 0.2f;

            if (TryShop())
                return;

            if (_spawnGrace > 0f)
            {
                // Если кристалл бьют — сразу защищаем, без «выбега на мид».
                var threat = FindCrystalThreat(12f);
                if (threat != null)
                {
                    _spawnGrace = 0f;
                    Combat.CommandAttack(threat);
                    return;
                }
                Combat.CommandMove(PushGoal);
                return;
            }

            UpdateRetreatState();
            if (_retreating)
            {
                DoRetreat();
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

            // Глобальная угроза кристалла даже вне обычного агро.
            var crystalThreat = FindCrystalThreat(CrystalDefendRadius + 8f);
            if (crystalThreat != null)
            {
                Combat.CommandAttack(crystalThreat);
                return;
            }

            Combat.CommandMove(PushGoal);
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
            var range = def.Rng(rank);
            var target = FindTarget(Mathf.Max(range, Combat.Def.AttackRange + 1f));
            if (def.Targeting == AbilityTargeting.NeedTarget || def.Targeting == AbilityTargeting.Ground)
            {
                target = FindTarget(range);
                if (target == null)
                    return false;
            }
            if (target != null)
                Combat.CommandAttack(target);
            var ground = target != null ? target.transform.position : Combat.transform.position + Combat.transform.forward * range * 0.6f;
            return Combat.TryCastSkill(slot, target, ground);
        }

        void UpdateRetreatState()
        {
            if (_retreating)
            {
                if (FoldMapBuilder.InFountain(transform.position, Team) && Unit.Hp01 >= RecoverHp)
                {
                    _retreating = false;
                    _goingToBrush = false;
                }
                return;
            }

            if (Unit.Hp01 < RetreatHp && !FoldMapBuilder.InFountain(transform.position, Team))
            {
                _retreating = true;
                _goingToBrush = DistToFountain() > NearBaseDist;
                if (_goingToBrush)
                    _recallSpot = NearestSafeBrush();
            }
        }

        void DoRetreat()
        {
            // Уже у базы — только пешком на пад, без recall.
            if (!_goingToBrush || DistToFountain() <= NearBaseDist)
            {
                _goingToBrush = false;
                Combat.CommandMove(Fountain);
                return;
            }

            // Далеко: дойти до куста вне линии, затем recall.
            var brush = BrushZone.FindAt(transform.position);
            var onLane = Mathf.Abs(transform.position.z) < 4.2f;
            if (brush != null && !onLane)
            {
                Combat.TryRecall();
                return;
            }

            Combat.CommandMove(_recallSpot);
        }

        Vector3 NearestSafeBrush()
        {
            var best = Fountain;
            var bestSq = float.MaxValue;
            var origin = transform.position;
            // Предпочитаем куст «назад» к своей базе (по X).
            var towardBase = Team == TeamId.Dawn ? -1f : 1f;

            foreach (var b in BrushZone.All)
            {
                if (b == null)
                    continue;
                var p = b.transform.position;
                // Не на миде.
                if (Mathf.Abs(p.z) < 4f)
                    continue;
                // Ближе к своей половине карты.
                if (Team == TeamId.Dawn && p.x > origin.x + 2f)
                    continue;
                if (Team == TeamId.Dusk && p.x < origin.x - 2f)
                    continue;

                var d = p - origin;
                d.y = 0f;
                // Небольшой бонус за направление к базе.
                var score = d.sqrMagnitude - towardBase * (p.x - origin.x) * 8f;
                if (score < bestSq)
                {
                    bestSq = score;
                    best = p;
                    best.y = 1.35f;
                }
            }

            // Fallback: сместиться с линии в сторону леса + назад.
            if (bestSq > 1e8f)
            {
                best = origin;
                best.z = origin.z >= 0f ? 8f : -8f;
                best.x += towardBase * 6f;
                best.y = 1.35f;
            }
            return best;
        }

        float DistToFountain()
        {
            var a = transform.position;
            a.y = 0f;
            var b = Fountain;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        bool TryShop()
        {
            if (_bought || Combat.Items.Count > 0)
                return false;
            if (!FoldMapBuilder.InFountain(transform.position, Team))
                return false;

            var item = GameContent.GetItem(PreferredItemId) ?? GameContent.Items[0];
            if (Gold < item.Cost)
                return false;
            if (!Combat.TryBuyFree(item))
                return false;
            Gold -= item.Cost;
            _bought = true;
            return true;
        }

        /// <summary>
        /// Приоритет: герои у союзного кристалла → герои → крипы → постройки.
        /// </summary>
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
            _goingToBrush = false;
            StartCoroutine(Respawn());
        }

        System.Collections.IEnumerator Respawn()
        {
            _respawning = true;
            Combat.BeginDeathLock();

            var wait = RespawnRules.DurationSeconds();
            yield return new WaitForSeconds(wait);

            Combat.ReviveAt(Fountain);
            _spawnGrace = 3.5f;
            _retreating = false;
            _goingToBrush = false;
            _respawning = false;
        }

        public void AddGold(int amount)
        {
            Gold += amount;
        }
    }
}
