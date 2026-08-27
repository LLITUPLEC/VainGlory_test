using UnityEngine;

namespace Ashfold
{
    public sealed class TurretAi : MonoBehaviour
    {
        public CombatUnit Unit;
        public float Damage = 85f;
        public float Range = 9f;
        public float Interval = 1.15f;
        float _cd;
        CombatUnit _target;

        void Update()
        {
            if (Unit == null || !Unit.IsAlive)
                return;
            _cd -= Time.deltaTime;
            if (_target != null && (!_target.IsAlive || Dist(_target) > Range + 0.4f))
                _target = null;
            if (_target == null)
                _target = Find();
            if (_target == null || _cd > 0f)
                return;
            _cd = Interval;
            Projectile.Spawn(Unit, _target, Damage, Unit.Team == TeamId.Dawn ? GameTheme.Teal : GameTheme.Crimson);
        }

        CombatUnit Find()
        {
            CombatUnit bestHero = null;
            CombatUnit bestOther = null;
            var bestHeroSq = Range * Range;
            var bestOtherSq = Range * Range;
            var origin = transform.position;
            foreach (var u in CombatUnit.All)
            {
                if (!Unit.IsEnemy(u))
                    continue;
                var d = u.transform.position - origin;
                d.y = 0f;
                var sq = d.sqrMagnitude;
                if (sq > Range * Range)
                    continue;
                if (u.IsHero && sq < bestHeroSq)
                {
                    bestHeroSq = sq;
                    bestHero = u;
                }
                else if (!u.IsHero && sq < bestOtherSq)
                {
                    bestOtherSq = sq;
                    bestOther = u;
                }
            }
            return bestHero != null ? bestHero : bestOther;
        }

        float Dist(CombatUnit u)
        {
            var d = u.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
