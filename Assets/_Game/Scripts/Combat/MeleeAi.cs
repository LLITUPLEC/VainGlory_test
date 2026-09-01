using UnityEngine;

namespace Ashfold
{
    public sealed class MeleeAi : MonoBehaviour
    {
        public CombatUnit Unit;
        public TapMoveMotor Motor;
        public float Damage = 18f;
        public float Range = 1.8f;
        public float Aggro = 3.2f;
        public float Interval = 1.1f;
        public Vector3 LaneGoal;
        public bool RoamLane = true;

        Vector3 _home;
        float _cd;
        CombatUnit _target;

        void Start()
        {
            _home = transform.position;
        }

        void Update()
        {
            if (Unit == null || !Unit.IsAlive || Unit.Stunned)
            {
                if (Motor != null)
                    Motor.Stop();
                return;
            }
            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
            {
                if (Motor != null)
                    Motor.Stop();
                return;
            }

            _cd -= Time.deltaTime;
            if (!RoamLane && DistFlat(transform.position, _home) > 8f)
            {
                _target = null;
                Motor.MoveTo(_home);
                return;
            }

            if (_target != null && !_target.IsAlive)
                _target = null;
            if (_target == null)
                _target = AggroRules.Pick(Unit, Aggro, RoamLane ? AggroKind.Lane : AggroKind.Jungle);

            if (_target != null)
            {
                var dist = Motor.DistTo(_target.transform.position);
                if (dist > Range)
                {
                    Motor.MoveTo(_target.transform.position);
                    return;
                }

                Motor.Stop();
                Motor.Face(_target.transform.position);
                if (_cd > 0f)
                    return;
                _cd = Interval;
                var anim = GetComponent<UnitAnim>();
                if (anim != null)
                    anim.PlayAttack();
                _target.ApplyDamage(Damage, Unit);
                return;
            }

            if (RoamLane)
                Motor.MoveTo(LaneGoal);
            else if (DistFlat(transform.position, _home) > 0.3f)
                Motor.MoveTo(_home);
            else
                Motor.Stop();
        }

        static float DistFlat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
