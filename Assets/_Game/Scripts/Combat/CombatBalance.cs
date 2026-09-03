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

        public const float TurretHp = 1540f;
        public const float TurretDamage = 119f;
        public const float TurretRange = 9f;
        public const float TurretInterval = 1.15f;
        public const float TurretLockPortion = StructureRules.LockPortion;
        public const float TurretUnlockRange = StructureRules.UnlockRange;
        public const float CrystalHp = 2800f;
        public const float BossSpawnTime = 420f;
        public const float BossHp = 4800f;
        public const float BossDamage = 72f;
        public const float BossRange = 2.8f;
        public const float BossAggro = 8f;
        public const float BossSpeed = 2.4f;
        public const float BossInterval = 1.35f;
        public const int BossBounty = 300;

        public static UnityEngine.Vector3 MinionSpawn(TeamId team, int index)
        {
            var origin = FoldMapBuilder.LaneOrigin(team);
            var dir = FoldMapBuilder.LaneDir(team);
            var along = (WaveSize - 1 - index) * WaveSpacing;
            var gy = index == WaveSize - 1 ? CaptainGroundY : MinionGroundY;
            var p = origin + dir * along;
            return GroundProbe.OnSurface(p, gy);
        }

        public static UnityEngine.Vector3 MinionGoal(TeamId team)
        {
            var goal = FoldMapBuilder.LaneGoal(team);
            goal.y = MinionGroundY;
            return goal;
        }
    }
}
