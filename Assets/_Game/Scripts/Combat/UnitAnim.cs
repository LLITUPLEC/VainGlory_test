using UnityEngine;

namespace Ashfold
{
    /// <summary>Бег / атака по мотору и ближнему ИИ. Idle-клипа нет — на месте аниматор замирает.</summary>
    public sealed class UnitAnim : MonoBehaviour
    {
        public Animator Animator;
        public TapMoveMotor Motor;

        static readonly int AttackHash = Animator.StringToHash("Attack");

        void Awake()
        {
            if (Animator == null)
                Animator = GetComponentInChildren<Animator>();
            if (Animator != null)
                Animator.applyRootMotion = false;
        }

        public void PlayAttack()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null)
                return;
            Animator.speed = 1f;
            Animator.ResetTrigger(AttackHash);
            Animator.SetTrigger(AttackHash);
        }

        void LateUpdate()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null)
                return;
            var st = Animator.GetCurrentAnimatorStateInfo(0);
            var attacking = st.IsTag("Attack") || st.IsName("Attack") || Animator.IsInTransition(0);
            var moving = Motor != null && Motor.HasOrder && Motor.CanMove;
            Animator.speed = moving || attacking ? 1f : 0f;
        }
    }
}
