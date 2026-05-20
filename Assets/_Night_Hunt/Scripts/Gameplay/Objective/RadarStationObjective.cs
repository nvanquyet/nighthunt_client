using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Radar station objective — Server Authoritative.
    ///
    /// Interaction pipeline:
    ///   1. Player aims at the radar collider.
    ///   2. RaycastDetector / ProximityInteractScanner finds IHoldInteractable.
    ///   3. InteractionPromptUI shows "[Hold E] Activate Radar · Xs".
    ///   4. Player holds E for activationTime seconds (timer in PlayerInteractionSystem).
    ///   5. On completion PlayerInteractionSystem calls Interact(interactor).
    ///   6. Interact() → ActivateRadarServerRpc(teamId) → OnComplete() server-side.
    ///
    /// NOTE: The radar collider must be on the layer included in RaycastDetector's hitLayers
    ///       (usually the "Interactable" layer) so the interaction system can find it.
    /// </summary>
    public class RadarStationObjective : NetworkBehaviour, IObjective, IHoldInteractable
    {
        [Header("Radar Station Settings")]
        [SerializeField] private string objectiveId   = "RADAR_STATION";
        [SerializeField] private string objectiveName = "Secure Radar Uplink";
        [SerializeField] private float  activationTime = 5f;

        [Header("Score")]
        [Tooltip("Score awarded to the activating team via MatchEndManager.AddObjectiveScore.")]
        [SerializeField] private float completionScore = 300f;

        private readonly SyncVar<bool>  _syncIsActivated = new SyncVar<bool>();
        private readonly SyncVar<float> _syncProgress    = new SyncVar<float>();

        // ── IObjective ─────────────────────────────────────────────────────────
        public string ObjectiveId   => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool   IsCompleted   => _syncIsActivated.Value;
        public float  Progress      => _syncProgress.Value;

        // ── IHoldInteractable (extends IInteractable) ─────────────────────────

        /// <summary>Seconds the player must hold [E]. Mirrors the server-side activationTime.</summary>
        public float HoldDuration => activationTime;

        public string InteractLabel => _syncIsActivated.Value
            ? $"({objectiveName} — Already Activated)"
            : $"[Hold E] {objectiveName} ({activationTime:F0}s)";

        /// <summary>
        /// Client-side pre-check. SyncVar is readable on all peers so this is correct.
        /// </summary>
        public bool CanInteract(GameObject interactor) => !_syncIsActivated.Value;

        /// <summary>
        /// Called by PlayerInteractionSystem after the hold timer completes.
        /// Resolves the player's team from the interactor's NetworkPlayer and fires a ServerRpc.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            var np = interactor.GetComponentInParent<NetworkPlayer>()
                  ?? interactor.GetComponent<NetworkPlayer>();
            int teamId = np != null ? np.TeamId : -1;
            ActivateRadarServerRpc(teamId);
        }

        public void OnHoverEnter(GameObject interactor) { /* TODO: highlight shader / outline */ }
        public void OnHoverExit(GameObject interactor)  { /* TODO: remove highlight */ }

        // ── IObjective lifecycle ───────────────────────────────────────────────

        public void OnStart()
        {
            if (!IsServerStarted) return;
            _syncIsActivated.Value = false;
            _syncProgress.Value    = 0f;
        }

        public void OnUpdate()
        {
            // Progress is driven by the client-side hold timer in PlayerInteractionSystem.
            // No server-side update needed; _syncProgress is set to 1 on completion.
        }

        [Server]
        public void OnComplete()
        {
            if (_syncIsActivated.Value) return;

            _syncIsActivated.Value = true;
            _syncProgress.Value    = 1f;

            Debug.Log($"[RadarStationObjective] '{objectiveName}' activated by team {_lastActivatingTeamId}.");

            if (_lastActivatingTeamId >= 0)
            {
                var mem = FindFirstObjectByType<MatchEndManager>();
                mem?.AddObjectiveScore(_lastActivatingTeamId, completionScore);

                var scoring = FindFirstObjectByType<ScoringSystem>();
                scoring?.AwardObjectiveCapture(_lastActivatingTeamId, activationTime);
            }
        }

        public void OnFail() { /* Radar station does not fail */ }

        // ── Network ────────────────────────────────────────────────────────────

        private int _lastActivatingTeamId = -1;

        /// <summary>
        /// Fired by the local player's Interact() after hold completes.
        /// RequireOwnership = false: the radar is not owned by the activating player.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ActivateRadarServerRpc(int teamId, NetworkConnection conn = null)
        {
            if (_syncIsActivated.Value) return;   // already activated — ignore race condition
            _lastActivatingTeamId = ResolveTeamId(conn, teamId);
            OnComplete();
        }

        private static int ResolveTeamId(NetworkConnection conn, int fallbackTeamId)
        {
            if (conn != null)
            {
                var player = RegistryService.Instance?.GetPlayerByFishNetId(conn.ClientId);
                if (player != null)
                    return player.TeamId;
            }

            return fallbackTeamId;
        }

        // ── Legacy server helpers (kept for backward compatibility if called elsewhere) ─

        /// <summary>
        /// Direct server call to start activation (bypasses IHoldInteractable pipeline).
        /// Prefer using the IHoldInteractable path via player interaction.
        /// </summary>
        [Server]
        public void StartInteraction(int teamId = -1)
        {
            _lastActivatingTeamId = teamId;
        }

        /// <summary>Legacy stop — no-op since progress is client-driven. </summary>
        [Server]
        public void StopInteraction() { }
    }
}
