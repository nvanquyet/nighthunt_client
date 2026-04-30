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
    /// Ho?t ??ng ho�n to�n b?ng Event-Driven (C�c h? th?ng kh�c t? Subscribe v�o OnPhaseStarted).
    /// </summary>
    public class MatchPhaseManager : NetworkBehaviour
    {
        [Header("Phase Settings")]
        [SerializeField] private MatchPhaseState initialState = MatchPhaseState.Preparation;

        [Tooltip("Thời gian đếm ngược (giây) trước khi Phase đầu tiên bắt đầu sau khi tất cả player đã kết nối.\n" +
                 "Timer này bắt đầu sau AllPlayersReady (server-side), độc lập với overlay minimumDisplayDuration.\n" +
                 "Đặt 0 để bắt đầu ngay lập tức (không có countdown).")]
        [Min(0f)]
        [SerializeField] private float _delayBeforeFirstPhase = 5f;

        [Header("Phase Config Data")]
        [Tooltip("Config data cho từng phase (Preparation, Hunt, Lockdown). Assign trong Inspector.")]
        [SerializeField] private List<MatchPhaseConfigData> phaseConfigs = new List<MatchPhaseConfigData>();
        // ✅ Event system - decoupled communication
        public event Action<MatchPhaseState, string> OnPhaseStarted; // (newPhase, phaseName)
        public event Action<MatchPhaseState, MatchPhaseState> OnPhaseTransitioned; // (oldPhase, newPhase)        /// <summary>Fired on server when the Lockdown (Phase 3) timer runs out. Used by MatchEndManager for score-based win resolution.</summary>
        public event Action OnLockdownTimerExpired;
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
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
                Debug.Log("[MatchPhaseManager] Server started");
        }

        // ?? Match Start with Countdown ????????????????????????????????????????

        /// <summary>
        /// Server: Begin the match � countdown <see cref="_delayBeforeFirstPhase"/> seconds
        /// then start <see cref="initialState"/>.
        /// Called by ServerGameManager after all players have spawned.
        /// </summary>
        [Server]
        public void BeginMatch()
        {
            if (!IsServerStarted || !IsSpawned)
            {
                Debug.LogWarning("[MatchPhaseManager] BeginMatch called but server not ready.");
                return;
            }
            Debug.Log($"[FLOW §14] SERVER MatchPhaseManager.BeginMatch: countdown={_delayBeforeFirstPhase}s before {initialState}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            StartCoroutine(CountdownAndStartFirstPhase());
        }

        [Server]
        private IEnumerator CountdownAndStartFirstPhase()
        {
            int remaining = Mathf.CeilToInt(_delayBeforeFirstPhase);

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Match countdown: {remaining}s before {initialState}");

            while (remaining > 0)
            {
                RpcMatchCountdown(remaining);
                yield return new WaitForSeconds(1f);
                remaining--;
            }

            // Tick 0 ? clients show "GO!" / "B?t ??u!"
            RpcMatchCountdown(0);
            StartPhase(initialState);
        }

        [ObserversRpc]
        private void RpcMatchCountdown(int secondsRemaining)
        {
            Debug.Log($"[FLOW §14] CLIENT MatchCountdown: {secondsRemaining}s remaining  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            GameplayEventBus.Instance?.Publish(
                new NightHunt.Gameplay.Core.Events.MatchCountdownEvent
                {
                    SecondsRemaining = secondsRemaining
                });
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
            if (!IsServerInitialized && networkPhase.Value != 0)
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
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
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

            // ? Lookup b?ng enum PhaseType � type-safe, kh�ng b? typo string
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

            // Display name: d�ng field DisplayName n?u c�, fallback v? t�n enum
            string displayName = !string.IsNullOrEmpty(config.DisplayName)
                ? config.DisplayName
                : phase.ToString();

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Started phase: {phase} (Duration: {networkPhaseDuration.Value}s)");

            hasStartedFirstPhase = true;

            // ? Broadcast � pass enum state + display name (k�ng ph?i raw string const n?a)
            OnPhaseStarted?.Invoke(phase, displayName);
        }

        /// <summary>
        /// T�n chu?n c?a c�c Phase - D�ng ?? sync chu?i tr�n to�n h? th?ng
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
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
                        Debug.Log($"[MatchPhaseManager] ?? Phase warning: {CurrentPhase} ends in {remaining:F0}s");
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

            // Lockdown is the final phase � fire timer-expired event for score-based win resolution.
            if (CurrentPhase == MatchPhaseState.Lockdown)
            {
                Debug.Log("[MatchPhaseManager] Lockdown timer expired � notifying MatchEndManager.");
                OnLockdownTimerExpired?.Invoke();
                return;
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
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
