using UnityEngine;

namespace Ashfold
{
    /// <summary>Слот умения как в VG: A / B / ульта.</summary>
    public enum AbilitySlot
    {
        A = 0,
        B = 1,
        C = 2
    }

    /// <summary>Как игрок выбирает цель. Это не «что делает умение».</summary>
    public enum AbilityTargeting
    {
        Instant,
        NeedTarget,
        Direction,
        Ground
    }

    public enum DamageKind
    {
        Physical,
        Magical
    }

    /// <summary>Каркас эффекта. Новые умения = новый kind + данные, без правок HeroCombat.Update.</summary>
    public enum AbilityEffect
    {
        None,
        ConeStun,
        Skillshot,
        NovaHealHarm,
        TargetDamageSlow,
        GroundBurst
    }

    public static class HeroRules
    {
        public const int MaxLevel = 12;
        public const int MaxRankAB = 5;
        public const int MaxRankUlt = 3;
        public const int SlotCount = 3;

        public static readonly int[] UltUnlockLevel = { 6, 9, 12 };

        public static readonly int[] XpToReachLevel =
        {
            0,
            0,
            180,
            420,
            720,
            1080,
            1500,
            1980,
            2520,
            3120,
            3780,
            4500,
            5280
        };

        public static int MaxRank(int slot)
        {
            return slot == (int)AbilitySlot.C ? MaxRankUlt : MaxRankAB;
        }

        /// <summary>Пассивный XP как в VG: уровень растёт и без киллов.</summary>
        public const int PassiveXpPerSecond = 9;

        public static int XpForKill(CombatUnit victim)
        {
            if (victim == null || victim.IsStructure)
                return 0;
            if (victim.IsHero)
                return 140;
            if (victim.Team == TeamId.Neutral)
                return 55;
            return 28;
        }
    }
}
