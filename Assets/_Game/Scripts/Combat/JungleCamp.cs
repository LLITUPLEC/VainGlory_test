using System.Collections;
using UnityEngine;

namespace Ashfold
{
    public sealed class JungleCamp : MonoBehaviour
    {
        public CombatUnit Unit;
        public Vector3 Home;
        public float Respawn = 18f;

        void Awake()
        {
            Home = transform.position;
        }

        public void Bind()
        {
            Unit.Killed += OnKilled;
        }

        void OnKilled(CombatUnit victim, CombatUnit killer)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(Respawn);
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = true;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
            transform.position = Home;
            Unit.Hp = Unit.MaxHp;
        }
    }
}
