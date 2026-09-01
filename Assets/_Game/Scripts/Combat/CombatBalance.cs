namespace Ashfold
{
    /// <summary>Числа боя, общие для офлайна. Сервер (world.go) держит те же значения.</summary>
    public static class CombatBalance
    {
        public const float MinionHp = 170f;
        public const float MinionDamage = 16f;
        public const float MinionSpeed = 3.68f;
        public const int MinionBounty = 14;
        public const float MinionGroundY = 0.7f;

        public const float CaptainScale = 1.5f;
        public const float CaptainHp = 250f;
        public const float CaptainDamage = 22f;
        public const int CaptainBounty = 24;
        public static float CaptainGroundY => MinionGroundY * CaptainScale;

        public const int WaveSize = 4;
        public const float WaveSpacing = 1.45f;
        public const float WaveSpawnX = 32f;

        public const float TurretHp = 1540f;
        public const float TurretDamage = 119f;
        public const float TurretRange = 9f;
        public const float TurretInterval = 1.15f;
        public const float CrystalHp = 2800f;

        public static UnityEngine.Vector3 MinionSpawn(TeamId team, int index)
        {
            var dir = team == TeamId.Dawn ? 1f : -1f;
            var x = team == TeamId.Dawn ? -WaveSpawnX : WaveSpawnX;
            var along = (WaveSize - 1 - index) * WaveSpacing;
            var gy = index == WaveSize - 1 ? CaptainGroundY : MinionGroundY;
            return new UnityEngine.Vector3(x + dir * along, gy, 0f);
        }

        public static UnityEngine.Vector3 MinionGoal(TeamId team)
        {
            var gy = MinionGroundY;
            return team == TeamId.Dawn
                ? new UnityEngine.Vector3(40f, gy, 0f)
                : new UnityEngine.Vector3(-40f, gy, 0f);
        }
    }
}
