using UnityEngine;

namespace Ashfold
{
    public sealed class TapMoveMotor : MonoBehaviour
    {
        public float Speed = 8f;
        public float Hover = 1.35f;
        public float GroundY = 1.35f;
        public Vector3 Destination;
        public bool HasOrder;
        public float StunUntil;
        public bool Locked;

        int _orbitSign;

        public bool CanMove => !Locked && Time.time >= StunUntil && enabled && isActiveAndEnabled
            && (BattleRuntime.I == null || !BattleRuntime.I.InPrep);

        public void MoveTo(Vector3 world)
        {
            world = FoldMapBuilder.ClampPlayable(world);
            world.y = GroundProbe.SurfaceY(world) + Hover;
            if (HasOrder && (Flat(Destination) - Flat(world)).sqrMagnitude < 0.09f)
            {
                Destination = world;
                return;
            }
            Destination = world;
            HasOrder = true;
        }

        public void Stop()
        {
            HasOrder = false;
            _orbitSign = 0;
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
            var d = world - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 16f * Time.deltaTime);
        }

        public void SnapToGround()
        {
            var p = transform.position;
            GroundY = GroundProbe.SurfaceY(p) + Hover;
            p.y = GroundY;
            transform.position = p;
            var unit = GetComponent<CombatUnit>();
            if (unit != null)
                unit.GroundY = GroundY;
        }

        void Update()
        {
            SnapToGround();
            if (!CanMove || !HasOrder)
                return;

            var from = transform.position;
            var dest = Destination;
            dest.y = GroundY;
            var step = Speed * MoveMul() * Time.deltaTime;
            var desired = Vector3.MoveTowards(from, dest, step);
            desired.y = GroundY;

            Vector3 next;
            if (CanStep(from, desired, out var hit))
            {
                next = desired;
                _orbitSign = 0;
            }
            else
                next = Slide(from, desired, dest, hit, step);

            next.y = GroundY;
            var moved = next - from;
            moved.y = 0f;
            transform.position = next;
            if (moved.sqrMagnitude > 0.00001f)
                Face(transform.position + moved);
            else
                Face(dest);

            if ((Flat(transform.position) - Flat(dest)).sqrMagnitude < 0.04f)
                Stop();
        }

        Vector3 Slide(Vector3 from, Vector3 desired, Vector3 dest, RaycastHit hit, float step)
        {
            var n = hit.normal;
            n.y = 0f;
            if (n.sqrMagnitude < 0.01f)
            {
                var into = dest - from;
                into.y = 0f;
                n = into.sqrMagnitude > 0.01f ? -into.normalized : Vector3.forward;
            }
            else
                n.Normalize();

            var tangent = Vector3.Cross(Vector3.up, n);
            if (tangent.sqrMagnitude < 0.01f)
                tangent = Vector3.right;
            tangent.Normalize();

            var progress = dest - from;
            progress.y = 0f;
            if (_orbitSign == 0)
                _orbitSign = Vector3.Dot(tangent, progress) >= 0f ? 1 : -1;
            tangent *= _orbitSign;

            var along = from + tangent * step;
            along.y = GroundY;
            if (CanStep(from, along, out _))
                return along;

            _orbitSign = -_orbitSign;
            along = from - tangent * step;
            along.y = GroundY;
            if (CanStep(from, along, out _))
                return along;

            return from;
        }

        bool CanStep(Vector3 from, Vector3 to, out RaycastHit block)
        {
            block = default;
            var delta = to - from;
            delta.y = 0f;
            var dist = delta.magnitude;
            if (dist < 0.0001f)
                return true;
            var dir = delta / dist;
            var probe = dist + 0.08f;
            var lifts = new[] { 0.18f, 0.55f, 1.05f };
            for (var i = 0; i < lifts.Length; i++)
            {
                var origin = from + Vector3.up * lifts[i] - dir * 0.12f;
                if (!Physics.Raycast(origin, dir, out var hit, probe + 0.12f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;
                if (!BlocksMove(hit.collider))
                    continue;
                block = hit;
                return false;
            }
            return true;
        }

        static bool BlocksMove(Collider col)
        {
            if (GroundProbe.IsGround(col))
                return false;
            var t = col.transform;
            while (t != null)
            {
                if (t.name.StartsWith("Stairs_"))
                    return false;
                t = t.parent;
            }
            var unit = col.GetComponentInParent<CombatUnit>();
            if (unit == null)
                return true;
            return unit.IsStructure && unit.IsAlive;
        }

        static Vector3 Flat(Vector3 p)
        {
            p.y = 0f;
            return p;
        }

        float MoveMul()
        {
            var unit = GetComponent<CombatUnit>();
            return unit != null ? unit.MoveMul : 1f;
        }
    }
}
