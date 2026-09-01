using UnityEngine;

namespace Ashfold
{
    public sealed class TurretAi : MonoBehaviour
    {
        public CombatUnit Unit;
        public float Damage = CombatBalance.TurretDamage;
        public float Range = CombatBalance.TurretRange;
        public float Interval = CombatBalance.TurretInterval;
        float _cd;
        CombatUnit _target;

        void Update()
        {
            if (Unit == null || !Unit.IsAlive)
                return;
            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
                return;
            _cd -= Time.deltaTime;
            if (_target != null && (!_target.IsAlive || Dist(_target) > Range + 0.4f))
                _target = null;
            if (_target == null)
                _target = AggroRules.Pick(Unit, Range, AggroKind.Turret);
            if (_target == null || _cd > 0f)
                return;
            _cd = Interval;
            Projectile.Spawn(Unit, _target, Damage, Unit.Team == TeamId.Dawn ? GameTheme.Teal : GameTheme.Crimson);
        }

        float Dist(CombatUnit u)
        {
            var d = u.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
