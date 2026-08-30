using System.Collections.Generic;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Исполняет эффект умения. Наведение уже решено снаружи.</summary>
    public static class AbilityCaster
    {
        public static void Execute(HeroCombat hero, AbilityDef def, int rank, CombatUnit target, Vector3 ground)
        {
            if (hero == null || def == null || rank < 1)
                return;
            var power = def.Pwr(rank) * (1f + hero.ExtraHeal);
            var range = def.Rng(rank);
            switch (def.Effect)
            {
                case AbilityEffect.ConeStun:
                    Cone(hero, power, range, def.Dur(rank), def.Damage);
                    break;
                case AbilityEffect.Skillshot:
                    Projectile.SpawnSkillshot(hero.Unit, hero.transform.forward, power, 22f, 0.7f, GameContent.HeroColor(hero.Def.Id));
                    break;
                case AbilityEffect.NovaHealHarm:
                    Nova(hero, power, range, def.Damage);
                    break;
                case AbilityEffect.TargetDamageSlow:
                    HitSlow(hero, target, power, def.Dur(rank), def.SlowAmt(rank), def.Damage);
                    break;
                case AbilityEffect.GroundBurst:
                    Burst(hero, ground, power, def.Dur(rank), def.SlowAmt(rank), def.Damage);
                    break;
            }
        }

        public static void PlayFx(HeroCombat hero, AbilityDef def, Vector3 ground)
        {
            if (hero == null || def == null)
                return;
            var color = GameContent.HeroColor(hero.Def != null ? hero.Def.Id : "");
            switch (def.Effect)
            {
                case AbilityEffect.Skillshot:
                    Projectile.SpawnSkillshot(hero.Unit, hero.transform.forward, 0f, 22f, 0.7f, color);
                    break;
                case AbilityEffect.GroundBurst:
                    FlashAt(ground, color, Mathf.Max(2.4f, def.Dur(Mathf.Max(1, hero.Progress != null ? hero.Progress.RankOf(2) : 1)) * 2f));
                    break;
                default:
                    FlashAt(hero.transform.position, color, def.Effect == AbilityEffect.NovaHealHarm ? def.Rng(1) * 2f : 2.2f);
                    break;
            }
        }

        static void Cone(HeroCombat hero, float power, float range, float stun, DamageKind kind)
        {
            var origin = hero.transform;
            foreach (var u in Snapshot())
            {
                if (u == null || !hero.Unit.IsEnemy(u))
                    continue;
                var to = u.transform.position - origin.position;
                to.y = 0f;
                if (to.magnitude > range)
                    continue;
                if (Vector3.Angle(origin.forward, to) > 55f)
                    continue;
                u.ApplyDamage(power, hero.Unit, kind);
                u.Stun(stun);
            }
            FlashAt(origin.position, GameTheme.Gold, 2.2f);
        }

        static void Nova(HeroCombat hero, float power, float range, DamageKind kind)
        {
            foreach (var u in Snapshot())
            {
                if (u == null || !u.IsAlive)
                    continue;
                var d = u.transform.position - hero.transform.position;
                d.y = 0f;
                if (d.magnitude > range)
                    continue;
                if (CombatUnit.CanReceiveHeroHeal(hero.Unit, u))
                    u.Heal(power);
                else if (hero.Unit.IsEnemy(u))
                    u.ApplyDamage(power * 0.55f, hero.Unit, kind);
            }
            FlashAt(hero.transform.position, GameTheme.Teal, range * 2f);
        }

        static void HitSlow(HeroCombat hero, CombatUnit target, float power, float duration, float slow, DamageKind kind)
        {
            if (target == null || !hero.Unit.IsEnemy(target))
                return;
            var color = GameContent.HeroColor(hero.Def != null ? hero.Def.Id : "");
            Projectile.Spawn(hero.Unit, target, 0f, color, true);
            target.ApplyDamage(power, hero.Unit, kind);
            if (slow > 0f && duration > 0f)
                target.ApplySlow(duration, slow);
        }

        static void Burst(HeroCombat hero, Vector3 ground, float power, float radius, float slow, DamageKind kind)
        {
            ground.y = 0f;
            foreach (var u in Snapshot())
            {
                if (u == null || !hero.Unit.IsEnemy(u))
                    continue;
                var p = u.transform.position;
                p.y = 0f;
                if ((p - ground).magnitude > radius)
                    continue;
                u.ApplyDamage(power, hero.Unit, kind);
                if (slow > 0f)
                    u.ApplySlow(1.2f, slow);
            }
            FlashAt(ground + Vector3.up * 0.4f, GameContent.HeroColor(hero.Def != null ? hero.Def.Id : ""), radius * 2f);
        }

        static List<CombatUnit> Snapshot()
        {
            return new List<CombatUnit>(CombatUnit.All);
        }

        static void FlashAt(Vector3 pos, Color color, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
            go.transform.position = pos + Vector3.up * 0.2f;
            go.transform.localScale = Vector3.one * Mathf.Max(1.2f, size);
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(new Color(color.r, color.g, color.b, 0.35f));
            Object.Destroy(go, 0.28f);
        }
    }
}
