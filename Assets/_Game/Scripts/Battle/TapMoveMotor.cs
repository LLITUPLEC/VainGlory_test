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

        public bool CanMove => !Locked && Time.time >= StunUntil && enabled && isActiveAndEnabled;

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

            var next = Vector3.MoveTowards(transform.position, Destination, Speed * Time.deltaTime);
            transform.position = next;
            Face(Destination);
            if ((transform.position - Destination).sqrMagnitude < 0.04f)
                HasOrder = false;
        }
    }
}
