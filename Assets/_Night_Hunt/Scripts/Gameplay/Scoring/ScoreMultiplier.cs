using UnityEngine;
using NightHunt.Gameplay.Match;

namespace NightHunt.Gameplay.Scoring
{
    /// <summary>
    /// Phase-based score multipliers
    /// </summary>
    public static class ScoreMultiplier
    {
        /// <summary>
        /// Get score multiplier for current phase
        /// </summary>
        public static float GetPhaseMultiplier(MatchPhaseState phase)
        {
            switch (phase)
            {
                case MatchPhaseState.Preparation:
                    return 1f; // No bonus in preparation
                case MatchPhaseState.Hunt:
                    return 1.2f; // 20% bonus in hunt phase
                case MatchPhaseState.Lockdown:
                    return 1.5f; // 50% bonus in lockdown
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Apply phase multiplier to score
        /// </summary>
        public static int ApplyMultiplier(int baseScore, MatchPhaseState phase)
        {
            return Mathf.RoundToInt(baseScore * GetPhaseMultiplier(phase));
        }
    }
}

