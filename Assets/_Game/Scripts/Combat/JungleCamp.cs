using System.Collections;
using UnityEngine;

namespace Ashfold
{
    public sealed class JungleCamp : MonoBehaviour
    {
        public CombatUnit Unit;
        public Vector3 Home;
        public float Respawn = 18f;

        void Start()
        {
            GroundProbe.SitOnGround(transform);
            var motor = GetComponent<TapMoveMotor>();
            if (motor != null)
            {
                motor.Hover = Mathf.Max(0.02f, transform.position.y - GroundProbe.SurfaceY(transform.position));
                motor.SnapToGround();
            }
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
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform == transform && transform.Find("Visual") != null)
                    continue;
                r.enabled = true;
            }
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
            transform.position = Home;
            var motor = GetComponent<TapMoveMotor>();
            if (motor != null)
                motor.SnapToGround();
            Unit.Hp = Unit.MaxHp;
        }
    }
}
