namespace Ashfold
{
    public sealed class PlayerProfile
    {
        public string UserId;
        public string DisplayName;
        public string Username;
        public int Level = 1;
        public int Essence;
        public string AuthProvider = "dev-guest";
        public string Email = "";
        public string UnlockedHeroesCsv = "bastion,vesper,mira";

        public bool HasEmail => !string.IsNullOrEmpty(Email);

        public bool IsHeroUnlocked(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
                return false;
            if (string.IsNullOrEmpty(UnlockedHeroesCsv))
                return true;
            var parts = UnlockedHeroesCsv.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), heroId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
