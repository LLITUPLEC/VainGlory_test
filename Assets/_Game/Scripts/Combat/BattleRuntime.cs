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

        void Awake()
        {
            I = this;
        }

        void Update()
        {
            if (!MatchOver)
                MatchTime += Time.deltaTime;
        }

        void OnDestroy()
        {
            if (I == this)
                I = null;
        }

        public void AddGold(int amount)
        {
            Gold += amount;
        }

        public void RegisterKill(CombatUnit victim, CombatUnit killer)
        {
            if (killer != null && killer.IsPlayer && victim != null && !victim.IsStructure && victim.IsHero)
                Kills++;
        }
    }
}
