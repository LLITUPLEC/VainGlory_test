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
        public float MagResist;
        public int NetId;
        public System.Action Damaged;
        public float LastHeroCombatAt;
        public const float HeroCombatReveal = 2f;
        public CombatUnit LastHitBy;
        public float LastHitAt;
        public float SlowUntil;
        public float SlowFactor;

        public bool DisableOnDeath = true;

        public bool IsAlive => Hp > 0f && isActiveAndEnabled;
        public bool Stunned => Time.time < StunUntil;
        public float Hp01 => MaxHp <= 0f ? 0f : Mathf.Clamp01(Hp / MaxHp);
        public bool InHeroCombat => LastHeroCombatAt > 0f && Time.time - LastHeroCombatAt < HeroCombatReveal;
        public float MoveMul => Time.time < SlowUntil ? Mathf.Clamp(1f - SlowFactor, 0.25f, 1f) : 1f;

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

        public static void PurgeDead()
        {
            for (var i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null)
                    All.RemoveAt(i);
            }
        }

        /// <summary>Лечение умений: живые герои-игроки своей команды. Не турели, не крипы, не боты.</summary>
        public static bool CanReceiveHeroHeal(CombatUnit healer, CombatUnit target)
        {
            if (healer == null || target == null || !target.IsAlive)
                return false;
            if (target.IsStructure || !target.IsHero || !target.IsPlayer)
                return false;
            return target.Team == healer.Team;
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

        public void ApplySlow(float seconds, float factor)
        {
            if (seconds <= 0f || factor <= 0f)
                return;
            SlowUntil = Mathf.Max(SlowUntil, Time.time + seconds);
            SlowFactor = Mathf.Max(SlowFactor, Mathf.Clamp01(factor));
        }

        public static void MarkHeroFight(CombatUnit a, CombatUnit b)
        {
            if (a == null || b == null || a == b)
                return;
            if (!a.IsHero || !b.IsHero)
                return;
            if (a.Team == b.Team)
                return;
            var now = Time.time;
            a.LastHeroCombatAt = now;
            b.LastHeroCombatAt = now;
        }

        static bool NetBattle()
        {
            return GameSession.I != null && GameSession.I.Match != null && GameSession.I.Match.IsNetworked;
        }

        public void ApplyDamage(float amount, CombatUnit source)
        {
            ApplyDamage(amount, source, DamageKind.Physical);
        }

        public void ApplyDamage(float amount, CombatUnit source, DamageKind kind)
        {
            if (!IsAlive || amount <= 0f)
                return;
            MarkHeroFight(this, source);
            if (source != null)
            {
                LastHitBy = source;
                LastHitAt = Time.time;
            }
            var resist = kind == DamageKind.Magical ? MagResist : Resist;
            amount *= 1f - Mathf.Clamp01(resist);
            Hp -= amount;
            Damaged?.Invoke();
            if (!NetBattle())
                DamagePopup.TryShow(this, source, amount);
            if (Hp > 0f)
                return;
            Hp = 0f;
            Killed?.Invoke(this, source);
            GrantRewards(source);
            if (DisableOnDeath && !(IsHero && IsPlayer))
                gameObject.SetActive(false);
        }

        void GrantRewards(CombatUnit source)
        {
            if (source == null)
                return;
            if (Bounty > 0)
            {
                if (source.IsPlayer && BattleRuntime.I != null)
                {
                    BattleRuntime.I.AddGold(Bounty);
                    if (!IsHero && !IsStructure && !NetBattle())
                        DamagePopup.TryShowGold(this, Bounty);
                }
                else
                {
                    var bot = source.GetComponent<HeroBotAi>();
                    if (bot != null)
                        bot.AddGold(Bounty);
                }
            }
            var xp = HeroRules.XpForKill(this);
            if (xp <= 0)
                return;
            var prog = source.GetComponent<HeroProgression>();
            if (prog != null)
                prog.AddXp(xp);
        }
    }
}
