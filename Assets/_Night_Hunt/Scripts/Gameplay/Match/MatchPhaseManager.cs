using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Events;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.Gameplay.Match
{
    /// <summary>
    /// Manages match phases (Phase 1: Preparation, Phase 2: Hunt, Phase 3: Lockdown).
    /// Hoạt động hoàn toàn bằng Event-Driven (Các hệ thống khác tự Subscribe vào OnPhaseStarted).
    /// </summary>
    public class MatchPhaseManager : NetworkBehaviour
    {
        [Header("Phase Settings")] [SerializeField]
        private MatchPhaseState initialState = MatchPhaseState.Preparation;

        [SerializeField] private float phaseStartTime;
        [SerializeField] private float phaseDuration;

        [Header("Phase Config Data")]
        [Tooltip("Config data cho tá»«ng phase (Preparation, Hunt, Lockdown). Assign trong Inspector.")]
        [SerializeField] private List<MatchPhaseConfigData> phaseConfigs = new List<MatchPhaseConfigData>();
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        // âœ… Event system - decoupled communication
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

        [ContextMenu("🔥 Auto Setup 3 Default Phases")]
        private void AutoSetupDefaultPhases()
        {
            phaseConfigs.Clear();
            
            phaseConfigs.Add(new MatchPhaseConfigData { 
                PhaseType = MatchPhaseState.Preparation,
                DisplayName = "Chuẩn Bị",
                DurationMin = 3, DurationMax = 4, 
                RespawnEnabled = true, RespawnDelay = 5f,
                WarningTime = 30f, ScoreMultiplier = 1f, SurvivalMultiplier = 1f
            });
            
            phaseConfigs.Add(new MatchPhaseConfigData { 
                PhaseType = MatchPhaseState.Hunt,
                DisplayName = "Săn Mồi",
                DurationMin = 5, DurationMax = 8, 
                RespawnEnabled = true, RespawnDelay = 7f,
                WarningTime = 30f, ScoreMultiplier = 2f, SurvivalMultiplier = 1.5f
            });
            
            phaseConfigs.Add(new MatchPhaseConfigData { 
                PhaseType = MatchPhaseState.Lockdown,
                DisplayName = "Phong Tỏa",
                DurationMin = 3, DurationMax = 3, 
                RespawnEnabled = true, RespawnDelay = 10f,
                WarningTime = 30f, ScoreMultiplier = 3f, SurvivalMultiplier = 2f
            });

            Debug.Log("[MatchPhaseManager] Đã tự động tạo 3 Phase mặc định với Config Hồi Sinh riêng biệt!");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_debugConfig != null && _debugConfig.EnableMatchDebugLogs)
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
            if (_debugConfig != null && _debugConfig.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Phase state changed: {previousState} -> {newState}");

            // âœ… Trigger event cho subscribers (ServerGameManager, Bootstrap, etc.)
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

            // ✅ Lookup bằng enum PhaseType — type-safe, không bị typo string
            MatchPhaseConfigData config = phaseConfigs.Find(c => c.PhaseType == phase);
            if (config == null)
            {
                // Fallback: use first available config to avoid hard stop in development
                config = phaseConfigs.Count > 0 ? phaseConfigs[0] : null;
                if (config == null)
                {
                    Debug.LogError($"[MatchPhaseManager] Phase config not found for PhaseType: {phase}. Add entries to phaseConfigs in Inspector.");
                    return;
                }
                Debug.LogWarning($"[MatchPhaseManager] No config for phase {phase}, using fallback config.");
            }

            currentPhaseConfig = config;
            _warningSent = false;

            // Display name: dùng field DisplayName nếu có, fallback về tên enum
            string displayName = !string.IsNullOrEmpty(config.DisplayName)
                ? config.DisplayName
                : phase.ToString();

            if (_debugConfig != null && _debugConfig.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Starting phase: {phase} ('{displayName}')");

            // Store old phase for event
            MatchPhaseState oldPhase = CurrentPhase;

            // Update state machine BEFORE setting SyncVar
            if (phaseStateMachine != null && phaseStateMachine.CurrentState != phase)
                phaseStateMachine.TransitionTo(phase);

            // Sync to network
            networkPhase.Value          = (int)phase;
            networkPhaseStartTime.Value = Time.time;
            networkPhaseDuration.Value  = UnityEngine.Random.Range(config.DurationMin, config.DurationMax) * 60f;

            if (_debugConfig != null && _debugConfig.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Started phase: {phase} (Duration: {networkPhaseDuration.Value}s)");

            hasStartedFirstPhase = true;

            // ✅ Broadcast — pass enum state + display name (kông phải raw string const nữa)
            OnPhaseStarted?.Invoke(phase, displayName);
        }

        /// <summary>
        /// Tên chuẩn của các Phase - Dùng để sync chuỗi trên toàn hệ thống
        /// </summary>
        public static class PhaseNames
        {
            public const string Preparation = "Phase1_Preparation";
            public const string Hunt        = "Phase2_HuntObjectives";
            public const string Lockdown    = "Phase3_FinalLockdown";
        }

        private string GetPhaseName(MatchPhaseState phase)
        {
            return phase switch
            {
                MatchPhaseState.Preparation => PhaseNames.Preparation,
                MatchPhaseState.Hunt        => PhaseNames.Hunt,
                MatchPhaseState.Lockdown    => PhaseNames.Lockdown,
                _                           => PhaseNames.Preparation,
            };
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
                    if (_debugConfig != null && _debugConfig.EnableMatchDebugLogs)
                        Debug.Log($"[MatchPhaseManager] ⚠️ Phase warning: {CurrentPhase} ends in {remaining:F0}s");
                    RpcPhaseWarning(CurrentPhase, remaining);
                }
            }

            // Check if phase should transition
            if (remaining <= 0f)
            {
                TransitionToNextPhase();
            }
        }

        [ObserversRpc]
        private void RpcPhaseWarning(MatchPhaseState phase, float secondsRemaining)
        {
            GameplayEventBus.Instance?.Publish(new PhaseWarningEvent
            {
                CurrentPhase    = phase,
                SecondsRemaining = secondsRemaining
            });
        }

        [Server]
        private void TransitionToNextPhase()
        {
            MatchPhaseState nextPhase = GetNextPhase(CurrentPhase);

            // Avoid transition when already in final phase
            if (CurrentPhase == MatchPhaseState.Lockdown)
                return;

            if (_debugConfig != null && _debugConfig.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Auto-transitioning from {CurrentPhase} to {nextPhase}");
            StartPhase(nextPhase);
        }

        private MatchPhaseState GetNextPhase(MatchPhaseState current)
        {
            return current switch
            {
                MatchPhaseState.Preparation => MatchPhaseState.Hunt,
                MatchPhaseState.Hunt        => MatchPhaseState.Lockdown,
                MatchPhaseState.Lockdown    => MatchPhaseState.Lockdown,
                _                           => MatchPhaseState.Preparation,
            };
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
            MatchPhaseState state = (MatchPhaseState)networkPhase.Value;
            return phaseConfigs.Find(c => c.PhaseType == state);
        }
    }
}
