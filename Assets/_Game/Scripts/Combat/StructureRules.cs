using UnityEngine;

namespace Ashfold
{
    /// <summary>
    /// Vainglory-правила построек: блок 20% HP турели без крипов,
    /// кристалл закрыт, пока живы турели стороны.
    /// </summary>
    public static class StructureRules
    {
        public const float LockPortion = 0.2f;
        public const float UnlockRange = 9f;
        public const float TurretBody = 2.2f;
        public const float CrystalBody = 2.8f;
        public const float UnitBody = 0.4f;

        public static float BodyRadius(CombatUnit u)
        {
            if (u == null)
                return 0f;
            if (u.IsCrystal)
                return CrystalBody;
            if (u.IsTurret || u.IsStructure)
                return TurretBody;
            return UnitBody;
        }

        public static float CenterDist(CombatUnit a, CombatUnit b)
        {
            if (a == null || b == null)
                return float.PositiveInfinity;
            var d = a.transform.position - b.transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        public static bool InAttackRange(CombatUnit atk, CombatUnit tgt, float range)
        {
            return CenterDist(atk, tgt) <= range + BodyRadius(atk) + BodyRadius(tgt);
        }

        public static Vector3 ApproachPoint(CombatUnit atk, CombatUnit tgt, float range)
        {
            var from = atk.transform.position;
            var to = tgt.transform.position;
            from.y = 0f;
            to.y = 0f;
            var delta = to - from;
            var mag = delta.magnitude;
            var stop = BodyRadius(tgt) + Mathf.Max(0.35f, range * 0.85f);
            if (mag < 0.001f)
                return atk.transform.position;
            var p = to - delta / mag * stop;
            p.y = atk.transform.position.y;
            return p;
        }

        public static bool TurretFortified(CombatUnit turret)
        {
            if (turret == null || !turret.IsTurret || !turret.IsAlive)
                return false;
            return !EnemyLaneMinionNear(turret, UnlockRange);
        }

        public static bool CrystalOpen(TeamId team)
        {
            foreach (var u in CombatUnit.All)
            {
                if (u == null || !u.IsTurret || !u.IsAlive)
                    continue;
                if (u.Team == team)
                    return false;
            }
            return true;
        }

        public static bool CanHurt(CombatUnit target)
        {
            if (target == null || !target.IsAlive)
                return false;
            if (target.IsCrystal && !CrystalOpen(target.Team))
                return false;
            return true;
        }

        public static float FilterDamage(CombatUnit target, float amount)
        {
            if (amount <= 0f || target == null || !target.IsAlive)
                return 0f;
            if (target.IsCrystal && !CrystalOpen(target.Team))
                return 0f;
            if (target.IsTurret && TurretFortified(target))
            {
                var floor = target.MaxHp * (1f - LockPortion);
                if (target.Hp <= floor + 0.01f)
                    return 0f;
                return Mathf.Min(amount, target.Hp - floor);
            }
            return amount;
        }

        static bool EnemyLaneMinionNear(CombatUnit turret, float range)
        {
            var origin = turret.transform.position;
            var r2 = range * range;
            foreach (var u in CombatUnit.All)
            {
                if (u == null || !u.IsAlive || u.IsHero || u.IsStructure)
                    continue;
                if (u.Team == TeamId.Neutral || !turret.IsEnemy(u))
                    continue;
                var d = u.transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude <= r2)
                    return true;
            }
            return false;
        }
    }
}
