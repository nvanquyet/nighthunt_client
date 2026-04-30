using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Utilities;
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

        [Header("Score Action Configs")]
        [Tooltip("Config cho từng action (Kill, Assist, BossKill, ...). Assign trong Inspector.")]
        [SerializeField] private List<ScoreSystemData> _scoreConfigList = new List<ScoreSystemData>();

        // Synchronized scores
        private readonly SyncVar<string> scoreDataJson = new SyncVar<string>();

        private MatchPhaseManager phaseManager;
        private List<ScoreSystemData> scoreConfigs;
        private ScoreSync _scoreSync;

        public override void OnStartServer()
        {
            base.OnStartServer();

            scoreConfigs = _scoreConfigList.Count > 0 ? _scoreConfigList : null;
            phaseManager = FindFirstObjectByType<MatchPhaseManager>();
            _scoreSync   = ComponentResolver.Find<ScoreSync>(this).OnSelf().InChildren().Resolve()
                           ?? FindFirstObjectByType<ScoreSync>();

            if (scoreConfigs == null)
                Debug.LogWarning("[ScoringSystem] _scoreConfigList is empty! " +
                                 "Right-click component → 'NightHunt/Setup Default Score Configs' to populate. " +
                                 "Falling back to hardcoded defaults.");

            // Kills are awarded directly by PlayerHealthSystem on the server when death is confirmed.
            GameplayEventBus.Instance?.Subscribe<BossKilledEvent>(OnBossKilled);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            GameplayEventBus.Instance?.Unsubscribe<BossKilledEvent>(OnBossKilled);
        }

        [Server]
        private void OnPlayerKilled(PlayerKilledEvent evt)
        {
            if (evt.KillerNetworkObjectId == 0) return; // world/environment kill — no score
            AwardKill(evt.KillerNetworkObjectId, evt.VictimNetworkObjectId);
        }

        [Server]
        private void OnBossKilled(BossKilledEvent evt)
        {
            // Award all living players on the killer team
            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return;
            foreach (var player in players)
            {
                if (player != null && player.IsAlive && player.TeamId == evt.KillerTeamId)
                    AwardBossKill((uint)player.ObjectId);
            }
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
                phaseMultiplier = phaseManager.GetScoreMultiplier();
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
            AwardScore(killerId, "Kill", killConfig.BaseScore, killConfig.PhaseMultiplier);

            // Track Kills stat (AwardScore already initialized playerScores/teamScores entries)
            if (playerScores.ContainsKey(killerId))
                playerScores[killerId].Kills++;

            NetworkPlayer killer = GetPlayerById(killerId);
            if (killer != null && teamScores.ContainsKey(killer.TeamId))
                teamScores[killer.TeamId].Kills++;
        }

        /// <summary>
        /// Server: Award assist score
        /// </summary>
        [Server]
        public void AwardAssist(uint assistId, uint victimId)
        {
            var assistConfig = GetScoreConfig("Assist");
            AwardScore(assistId, "Assist", assistConfig.BaseScore, assistConfig.PhaseMultiplier);
        }

        /// <summary>
        /// Server: Award boss kill score
        /// </summary>
        [Server]
        public void AwardBossKill(uint killerId)
        {
            var bossKillConfig = GetScoreConfig("BossKill");
            AwardScore(killerId, "BossKill", bossKillConfig.BaseScore, bossKillConfig.PhaseMultiplier);
        }

        /// <summary>
        /// Server: Award objective capture score
        /// </summary>
        [Server]
        public void AwardObjectiveCapture(int teamId, float captureTime)
        {
            var objectiveConfig = GetScoreConfig("ObjectiveCapture");
            int scorePerSecond = objectiveConfig.BaseScore;
            int totalScore = Mathf.RoundToInt(captureTime * scorePerSecond);

            // Award to all team members — O(n) via registry, no FindObjectsOfType
            var allPlayers = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (allPlayers != null)
                foreach (var player in allPlayers)
                    if (player != null && player.TeamId == teamId)
                        AwardScore((uint)player.ObjectId, "ObjectiveCapture", totalScore);
        }

        /// <summary>
        /// Server: Award survival score
        /// </summary>
        [Server]
        public void AwardSurvival(uint playerId, float timeAlive)
        {
            var survivalConfig = GetScoreConfig("SurvivalMinute");
            int minutes = Mathf.FloorToInt(timeAlive / 60f);
            int score = minutes * survivalConfig.BaseScore;
            AwardScore(playerId, "Survival", score);
        }

        /// <summary>
        /// Returns the ScoreSystemData for the given action. When the designer list is
        /// empty or missing an entry, falls back to hardcoded defaults so scoring never
        /// silently drops kills/objectives in untested scenes.
        /// </summary>
        private ScoreSystemData GetScoreConfig(string action)
        {
            if (scoreConfigs != null)
            {
                var cfg = scoreConfigs.Find(s => s.Action == action);
                if (cfg != null) return cfg;
            }
            return GetFallbackScoreConfig(action);
        }

        // Hardcoded fallback table — keeps gameplay functional when Inspector list is empty.
        private static ScoreSystemData GetFallbackScoreConfig(string action)
        {
            switch (action)
            {
                case "Kill":             return new ScoreSystemData { Action = action, BaseScore = 100, PhaseMultiplier = 1f };
                case "Assist":           return new ScoreSystemData { Action = action, BaseScore = 75,  PhaseMultiplier = 1f };
                case "BossKill":         return new ScoreSystemData { Action = action, BaseScore = 300, PhaseMultiplier = 1.2f };
                case "ObjectiveCapture": return new ScoreSystemData { Action = action, BaseScore = 20,  PhaseMultiplier = 1.1f };
                case "BeaconDestroy":    return new ScoreSystemData { Action = action, BaseScore = 50,  PhaseMultiplier = 1f };
                case "SurvivalMinute":   return new ScoreSystemData { Action = action, BaseScore = 5,   PhaseMultiplier = 1f };
                default:
                    Debug.LogWarning($"[ScoringSystem] No score config for '{action}' and no hardcoded fallback — returning 0.");
                    return new ScoreSystemData { Action = action, BaseScore = 0, PhaseMultiplier = 1f };
            }
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
        /// Get player by network object ID — uses PlayerPublicRegistry (O(n) but no FindObjects).
        /// </summary>
        private NetworkPlayer GetPlayerById(uint playerId)
        {
            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return null;
            foreach (var player in players)
            {
                if (player != null && player.ObjectId == playerId)
                    return player;
            }
            return null;
        }

        /// <summary>
        /// Sync scores to clients
        /// </summary>
        [Server]
        private void SyncScores()
        {
            var snapshot = new ScoreSnapshot
            {
                Teams   = new List<TeamScore>(teamScores.Values),
                Players = new List<PlayerScore>(playerScores.Values),
            };
            string json = JsonUtility.ToJson(snapshot);
            scoreDataJson.Value = json;
            _scoreSync?.SyncScoreData(json);
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

#if UNITY_EDITOR
        // ── Editor ───────────────────────────────────────────────────────────

        [UnityEngine.ContextMenu("NightHunt/Setup Default Score Configs")]
        private void Editor_SetupDefaultScoreConfigs()
        {
            _scoreConfigList = new List<ScoreSystemData>
            {
                new ScoreSystemData { Action = "Kill",             BaseScore = 100, PhaseMultiplier = 1f,   Notes = "" },
                new ScoreSystemData { Action = "Assist",           BaseScore = 75,  PhaseMultiplier = 1f,   Notes = "" },
                new ScoreSystemData { Action = "BeaconDestroy",    BaseScore = 50,  PhaseMultiplier = 1f,   Notes = "" },
                new ScoreSystemData { Action = "BossKill",         BaseScore = 300, PhaseMultiplier = 1.2f, Notes = "" },
                new ScoreSystemData { Action = "ObjectiveCapture", BaseScore = 20,  PhaseMultiplier = 1.1f, Notes = "per second" },
                new ScoreSystemData { Action = "SurvivalMinute",   BaseScore = 5,   PhaseMultiplier = 1f,   Notes = "1 point per minute alive" },
            };
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ScoringSystem] _scoreConfigList populated with 6 default entries from GameDesign JSON. Save scene để áp dụng.");
        }

        [UnityEngine.ContextMenu("NightHunt/Log Score Config Summary")]
        private void Editor_LogScoreConfigSummary()
        {
            if (_scoreConfigList == null || _scoreConfigList.Count == 0)
            {
                Debug.LogWarning("[ScoringSystem] _scoreConfigList trống! Run 'NightHunt/Setup Default Score Configs' trước.");
                return;
            }
            var sb = new System.Text.StringBuilder("[ScoringSystem] Score configs:\n");
            foreach (var cfg in _scoreConfigList)
                sb.AppendLine($"  {cfg.Action,-20} BaseScore={cfg.BaseScore,4}  ×{cfg.PhaseMultiplier}  {cfg.Notes}");
            Debug.Log(sb.ToString());
        }
#endif
    }

    /// <summary>
    /// Serializable snapshot of all scores — sent as JSON via ScoreSync.
    /// </summary>
    [System.Serializable]
    public class ScoreSnapshot
    {
        public List<TeamScore>   Teams;
        public List<PlayerScore> Players;
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

