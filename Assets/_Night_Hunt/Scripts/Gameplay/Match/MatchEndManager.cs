using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Zone;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.Diagnostics;

namespace NightHunt.Gameplay.Match
{
    /// <summary>
    /// Server-authoritative match-end arbitration.
    ///
    /// Elimination rules:
    ///   Phase 1 & 2  — a team is "eliminated" when ALL its players are dead
    ///                  AND it has no active beacons left.
    ///   Phase 3      — same as above, but dead players can respawn after a delay;
    ///                  the check is deferred: we only act when the respawn queue
    ///                  for that team is also empty (handled by RespawnSystem callback).
    ///
    /// Tie-break priority:
    ///   1. More alive players
    ///   2. Higher total score (CaptureZone + kills)
    ///   3. Draw
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchEndManager : NetworkBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("References")] [SerializeField]
        private ScoringSystem _scoringSystem;

        [Header("Settings")] [Tooltip("Team IDs present in the match (usually {0, 1}).")] [SerializeField]
        private int[] _teamIds = { 0, 1 };

        [Tooltip("If a team is fully cleared before final zone and cannot respawn, wait this many seconds then force final zone.")]
        [SerializeField, Min(0f)] private float _earlyClearFinalZoneDelaySeconds = 10f;

        [Tooltip("Delay applied to the forced final-zone respawn after the early-clear countdown completes.")]
        [SerializeField, Min(0f)] private float _earlyClearForcedRespawnDelaySeconds = 0f;

        // ── Public events (server-only) ────────────────────────────────────────
        /// <summary>Raised on the server when the match resolves. winnerTeamId == -1 → Draw.</summary>
        public event Action<int, MatchEndReason> OnMatchEnded;

        // ── Runtime ────────────────────────────────────────────────────────────
        private bool _matchEnded = false;

        // Per-team kill score accumulator (server-side, not synced — only needed for tie-break)
        private readonly Dictionary<int, int> _teamKillScore = new();

        // Per-team objective score (supplied by CaptureZoneObjective via AddScore)
        private readonly Dictionary<int, float> _teamObjectiveScore = new();

        // Per-player kill counter keyed by BackendPlayerId
        private readonly Dictionary<string, int> _playerKillCount = new();

        // Per-player death counter keyed by BackendPlayerId (Bug #13 fix)
        private readonly Dictionary<string, int> _playerDeathCount = new();
        private readonly Dictionary<CharacterLifecycleController, Action> _deathSubscriptions = new();
        private SafeZoneManager _subscribedSafeZoneManager;
        private Coroutine _earlyClearFinalZoneCoroutine;
        private bool _forcingFinalZoneAfterClear;

        // ── Dependency injection ───────────────────────────────────────────────
        // BeaconManager will self-register; we keep a weak reference here.
        private IBeaconProvider _beaconProvider;

        // ──────────────────────────────────────────────────────────────────────

        #region Unity / FishNet Lifecycle

        private void Awake()
        {
            if (_scoringSystem == null)
                _scoringSystem = FindFirstObjectByType<ScoringSystem>();

            foreach (int teamId in _teamIds)
            {
                _teamKillScore[teamId] = 0;
                _teamObjectiveScore[teamId] = 0f;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            RegistryService registry = RegistryService.Instance;
            if (registry != null)
            {
                registry.OnPlayerRegistered += HandlePlayerRegistered;
                registry.OnPlayerUnregistered += HandlePlayerUnregistered;

                foreach (var player in registry.GetAllPlayers())
                    SubscribePlayerDeath(player);
            }
            else
            {
                Debug.LogWarning("[MatchEndManager] RegistryService not found; elimination death hooks will attach when registry is available.");
            }

            // Listen for beacon destruction
            GameplayEventBus.Instance?.Subscribe<BeaconDestroyedEvent>(OnBeaconDestroyed);

            // Listen for SafeZoneManager: final zone expired -> score-based win resolution.
            TrySubscribeSafeZoneManager();
            if (_subscribedSafeZoneManager == null)
                StartCoroutine(SubscribeSafeZoneWhenReady());
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RegistryService registry = RegistryService.Instance;
            if (registry != null)
            {
                registry.OnPlayerRegistered -= HandlePlayerRegistered;
                registry.OnPlayerUnregistered -= HandlePlayerUnregistered;
            }

            UnsubscribeAllPlayerDeaths();

            GameplayEventBus.Instance?.Unsubscribe<BeaconDestroyedEvent>(OnBeaconDestroyed);
            if (_subscribedSafeZoneManager != null)
            {
                _subscribedSafeZoneManager.OnFinalZoneExpired -= OnFinalZoneExpired;
                _subscribedSafeZoneManager = null;
            }

            if (_earlyClearFinalZoneCoroutine != null)
            {
                StopCoroutine(_earlyClearFinalZoneCoroutine);
                _earlyClearFinalZoneCoroutine = null;
            }
            _forcingFinalZoneAfterClear = false;
        }

        private void TrySubscribeSafeZoneManager()
        {
            if (_subscribedSafeZoneManager != null)
                return;

            SafeZoneManager manager = SafeZoneManager.Instance ?? FindFirstObjectByType<SafeZoneManager>();
            if (manager == null)
                return;

            _subscribedSafeZoneManager = manager;
            _subscribedSafeZoneManager.OnFinalZoneExpired += OnFinalZoneExpired;
        }

        private IEnumerator SubscribeSafeZoneWhenReady()
        {
            while (!_matchEnded && _subscribedSafeZoneManager == null)
            {
                TrySubscribeSafeZoneManager();
                if (_subscribedSafeZoneManager != null)
                    yield break;

                yield return null;
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Public API

        /// <summary>Register a beacon provider (BeaconManager calls this on Awake).</summary>
        public void RegisterBeaconProvider(IBeaconProvider provider) => _beaconProvider = provider;

        /// <summary>
        /// Returns final per-player results for reporting to backend (called by ServerBootstrap).
        /// Aggregates kills, deaths, scores from tracked dictionaries + RegistryService.
        /// </summary>
        public MatchResult[] GetFinalResults(int winnerTeamId, MatchEndReason reason)
        {
            var registry = RegistryService.Instance;
            if (registry == null) return System.Array.Empty<MatchResult>();

            var results = new System.Collections.Generic.List<MatchResult>();
            var allPlayers = registry.GetAllPlayers();

            foreach (var np in allPlayers)
            {
                var data = registry.GetPrivateDataByFishNetId(np.OwnerId);
                if (data == null) continue;

                string pid = data?.BackendPlayerId ?? string.Empty;
                _playerKillCount.TryGetValue(pid, out int kills);
                _playerDeathCount.TryGetValue(pid, out int deaths);
                int score = _scoringSystem != null
                    ? _scoringSystem.GetPlayerScore((uint)np.ObjectId)
                    : Mathf.RoundToInt(GetTotalScore(np.TeamId));

                results.Add(new MatchResult
                {
                    BackendPlayerId = pid,
                    DisplayName     = np.DisplayName ?? string.Empty,
                    TeamId          = np.TeamId,
                    Kills           = kills,
                    Deaths          = deaths,
                    Score           = score,
                });
            }

            return results.ToArray();
        }

        /// <summary>Called by CaptureZoneObjective every second to tick score.</summary>
        public void AddObjectiveScore(int teamId, float deltaScore)
        {
            if (!IsServerStarted) return;
            if (_teamObjectiveScore.ContainsKey(teamId))
            {
                _teamObjectiveScore[teamId] += deltaScore;
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Score,
                    "ObjectiveScoreAdded",
                    $"team={teamId} delta={deltaScore:F1} objectiveTotal={_teamObjectiveScore[teamId]:F1} total={GetTotalScore(teamId):F1}",
                    this);
            }
        }

        /// <summary>Called by CharacterCombat / ServerGameManager when a kill is confirmed.</summary>
        public void AddKill(int killerTeamId)
        {
            if (!IsServerStarted) return;
            if (_teamKillScore.ContainsKey(killerTeamId))
            {
                _teamKillScore[killerTeamId]++;
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Score,
                    "MatchKillAdded",
                    $"team={killerTeamId} teamKills={_teamKillScore[killerTeamId]} total={GetTotalScore(killerTeamId):F1}",
                    this);
            }
        }

        /// <summary>
        /// Overload that additionally tracks kills per player.
        /// Callers that know the killer's BackendPlayerId should prefer this.
        /// </summary>
        public void AddKill(int killerTeamId, string backendPlayerId)
        {
            AddKill(killerTeamId);
            if (!string.IsNullOrEmpty(backendPlayerId))
            {
                _playerKillCount.TryGetValue(backendPlayerId, out int prev);
                _playerKillCount[backendPlayerId] = prev + 1;
            }
        }

        /// <summary>
        /// Called externally (e.g. RespawnSystem) after a Phase-3 respawn attempt
        /// to re-evaluate whether a team is now eliminated.
        /// </summary>
        public void RecheckEliminationForTeam(int teamId)
        {
            if (!IsServerStarted || _matchEnded) return;
            EvaluateTeamElimination(teamId);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Player death hook

        private void HandlePlayerRegistered(NetworkPlayer np, PlayerRegistryData _)
        {
            SubscribePlayerDeath(np);
        }

        private void HandlePlayerUnregistered(NetworkPlayer np, PlayerRegistryData _)
        {
            UnsubscribePlayerDeath(np);
        }

        private void SubscribePlayerDeath(NetworkPlayer np)
        {
            if (np == null)
                return;

            // CharacterLifecycleController on the same GameObject fires OnDied
            var lifecycle = ComponentResolver.Find<CharacterLifecycleController>(np)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CharacterLifecycleController not found")
                .Resolve();
            if (lifecycle == null || _deathSubscriptions.ContainsKey(lifecycle))
                return;

            Action handler = () => OnPlayerDied(np);
            _deathSubscriptions[lifecycle] = handler;
            lifecycle.OnDied += handler;
        }

        private void UnsubscribePlayerDeath(NetworkPlayer np)
        {
            if (np == null)
                return;

            var lifecycle = ComponentResolver.Find<CharacterLifecycleController>(np)
                .OnSelf()
                .InChildren()
                .Resolve();

            if (lifecycle == null || !_deathSubscriptions.TryGetValue(lifecycle, out var handler))
                return;

            lifecycle.OnDied -= handler;
            _deathSubscriptions.Remove(lifecycle);
        }

        private void UnsubscribeAllPlayerDeaths()
        {
            foreach (var kvp in _deathSubscriptions)
            {
                if (kvp.Key != null)
                    kvp.Key.OnDied -= kvp.Value;
            }

            _deathSubscriptions.Clear();
        }

        private void OnPlayerDied(NetworkPlayer np)
        {
            if (!IsServerStarted || _matchEnded) return;

            // Mark player as dead in NetworkPlayer
            np.SetAlive(false);

            // Bug #13 fix: track per-player death count
            var data = RegistryService.Instance?.GetPrivateDataByFishNetId(np.OwnerId);
            string pid = data?.BackendPlayerId ?? string.Empty;
            if (!string.IsNullOrEmpty(pid))
            {
                _playerDeathCount.TryGetValue(pid, out int prev);
                _playerDeathCount[pid] = prev + 1;
            }

            int teamId = np.TeamId;
            int deathCount = 0;
            if (!string.IsNullOrEmpty(pid))
                _playerDeathCount.TryGetValue(pid, out deathCount);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "MatchPlayerDied",
                $"player={np.DisplayName} team={teamId} backend={pid} deaths={deathCount} aliveCount={RegistryService.Instance?.GetAliveCount(teamId) ?? -1}",
                this);
            EvaluateTeamElimination(teamId);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Beacon event

        private void OnBeaconDestroyed(BeaconDestroyedEvent evt)
        {
            if (_matchEnded) return;
            // Re-check the owning team: if all dead + no beacons → eliminated
            EvaluateTeamElimination(evt.OwnerTeamId);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Final zone event

        /// <summary>
        /// Called when SafeZoneManager fires OnFinalZoneExpired.
        /// Final timer expiry is a hard match-end point: resolve elimination first, then score.
        /// </summary>
        private void OnFinalZoneExpired()
        {
            if (_matchEnded) return;

            foreach (int teamId in _teamIds)
                EvaluateTeamElimination(teamId);

            if (!_matchEnded)
                OnFinalZoneTimerResolution();
        }

        /// <summary>
        /// Score-based match resolution for final-zone timeout.
        /// Uses ScoringSystem.GetTeamScore() for accurate accumulated scores.
        /// </summary>
        private void OnFinalZoneTimerResolution()
        {
            if (_matchEnded) return;
            _matchEnded = true;
            SafeZoneManager.Instance?.EndMatch();

            int winnerTeamId = -1;
            float bestScore  = -1f;
            int tiedCount    = 0;

            foreach (int teamId in _teamIds)
            {
                float score = GetTotalScore(teamId);
                if (score > bestScore)
                {
                    bestScore    = score;
                    winnerTeamId = teamId;
                    tiedCount    = 1;
                }
                else if (Mathf.Approximately(score, bestScore))
                    tiedCount++;
            }

            if (tiedCount > 1) winnerTeamId = -1;

            MatchEndReason reason = MatchEndReason.TimerExpired;
            Debug.Log($"[MatchEndManager] Final zone expired — Winner: {(winnerTeamId >= 0 ? $"Team {winnerTeamId}" : "DRAW")} scores={BuildScoreSummary()}");

            PhaseTestLog.Log(PhaseTestLogCategory.Score, "MatchEnded",
                $"reason={reason} winnerTeam={winnerTeamId} scores={BuildScoreSummary()}", this);
            OnMatchEnded?.Invoke(winnerTeamId, reason);
            RpcNotifyMatchEnd(winnerTeamId, (int)reason);

            MatchResult[] results = BuildResults(winnerTeamId, reason);
            GameplayEventBus.Instance?.Publish(new MatchEndedEvent
            {
                WinnerTeamId  = winnerTeamId,
                Reason        = reason,
                PlayerResults = results
            });
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Elimination logic

        private void EvaluateTeamElimination(int teamId)
        {
            if (_matchEnded) return;

            RegistryService registry = RegistryService.Instance;
            if (registry == null) return;

            int aliveCount = registry.GetAliveCount(teamId);
            int beaconCount = GetActiveBeaconCount(teamId);
            bool allDead   = aliveCount == 0;
            bool noBeacons = beaconCount == 0;

            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            bool hasPendingRespawn = CanTeamRespawn(teamId);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "TeamEliminationCheck",
                $"team={teamId} finalZone={isInFinalZone} alive={aliveCount} beacons={beaconCount} pendingRespawn={hasPendingRespawn} allDead={allDead} noBeacons={noBeacons}",
                this);

            if (!allDead || hasPendingRespawn)
            {
                return;
            }

            if (_forcingFinalZoneAfterClear)
            {
                return;
            }

            if (isInFinalZone)
            {
                // Final zone: only the one-time queued revival prevents elimination.
                if (allDead)
                    TriggerElimination(teamId);
            }
            else
            {
                if (allDead && noBeacons)
                    StartEarlyClearFinalZoneFlow(teamId);
            }
        }

        private void StartEarlyClearFinalZoneFlow(int eliminatedTeamId)
        {
            if (_forcingFinalZoneAfterClear || _matchEnded)
                return;

            _forcingFinalZoneAfterClear = true;
            _earlyClearFinalZoneCoroutine = StartCoroutine(EarlyClearFinalZoneFlow(eliminatedTeamId));
        }

        private IEnumerator EarlyClearFinalZoneFlow(int eliminatedTeamId)
        {
            float delay = Mathf.Max(0f, _earlyClearFinalZoneDelaySeconds);
            SafeZoneManager safeZone = SafeZoneManager.Instance ?? FindFirstObjectByType<SafeZoneManager>();
            bool forceStarted = safeZone != null && safeZone.ForceFinalZoneAfterDelay(delay);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "EarlyClearForceFinalZone",
                $"team={eliminatedTeamId} delay={delay:F2} safeZone={(safeZone != null ? "ok" : "null")} forceStarted={forceStarted}",
                this);

            if (!forceStarted && safeZone != null && safeZone.IsInFinalZone)
            {
                _respawnProvider?.ForceFinalZoneRespawn(_earlyClearForcedRespawnDelaySeconds, "early_team_clear_already_final");
                _forcingFinalZoneAfterClear = false;
                _earlyClearFinalZoneCoroutine = null;
                yield break;
            }

            if (forceStarted)
            {
                yield return new WaitForSeconds(delay);

                float deadline = Time.time + 1f;
                while (!_matchEnded && safeZone != null && !safeZone.IsInFinalZone && Time.time < deadline)
                    yield return null;

                if (!_matchEnded && safeZone != null && safeZone.IsInFinalZone)
                {
                    _respawnProvider?.ForceFinalZoneRespawn(
                        _earlyClearForcedRespawnDelaySeconds,
                        "early_team_clear_final_zone");
                }
            }
            else if (!_matchEnded)
            {
                Debug.LogWarning($"[MatchEndManager] Early clear flow could not force final zone. Falling back to elimination for team {eliminatedTeamId}.");
                TriggerElimination(eliminatedTeamId);
            }

            _forcingFinalZoneAfterClear = false;
            _earlyClearFinalZoneCoroutine = null;
        }

        private void TriggerElimination(int eliminatedTeamId)
        {
            if (_matchEnded) return;
            _matchEnded = true;
            SafeZoneManager.Instance?.EndMatch();

            int winnerTeamId = ResolveWinner(eliminatedTeamId);
            MatchEndReason reason = MatchEndReason.TeamEliminated;

            Debug.Log(
                $"[MatchEndManager] Team {eliminatedTeamId} eliminated → Winner: {(winnerTeamId >= 0 ? $"Team {winnerTeamId}" : "DRAW")}");

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "MatchEnded",
                $"reason={reason} eliminatedTeam={eliminatedTeamId} winnerTeam={winnerTeamId} scores={BuildScoreSummary()}",
                this);

            // Publish server-local event
            OnMatchEnded?.Invoke(winnerTeamId, reason);

            // Broadcast to all clients
            RpcNotifyMatchEnd(winnerTeamId, (int)reason);

            // Build result payloads
            MatchResult[] results = BuildResults(winnerTeamId, reason);
            GameplayEventBus.Instance?.Publish(new MatchEndedEvent
            {
                WinnerTeamId = winnerTeamId,
                Reason = reason,
                PlayerResults = results
            });
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Tie-break resolution

        private int ResolveWinner(int eliminatedTeamId)
        {
            // Determine the "other" surviving teams
            List<int> survivors = new List<int>();
            foreach (int id in _teamIds)
            {
                if (id != eliminatedTeamId)
                    survivors.Add(id);
            }

            if (survivors.Count == 0) return -1; // edge-case: draw
            if (survivors.Count == 1) return survivors[0]; // normal 2-team match

            // Multiple survivors — fall through to tie-break
            return ResolveTieBreak(survivors);
        }

        private int ResolveTieBreak(List<int> candidates)
        {
            RegistryService registry = RegistryService.Instance;

            int bestTeam = -1;
            int bestAlive = -1;
            float bestScore = -1f;

            foreach (int teamId in candidates)
            {
                int aliveCount = registry != null ? registry.GetAliveCount(teamId) : 0;
                float teamScore = GetTotalScore(teamId);

                if (aliveCount > bestAlive ||
                    (aliveCount == bestAlive && teamScore > bestScore))
                {
                    bestTeam = teamId;
                    bestAlive = aliveCount;
                    bestScore = teamScore;
                }
            }

            // Check for true draw (all tied on alive + score)
            int tiedCount = 0;
            foreach (int teamId in candidates)
            {
                int alive = registry != null ? registry.GetAliveCount(teamId) : 0;
                float score = GetTotalScore(teamId);
                if (alive == bestAlive && Mathf.Approximately(score, bestScore))
                    tiedCount++;
            }

            return tiedCount > 1 ? -1 : bestTeam;
        }

        private float GetTotalScore(int teamId)
        {
            // Prefer ScoringSystem's accumulated value (includes survival + zone ticks)
            if (_scoringSystem != null)
                return _scoringSystem.GetTeamScore(teamId);
            // Fallback: legacy kill + objective accumulators
            float kills = _teamKillScore.TryGetValue(teamId, out int k) ? k : 0;
            float obj   = _teamObjectiveScore.TryGetValue(teamId, out float o) ? o : 0f;
            return kills + obj;
        }

        private string BuildScoreSummary()
        {
            var parts = new List<string>();
            foreach (int teamId in _teamIds)
                parts.Add($"T{teamId}={GetTotalScore(teamId):F1}");
            return string.Join(",", parts);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Helpers

        private int GetActiveBeaconCount(int teamId)
        {
            return _beaconProvider?.GetActiveBeaconCount(teamId) ?? 0;
        }

        /// <summary>
        /// Returns true if the given team still has pending respawns queued.
        /// RespawnSystem should implement IRespawnProvider and self-register; 
        /// until it does, we return false (so Phase 3 elimination still works).
        /// </summary>
        private bool CanTeamRespawn(int teamId)
        {
            return _respawnProvider?.HasPendingRespawn(teamId) ?? false;
        }

        private IRespawnProvider _respawnProvider;

        /// <summary>Called by RespawnSystem.Awake() to register itself.</summary>
        public void RegisterRespawnProvider(IRespawnProvider provider) => _respawnProvider = provider;

        private MatchResult[] BuildResults(int winnerTeamId, MatchEndReason reason)
        {
            RegistryService registry = RegistryService.Instance;
            if (registry == null) return Array.Empty<MatchResult>();

            var list = new List<MatchResult>();
            foreach (int teamId in _teamIds)
            {
                var players = registry.GetPlayersByTeam(teamId);
                foreach (var np in players)
                {
                    var data = registry.GetPrivateDataByFishNetId(np.OwnerId);
                    string pid = data?.BackendPlayerId ?? string.Empty;
                    list.Add(new MatchResult
                    {
                        BackendPlayerId = pid,
                        DisplayName = data?.DisplayName ?? "Unknown",
                        TeamId = teamId,
                        Kills = _playerKillCount.TryGetValue(pid, out int k) ? k : 0,
                        Assists = _scoringSystem != null ? _scoringSystem.GetPlayerAssists((uint)np.ObjectId) : 0,
                        Deaths = _playerDeathCount.TryGetValue(pid, out int d) ? d : 0, // Bug #13 fix
                        Score = _scoringSystem != null ? _scoringSystem.GetPlayerScore((uint)np.ObjectId) : 0,
                        EloChange = 0 // calculated server-side post-match
                    });
                }
            }

            return list.ToArray();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region RPCs

        [ObserversRpc]
        private void RpcNotifyMatchEnd(int winnerTeamId, int reason)
        {
            GameplayEventBus.Instance?.Publish(new MatchEndedEvent
            {
                WinnerTeamId = winnerTeamId,
                Reason = (MatchEndReason)reason,
                PlayerResults = Array.Empty<MatchResult>() // full results sent separately via event
            });
        }

        #endregion
    }

    // ── Provider interfaces (implemented by future BeaconManager / RespawnSystem) ──

    /// <summary>Implemented by BeaconManager to supply active-beacon counts.</summary>
    public interface IBeaconProvider
    {
        int GetActiveBeaconCount(int teamId);
    }

    /// <summary>Implemented by RespawnSystem to indicate pending respawns.</summary>
    public interface IRespawnProvider
    {
        bool HasPendingRespawn(int teamId);
        void ForceFinalZoneRespawn(float delaySeconds, string reason);
    }
}
