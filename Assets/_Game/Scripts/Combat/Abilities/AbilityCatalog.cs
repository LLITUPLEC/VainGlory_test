using UnityEngine;

namespace Ashfold
{
    public sealed class AbilityDef
    {
        public string Id;
        public string LocKey;
        public AbilityTargeting Targeting;
        public DamageKind Damage;
        public AbilityEffect Effect;
        public bool Ultimate;
        public float[] Cooldown;
        public float[] Power;
        public float[] Range;
        public float[] Duration;
        public float[] Slow;

        public int MaxRank => Ultimate ? HeroRules.MaxRankUlt : HeroRules.MaxRankAB;

        public float ValueAt(float[] arr, int rank)
        {
            if (arr == null || arr.Length == 0 || rank < 1)
                return 0f;
            var i = Mathf.Clamp(rank - 1, 0, arr.Length - 1);
            return arr[i];
        }

        public float Cd(int rank) => Mathf.Max(0.35f, ValueAt(Cooldown, rank));
        public float Pwr(int rank) => ValueAt(Power, rank);
        public float Rng(int rank) => ValueAt(Range, rank);
        public float Dur(int rank) => ValueAt(Duration, rank);
        public float SlowAmt(int rank) => ValueAt(Slow, rank);
        public string DisplayName => Loc.T(LocKey);
    }

    /// <summary>Данные умений. Поведение каста — AbilityCaster, наведение — Targeting.</summary>
    public static class AbilityCatalog
    {
        public static readonly AbilityDef Bulwark = Make("bulwark", "hero.bastion.a", AbilityTargeting.Instant, DamageKind.Physical, AbilityEffect.ConeStun, false,
            new[] { 8f, 7.5f, 7f, 6.5f, 6f }, new[] { 90f, 110f, 130f, 150f, 175f }, new[] { 3.2f, 3.4f, 3.6f, 3.8f, 4.1f }, new[] { 0.7f, 0.8f, 0.9f, 1.0f, 1.1f }, null);

        public static readonly AbilityDef GuardBreak = Make("guard_break", "hero.bastion.b", AbilityTargeting.NeedTarget, DamageKind.Physical, AbilityEffect.TargetDamageSlow, false,
            new[] { 10f, 9.5f, 9f, 8.5f, 8f }, new[] { 70f, 90f, 110f, 130f, 155f }, new[] { 4f, 4.2f, 4.4f, 4.6f, 5f }, new[] { 1.2f, 1.3f, 1.4f, 1.5f, 1.7f }, new[] { 0.25f, 0.3f, 0.35f, 0.4f, 0.45f });

        public static readonly AbilityDef Earthsplit = Make("earthsplit", "hero.bastion.c", AbilityTargeting.Ground, DamageKind.Magical, AbilityEffect.GroundBurst, true,
            new[] { 40f, 34f, 28f }, new[] { 180f, 240f, 320f }, new[] { 8f, 9f, 10f }, new[] { 3.2f, 3.6f, 4.1f }, new[] { 0.3f, 0.4f, 0.5f });

        public static readonly AbilityDef Bolt = Make("bolt", "hero.vesper.a", AbilityTargeting.Direction, DamageKind.Physical, AbilityEffect.Skillshot, false,
            new[] { 7f, 6.6f, 6.2f, 5.8f, 5.4f }, new[] { 150f, 175f, 200f, 230f, 265f }, new[] { 12f, 12.5f, 13f, 13.5f, 14f }, null, null);

        public static readonly AbilityDef Pinshot = Make("pinshot", "hero.vesper.b", AbilityTargeting.NeedTarget, DamageKind.Physical, AbilityEffect.TargetDamageSlow, false,
            new[] { 9f, 8.5f, 8f, 7.5f, 7f }, new[] { 80f, 100f, 120f, 145f, 175f }, new[] { 7.5f, 7.8f, 8.1f, 8.4f, 8.8f }, new[] { 1.4f, 1.5f, 1.6f, 1.7f, 1.9f }, new[] { 0.3f, 0.35f, 0.4f, 0.45f, 0.5f });

        public static readonly AbilityDef Comet = Make("comet", "hero.vesper.c", AbilityTargeting.Ground, DamageKind.Magical, AbilityEffect.GroundBurst, true,
            new[] { 45f, 38f, 32f }, new[] { 200f, 270f, 360f }, new[] { 9f, 10f, 11f }, new[] { 2.8f, 3.2f, 3.6f }, null);

        public static readonly AbilityDef Mend = Make("mend", "hero.mira.a", AbilityTargeting.Instant, DamageKind.Magical, AbilityEffect.NovaHealHarm, false,
            new[] { 9f, 8.5f, 8f, 7.5f, 7f }, new[] { 140f, 160f, 180f, 205f, 235f }, new[] { 4.5f, 4.7f, 4.9f, 5.1f, 5.4f }, null, null);

        public static readonly AbilityDef Bind = Make("bind", "hero.mira.b", AbilityTargeting.NeedTarget, DamageKind.Magical, AbilityEffect.TargetDamageSlow, false,
            new[] { 11f, 10.5f, 10f, 9.5f, 9f }, new[] { 60f, 75f, 90f, 110f, 130f }, new[] { 6f, 6.3f, 6.6f, 6.9f, 7.3f }, new[] { 1.5f, 1.6f, 1.8f, 2.0f, 2.2f }, new[] { 0.35f, 0.4f, 0.45f, 0.5f, 0.55f });

        public static readonly AbilityDef Bloom = Make("bloom", "hero.mira.c", AbilityTargeting.Ground, DamageKind.Magical, AbilityEffect.GroundBurst, true,
            new[] { 42f, 36f, 30f }, new[] { 160f, 210f, 280f }, new[] { 8.5f, 9.5f, 10.5f }, new[] { 3.4f, 3.8f, 4.3f }, null);

        static AbilityDef[] _all;

        public static AbilityDef Get(string id)
        {
            Ensure();
            foreach (var a in _all)
            {
                if (a.Id == id)
                    return a;
            }
            return Bulwark;
        }

        static void Ensure()
        {
            if (_all != null)
                return;
            _all = new[] { Bulwark, GuardBreak, Earthsplit, Bolt, Pinshot, Comet, Mend, Bind, Bloom };
        }

        static AbilityDef Make(string id, string loc, AbilityTargeting targeting, DamageKind dmg, AbilityEffect fx, bool ult,
            float[] cd, float[] power, float[] range, float[] duration, float[] slow)
        {
            return new AbilityDef
            {
                Id = id,
                LocKey = loc,
                Targeting = targeting,
                Damage = dmg,
                Effect = fx,
                Ultimate = ult,
                Cooldown = cd,
                Power = power,
                Range = range,
                Duration = duration,
                Slow = slow
            };
        }
    }
}
