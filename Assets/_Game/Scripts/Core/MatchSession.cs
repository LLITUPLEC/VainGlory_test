using System.Collections.Generic;

namespace Ashfold
{
    public sealed class MatchParticipant
    {
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
}
