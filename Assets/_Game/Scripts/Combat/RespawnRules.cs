using UnityEngine;

namespace Ashfold
{
    public static class RespawnRules
    {
        public const float BaseSeconds = 5f;
        public const float PerMinuteSeconds = 2f;

        public static float DurationSeconds()
        {
            var minutes = 0f;
            if (BattleRuntime.I != null)
                minutes = BattleRuntime.I.MatchTime / 60f;
            return BaseSeconds + PerMinuteSeconds * Mathf.Floor(minutes);
        }
    }
}
