using UnityEngine;

namespace Ashfold
{
    /// <summary>Уровень в бою (1–12), очки умений. Как VG: ульта с 6/9/12, A/B до 5, C до 3.</summary>
    public sealed class HeroProgression : MonoBehaviour
    {
        public int Level = 1;
        public int Xp;
        public int Unspent = 1;
        public readonly int[] Rank = new int[HeroRules.SlotCount];
        float _passive;

        void Update()
        {
            var unit = GetComponent<CombatUnit>();
            if (unit == null || !unit.IsAlive || Level >= HeroRules.MaxLevel)
                return;
            if (BattleRuntime.I != null && BattleRuntime.I.Frozen)
                return;
            _passive += Time.deltaTime;
            if (_passive < 1f)
                return;
            _passive = 0f;
            AddXp(HeroRules.PassiveXpPerSecond);
        }

        public int RankOf(int slot)
        {
            if (slot < 0 || slot >= Rank.Length)
                return 0;
            return Rank[slot];
        }

        public bool CanUpgrade(int slot)
        {
            if (Unspent < 1 || slot < 0 || slot >= HeroRules.SlotCount)
                return false;
            var rank = Rank[slot];
            if (rank >= HeroRules.MaxRank(slot))
                return false;
            if (slot == (int)AbilitySlot.C)
            {
                var need = HeroRules.UltUnlockLevel[Mathf.Clamp(rank, 0, HeroRules.UltUnlockLevel.Length - 1)];
                if (Level < need)
                    return false;
            }
            return true;
        }

        public bool TryUpgrade(int slot)
        {
            if (!CanUpgrade(slot))
                return false;
            Rank[slot]++;
            Unspent--;
            return true;
        }

        public void AddXp(int amount)
        {
            if (amount <= 0 || Level >= HeroRules.MaxLevel)
                return;
            Xp += amount;
            while (Level < HeroRules.MaxLevel && Xp >= HeroRules.XpToReachLevel[Level + 1])
            {
                Level++;
                Unspent++;
            }
        }

        public int XpIntoLevel()
        {
            var cur = HeroRules.XpToReachLevel[Level];
            return Mathf.Max(0, Xp - cur);
        }

        public int XpForNext()
        {
            if (Level >= HeroRules.MaxLevel)
                return 0;
            return HeroRules.XpToReachLevel[Level + 1] - HeroRules.XpToReachLevel[Level];
        }

        /// <summary>Полигон: все умения на максимум, уровень 12.</summary>
        public void DebugMaxOut()
        {
            Level = HeroRules.MaxLevel;
            Xp = HeroRules.XpToReachLevel[HeroRules.MaxLevel];
            Unspent = 0;
            Rank[0] = HeroRules.MaxRankAB;
            Rank[1] = HeroRules.MaxRankAB;
            Rank[2] = HeroRules.MaxRankUlt;
        }

        public void DebugFresh()
        {
            Level = 1;
            Xp = 0;
            Unspent = 1;
            for (var i = 0; i < Rank.Length; i++)
                Rank[i] = 0;
        }

        /// <summary>Боты: A, потом ульта когда можно, потом B.</summary>
        public void AutoSpend()
        {
            var guard = 8;
            while (Unspent > 0 && guard-- > 0)
            {
                if (CanUpgrade((int)AbilitySlot.A))
                    TryUpgrade((int)AbilitySlot.A);
                else if (CanUpgrade((int)AbilitySlot.C))
                    TryUpgrade((int)AbilitySlot.C);
                else if (CanUpgrade((int)AbilitySlot.B))
                    TryUpgrade((int)AbilitySlot.B);
                else
                    break;
            }
        }
    }
}
