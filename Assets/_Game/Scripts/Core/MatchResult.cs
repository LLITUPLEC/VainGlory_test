using System.Collections.Generic;

namespace Ashfold
{
    public sealed class MatchStatRow
    {
        public string Name;
        public string HeroId;
        public int Team;
        public bool IsLocal;
        public bool IsBot;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int Gold;
        public readonly List<string> Items = new List<string>(6);
    }

    public sealed class MatchResult
    {
        public bool Victory;
        public bool Surrendered;
        public int EssenceReward;
        public string MapName = "Ashfold Lane";
        public string ModeName = "Casual 3v3";
        public readonly List<MatchStatRow> Rows = new List<MatchStatRow>(6);
    }
}
