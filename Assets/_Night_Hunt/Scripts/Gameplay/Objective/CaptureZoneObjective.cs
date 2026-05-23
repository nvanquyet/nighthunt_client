using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Zone;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Match;
using NightHunt.GameplaySystems.Loot;
using NightHunt.GameplaySystems.World;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Server-authoritative Capture Zone Objective.
    /// Capturing progress and scoring are run ONLY on the Server to prevent cheating.
    /// </summary>
    public class CaptureZoneObjective : NetworkBehaviour, IObjective
    {
        [Header("Capture Zone Settings")]
        [SerializeField] private string objectiveId     = "CAPTURE_ZONE";
        [SerializeField] private string objectiveName   = "Capture Zone";
        [SerializeField] private float  captureRadius   = 10f;
        [SerializeField] private float  captureTime     = 15f;
        [SerializeField] private int    requiredPlayers = 1;

        [Header("Reward Configuration")]
        [Tooltip("WorldSpawnConfig chứa SpawnTable thưởng khi chiếm xong Zone.")]
        [SerializeField] private WorldSpawnConfig _zoneRewardConfig;

        [Header("Zone Gate")]
        [Tooltip("Zone only runs capture logic from this zone index onward.")]
        [SerializeField] private int _activeFromZoneIndex = 0;
        [Tooltip("Zone stops running at this index (-1 = always active).")]
        [SerializeField] private int _activeUntilZoneIndex = -1;

        [Header("Scoring")]
        [SerializeField] private int _scorePerSecond = 20;

        [Header("Debug")]
        [Tooltip("Bật/tắt display vòng bo và text thông số ngay trong screen Scene Editor")]
        [SerializeField] private bool _showDebug = false;

        // ── SyncVars ──
        private readonly SyncVar<float> _syncProgress = new SyncVar<float>();
        private readonly SyncVar<int> _syncControllingTeam = new SyncVar<int>(-1); // -1: none, -2: contested
        private readonly SyncVar<bool> _syncIsCompleted = new SyncVar<bool>();

        // IObjective Properties 
        public string ObjectiveId   => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool  IsCompleted   => _syncIsCompleted.Value;
        public float Progress      => _syncProgress.Value; // Clients read this for UI
        public int ControllingTeamId => _syncControllingTeam.Value;
        public bool IsContested => _syncControllingTeam.Value == -2;
        public float CaptureRadius => captureRadius;
        public int ActiveFromZoneIndex => _activeFromZoneIndex;
        public int ActiveUntilZoneIndex => _activeUntilZoneIndex;

        // ── Server Runtime ──
        private float _scoreAccumulator = 0f;
        private readonly List<NetworkPlayer> _playersInZone = new();
        private MatchEndManager _matchEndManager;
        private ScoringSystem _scoringSystem;

        public void OnStart()
        {
            if (_matchEndManager == null)
                _matchEndManager = FindFirstObjectByType<MatchEndManager>();
            if (_scoringSystem == null)
                _scoringSystem = FindFirstObjectByType<ScoringSystem>();
        }

        public void OnUpdate()
        {
            if (!IsServerStarted || IsCompleted) return;

            // Zone Gate — only run capture logic during configured zone range
            int zoneIdx = SafeZoneManager.Instance?.ZoneIndex ?? 0;
            bool gateOk = zoneIdx >= _activeFromZoneIndex &&
                          (_activeUntilZoneIndex < 0 || zoneIdx <= _activeUntilZoneIndex);
            if (!gateOk) return;

            UpdatePlayersInZone();
            UpdateCapture();
            TickScore();
        }

        [Server]
        private void UpdateCapture()
        {
            int controllerTeam = GetControllingTeamId();
            _syncControllingTeam.Value = controllerTeam;

            if (controllerTeam >= 0)
            {
                // Unopposed team is capturing
                _syncProgress.Value += Time.deltaTime / captureTime;
                _syncProgress.Value = Mathf.Clamp01(_syncProgress.Value);

                if (_syncProgress.Value >= 1f)
                {
                    OnComplete();
                }
            }
            else if (controllerTeam == -1)
            {
                // No one in zone, decays slowly
                _syncProgress.Value = Mathf.Max(0f, _syncProgress.Value - Time.deltaTime / captureTime);
            }
            else if (controllerTeam == -2)
            {
                // Contested! Pause progress (Do nothing to _syncProgress)
            }
        }

        [Server]
        private void TickScore()
        {
            if (_syncControllingTeam.Value < 0) return;
            if (_matchEndManager == null) return;

            _scoreAccumulator += Time.deltaTime;
            while (_scoreAccumulator >= 1f)
            {
                _scoreAccumulator -= 1f;
                int score = _scorePerSecond;
                _matchEndManager.AddObjectiveScore(_syncControllingTeam.Value, score);
            }
        }

        [Server]
        public void OnComplete()
        {
            if (IsCompleted) return;
            
            _syncIsCompleted.Value = true;
            _syncProgress.Value = 1f;

            int team = _syncControllingTeam.Value;
            Debug.Log($"[CaptureZoneObjective] Zone '{objectiveName}' captured by team {team}");

            // Spawn phần thưởng
            if (WorldSpawnManager.Instance != null && _zoneRewardConfig != null)
            {
                WorldSpawnManager.Instance.SpawnWorldContainer(_zoneRewardConfig, transform.position);
                Debug.Log($"[CaptureZoneObjective] Spawned reward container at {transform.position}");
            }

            // Award bonus score to capturing team
            float multiplier = 1f; // phase multiplier removed — SafeZone uses flat rates
            int   captureBonus = Mathf.RoundToInt(500f * multiplier);
            _matchEndManager?.AddObjectiveScore(team, captureBonus);
            // AwardObjectiveCapture signature: (int teamId, float captureTime)
            _scoringSystem?.AwardObjectiveCapture(team, captureTime);

            // Publish event so KillFeed / Minimap can react
            GameplayEventBus.Instance?.Publish(new ObjectiveCapturedEvent
            {
                ObjectiveId = objectiveId,
                ObjectiveName = objectiveName,
                CapturingTeamId = team,
            });
        }

        public void OnFail()
        {
            // Zones don't fail, they just wait
        }

        // Returns -1 if no one, -2 if contested, or Team Id if unopposed
        [Server]
        private int GetControllingTeamId()
        {
            var countPerTeam = new Dictionary<int, int>();
            foreach (var p in _playersInZone)
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

        [Server]
        private void UpdatePlayersInZone()
        {
            _playersInZone.Clear();
            var allPlayers = RegistryService.Instance.GetAllPlayers();

            foreach (var player in allPlayers)
            {
                if (!player.IsAlive) continue;
                if (Vector3.Distance(transform.position, player.transform.position) <= captureRadius)
                    _playersInZone.Add(player);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showDebug) return;

            // Màu bo: Vàng (idle/chiếm), Đỏ (Đang tranh chấp - Contested)
            Gizmos.color = Application.isPlaying && _syncControllingTeam.Value == -2 ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, captureRadius);

            // Bảng thông số nổi
            string text = $"[ Zone: {objectiveName} ]\n";
            text += $"Bán kính: {captureRadius}m | Thời gian: {captureTime}s\n";

            if (Application.isPlaying)
            {
                text += "───── RUNTIME ─────\n";
                text += $"Tiến độ: {(_syncProgress.Value * 100):F1}%\n";
                string teamStr = _syncControllingTeam.Value switch {
                    -2 => "Tranh Chấp (Contested)",
                    -1 => "Empty (None)",
                     _ => $"Team {_syncControllingTeam.Value}"
                };
                text += $"Trạng thái: {teamStr}\n";
                text += $"Người trong bo: {_playersInZone.Count}/{requiredPlayers} (yc)\n";
                text += $"Tích lũy điểm: {_scoreAccumulator:F0}";
            }

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.cyan;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, text, style);
        }
#endif
    }
}
