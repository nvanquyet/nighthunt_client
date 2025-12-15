using System.Collections;
using UnityEngine;
using NightHunt.Data;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Match
{
    /// <summary>
    /// Manages match phases (Phase 1: Preparation, Phase 2: Hunt, Phase 3: Lockdown)
    /// Server-authoritative phase management
    /// </summary>
    public class MatchPhaseManager : NetworkBehaviour
    {
        [Header("Phase Settings")]
        [SerializeField] private string currentPhase = "Phase1_Preparation";
        [SerializeField] private float phaseStartTime;
        [SerializeField] private float phaseDuration;

        // Synchronized phase state
        private readonly SyncVar<string> networkPhase = new SyncVar<string>();
        private readonly SyncVar<float> networkPhaseStartTime = new SyncVar<float>();
        private readonly SyncVar<float> networkPhaseDuration = new SyncVar<float>();

        private MatchPhaseConfigData currentPhaseConfig;

        public string CurrentPhase => networkPhase.Value;
        public float PhaseElapsedTime => Time.time - networkPhaseStartTime.Value;
        public float PhaseRemainingTime => networkPhaseDuration.Value - PhaseElapsedTime;

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Notify server game manager of phase transitions
            StartPhase("Phase1_Preparation");
        }

        /// <summary>
        /// Server: Start a specific phase (Server-authoritative)
        /// </summary>
        [Server]
        public void StartPhase(string phaseName)
        {
            var config = GameConfigLoader.Instance?.GetMatchPhaseConfig(phaseName);
            if (config == null)
            {
                Debug.LogError($"[MatchPhaseManager] Phase config not found: {phaseName}");
                return;
            }

            currentPhaseConfig = config;
            networkPhase.Value = phaseName;
            networkPhaseStartTime.Value = Time.time;
            
            // Random duration within min/max
            networkPhaseDuration.Value = Random.Range(config.DurationMin, config.DurationMax) * 60f; // Convert to seconds

            Debug.Log($"[MatchPhaseManager] Started phase: {phaseName} (Duration: {networkPhaseDuration.Value}s)");

            // Apply phase-specific logic
            ApplyPhaseLogic(phaseName);

            // Notify server game manager
            var serverGameManager = FindFirstObjectByType<ServerGameManager>();
            if (serverGameManager != null)
            {
                serverGameManager.OnPhaseTransition(phaseName);
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Check if phase should transition
            if (PhaseElapsedTime >= networkPhaseDuration.Value)
            {
                TransitionToNextPhase();
            }
        }

        /// <summary>
        /// Transition to next phase
        /// </summary>
        [Server]
        private void TransitionToNextPhase()
        {
            string nextPhase = GetNextPhase(networkPhase.Value);
            StartPhase(nextPhase);
        }

        /// <summary>
        /// Get next phase based on current phase
        /// </summary>
        private string GetNextPhase(string current)
        {
            switch (current)
            {
                case "Phase1_Preparation":
                    return "Phase2_HuntObjectives";
                case "Phase2_HuntObjectives":
                    return "Phase3_FinalLockdown";
                case "Phase3_FinalLockdown":
                    return "Phase3_FinalLockdown"; // Stay in Phase 3 until match ends
                default:
                    return "Phase1_Preparation";
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkPhase.OnChange += OnPhaseChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkPhase != null)
                networkPhase.OnChange -= OnPhaseChanged;
        }

        /// <summary>
        /// Apply phase-specific logic
        /// </summary>
        private void ApplyPhaseLogic(string phaseName)
        {
            switch (phaseName)
            {
                case "Phase1_Preparation":
                    OnPhase1Start();
                    break;
                case "Phase2_HuntObjectives":
                    OnPhase2Start();
                    break;
                case "Phase3_FinalLockdown":
                    OnPhase3Start();
                    break;
            }
        }

        private void OnPhase1Start()
        {
            Debug.Log("[MatchPhaseManager] Phase 1: Preparation - Players can place beacons, loot items");
            // Enable beacon placement
            // Spawn initial loot
        }

        private void OnPhase2Start()
        {
            Debug.Log("[MatchPhaseManager] Phase 2: Hunt & Objectives - Boss spawns, capture zones active");
            // Spawn AI boss
            // Activate capture zones
            // Spawn rare loot
        }

        private void OnPhase3Start()
        {
            Debug.Log("[MatchPhaseManager] Phase 3: Final Lockdown - Beacons disabled, zone closing");
            // Disable beacons
            // Start zone closing
            // Apply predator/prey buffs/nerfs
        }

        /// <summary>
        /// Check if respawn is enabled in current phase
        /// </summary>
        public bool IsRespawnEnabled()
        {
            return currentPhaseConfig?.RespawnEnabled ?? false;
        }

        /// <summary>
        /// Check if beacons are enabled in current phase
        /// </summary>
        public bool AreBeaconsEnabled()
        {
            return currentPhaseConfig?.BeaconEnabled ?? false;
        }

        /// <summary>
        /// Get score multiplier for current phase
        /// </summary>
        public float GetScoreMultiplier()
        {
            return currentPhaseConfig?.ScoreMultiplier ?? 1f;
        }

        /// <summary>
        /// Get survival multiplier for current phase
        /// </summary>
        public float GetSurvivalMultiplier()
        {
            return currentPhaseConfig?.SurvivalMultiplier ?? 1f;
        }

        private void OnPhaseChanged(string oldPhase, string newPhase, bool asServer)
        {
            Debug.Log($"[MatchPhaseManager] Phase changed: {oldPhase} -> {newPhase}");
            
            // Notify clients of phase change
            if (!asServer)
            {
                // Client-side phase change handling
                OnClientPhaseChanged(newPhase);
            }
        }

        private void OnClientPhaseChanged(string phase)
        {
            // Update UI, play sounds, etc.
            Debug.Log($"[MatchPhaseManager] Client: Phase changed to {phase}");
        }

        /// <summary>
        /// Get phase config (client-safe)
        /// </summary>
        public MatchPhaseConfigData GetCurrentPhaseConfig()
        {
            return GameConfigLoader.Instance?.GetMatchPhaseConfig(networkPhase.Value);
        }
    }
}

