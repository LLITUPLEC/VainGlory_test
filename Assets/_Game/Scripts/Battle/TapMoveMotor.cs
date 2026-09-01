using UnityEngine;

namespace Ashfold
{
    public sealed class TapMoveMotor : MonoBehaviour
    {
        public float Speed = 8f;
        public float GroundY = 1.35f;
        public Vector3 Destination;
        public bool HasOrder;
        public float StunUntil;
        public bool Locked;

        static readonly Collider[] DestHits = new Collider[16];
        static readonly Collider[] FromHits = new Collider[16];

        public bool CanMove => !Locked && Time.time >= StunUntil && enabled && isActiveAndEnabled
            && (BattleRuntime.I == null || !BattleRuntime.I.InPrep);

        public void MoveTo(Vector3 world)
        {
            world.x = Mathf.Clamp(world.x, -FoldMapBuilder.HalfLength - 2f, FoldMapBuilder.HalfLength + 2f);
            world.z = Mathf.Clamp(world.z, -FoldMapBuilder.HalfWidth, FoldMapBuilder.HalfWidth);
            world.y = GroundY;
            Destination = world;
            HasOrder = true;
        }

        public void Stop()
        {
            HasOrder = false;
        }

        public float DistTo(Vector3 world)
        {
            var a = transform.position;
            a.y = 0f;
            world.y = 0f;
            return Vector3.Distance(a, world);
        }

        public void Face(Vector3 world)
        {
            world.y = transform.position.y;
            var d = world - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 16f * Time.deltaTime);
        }

        void Update()
        {
            if (!CanMove || !HasOrder)
                return;

            var from = transform.position;
            var next = Vector3.MoveTowards(from, Destination, Speed * MoveMul() * Time.deltaTime);
            next = ResolveSolid(from, next);
            transform.position = next;
            Face(Destination);
            if ((transform.position - Destination).sqrMagnitude < 0.04f)
                HasOrder = false;
            else if ((next - from).sqrMagnitude < 0.00001f)
                HasOrder = false;
        }

        Vector3 ResolveSolid(Vector3 from, Vector3 desired)
        {
            desired.y = GroundY;
            if (CanStand(desired, from))
                return desired;
            var alongX = new Vector3(desired.x, GroundY, from.z);
            if (CanStand(alongX, from))
                return alongX;
            var alongZ = new Vector3(from.x, GroundY, desired.z);
            if (CanStand(alongZ, from))
                return alongZ;
            return from;
        }

        bool CanStand(Vector3 pos, Vector3 from)
        {
            CapsuleAt(pos, out var p1, out var p2, out var radius);
            var n = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, DestHits, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var col = DestHits[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                    continue;
                if (!BlocksMove(col))
                    continue;
                if (!OverlapsPoint(col, from))
                    return false;
            }
            return true;
        }

        bool OverlapsPoint(Collider col, Vector3 pos)
        {
            CapsuleAt(pos, out var p1, out var p2, out var radius);
            var n = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, FromHits, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                if (FromHits[i] == col)
                    return true;
            }
            return false;
        }

        static bool BlocksMove(Collider col)
        {
            var unit = col.GetComponentInParent<CombatUnit>();
            if (unit == null)
                return true;
            return unit.IsStructure && unit.IsAlive;
        }

        void CapsuleAt(Vector3 pos, out Vector3 p1, out Vector3 p2, out float radius)
        {
            var cap = GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                var ls = transform.lossyScale;
                radius = Mathf.Max(0.12f, cap.radius * Mathf.Max(ls.x, ls.z) * 0.92f);
                var height = Mathf.Max(cap.height * ls.y, radius * 2f);
                var center = pos + Vector3.Scale(cap.center, ls);
                var half = Mathf.Max(0f, height * 0.5f - radius);
                p1 = center + Vector3.up * half;
                p2 = center - Vector3.up * half;
                return;
            }
            radius = 0.35f;
            p1 = pos + Vector3.up * 0.55f;
            p2 = pos + Vector3.up * 0.08f;
        }

        float MoveMul()
        {
            var unit = GetComponent<CombatUnit>();
            return unit != null ? unit.MoveMul : 1f;
        }
    }
}
