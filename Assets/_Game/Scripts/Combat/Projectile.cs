using UnityEngine;

namespace Ashfold
{
    public sealed class Projectile : MonoBehaviour
    {
        public CombatUnit Owner;
        public CombatUnit Target;
        public Vector3 Direction;
        public float Speed = 16f;
        public float Damage;
        public float Life = 1.2f;
        public bool Homing = true;
        public float HitRadius = 0.65f;

        float _t;

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= Life)
            {
                Destroy(gameObject);
                return;
            }

            if (Homing && Target != null && Target.IsAlive)
            {
                var to = Target.transform.position;
                to.y = transform.position.y;
                Direction = (to - transform.position).normalized;
            }

            transform.position += Direction * Speed * Time.deltaTime;

            if (Homing && Target != null && Target.IsAlive)
            {
                if (FlatSq(transform.position, Target.transform.position) <= HitRadius * HitRadius)
                {
                    Target.ApplyDamage(Damage, Owner);
                    Destroy(gameObject);
                }
                return;
            }

            foreach (var u in CombatUnit.All)
            {
                if (Owner == null || !Owner.IsEnemy(u))
                    continue;
                if (FlatSq(transform.position, u.transform.position) <= HitRadius * HitRadius)
                {
                    u.ApplyDamage(Damage, Owner);
                    Destroy(gameObject);
                    return;
                }
            }
        }

        static float FlatSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        public static void Spawn(CombatUnit owner, CombatUnit target, float damage, Color color)
        {
            Spawn(owner, target, damage, color, false);
        }

        public static void Spawn(CombatUnit owner, CombatUnit target, float damage, Color color, bool visualOnly)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bolt";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = owner.transform.position + Vector3.up * 1.15f;
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(color);
            var p = go.AddComponent<Projectile>();
            p.Owner = owner;
            p.Target = target;
            p.Damage = visualOnly ? 0f : damage;
            p.Homing = true;
            p.HitRadius = 1.15f;
            if (target != null)
            {
                var to = target.transform.position - owner.transform.position;
                to.y = 0f;
                p.Direction = to.sqrMagnitude > 0.01f ? to.normalized : owner.transform.forward;
            }
        }

        public static void SpawnSkillshot(CombatUnit owner, Vector3 dir, float damage, float speed, float life, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Skillshot";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = owner.transform.position + Vector3.up * 1.1f + dir.normalized * 0.8f;
            go.transform.localScale = Vector3.one * 0.45f;
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(color);
            var p = go.AddComponent<Projectile>();
            p.Owner = owner;
            p.Damage = damage;
            p.Homing = false;
            p.Speed = speed;
            p.Life = life;
            p.HitRadius = 0.9f;
            dir.y = 0f;
            p.Direction = dir.sqrMagnitude > 0.01f ? dir.normalized : owner.transform.forward;
        }
    }
}
