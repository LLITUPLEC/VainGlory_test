using UnityEngine;

namespace Ashfold
{
    public enum AggroKind
    {
        Lane,
        Jungle,
        Turret
    }

    /// <summary>
    /// Общий выбор цели. Лес держит лагерь, линия идёт к кристаллу,
    /// турель/крип перекидывают агро на героя, который бьёт союзного героя.
    /// </summary>
    public static class AggroRules
    {
        public const float AssistWindow = 3.2f;

        public static CombatUnit Pick(CombatUnit self, float radius, AggroKind kind)
        {
            if (self == null)
                return null;
            CombatUnit assist = null;
            CombatUnit bestUnit = null;
            CombatUnit bestHero = null;
            CombatUnit bestStruct = null;
            var assistSq = radius * radius;
            var unitSq = radius * radius;
            var heroSq = radius * radius;
            var structSq = radius * radius;
            var origin = self.transform.position;

            foreach (var u in CombatUnit.All)
            {
                if (!self.IsEnemy(u))
                    continue;
                var d = u.transform.position - origin;
                d.y = 0f;
                var sq = d.sqrMagnitude;
                if (sq > radius * radius)
                    continue;

                if (u.IsHero && HitsAlliedHero(self, u, radius + 4f) && sq < assistSq)
                {
                    assistSq = sq;
                    assist = u;
                }

                if (u.IsStructure)
                {
                    if (!StructureRules.CanHurt(u))
                        continue;
                    if (sq < structSq)
                    {
                        structSq = sq;
                        bestStruct = u;
                    }
                }
                else if (u.IsHero)
                {
                    if (sq < heroSq)
                    {
                        heroSq = sq;
                        bestHero = u;
                    }
                }
                else if (sq < unitSq)
                {
                    unitSq = sq;
                    bestUnit = u;
                }
            }

            if (assist != null)
                return assist;

            if (kind == AggroKind.Turret)
                return bestUnit != null ? bestUnit : bestHero;

            if (kind == AggroKind.Jungle)
                return bestHero != null ? bestHero : (bestUnit != null ? bestUnit : null);

            if (bestUnit != null)
                return bestUnit;
            if (bestHero != null)
                return bestHero;
            return bestStruct;
        }

        static bool HitsAlliedHero(CombatUnit self, CombatUnit enemy, float allyRadius)
        {
            if (enemy == null || !enemy.IsHero)
                return false;
            var origin = self.transform.position;
            foreach (var ally in CombatUnit.All)
            {
                if (ally == null || !ally.IsAlive || !ally.IsHero || ally.Team != self.Team)
                    continue;
                var d = ally.transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude > allyRadius * allyRadius)
                    continue;
                if (ally.LastHitBy == enemy && Time.time - ally.LastHitAt <= AssistWindow)
                    return true;
            }
            return false;
        }
    }
}
