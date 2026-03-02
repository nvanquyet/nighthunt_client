using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Core.State;
using NightHunt.Networking;
using UnityEngine;

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
        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        [Header("Settings")]
        [Tooltip("Team IDs present in the match (usually {0, 1}).")]
        [SerializeField] private int[] _teamIds = { 0, 1 };

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

        // ── Dependency injection ───────────────────────────────────────────────
        // BeaconManager will self-register; we keep a weak reference here.
        private IBeaconProvider _beaconProvider;

        // ──────────────────────────────────────────────────────────────────────
        #region Unity / FishNet Lifecycle

        private void Awake()
        {
            if (_phaseManager == null)
                _phaseManager = FindFirstObjectByType<MatchPhaseManager>();

            foreach (int teamId in _teamIds)
            {
                _teamKillScore[teamId]       = 0;
                _teamObjectiveScore[teamId]  = 0f;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Subscribe to per-player death so we can re-evaluate elimination after every death
            RegistryService.Instance.OnPlayerRegistered += (np, _) => SubscribePlayerDeath(np);

            // Listen for beacon destruction
            GameplayEventBus.Instance?.Subscribe<BeaconDestroyedEvent>(OnBeaconDestroyed);

            // Listen for phase changes so we know which rules apply
            _phaseManager.OnPhaseTransitioned += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            GameplayEventBus.Instance?.Unsubscribe<BeaconDestroyedEvent>(OnBeaconDestroyed);
            if (_phaseManager != null)
                _phaseManager.OnPhaseTransitioned -= OnPhaseChanged;
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Register a beacon provider (BeaconManager calls this on Awake).</summary>
        public void RegisterBeaconProvider(IBeaconProvider provider) => _beaconProvider = provider;

        /// <summary>Called by CaptureZoneObjective every second to tick score.</summary>
        public void AddObjectiveScore(int teamId, float deltaScore)
        {
            if (!IsServerStarted) return;
            if (_teamObjectiveScore.ContainsKey(teamId))
                _teamObjectiveScore[teamId] += deltaScore;
        }

        /// <summary>Called by CharacterCombat / ServerGameManager when a kill is confirmed.</summary>
        public void AddKill(int killerTeamId)
        {
            if (!IsServerStarted) return;
            if (_teamKillScore.ContainsKey(killerTeamId))
                _teamKillScore[killerTeamId]++;
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

        private void SubscribePlayerDeath(NetworkPlayer np)
        {
            // CharacterLifecycleController on the same GameObject fires OnDied
            var lifecycle = np.GetComponent<CharacterLifecycleController>();
            if (lifecycle != null)
                lifecycle.OnDied += () => OnPlayerDied(np);
        }

        private void OnPlayerDied(NetworkPlayer np)
        {
            if (!IsServerStarted || _matchEnded) return;

            // Mark player as dead in NetworkPlayer
            np.SetAlive(false);

            int teamId = np.TeamId;
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
        #region Phase change

        private void OnPhaseChanged(MatchPhaseState oldPhase, MatchPhaseState newPhase)
        {
            // When Lockdown starts, kick off periodic alive-count evaluation
            // (Phase 3 has respawn → we must re-check often, not just on death)
            if (newPhase == MatchPhaseState.Lockdown)
                StartCoroutine(Phase3EliminationLoop());
        }

        /// <summary>Periodic loop during Phase 3: re-checks every second.</summary>
        private IEnumerator Phase3EliminationLoop()
        {
            while (!_matchEnded && _phaseManager.CurrentPhase == MatchPhaseState.Lockdown)
            {
                foreach (int teamId in _teamIds)
                    EvaluateTeamElimination(teamId);
                yield return new WaitForSeconds(1f);
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Elimination logic

        private void EvaluateTeamElimination(int teamId)
        {
            if (_matchEnded) return;

            RegistryService registry = RegistryService.Instance;
            if (registry == null) return;

            bool allDead    = registry.GetAliveCount(teamId) == 0;
            bool noBeacons  = GetActiveBeaconCount(teamId) == 0;

            if (_phaseManager.CurrentPhase == MatchPhaseState.Lockdown)
            {
                // Phase 3: respawn is possible → only eliminate when BOTH alive AND respawn queue empty
                // RespawnSystem calls RecheckEliminationForTeam after each respawn attempt,
                // so we detect the "truly eliminated" state here.
                bool canRespawn = CanTeamRespawn(teamId);
                if (allDead && noBeacons && !canRespawn)
                {
                    TriggerElimination(teamId);
                }
            }
            else
            {
                // Phase 1 & 2: elimination is immediate
                if (allDead && noBeacons)
                    TriggerElimination(teamId);
            }
        }

        private void TriggerElimination(int eliminatedTeamId)
        {
            if (_matchEnded) return;
            _matchEnded = true;

            int winnerTeamId = ResolveWinner(eliminatedTeamId);
            MatchEndReason reason = MatchEndReason.TeamEliminated;

            Debug.Log($"[MatchEndManager] Team {eliminatedTeamId} eliminated → Winner: {(winnerTeamId >= 0 ? $"Team {winnerTeamId}" : "DRAW")}");

            // Publish server-local event
            OnMatchEnded?.Invoke(winnerTeamId, reason);

            // Broadcast to all clients
            RpcNotifyMatchEnd(winnerTeamId, (int)reason);

            // Build result payloads
            MatchResult[] results = BuildResults(winnerTeamId, reason);
            GameplayEventBus.Instance?.Publish(new MatchEndedEvent
            {
                WinnerTeamId   = winnerTeamId,
                Reason         = reason,
                PlayerResults  = results
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

            if (survivors.Count == 0) return -1;   // edge-case: draw
            if (survivors.Count == 1) return survivors[0]; // normal 2-team match

            // Multiple survivors — fall through to tie-break
            return ResolveTieBreak(survivors);
        }

        private int ResolveTieBreak(List<int> candidates)
        {
            RegistryService registry = RegistryService.Instance;

            int bestTeam  = -1;
            int bestAlive = -1;
            float bestScore = -1f;

            foreach (int teamId in candidates)
            {
                int aliveCount  = registry != null ? registry.GetAliveCount(teamId) : 0;
                float teamScore = GetTotalScore(teamId);

                if (aliveCount > bestAlive ||
                    (aliveCount == bestAlive && teamScore > bestScore))
                {
                    bestTeam  = teamId;
                    bestAlive = aliveCount;
                    bestScore = teamScore;
                }
            }

            // Check for true draw (all tied on alive + score)
            int tiedCount = 0;
            foreach (int teamId in candidates)
            {
                int alive  = registry != null ? registry.GetAliveCount(teamId) : 0;
                float score = GetTotalScore(teamId);
                if (alive == bestAlive && Mathf.Approximately(score, bestScore))
                    tiedCount++;
            }

            return tiedCount > 1 ? -1 : bestTeam;
        }

        private float GetTotalScore(int teamId)
        {
            float kills  = _teamKillScore.TryGetValue(teamId, out int k) ? k : 0;
            float obj    = _teamObjectiveScore.TryGetValue(teamId, out float o) ? o : 0f;
            return kills + obj;
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
                        DisplayName     = data?.DisplayName ?? "Unknown",
                        TeamId          = teamId,
                        Kills           = _playerKillCount.TryGetValue(pid, out int k) ? k : 0,
                        Deaths          = 0,
                        Score           = (int)GetTotalScore(teamId),
                        EloChange       = 0    // calculated server-side post-match
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
                WinnerTeamId  = winnerTeamId,
                Reason        = (MatchEndReason)reason,
                PlayerResults = Array.Empty<MatchResult>()   // full results sent separately via event
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
    }
}
