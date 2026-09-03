using UnityEngine;

namespace Ashfold
{
    public sealed class BattleRuntime : MonoBehaviour
    {
        public static BattleRuntime I { get; private set; }

        public int Gold = 80;
        public int Kills;
        public int Deaths;
        public CombatUnit Player;
        public CombatUnit CrystalDawn;
        public CombatUnit CrystalDusk;
        public bool MatchOver;
        public float MatchTime;
        public float Countdown;
        public const float CountdownSeconds = 10f;
        /// <summary>PvP: часы и престарт ведёт сервер через снапшоты.</summary>
        public bool NetClock;

        public bool InPrep => Countdown > 0f;
        public bool Frozen => MatchOver || InPrep;

        public void BeginCountdown(float seconds = CountdownSeconds)
        {
            Countdown = Mathf.Max(0.05f, seconds);
        }

        void Awake()
        {
            I = this;
        }

        void Update()
        {
            if (MatchOver || NetClock)
                return;
            if (Countdown > 0f)
            {
                Countdown -= Time.deltaTime;
                if (Countdown < 0f)
                    Countdown = 0f;
                return;
            }
            MatchTime += Time.deltaTime;
        }

        void OnDestroy()
        {
            if (I == this)
                I = null;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;
            Gold += amount;
            if (Player != null && MatchStatsTracker.I != null)
                MatchStatsTracker.I.AddGoldEarned(Player, amount);
        }

        public void RegisterKill(CombatUnit victim, CombatUnit killer)
        {
            if (killer != null && killer.IsPlayer && victim != null && !victim.IsStructure && victim.IsHero)
                Kills++;
        }
    }
}
