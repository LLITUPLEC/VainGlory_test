using UnityEngine;

namespace Ashfold
{
    /// <summary>Бег / атака / idle. У босса на месте: Idle ~5с, затем один каст, снова Idle.</summary>
    public sealed class UnitAnim : MonoBehaviour
    {
        public Animator Animator;
        public TapMoveMotor Motor;

        const float IdleHold = 5f;

        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int CastHash = Animator.StringToHash("Cast");
        static readonly int MovingHash = Animator.StringToHash("Moving");
        static readonly int IdleHash = Animator.StringToHash("Idle");

        bool _resolved;
        bool _hasIdle;
        bool _hasMoving;
        bool _hasCast;
        float _idleHold;

        public void PlayAttack()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();
            if (Animator == null || Animator.runtimeAnimatorController == null)
                return;
            Resolve();
            Animator.speed = 1f;
            if (_hasCast)
                Animator.ResetTrigger(CastHash);
            Animator.ResetTrigger(AttackHash);
            Animator.SetTrigger(AttackHash);
            _idleHold = 0f;
        }

        void LateUpdate()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();
            if (Animator == null || Animator.runtimeAnimatorController == null)
                return;
            Resolve();
            var st = Animator.GetCurrentAnimatorStateInfo(0);
            var attacking = st.IsTag("Attack") || st.IsName("Attack");
            var casting = st.IsTag("Cast") || st.IsName("Cast");
            var busy = attacking || casting || Animator.IsInTransition(0);
            var moving = Motor != null && Motor.HasOrder && Motor.CanMove;
            if (_hasIdle)
            {
                Animator.speed = 1f;
                if (_hasMoving)
                    Animator.SetBool(MovingHash, moving && !busy);
                TickIdleCast(moving, busy, casting);
                return;
            }
            Animator.speed = moving || busy ? 1f : 0f;
        }

        void TickIdleCast(bool moving, bool busy, bool casting)
        {
            if (!_hasCast)
                return;
            if (moving || busy)
            {
                if (moving && !casting)
                    Animator.ResetTrigger(CastHash);
                _idleHold = 0f;
                return;
            }
            _idleHold += Time.deltaTime;
            if (_idleHold < IdleHold)
                return;
            _idleHold = 0f;
            Animator.ResetTrigger(CastHash);
            Animator.SetTrigger(CastHash);
        }

        void Resolve()
        {
            if (_resolved || Animator == null)
                return;
            _resolved = true;
            Animator.applyRootMotion = false;
            foreach (var p in Animator.parameters)
            {
                if (p.nameHash == MovingHash)
                    _hasMoving = true;
                if (p.nameHash == CastHash)
                    _hasCast = true;
            }
            _hasIdle = Animator.HasState(0, IdleHash);
        }
    }
}
