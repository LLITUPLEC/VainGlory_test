using System.Collections.Generic;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Статы всех героев матча для Results.</summary>
    public sealed class MatchStatsTracker : MonoBehaviour
    {
        public static MatchStatsTracker I { get; private set; }

        public readonly Dictionary<CombatUnit, MatchStatRow> ByUnit = new Dictionary<CombatUnit, MatchStatRow>();
        public readonly List<MatchStatRow> Rows = new List<MatchStatRow>(6);

        void Awake()
        {
            I = this;
        }

        void OnDestroy()
        {
            if (I == this)
                I = null;
        }

        public MatchStatRow Register(CombatUnit unit, string name, string heroId, int team, bool isLocal, bool isBot)
        {
            var row = new MatchStatRow
            {
                Name = name,
                HeroId = heroId,
                Team = team,
                IsLocal = isLocal,
                IsBot = isBot,
                Gold = isLocal ? (BattleRuntime.I != null ? BattleRuntime.I.Gold : 80) : 80
            };
            Rows.Add(row);
            ByUnit[unit] = row;
            unit.Killed += OnUnitKilled;
            return row;
        }

        void OnUnitKilled(CombatUnit victim, CombatUnit killer)
        {
            if (!victim.IsHero)
                return;

            if (ByUnit.TryGetValue(victim, out var deadRow))
                deadRow.Deaths++;

            if (killer != null && killer.IsHero && ByUnit.TryGetValue(killer, out var killRow))
            {
                killRow.Kills++;
                if (killer.IsPlayer && BattleRuntime.I != null)
                    BattleRuntime.I.Kills++;
            }

            // Простые ассисты: союзные герои рядом с жертвой.
            if (killer == null)
                return;
            foreach (var kv in ByUnit)
            {
                var u = kv.Key;
                if (u == null || u == killer || u == victim || !u.IsAlive)
                    continue;
                if (u.Team != killer.Team)
                    continue;
                var d = u.transform.position - victim.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= 100f)
                    kv.Value.Assists++;
            }
        }

        public void SyncGold()
        {
            foreach (var kv in ByUnit)
            {
                var unit = kv.Key;
                var row = kv.Value;
                if (unit == null)
                    continue;
                if (unit.IsPlayer && BattleRuntime.I != null)
                    row.Gold = BattleRuntime.I.Gold;
                else
                {
                    var bot = unit.GetComponent<HeroBotAi>();
                    if (bot != null)
                        row.Gold = bot.Gold;
                }

                row.Items.Clear();
                var combat = unit.GetComponent<HeroCombat>();
                if (combat != null)
                    row.Items.AddRange(combat.Items);
            }
        }

        public MatchResult BuildResult(bool victory, bool surrendered)
        {
            SyncGold();
            var result = new MatchResult
            {
                Victory = victory,
                Surrendered = surrendered,
                EssenceReward = victory ? 40 : (surrendered ? 5 : 15)
            };
            if (GameSession.I != null && GameSession.I.Match != null)
            {
                result.MapName = GameSession.I.Match.MapName;
                result.ModeName = GameSession.I.Match.ModeName;
            }
            result.Rows.AddRange(Rows);
            return result;
        }
    }
}
