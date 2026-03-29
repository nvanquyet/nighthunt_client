using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Match;

namespace NightHunt.Gameplay.Scoring
{
    /// <summary>
    /// Phase-based score multipliers.
    /// Prefer the config-aware overload <see cref="GetPhaseMultiplier(MatchPhaseState, IEnumerable{MatchPhaseConfigData})"/>
    /// so values come from designer data rather than code.
    /// </summary>
    public static class ScoreMultiplier
    {
        // ── Config-aware (preferred) ──────────────────────────────────────────
        /// <summary>
        /// Returns the ScoreMultiplier from the matching MatchPhaseConfigData entry.
        /// Falls back to hardcoded defaults if configs is null or no entry matches.
        /// </summary>
        public static float GetPhaseMultiplier(MatchPhaseState phase, IEnumerable<MatchPhaseConfigData> configs)
        {
            if (configs != null)
            {
                var entry = configs.FirstOrDefault(c => c.PhaseType == phase);
                if (entry != null) return entry.ScoreMultiplier;
            }
            return GetPhaseMultiplier(phase);
        }

        /// <summary>
        /// Apply phase multiplier to score (config-aware).
        /// </summary>
        public static int ApplyMultiplier(int baseScore, MatchPhaseState phase, IEnumerable<MatchPhaseConfigData> configs)
            => Mathf.RoundToInt(baseScore * GetPhaseMultiplier(phase, configs));

        // ── Fallback (hardcoded defaults) ─────────────────────────────────────
        /// <summary>
        /// Hardcoded fallback multipliers — use only when no config data is available.
        /// </summary>
        public static float GetPhaseMultiplier(MatchPhaseState phase)
        {
            switch (phase)
            {
                case MatchPhaseState.Preparation: return 1f;
                case MatchPhaseState.Hunt:        return 1.2f;
                case MatchPhaseState.Lockdown:    return 1.5f;
                default:                          return 1f;
            }
        }

        /// <summary>
        /// Apply phase multiplier to score (hardcoded fallback).
        /// </summary>
        public static int ApplyMultiplier(int baseScore, MatchPhaseState phase)
            => Mathf.RoundToInt(baseScore * GetPhaseMultiplier(phase));
    }
}

