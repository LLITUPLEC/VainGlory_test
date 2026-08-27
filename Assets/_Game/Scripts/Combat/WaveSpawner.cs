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
            if (BattleRuntime.I != null && BattleRuntime.I.MatchOver)
                return;
            _next -= Time.deltaTime;
            if (_next > 0f)
                return;
            _next = Interval;
            SpawnTeam(TeamId.Dawn, -32f, 1f);
            SpawnTeam(TeamId.Dusk, 32f, -1f);
        }

        void SpawnTeam(TeamId team, float x, float dir)
        {
            for (var i = 0; i < 3; i++)
            {
                var z = (i - 1) * 1.6f;
                UnitFactory.SpawnMinion(Parent, new Vector3(x, 0.7f, z), team, new Vector3(dir * 40f, 0.7f, 0f));
            }
        }
    }
}
