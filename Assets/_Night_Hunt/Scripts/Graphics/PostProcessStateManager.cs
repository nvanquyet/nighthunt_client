using UnityEngine;
using UnityEngine.Rendering;
using NightHunt.Core;
using NightHunt.UI;
using NightHunt.Audio;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Systems;

namespace NightHunt.Graphics
{
    /// <summary>
    /// PostProcessStateManager — drives Post-Processing Volume weights per game state.
    ///
    /// REQUIRES: Unity URP or HDRP with Volume system. Add Global Volumes to the scene
    /// and assign them to the corresponding fields in the Inspector.
    ///
    /// VOLUMES TO CREATE (all Global, weight driven by this manager):
    ///   • BaseVolume       — always-on base profile (Bloom, Color Grading, Vignette subtle)
    ///   • HomeVolume       — Home/Lobby override: warm LUT, subtle DOF, soft bloom
    ///   • LowHealthVolume  — In-match low health: red vignette, desaturate slightly
    ///   • DeathVolume      — Full grayscale, heavy vignette, radial blur (Lens Distortion)
    ///   • SpectatorVolume  — Cinematic letterbox, tint overlay
    ///
    /// SETUP:
    ///   1. In Scene hierarchy: create "PostProcess" empty GO.
    ///   2. Add PostProcessStateManager component.
    ///   3. Create URP Global Volumes, assign to fields below.
    ///   4. Call SetState() from game state events (UINavigator, PlayerHealthSystem, etc.)
    ///
    /// LOWHEALTH PULSE:
    ///   Uses AnimationCurve for vignette intensity pulsing in sync with heartbeat.
    ///   Falls back to linear oscillation if no curve set.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PostProcessStateManager : Singleton<PostProcessStateManager>
    {
        [Header("Scene Volumes — assign Global Volume GameObjects")]
        [Tooltip("Standard in-match profile. Weight stays at 1 always.")]
        [SerializeField] private Volume baseVolume;

        [Tooltip("Home screen profile (warm, soft). Weight fades in on Home, fades out in Match.")]
        [SerializeField] private Volume homeVolume;

        [Tooltip("Low-health overlay: red vignette pulsing. Enabled by health threshold.")]
        [SerializeField] private Volume lowHealthVolume;

        [Tooltip("Death overlay: full grayscale + heavy vignette. Weight 0→1 on died, 1→0 on respawn.")]
        [SerializeField] private Volume deathVolume;

        [Tooltip("Spectator mode: letterbox crop + tint. Optional.")]
        [SerializeField] private Volume spectatorVolume;

        [Header("Transition Speed")]
        [Tooltip("Weight lerp speed for normal transitions (e.g., Home ↔ Match).")]
        [SerializeField, Min(0.1f)] private float transitionSpeed = 2.5f;

        [Tooltip("Weight lerp speed for critical transitions (death).")]
        [SerializeField, Min(0.5f)] private float criticalTransitionSpeed = 5f;

        [Header("Low Health Pulse")]
        [Tooltip("Pulse frequency in Hz when low health is active (syncs roughly with heartbeat at 1 Hz).")]
        [SerializeField, Range(0.5f, 3f)] private float pulseFrequency = 1.1f;
        [Tooltip("Min volume weight during low-health pulse cycle.")]
        [SerializeField, Range(0f, 0.5f)] private float pulseMin = 0.15f;
        [Tooltip("Max volume weight during low-health pulse cycle.")]
        [SerializeField, Range(0.3f, 1f)] private float pulseMax = 0.75f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private PostProcessState _currentState = PostProcessState.None;
        private bool             _lowHealthActive;
        private bool             _isDead;

        // ── Target weights (driven per-frame toward target) ─────────────────────
        private float _targetHome;
        private float _targetLowHealth;
        private float _targetDeath;
        private float _targetSpectator;

        // ── Local player references (registered from CharacterLifecycleController) ─
        private CharacterLifecycleController _localLifecycle;
        private IPlayerStatSystem            _localStatSystem;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            // Set initial weights — all off except base
            SetWeight(homeVolume,      0f);
            SetWeight(lowHealthVolume, 0f);
            SetWeight(deathVolume,     0f);
            SetWeight(spectatorVolume, 0f);
        }

        private void Start()
        {
            // Subscribe to UINavigator for menu-driven state transitions.
            // UINavigator lives in the UI/menu scene; in a pure match scene
            // it may not exist — in that case default to Match.
            if (UINavigator.HasInstance)
            {
                UINavigator.Instance.OnPanelChanged += OnUIPanelChanged;
                OnUIPanelChanged(UINavigator.Instance.CurrentPanel);
            }
            else
            {
                SetState(PostProcessState.Match);
            }
        }

        protected override void OnDestroy()
        {
            // Unsubscribe UINavigator
            if (UINavigator.HasInstance)
                UINavigator.Instance.OnPanelChanged -= OnUIPanelChanged;

            // Unsubscribe local player
            UnregisterLocalPlayerInternal();

            base.OnDestroy(); // clears singleton static ref
        }

        private void Update()
        {
            float dt   = Time.deltaTime;
            float speed = _isDead ? criticalTransitionSpeed : transitionSpeed;

            // Smooth transitions
            LerpWeight(homeVolume,      ref _targetHome,      speed, dt);
            LerpWeight(deathVolume,     ref _targetDeath,     speed, dt);
            LerpWeight(spectatorVolume, ref _targetSpectator, speed, dt);

            // Low health pulse (sine wave oscillation between min/max)
            if (_lowHealthActive)
            {
                float pulse = pulseMin + (pulseMax - pulseMin)
                    * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f));
                SetWeight(lowHealthVolume, pulse);
            }
            else
            {
                LerpWeight(lowHealthVolume, ref _targetLowHealth, speed, dt);
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Switch top-level state (Home, Match, Spectator).</summary>
        public void SetState(PostProcessState state)
        {
            if (_currentState == state) return;
            _currentState = state;

            _targetHome      = state == PostProcessState.Home      ? 1f : 0f;
            _targetSpectator = state == PostProcessState.Spectator ? 1f : 0f;
        }

        /// <summary>Activate low-health red vignette pulse.</summary>
        public void SetLowHealth(bool active)
        {
            _lowHealthActive   = active;
            _targetLowHealth   = 0f; // smooth out when deactivated
        }

        /// <summary>
        /// Activate death desaturation overlay.
        /// Call with active=false when player respawns.
        /// </summary>
        public void SetDead(bool dead)
        {
            _isDead       = dead;
            _targetDeath  = dead ? 1f : 0f;

            if (dead)
            {
                SetLowHealth(false);
            }
        }

        /// <summary>Manually force spectator overlay. Useful for direct SpectateManager calls.</summary>
        public void SetSpectator(bool active)
        {
            _targetSpectator = active ? 1f : 0f;
        }

        // ── Local player registration ──────────────────────────────────────────

        /// <summary>
        /// Called by CharacterLifecycleController.OnStartNetwork() on the local owner.
        /// Wires health → low-health overlay and death → death overlay automatically.
        /// </summary>
        public static void RegisterLocalPlayer(
            CharacterLifecycleController lifecycle,
            IPlayerStatSystem statSystem)
        {
            if (!HasInstance) return;
            Instance.RegisterLocalPlayerInternal(lifecycle, statSystem);
        }

        private void RegisterLocalPlayerInternal(
            CharacterLifecycleController lifecycle,
            IPlayerStatSystem statSystem)
        {
            // Ensure clean state before wiring new player (e.g. reconnect / respawn scene)
            UnregisterLocalPlayerInternal();

            _localLifecycle  = lifecycle;
            _localStatSystem = statSystem;

            if (_localLifecycle != null)
            {
                _localLifecycle.OnDied      += HandleLocalDied;
                _localLifecycle.OnRespawned += HandleLocalRespawned;
            }

            if (_localStatSystem is PlayerStatSystem psys)
                psys.OnStatChanged += HandleStatChanged;

            // Sync current health state immediately so the overlay is correct on spawn
            if (_localStatSystem != null)
                EvaluateLowHealth(
                    _localStatSystem.GetStat(PlayerStatType.Health),
                    _localStatSystem.GetStat(PlayerStatType.MaxHealth));
        }

        private void UnregisterLocalPlayerInternal()
        {
            if (_localLifecycle != null)
            {
                _localLifecycle.OnDied      -= HandleLocalDied;
                _localLifecycle.OnRespawned -= HandleLocalRespawned;
                _localLifecycle = null;
            }

            if (_localStatSystem is PlayerStatSystem psys)
                psys.OnStatChanged -= HandleStatChanged;
            _localStatSystem = null;
        }

        // ── Local player event handlers ────────────────────────────────────────

        private void HandleLocalDied()
        {
            SetDead(true);
        }

        private void HandleLocalRespawned()
        {
            SetDead(false);
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (type != PlayerStatType.Health) return;
            float maxHealth = _localStatSystem?.GetStat(PlayerStatType.MaxHealth) ?? 100f;
            EvaluateLowHealth(newValue, maxHealth);
        }

        private void EvaluateLowHealth(float health, float maxHealth)
        {
            if (maxHealth <= 0f) return;
            float threshold = (AudioManager.HasInstance && AudioManager.Instance.Library != null)
                ? AudioManager.Instance.Library.lowHealthThreshold
                : 0.3f;

            bool isLow = (health / maxHealth) < threshold;
            SetLowHealth(isLow);
        }

        // ── UINavigator handler ────────────────────────────────────────────────

        private void OnUIPanelChanged(PanelType panel)
        {
            switch (panel)
            {
                case PanelType.Home:
                case PanelType.Login:
                case PanelType.Lobby:
                case PanelType.PartyCustomMode:
                    SetState(PostProcessState.Home);
                    break;

                default: // PanelType.None = in-match
                    SetState(PostProcessState.Match);
                    break;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void SetWeight(Volume vol, float weight)
        {
            if (vol != null) vol.weight = weight;
        }

        private static void LerpWeight(Volume vol, ref float target, float speed, float dt)
        {
            if (vol == null) return;
            vol.weight = Mathf.MoveTowards(vol.weight, target, speed * dt);
        }

        // ── Enums ──────────────────────────────────────────────────────────────

        public enum PostProcessState { None, Home, Match, Spectator }
    }
}
