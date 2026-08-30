using UnityEngine;

namespace Ashfold
{
    public enum HeroRole
    {
        Tank,
        Carry,
        Support
    }

    public sealed class HeroDef
    {
        public string Id;
        public string DisplayName;
        public HeroRole Role;
        public string Tagline;
        public float MaxHp;
        public float Damage;
        public float AttackRange;
        public float AttackInterval;
        public float MoveSpeed;
        public bool Ranged;
        public string[] Abilities;

        public AbilityDef Ability(int slot)
        {
            if (Abilities == null || slot < 0 || slot >= Abilities.Length)
                return AbilityCatalog.Bulwark;
            return AbilityCatalog.Get(Abilities[slot]);
        }
    }

    public sealed class ItemDef
    {
        public string Id;
        public string DisplayName;
        public string Branch;
        public int Cost;
        public string Effect;
        public float BonusDamage;
        public float BonusAttackSpeed;
        public float BonusHp;
        public float BonusResist;
        public float BonusHealPower;
        public float BonusMoveSpeed;
    }

    public static class GameContent
    {
        public static readonly HeroDef[] Heroes =
        {
            new HeroDef
            {
                Id = "bastion", DisplayName = "Bastion", Role = HeroRole.Tank, Tagline = "Frontline steel",
                MaxHp = 820, Damage = 52, AttackRange = 2.3f, AttackInterval = 1.05f, MoveSpeed = 6.4f, Ranged = false,
                Abilities = new[] { "bulwark", "guard_break", "earthsplit" }
            },
            new HeroDef
            {
                Id = "vesper", DisplayName = "Vesper", Role = HeroRole.Carry, Tagline = "Lane pressure",
                MaxHp = 470, Damage = 72, AttackRange = 7.2f, AttackInterval = 0.85f, MoveSpeed = 7.1f, Ranged = true,
                Abilities = new[] { "bolt", "pinshot", "comet" }
            },
            new HeroDef
            {
                Id = "mira", DisplayName = "Mira", Role = HeroRole.Support, Tagline = "Keeps the fold",
                MaxHp = 540, Damage = 38, AttackRange = 5.4f, AttackInterval = 1.0f, MoveSpeed = 7.0f, Ranged = true,
                Abilities = new[] { "mend", "bind", "bloom" }
            }
        };

        public static readonly ItemDef[] Items =
        {
            new ItemDef { Id = "iron_edge", DisplayName = "Iron Edge", Branch = "Damage", Cost = 150, Effect = "+25 attack", BonusDamage = 25 },
            new ItemDef { Id = "storm_charm", DisplayName = "Storm Charm", Branch = "Damage", Cost = 150, Effect = "+25% attack speed", BonusAttackSpeed = 0.25f },
            new ItemDef { Id = "stoneplate", DisplayName = "Stoneplate", Branch = "Defense", Cost = 150, Effect = "+180 HP", BonusHp = 180 },
            new ItemDef { Id = "wardcloak", DisplayName = "Wardcloak", Branch = "Defense", Cost = 150, Effect = "+18% resist", BonusResist = 0.18f },
            new ItemDef { Id = "lifewell", DisplayName = "Lifewell", Branch = "Support", Cost = 150, Effect = "+40% heal power", BonusHealPower = 0.4f },
            new ItemDef { Id = "pulse_beacon", DisplayName = "Pulse Beacon", Branch = "Support", Cost = 150, Effect = "+12% move speed", BonusMoveSpeed = 0.12f }
        };

        public static readonly string[] BotNames =
        {
            "Rook", "Needle", "Grove", "Ember", "Cinder", "Shade"
        };

        public static ItemDef GetItem(string id)
        {
            foreach (var item in Items)
            {
                if (item.Id == id)
                    return item;
            }
            return null;
        }

        public static HeroDef GetHero(string id)
        {
            foreach (var h in Heroes)
            {
                if (h.Id == id)
                    return h;
            }
            return Heroes[0];
        }

        public static Color HeroColor(string id)
        {
            switch (id)
            {
                case "bastion": return GameTheme.Gold;
                case "vesper": return GameTheme.Crimson;
                case "mira": return GameTheme.Teal;
                default: return GameTheme.TextMuted;
            }
        }

        public static string RoleLabel(HeroRole role)
        {
            switch (role)
            {
                case HeroRole.Tank: return Loc.T("role.tank");
                case HeroRole.Carry: return Loc.T("role.carry");
                case HeroRole.Support: return Loc.T("role.support");
                default: return role.ToString().ToUpperInvariant();
            }
        }

        public static string HeroTagline(HeroDef hero)
        {
            return Loc.T("hero." + hero.Id + ".tagline");
        }

        public static string HeroSkill(HeroDef hero)
        {
            var a = hero != null ? hero.Ability(0) : null;
            return a != null ? a.DisplayName : "";
        }

        public static string HeroAbility(HeroDef hero, int slot)
        {
            var a = hero != null ? hero.Ability(slot) : null;
            return a != null ? a.DisplayName : "";
        }

        public static string ItemName(ItemDef item)
        {
            return Loc.T("item." + item.Id + ".name");
        }

        public static string ItemEffect(ItemDef item)
        {
            return Loc.T("item." + item.Id + ".effect");
        }

        public static string ItemBranch(ItemDef item)
        {
            return Loc.T("item.branch." + item.Branch.ToLowerInvariant());
        }
    }
}
