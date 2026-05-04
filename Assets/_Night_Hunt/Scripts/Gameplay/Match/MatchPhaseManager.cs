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
using NightHunt.State;

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

        [Serializable]
        private sealed class PlayerCountPhaseProfile
        {
            [Tooltip("Total players in the match: 2=1v1, 4=2v2, 8=4v4.")]
            public int ExpectedPlayerCount;
            public string ProfileName;
            public List<MatchPhaseConfigData> PhaseConfigs = new List<MatchPhaseConfigData>();
        }

        [Header("Mode-Specific Phase Profiles")]
        [Tooltip("Optional exact profiles by total player count. If empty, phaseConfigs are scaled by player count at runtime.")]
        [SerializeField] private List<PlayerCountPhaseProfile> _phaseProfilesByExpectedPlayers = new List<PlayerCountPhaseProfile>();

        [Tooltip("When no exact profile is assigned, scale phase durations for 1v1/2v2/4v4 from phaseConfigs.")]
        [SerializeField] private bool _usePlayerCountPhaseScaling = true;

        [Tooltip("Send PhaseWarningEvent for the final Lockdown phase as well.")]
        [SerializeField] private bool _sendFinalPhaseWarning = true;
        // ✅ Event system - decoupled communication
        public event Action<MatchPhaseState, string> OnPhaseStarted; // (newPhase, phaseName)
        public event Action<MatchPhaseState, MatchPhaseState> OnPhaseTransitioned; // (oldPhase, newPhase)

        /// <summary>Fired on server when the Lockdown timer runs out. Used by MatchEndManager for score-based win resolution.</summary>
        public event Action OnLockdownTimerExpired;
        // State machine
        private StateMachine<MatchPhaseState> phaseStateMachine;

        // Synchronized phase state
        private readonly SyncVar<int> networkPhase = new SyncVar<int>();
        // Tick-based start time — uint wraps safely at 4 billion ticks (~27 hours at 50 Hz).
        // FishNet synchronizes TimeManager.Tick across all peers, so late-joining clients
        // compute the correct elapsed time without relying on the unsynchronized Time.time.
        private readonly SyncVar<uint> networkPhaseStartTick = new SyncVar<uint>();
        private readonly SyncVar<float> networkPhaseDuration = new SyncVar<float>();

        private MatchPhaseConfigData currentPhaseConfig;
        private List<MatchPhaseConfigData> _activePhaseConfigs;
        private bool isInitialized = false;
        private bool hasStartedFirstPhase = false;
        private bool _warningSent = false;   // reset each time a new phase starts
        private bool _lockdownTimerExpiredSent = false;

        public MatchPhaseState CurrentPhase => phaseStateMachine?.CurrentState ?? initialState;
        public string CurrentPhaseName => GetPhaseName(CurrentPhase);
        public float PhaseElapsedTime
        {
            get
            {
                // TimeManager.Tick is synchronized across all FishNet peers.
                // uint subtraction wraps correctly for positive elapsed ticks.
                if (TimeManager == null) return 0f;
                uint elapsed = TimeManager.Tick - networkPhaseStartTick.Value;
                return elapsed * (float)TimeManager.TickDelta;
            }
        }
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

            ResolveActivePhaseConfigs();
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
            ResolveActivePhaseConfigs();
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
            MatchPhaseConfigData config = GetPhaseConfig(phase);
            if (config == null)
            {
                // Fallback: use first available config to avoid hard stop in development
                var configs = ActivePhaseConfigs;
                config = configs.Count > 0 ? configs[0] : null;
                if (config == null)
                {
                    Debug.LogError($"[MatchPhaseManager] Phase config not found for PhaseType: {phase}. Add entries to phaseConfigs in Inspector.");
                    return;
                }
                Debug.LogWarning($"[MatchPhaseManager] No config for phase {phase}, using fallback config.");
            }

            currentPhaseConfig = config;
            _warningSent = false;
            _lockdownTimerExpiredSent = false;

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
            networkPhaseStartTick.Value = TimeManager.Tick;
            float durationMinutes = config.DurationMax > config.DurationMin
                ? UnityEngine.Random.Range((float)config.DurationMin, (float)config.DurationMax)
                : Mathf.Max(0.1f, config.DurationMin);
            networkPhaseDuration.Value = durationMinutes * 60f;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableMatchDebugLogs)
                Debug.Log($"[MatchPhaseManager] Started phase: {phase} (Duration: {networkPhaseDuration.Value}s)");

            hasStartedFirstPhase = true;

            // Broadcast to all clients via GameplayEventBus so UI panels can react.
            string objectives = config.BuildObjectivesSummary();
            RpcPhaseStarted((int)phase, displayName, objectives);

            // ✅ Broadcast — pass enum state + display name (không phải raw string const nữa)
            OnPhaseStarted?.Invoke(phase, displayName);
        }

        [ObserversRpc]
        private void RpcPhaseStarted(int phaseInt, string displayName, string objectivesSummary)
        {
            var phase = (MatchPhaseState)phaseInt;
            Debug.Log($"[FLOW §15] CLIENT MatchPhaseManager.RpcPhaseStarted: phase={phase} name='{displayName}'  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            GameplayEventBus.Instance?.Publish(new NightHunt.Gameplay.Core.Events.PhaseStartedEvent
            {
                Phase            = phase,
                DisplayName      = displayName,
                ObjectivesSummary = objectivesSummary
            });
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

            // Phase warning: send near the end of the phase, never at phase start.
            if (!_warningSent && ShouldSendWarningForCurrentPhase())
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
                if (_lockdownTimerExpiredSent)
                    return;

                Debug.Log("[MatchPhaseManager] Lockdown timer expired � notifying MatchEndManager.");
                _lockdownTimerExpiredSent = true;
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
            return GetPhaseConfig(state);
        }

        private List<MatchPhaseConfigData> ActivePhaseConfigs => _activePhaseConfigs ?? phaseConfigs ?? new List<MatchPhaseConfigData>();

        private MatchPhaseConfigData GetPhaseConfig(MatchPhaseState phase)
        {
            return ActivePhaseConfigs.Find(c => c.PhaseType == phase);
        }

        private bool ShouldSendWarningForCurrentPhase()
        {
            return CurrentPhase != MatchPhaseState.Lockdown || _sendFinalPhaseWarning;
        }

        [Server]
        private void ResolveActivePhaseConfigs()
        {
            int expectedPlayers = ResolveExpectedPlayerCountForPhaseConfig();

            if (expectedPlayers > 0)
            {
                int profileCount = _phaseProfilesByExpectedPlayers?.Count ?? 0;
                for (int i = 0; i < profileCount; i++)
                {
                    var profile = _phaseProfilesByExpectedPlayers[i];
                    if (profile == null || profile.ExpectedPlayerCount != expectedPlayers ||
                        profile.PhaseConfigs == null || profile.PhaseConfigs.Count == 0)
                    {
                        continue;
                    }

                    _activePhaseConfigs = profile.PhaseConfigs;
                    Debug.Log($"[MatchPhaseManager] Phase profile selected: players={expectedPlayers} name='{profile.ProfileName}' configs={_activePhaseConfigs.Count}");
                    return;
                }

                if (_usePlayerCountPhaseScaling && phaseConfigs != null && phaseConfigs.Count > 0)
                {
                    _activePhaseConfigs = BuildScaledPhaseConfigs(expectedPlayers);
                    Debug.Log($"[MatchPhaseManager] Runtime phase profile built for players={expectedPlayers} configs={_activePhaseConfigs.Count}");
                    return;
                }
            }

            _activePhaseConfigs = phaseConfigs;
        }

        private static int ResolveExpectedPlayerCountForPhaseConfig()
        {
            if (RoomState.Instance != null && RoomState.Instance.PlayerCount > 0)
                return RoomState.Instance.PlayerCount;

            if (NightHunt.Server.ServerBootstrap.BootstrappedExpectedPlayers > 0)
                return NightHunt.Server.ServerBootstrap.BootstrappedExpectedPlayers;

            int registered = NightHunt.Networking.RegistryService.Instance?.GetConnectedPlayerCount() ?? 0;
            return registered > 0 ? registered : 0;
        }

        private List<MatchPhaseConfigData> BuildScaledPhaseConfigs(int expectedPlayers)
        {
            float durationScale = expectedPlayers <= 2 ? 0.67f : expectedPlayers >= 8 ? 1.25f : 1f;
            float warningSeconds = expectedPlayers <= 2 ? 15f : expectedPlayers >= 8 ? 45f : 30f;

            var result = new List<MatchPhaseConfigData>(phaseConfigs?.Count ?? 0);
            foreach (var source in phaseConfigs)
            {
                if (source == null)
                    continue;

                var clone = ClonePhaseConfig(source);
                clone.DurationMin = Mathf.Max(1, Mathf.RoundToInt(source.DurationMin * durationScale));
                clone.DurationMax = Mathf.Max(clone.DurationMin, Mathf.RoundToInt(Mathf.Max(source.DurationMax, source.DurationMin) * durationScale));
                clone.WarningTime = source.WarningTime > 0f ? Mathf.Min(source.WarningTime, warningSeconds) : warningSeconds;
                result.Add(clone);
            }

            return result;
        }

        private static MatchPhaseConfigData ClonePhaseConfig(MatchPhaseConfigData source)
        {
            return new MatchPhaseConfigData
            {
                PhaseType = source.PhaseType,
                DisplayName = source.DisplayName,
                DurationMin = source.DurationMin,
                DurationMax = source.DurationMax,
                RespawnEnabled = source.RespawnEnabled,
                RespawnDelay = source.RespawnDelay,
                ScoreMultiplier = source.ScoreMultiplier,
                SurvivalMultiplier = source.SurvivalMultiplier,
                WarningTime = source.WarningTime,
                PhaseObjectives = source.PhaseObjectives
            };
        }
    }
}
