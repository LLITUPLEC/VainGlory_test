using UnityEngine;

namespace Ashfold
{
    /// <summary>Куст: враги внутри невидимы, пока союзник не зашёл в тот же куст.</summary>
    public sealed class BrushZone : MonoBehaviour
    {
        public static BrushZone[] All = System.Array.Empty<BrushZone>();

        public float Radius = 3.2f;

        void OnEnable()
        {
            var list = new System.Collections.Generic.List<BrushZone>(All) { this };
            All = list.ToArray();
        }

        void OnDisable()
        {
            var list = new System.Collections.Generic.List<BrushZone>(All);
            list.Remove(this);
            All = list.ToArray();
        }

        public bool Contains(Vector3 world)
        {
            world.y = 0f;
            var p = transform.position;
            p.y = 0f;
            return (world - p).sqrMagnitude <= Radius * Radius;
        }

        public static BrushZone FindAt(Vector3 world)
        {
            foreach (var b in All)
            {
                if (b != null && b.Contains(world))
                    return b;
            }
            return null;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 0.85f, 0.35f, 0.22f);
            Gizmos.DrawSphere(transform.position, Radius);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
#endif
    }
}
