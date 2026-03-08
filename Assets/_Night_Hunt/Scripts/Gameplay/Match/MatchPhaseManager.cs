using System;
using System.Collections;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Events;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using NightHunt.Gameplay.Core.State;

namespace NightHunt.Gameplay.Match
{
    /// <summary>
    /// Manages match phases (Phase 1: Preparation, Phase 2: Hunt, Phase 3: Lockdown)
    /// Server-authoritative phase management with state machine
    /// Uses events to decouple from ServerGameManager
    /// </summary>
    public class MatchPhaseManager : NetworkBehaviour
    {
        [Header("Phase Settings")] [SerializeField]
        private MatchPhaseState initialState = MatchPhaseState.Preparation;

        [SerializeField] private float phaseStartTime;
        [SerializeField] private float phaseDuration;

        // ✅ Event system - decoupled communication
        public event Action<MatchPhaseState, string> OnPhaseStarted; // (newPhase, phaseName)
        public event Action<MatchPhaseState, MatchPhaseState> OnPhaseTransitioned; // (oldPhase, newPhase)

        // State machine
        private StateMachine<MatchPhaseState> phaseStateMachine;

        // Synchronized phase state
        private readonly SyncVar<int> networkPhase = new SyncVar<int>();
        private readonly SyncVar<float> networkPhaseStartTime = new SyncVar<float>();
        private readonly SyncVar<float> networkPhaseDuration = new SyncVar<float>();

        private MatchPhaseConfigData currentPhaseConfig;
        private bool isInitialized = false;
        private bool hasStartedFirstPhase = false;
        private bool _warningSent = false;   // reset each time a new phase starts

        public MatchPhaseState CurrentPhase => phaseStateMachine?.CurrentState ?? initialState;
        public string CurrentPhaseName => GetPhaseName(CurrentPhase);
        public float PhaseElapsedTime => Time.time - networkPhaseStartTime.Value;
        public float PhaseRemainingTime => networkPhaseDuration.Value - PhaseElapsedTime;

        private void Awake()
        {
            InitializeStateMachine();
        }

        /// <summary>
        /// Initialize state machine
        /// </summary>
        private void InitializeStateMachine()
        {
            phaseStateMachine = new StateMachine<MatchPhaseState>(initialState);

            // Define allowed transitions
            phaseStateMachine.AddTransition(MatchPhaseState.Preparation, MatchPhaseState.Hunt);
            phaseStateMachine.AddTransition(MatchPhaseState.Hunt, MatchPhaseState.Lockdown);

            phaseStateMachine.OnStateChanged += OnPhaseStateChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[MatchPhaseManager] Server started");
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (!isInitialized)
            {
                networkPhase.OnChange += OnNetworkPhaseChanged;
                isInitialized = true;
            }

            // Sync initial phase state for clients
            if (!IsServer && networkPhase.Value != 0)
            {
                MatchPhaseState currentPhase = (MatchPhaseState)networkPhase.Value;
                if (phaseStateMachine != null)
                {
                    phaseStateMachine.ForceTransition(currentPhase);
                }
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            networkPhase.OnChange -= OnNetworkPhaseChanged;
            phaseStateMachine.OnStateChanged -= OnPhaseStateChanged;
        }

        /// <summary>
        /// Handle phase state changes
        /// </summary>
        private void OnPhaseStateChanged(MatchPhaseState previousState, MatchPhaseState newState)
        {
            Debug.Log($"[MatchPhaseManager] Phase state changed: {previousState} -> {newState}");

            // ✅ Trigger event cho subscribers (ServerGameManager, Bootstrap, etc.)
            OnPhaseTransitioned?.Invoke(previousState, newState);
        }

        /// <summary>
        /// Handle network phase changes
        /// </summary>
        private void OnNetworkPhaseChanged(int oldPhase, int newPhase, bool asServer)
        {
            if (!asServer)
            {
                // Client: Sync phase state
                MatchPhaseState newPhaseState = (MatchPhaseState)newPhase;
                if (phaseStateMachine != null && phaseStateMachine.CurrentState != newPhaseState)
                {
                    phaseStateMachine.ForceTransition(newPhaseState);
                }
            }
        }

        /// <summary>
        /// Server: Start a specific phase (Server-authoritative)
        /// </summary>
        [Server]
        public void StartPhase(MatchPhaseState phase)
        {
            // Validate server state
            if (!IsServerStarted || !IsSpawned)
            {
                Debug.LogWarning(
                    $"[MatchPhaseManager] Cannot start phase {phase} - server not ready (IsServerStarted: {IsServerStarted}, IsSpawned: {IsSpawned})");
                return;
            }

            string phaseName = GetPhaseName(phase);
            MatchPhaseConfigData config = null; // TODO: load from new data source
            if (config == null)
            {
                Debug.LogError($"[MatchPhaseManager] Phase config not found: {phaseName}");
                return;
            }

            currentPhaseConfig = config;
            _warningSent = false;

            Debug.Log($"[MatchPhaseManager] Starting phase: {phaseName}");

            // Store old phase for event
            MatchPhaseState oldPhase = CurrentPhase;

            // Update state machine BEFORE setting SyncVar
            if (phaseStateMachine != null && phaseStateMachine.CurrentState != phase)
            {
                phaseStateMachine.TransitionTo(phase);
            }

            // Sync to network
            networkPhase.Value = (int)phase;
            networkPhaseStartTime.Value = Time.time;

            // Random duration within min/max
            networkPhaseDuration.Value = UnityEngine.Random.Range(config.DurationMin, config.DurationMax) * 60f;

            Debug.Log($"[MatchPhaseManager] Started phase: {phaseName} (Duration: {networkPhaseDuration.Value}s)");

            // Apply phase-specific logic
            ApplyPhaseLogic(phase);

            // Mark first phase as started
            hasStartedFirstPhase = true;

            // ✅ Trigger event - ServerGameManager sẽ subscribe và handle
            OnPhaseStarted?.Invoke(phase, phaseName);
        }

        /// <summary>
        /// Get phase name from state
        /// </summary>
        private string GetPhaseName(MatchPhaseState phase)
        {
            switch (phase)
            {
                case MatchPhaseState.Preparation:
                    return "Phase1_Preparation";
                case MatchPhaseState.Hunt:
                    return "Phase2_HuntObjectives";
                case MatchPhaseState.Lockdown:
                    return "Phase3_FinalLockdown";
                default:
                    return "Phase1_Preparation";
            }
        }

        private void Update()
        {
            if (!IsServerStarted) return;
            if (!hasStartedFirstPhase) return;

            float remaining = PhaseRemainingTime;

            // Phase warning (only for transitioning phases, not final Lockdown)
            if (!_warningSent && CurrentPhase != MatchPhaseState.Lockdown)
            {
                float warningTime = currentPhaseConfig?.WarningTime > 0
                    ? currentPhaseConfig.WarningTime
                    : 30f;

                if (remaining <= warningTime)
                {
                    _warningSent = true;
                    Debug.Log($"[MatchPhaseManager] ⚠ Phase warning: {CurrentPhase} ends in {remaining:F0}s");
                    RpcPhaseWarning(CurrentPhase, remaining);
                }
            }

            // Check if phase should transition
            if (remaining <= 0f)
            {
                TransitionToNextPhase();
            }
        }

        /// <summary>Broadcast phase-warning to all clients so HUD can show countdown.</summary>
        [ObserversRpc]
        private void RpcPhaseWarning(MatchPhaseState phase, float secondsRemaining)
        {
            GameplayEventBus.Instance?.Publish(new PhaseWarningEvent
            {
                CurrentPhase    = phase,
                SecondsRemaining = secondsRemaining
            });
        }

        /// <summary>
        /// Transition to next phase
        /// </summary>
        [Server]
        private void TransitionToNextPhase()
        {
            MatchPhaseState nextPhase = GetNextPhase(CurrentPhase);

            // Avoid transition when already in final phase
            if (CurrentPhase == MatchPhaseState.Lockdown)
            {
                Debug.Log("[MatchPhaseManager] Already in final phase (Lockdown)");
                return;
            }

            Debug.Log($"[MatchPhaseManager] Auto-transitioning from {CurrentPhase} to {nextPhase}");
            StartPhase(nextPhase);
        }

        /// <summary>
        /// Get next phase based on current phase
        /// </summary>
        private MatchPhaseState GetNextPhase(MatchPhaseState current)
        {
            switch (current)
            {
                case MatchPhaseState.Preparation:
                    return MatchPhaseState.Hunt;
                case MatchPhaseState.Hunt:
                    return MatchPhaseState.Lockdown;
                case MatchPhaseState.Lockdown:
                    return MatchPhaseState.Lockdown; // Final phase
                default:
                    return MatchPhaseState.Preparation;
            }
        }

        /// <summary>
        /// Apply phase-specific logic
        /// </summary>
        private void ApplyPhaseLogic(MatchPhaseState phase)
        {
            switch (phase)
            {
                case MatchPhaseState.Preparation:
                    OnPhase1Start();
                    break;
                case MatchPhaseState.Hunt:
                    OnPhase2Start();
                    break;
                case MatchPhaseState.Lockdown:
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

        /// <summary>
        /// Get phase config (client-safe)
        /// </summary>
        public MatchPhaseConfigData GetCurrentPhaseConfig()
        {
            string phaseName = GetPhaseName((MatchPhaseState)networkPhase.Value);
            return null; // TODO: load from new data source
        }
    }
}