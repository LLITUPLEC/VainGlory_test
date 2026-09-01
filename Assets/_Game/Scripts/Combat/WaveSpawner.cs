using UnityEngine;

namespace Ashfold
{
    public sealed class WaveSpawner : MonoBehaviour
    {
        public Transform Parent;
        public float FirstWave = 2f;
        public float Interval = 20f;
        float _next;

        void Start()
        {
            _next = FirstWave;
        }

        void Update()
        {
            if (BattleRuntime.I != null && BattleRuntime.I.Frozen)
                return;
            _next -= Time.deltaTime;
            if (_next > 0f)
                return;
            _next = Interval;
            SpawnTeam(TeamId.Dawn);
            SpawnTeam(TeamId.Dusk);
        }

        void SpawnTeam(TeamId team)
        {
            var goal = CombatBalance.MinionGoal(team);
            for (var i = 0; i < CombatBalance.WaveSize; i++)
            {
                var captain = i == CombatBalance.WaveSize - 1;
                UnitFactory.SpawnMinion(Parent, CombatBalance.MinionSpawn(team, i), team, goal, true, captain);
            }
        }
    }
}
