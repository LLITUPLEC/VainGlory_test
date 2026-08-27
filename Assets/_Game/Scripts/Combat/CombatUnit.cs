using System.Collections.Generic;
using UnityEngine;

namespace Ashfold
{
    public enum TeamId
    {
        Neutral = 0,
        Dawn = 1,
        Dusk = 2
    }

    public sealed class CombatUnit : MonoBehaviour
    {
        public static readonly List<CombatUnit> All = new List<CombatUnit>(64);

        public TeamId Team;
        public float MaxHp = 100f;
        public float Hp = 100f;
        public int Bounty = 10;
        public bool IsHero;
        public bool IsPlayer;
        public bool IsStructure;
        public string DisplayName = "Unit";
        public float StunUntil;
        public float GroundY = 1f;

        public float Resist;
        public int NetId;
        public System.Action Damaged;

        public bool DisableOnDeath = true;

        public bool IsAlive => Hp > 0f && isActiveAndEnabled;
        public bool Stunned => Time.time < StunUntil;
        public float Hp01 => MaxHp <= 0f ? 0f : Mathf.Clamp01(Hp / MaxHp);

        public System.Action<CombatUnit, CombatUnit> Killed;

        void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        public bool IsEnemy(CombatUnit other)
        {
            if (other == null || other == this || !other.IsAlive)
                return false;
            if (Team == TeamId.Neutral && other.Team == TeamId.Neutral)
                return false;
            if (Team == TeamId.Neutral || other.Team == TeamId.Neutral)
                return true;
            return Team != other.Team;
        }

        public void Heal(float amount)
        {
            if (!IsAlive)
                return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        public void Stun(float seconds)
        {
            StunUntil = Mathf.Max(StunUntil, Time.time + seconds);
        }

        public void ApplyDamage(float amount, CombatUnit source)
        {
            if (!IsAlive || amount <= 0f)
                return;
            amount *= 1f - Mathf.Clamp01(Resist);
            Hp -= amount;
            Damaged?.Invoke();
            if (Hp > 0f)
                return;
            Hp = 0f;
            Killed?.Invoke(this, source);
            if (source != null && Bounty > 0)
            {
                if (source.IsPlayer && BattleRuntime.I != null)
                    BattleRuntime.I.AddGold(Bounty);
                else
                {
                    var bot = source.GetComponent<HeroBotAi>();
                    if (bot != null)
                        bot.AddGold(Bounty);
                }
            }
            if (DisableOnDeath && !(IsHero && IsPlayer))
                gameObject.SetActive(false);
        }
    }
}
