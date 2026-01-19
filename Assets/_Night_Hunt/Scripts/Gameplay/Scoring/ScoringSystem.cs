using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Gameplay.Match;
using FishNet;

namespace NightHunt.Gameplay.Scoring
{
    /// <summary>
    /// Manages scoring system for kills, assists, objectives, etc.
    /// Server-authoritative scoring
    /// Works with both host and dedicated server
    /// </summary>
    public class ScoringSystem : NetworkBehaviour
    {
        [Header("Score Configuration")]
        [SerializeField] private Dictionary<int, TeamScore> teamScores = new Dictionary<int, TeamScore>();
        [SerializeField] private Dictionary<uint, PlayerScore> playerScores = new Dictionary<uint, PlayerScore>();

        // Synchronized scores
        private readonly SyncVar<string> scoreDataJson = new SyncVar<string>();

        private MatchPhaseManager phaseManager;
        private List<ScoreSystemData> scoreConfigs;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Load score configs
            scoreConfigs = GameConfigLoader.Instance?.ConfigData?.ScoreSystem;
            phaseManager = FindObjectOfType<MatchPhaseManager>();
        }

        /// <summary>
        /// Server: Award score for action
        /// </summary>
        [Server]
        public void AwardScore(uint playerId, string action, int baseScore, float multiplier = 1f)
        {
            if (!playerScores.ContainsKey(playerId))
            {
                playerScores[playerId] = new PlayerScore { PlayerId = playerId };
            }

            // Get phase multiplier using ScoreMultiplier utility
            float phaseMultiplier = 1f;
            if (phaseManager != null)
            {
                phaseMultiplier = ScoreMultiplier.GetPhaseMultiplier(phaseManager.CurrentPhase);
            }

            // Calculate final score
            int finalScore = Mathf.RoundToInt(baseScore * multiplier * phaseMultiplier);
            
            // Publish score event
            var scoreEvent = new ScoreEvent(GetTeamId(playerId), finalScore, action);
            NightHunt.Gameplay.Core.Events.GameplayEventBus.Instance?.Publish(scoreEvent);
            playerScores[playerId].TotalScore += finalScore;

            // Update team score
            NetworkPlayer player = GetPlayerById(playerId);
            if (player != null)
            {
                int teamId = player.TeamId;
                if (!teamScores.ContainsKey(teamId))
                {
                    teamScores[teamId] = new TeamScore { TeamId = teamId };
                }
                teamScores[teamId].TotalScore += finalScore;
            }

            // Sync scores
            SyncScores();

            Debug.Log($"[ScoringSystem] Awarded {finalScore} points to player {playerId} for {action}");
        }

        /// <summary>
        /// Server: Award kill score
        /// </summary>
        [Server]
        public void AwardKill(uint killerId, uint victimId)
        {
            var killConfig = GetScoreConfig("Kill");
            if (killConfig != null)
            {
                AwardScore(killerId, "Kill", killConfig.BaseScore, killConfig.PhaseMultiplier);
            }
        }

        /// <summary>
        /// Server: Award assist score
        /// </summary>
        [Server]
        public void AwardAssist(uint assistId, uint victimId)
        {
            var assistConfig = GetScoreConfig("Assist");
            if (assistConfig != null)
            {
                AwardScore(assistId, "Assist", assistConfig.BaseScore, assistConfig.PhaseMultiplier);
            }
        }

        /// <summary>
        /// Server: Award boss kill score
        /// </summary>
        [Server]
        public void AwardBossKill(uint killerId)
        {
            var bossKillConfig = GetScoreConfig("BossKill");
            if (bossKillConfig != null)
            {
                AwardScore(killerId, "BossKill", bossKillConfig.BaseScore, bossKillConfig.PhaseMultiplier);
            }
        }

        /// <summary>
        /// Server: Award objective capture score
        /// </summary>
        [Server]
        public void AwardObjectiveCapture(int teamId, float captureTime)
        {
            var objectiveConfig = GetScoreConfig("ObjectiveCapture");
            if (objectiveConfig != null)
            {
                // Award per second of capture
                int scorePerSecond = objectiveConfig.BaseScore;
                int totalScore = Mathf.RoundToInt(captureTime * scorePerSecond);

            // Award to all team members
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.TeamId == teamId)
                {
                    AwardScore((uint)player.ObjectId, "ObjectiveCapture", totalScore);
                }
            }
            }
        }

        /// <summary>
        /// Server: Award survival score
        /// </summary>
        [Server]
        public void AwardSurvival(uint playerId, float timeAlive)
        {
            var survivalConfig = GetScoreConfig("SurvivalMinute");
            if (survivalConfig != null)
            {
                int minutes = Mathf.FloorToInt(timeAlive / 60f);
                int score = minutes * survivalConfig.BaseScore;
                AwardScore(playerId, "Survival", score);
            }
        }

        /// <summary>
        /// Get score config by action name
        /// </summary>
        private ScoreSystemData GetScoreConfig(string action)
        {
            if (scoreConfigs == null) return null;
            return scoreConfigs.Find(s => s.Action == action);
        }

        /// <summary>
        /// Get team ID for player
        /// </summary>
        private int GetTeamId(uint playerId)
        {
            var player = GetPlayerById(playerId);
            return player != null ? player.TeamId : -1;
        }

        /// <summary>
        /// Get player by network ID
        /// </summary>
        private NetworkPlayer GetPlayerById(uint playerId)
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.ObjectId == playerId)
                {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// Sync scores to clients
        /// </summary>
        [Server]
        private void SyncScores()
        {
            // Serialize scores to JSON (simplified)
            // In production, use proper serialization
            scoreDataJson.Value = "scores_updated";
        }

        /// <summary>
        /// Get player score
        /// </summary>
        public int GetPlayerScore(uint playerId)
        {
            if (playerScores.ContainsKey(playerId))
            {
                return playerScores[playerId].TotalScore;
            }
            return 0;
        }

        /// <summary>
        /// Get team score
        /// </summary>
        public int GetTeamScore(int teamId)
        {
            if (teamScores.ContainsKey(teamId))
            {
                return teamScores[teamId].TotalScore;
            }
            return 0;
        }

        /// <summary>
        /// Get leading team
        /// </summary>
        public int GetLeadingTeam()
        {
            int leadingTeam = -1;
            int maxScore = int.MinValue;

            foreach (var kvp in teamScores)
            {
                if (kvp.Value.TotalScore > maxScore)
                {
                    maxScore = kvp.Value.TotalScore;
                    leadingTeam = kvp.Key;
                }
            }

            return leadingTeam;
        }
    }

    /// <summary>
    /// Player score data
    /// </summary>
    [System.Serializable]
    public class PlayerScore
    {
        public uint PlayerId;
        public int TotalScore = 0;
        public int Kills = 0;
        public int Assists = 0;
        public int Deaths = 0;
        public int ObjectiveScore = 0;
    }

    /// <summary>
    /// Team score data
    /// </summary>
    [System.Serializable]
    public class TeamScore
    {
        public int TeamId;
        public int TotalScore = 0;
        public int Kills = 0;
        public int ObjectiveScore = 0;
    }
}

