using UnityEngine;
using System.Collections.Generic;
using FishNet;
using NightHunt.Data;
using NightHunt.Gameplay.Match;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Capture zone objective — extended with tick scoring.
    ///
    /// Scoring logic (server-authoritative):
    ///   • Checks which team has ≥ minPlayers inside.
    ///   • If one team controls the zone, ticks <c>scorePerSecond</c> points
    ///     to that team via <see cref="MatchEndManager.AddObjectiveScore"/>.
    ///   • Config values loaded from <see cref="GameConfigLoader.GetMatchEndConfig"/>.
    ///
    /// This MonoBehaviour is expected to run on the server GameObject only,
    /// or is safe to run client-side because <see cref="IsServer"/> guards it.
    /// </summary>
    public class CaptureZoneObjective : MonoBehaviour, IObjective
    {
        [Header("Capture Zone Settings")]
        [SerializeField] private string objectiveId     = "CAPTURE_ZONE";
        [SerializeField] private string objectiveName   = "Capture Zone";
        [SerializeField] private float  captureRadius   = 10f;
        [SerializeField] private float  captureTime     = 10f;
        [SerializeField] private int    requiredPlayers = 1;

        [Header("Scoring (overridden at runtime from GameConfig)")]
        [SerializeField] private float _scorePerSecond   = 1f;
        [SerializeField] private float _scoreAccumulator = 0f;

        public string ObjectiveId   => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool  IsCompleted   { get; private set; }
        public float Progress      { get; private set; }

        // ── Runtime ───────────────────────────────────────────────────────────
        private float   captureProgress = 0f;
        private int     _controllingTeam = -1;       // -1 = none, -2 = contested
        private readonly List<NetworkPlayer> playersInZone = new();

        private MatchEndManager _matchEndManager;

        // ──────────────────────────────────────────────────────────────────────
        private bool IsServer => InstanceFinder.IsServerStarted;

        // ──────────────────────────────────────────────────────────────────────
        public void OnStart()
        {
            IsCompleted     = false;
            Progress        = 0f;
            captureProgress = 0f;
            _controllingTeam = -1;

            // Load config
            var cfg = GameConfigLoader.Instance?.GetMatchEndConfig();
            if (cfg != null)
            {
                if (cfg.CaptureZoneScorePerSecond > 0f)
                    _scorePerSecond = cfg.CaptureZoneScorePerSecond;
                if (cfg.CaptureZoneMinPlayers > 0)
                    requiredPlayers = cfg.CaptureZoneMinPlayers;
            }

            if (_matchEndManager == null)
                _matchEndManager = FindFirstObjectByType<MatchEndManager>();
        }

        public void OnUpdate()
        {
            UpdatePlayersInZone();
            UpdateCapture();

            if (IsServer)
                TickScore();
        }

        public void OnComplete()
        {
            IsCompleted = true;
            Progress    = 1f;
            Debug.Log($"[CaptureZoneObjective] Zone captured: {objectiveName} by team {_controllingTeam}");
        }

        public void OnFail()
        {
            // Capture zone doesn't fail
        }

        // ──────────────────────────────────────────────────────────────────────
        #region Zone logic

        private void UpdateCapture()
        {
            int controllerTeam = GetControllingTeamId();
            _controllingTeam = controllerTeam;

            bool activeCapture = controllerTeam >= 0;

            if (activeCapture)
            {
                captureProgress += Time.deltaTime / captureTime;
                captureProgress  = Mathf.Clamp01(captureProgress);
                Progress         = captureProgress;

                if (captureProgress >= 1f && !IsCompleted)
                    OnComplete();
            }
            else
            {
                // Decay when contested or empty
                captureProgress  = Mathf.Max(0f, captureProgress - Time.deltaTime / captureTime);
                Progress         = captureProgress;
            }
        }

        private void TickScore()
        {
            if (_controllingTeam < 0) return;
            if (_matchEndManager  == null) return;

            _scoreAccumulator += Time.deltaTime;
            while (_scoreAccumulator >= 1f)
            {
                _scoreAccumulator -= 1f;
                _matchEndManager.AddObjectiveScore(_controllingTeam, _scorePerSecond);
            }
        }

        /// <summary>
        /// Returns the team that has ≥ requiredPlayers in zone with no opposition,
        /// -1 if no one qualifies, -2 if contested.
        /// </summary>
        private int GetControllingTeamId()
        {
            var countPerTeam = new Dictionary<int, int>();
            foreach (var p in playersInZone)
            {
                int tid = p.TeamId;
                countPerTeam.TryGetValue(tid, out int cnt);
                countPerTeam[tid] = cnt + 1;
            }

            int winner   = -1;
            bool contest = false;

            foreach (var kv in countPerTeam)
            {
                if (kv.Value >= requiredPlayers)
                {
                    if (winner == -1)
                        winner = kv.Key;
                    else
                    {
                        contest = true;
                        break;
                    }
                }
            }

            if (contest) return -2;
            return winner;
        }

        private void UpdatePlayersInZone()
        {
            playersInZone.Clear();
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                if (!player.IsAlive) continue;
                if (Vector3.Distance(transform.position, player.transform.position) <= captureRadius)
                    playersInZone.Add(player);
            }
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }
}
