using UnityEngine;

namespace Ashfold
{
    public enum SandboxDummyKind
    {
        Post,
        Minion,
        Jungle,
        Hero,
        Kraken
    }

    public enum SandboxDummyAct
    {
        Idle,
        Attack,
        Flee
    }

    /// <summary>Цель на полигоне: стоит / бьёт игрока / убегает.</summary>
    public sealed class SandboxDummy : MonoBehaviour
    {
        public SandboxDummyAct Act;
        public bool Immortal;
        public CombatUnit Player;
        public float MeleeDamage = 18f;
        public float MeleeRange = 1.8f;

        CombatUnit _unit;
        TapMoveMotor _motor;
        HeroCombat _hero;
        float _cd;

        void Awake()
        {
            _unit = GetComponent<CombatUnit>();
            _motor = GetComponent<TapMoveMotor>();
            _hero = GetComponent<HeroCombat>();
        }

        void Update()
        {
            if (_unit == null || !_unit.IsAlive)
                return;
            if (Immortal && _unit.Hp < _unit.MaxHp)
                _unit.Hp = _unit.MaxHp;

            if (Player == null || !Player.IsAlive)
            {
                if (_motor != null)
                    _motor.Stop();
                return;
            }

            if (Act == SandboxDummyAct.Idle)
            {
                if (_hero != null)
                    _hero.AttackTarget = null;
                if (_motor != null)
                    _motor.Stop();
                return;
            }

            if (Act == SandboxDummyAct.Flee)
            {
                var away = transform.position - Player.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f)
                    away = Vector3.right;
                away.Normalize();
                var dest = transform.position + away * 10f;
                dest.x = Mathf.Clamp(dest.x, -FoldMapBuilder.HalfLength + 2f, FoldMapBuilder.HalfLength - 2f);
                dest.z = Mathf.Clamp(dest.z, -FoldMapBuilder.HalfWidth + 2f, FoldMapBuilder.HalfWidth - 2f);
                if (_hero != null)
                {
                    _hero.AttackTarget = null;
                    _hero.CommandMove(dest);
                }
                else if (_motor != null)
                    _motor.MoveTo(dest);
                return;
            }

            if (_hero != null)
            {
                _hero.CommandAttack(Player);
                return;
            }

            var to = Player.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > MeleeRange)
            {
                if (_motor != null)
                    _motor.MoveTo(Player.transform.position);
                return;
            }

            if (_motor != null)
            {
                _motor.Stop();
                _motor.Face(Player.transform.position);
            }
            _cd -= Time.deltaTime;
            if (_cd > 0f)
                return;
            _cd = 1.05f;
            Player.ApplyDamage(MeleeDamage, _unit);
        }
    }
}
