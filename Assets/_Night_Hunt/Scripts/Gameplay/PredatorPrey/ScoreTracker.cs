using System.Collections.Generic;
using UnityEngine;
using NightHunt.Networking;
using NightHunt.Gameplay.Scoring;

namespace NightHunt.Gameplay.PredatorPrey
{
    /// <summary>
    /// Score tracking for predator/prey system
    /// </summary>
    public class ScoreTracker
    {
        private readonly Dictionary<int, int> teamScores = new Dictionary<int, int>();
        private ScoringSystem scoringSystem;

        public ScoreTracker(ScoringSystem scoring)
        {
            scoringSystem = scoring;
        }

        /// <summary>
        /// Get team score
        /// </summary>
        public int GetTeamScore(int teamId)
        {
            if (scoringSystem != null)
            {
                return scoringSystem.GetTeamScore(teamId);
            }

            return teamScores.ContainsKey(teamId) ? teamScores[teamId] : 0;
        }

        /// <summary>
        /// Get leading team ID
        /// </summary>
        public int GetLeadingTeam()
        {
            if (scoringSystem != null)
            {
                return scoringSystem.GetLeadingTeam();
            }

            int leadingTeam = -1;
            int maxScore = int.MinValue;

            foreach (var kvp in teamScores)
            {
                if (kvp.Value > maxScore)
                {
                    maxScore = kvp.Value;
                    leadingTeam = kvp.Key;
                }
            }

            return leadingTeam;
        }

        /// <summary>
        /// Get trailing team ID
        /// </summary>
        public int GetTrailingTeam()
        {
            int leadingTeam = GetLeadingTeam();
            if (leadingTeam < 0) return -1;

            // Find team with lowest score
            int trailingTeam = -1;
            int minScore = int.MaxValue;

            foreach (var kvp in teamScores)
            {
                if (kvp.Key != leadingTeam && kvp.Value < minScore)
                {
                    minScore = kvp.Value;
                    trailingTeam = kvp.Key;
                }
            }

            return trailingTeam;
        }

        /// <summary>
        /// Update team score
        /// </summary>
        public void UpdateTeamScore(int teamId, int score)
        {
            teamScores[teamId] = score;
        }
    }
}

