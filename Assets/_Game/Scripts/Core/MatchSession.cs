using System;
using System.Collections.Generic;

namespace Ashfold
{
    public sealed class MatchParticipant
    {
        public string UserId;
        public string Name;
        public bool IsBot;
        public bool IsLocal;
        public int Team;
        public int Slot;
        public string HeroId;
        public bool Locked;
    }

    public sealed class MatchSession
    {
        public string ModeId = "casual_3v3";
        public string ModeName = "Casual 3v3";
        public string MapName = "Ashfold Lane";
        public string NakamaMatchId;
        public readonly List<MatchParticipant> Players = new List<MatchParticipant>(6);

        public MatchParticipant Local
        {
            get
            {
                foreach (var p in Players)
                {
                    if (p.IsLocal)
                        return p;
                }
                return null;
            }
        }

        public IEnumerable<MatchParticipant> Team(int team)
        {
            foreach (var p in Players)
            {
                if (p.Team == team)
                    yield return p;
            }
        }
    }

    [Serializable]
    public sealed class NakamaRosterDto
    {
        public string type;
        public int count;
        public NakamaRosterPlayer[] players;
    }

    [Serializable]
    public sealed class NakamaRosterPlayer
    {
        public string userId;
        public string username;
        public int team;
        public int slot;
    }

    public static class MatchRoster
    {
        public static MatchSession FromNakama(NakamaRosterDto roster, string localUserId, string matchId)
        {
            var match = new MatchSession { NakamaMatchId = matchId };
            var used = new bool[2, 3];
            if (roster != null && roster.players != null)
            {
                foreach (var h in roster.players)
                {
                    var team = h.team == 1 ? 1 : 0;
                    var slot = h.slot;
                    if (slot < 0 || slot > 2)
                        slot = 0;
                    match.Players.Add(new MatchParticipant
                    {
                        UserId = h.userId,
                        Name = string.IsNullOrEmpty(h.username) ? "Player" : h.username,
                        IsLocal = h.userId == localUserId,
                        IsBot = false,
                        Team = team,
                        Slot = slot
                    });
                    used[team, slot] = true;
                }
            }

            var bot = 0;
            for (var team = 0; team < 2; team++)
            {
                for (var slot = 0; slot < 3; slot++)
                {
                    if (used[team, slot])
                        continue;
                    var name = GameContent.BotNames[bot % GameContent.BotNames.Length];
                    bot++;
                    match.Players.Add(new MatchParticipant
                    {
                        Name = name,
                        IsBot = true,
                        Team = team,
                        Slot = slot
                    });
                }
            }

            return match;
        }
    }
}
