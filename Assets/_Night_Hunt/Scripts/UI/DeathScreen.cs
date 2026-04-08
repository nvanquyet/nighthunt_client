using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Death screen overlay shown when the local player dies.
    ///
    /// Features:
    ///   • "YOU DIED" header + killer name label.
    ///   • Respawn countdown timer (counts down to 0 then enables Respawn button).
    ///   • Spectate button → cycles through living teammates via SpectateManager.
    ///   • Auto-hides when the player respawns (OnRespawned event).
    ///
    /// Setup:
    ///   1. Place in GameHUD canvas at high sort order so it draws over HUD.
    ///   2. Assign all [SerializeField] refs in the Inspector.
    ///   3. Call <see cref="RegisterPlayer"/> once the local NetworkPlayer is ready.
    /// </summary>
    public class DeathScreen : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Panel")] [SerializeField] private GameObject _deathPanel;

        [Header("Labels")] [SerializeField] private TextMeshProUGUI _killedByText; // "Killed by: PlayerX"
        [SerializeField] private TextMeshProUGUI _respawnTimerText; // "Respawn in: 5"

        [Header("Buttons")] [SerializeField] private Button _spectateButton;
        [SerializeField] private Button _respawnButton;

        [Header("Settings")]
        [Tooltip("Seconds before the Respawn button becomes active. " +
                 "Set 0 to make it available instantly.")]
        [SerializeField]
        private float _respawnDelay = 5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private CharacterLifecycleController _lifecycle;
        private PlayerHealthSystem _healthSystem;
        private Coroutine _countdownRoutine;
        private NetworkPlayer _localPlayer;
        private RespawnSystem _respawnSystem;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Register the local NetworkPlayer so this screen can subscribe to
        /// death / respawn events.  Call once the player GameObject is ready.
        /// </summary>
        public void RegisterPlayer(NetworkPlayer player)
        {
            if (player == null) return;

            // Unsubscribe from previous player (spectate scenario)
            UnregisterCurrent();

            _localPlayer = player;
            _respawnSystem = UnityEngine.Object.FindFirstObjectByType<RespawnSystem>();

            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(player)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CharacterLifecycleController not found")
                .Resolve();
            _healthSystem = ComponentResolver.Find<PlayerHealthSystem>(player)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] PlayerHealthSystem not found")
                .Resolve();

            // Lifecycle: used only to hide screen on respawn.
            if (_lifecycle != null)
                _lifecycle.OnRespawned += HandleRespawned;

            // HealthSystem: primary trigger for SHOW — carries the confirmed killer name from server.
            if (_healthSystem != null)
                _healthSystem.OnPlayerDied += HandlePlayerHealthDied;
        }

        public void Show(string killerName = "")
        {
            if (_deathPanel != null) _deathPanel.SetActive(true);

            if (_killedByText != null)
                _killedByText.text = string.IsNullOrEmpty(killerName)
                    ? "You died"
                    : $"Killed by: {killerName}";

            // Disable respawn button until countdown finishes
            if (_respawnButton != null) _respawnButton.interactable = false;

            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(RespawnCountdown());
        }

        public void Hide()
        {
            if (_deathPanel != null) _deathPanel.SetActive(false);
            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
                _countdownRoutine = null;
            }
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Hide();
            SetupButtons();
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<RespawnTimerEvent>(OnRespawnTimerReceived);
            GameplayEventBus.Instance?.Subscribe<RespawnCancelledEvent>(OnRespawnCancelled);
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<RespawnTimerEvent>(OnRespawnTimerReceived);
            GameplayEventBus.Instance?.Unsubscribe<RespawnCancelledEvent>(OnRespawnCancelled);
        }

        private void OnDestroy() => UnregisterCurrent();

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetupButtons()
        {
            if (_spectateButton != null)
                _spectateButton.onClick.AddListener(OnSpectateClicked);

            if (_respawnButton != null)
                _respawnButton.onClick.AddListener(OnRespawnClicked);
        }

        private void UnregisterCurrent()
        {
            if (_lifecycle != null)
            {
                _lifecycle.OnRespawned -= HandleRespawned;
                _lifecycle = null;
            }

            if (_healthSystem != null)
            {
                _healthSystem.OnPlayerDied -= HandlePlayerHealthDied;
                _healthSystem = null;
            }

            _localPlayer = null;
            _respawnSystem = null;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        // Killer name comes from the server-authoritative kill RPC — always populated correctly.
        private void HandlePlayerHealthDied(string killerName) => Show(killerName);

        private void HandleRespawned() => Hide();

        // RespawnSystem syncs the actual server delay via SyncVar → GameplayEventBus on clients.
        // Restart our countdown with the authoritative value instead of the local fallback.
        private void OnRespawnTimerReceived(RespawnTimerEvent evt)
        {
            if (_deathPanel == null || !_deathPanel.activeSelf) return;

            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(RespawnCountdown(evt.DelaySeconds));
        }

        private void OnRespawnCancelled(RespawnCancelledEvent evt)
        {
            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
                _countdownRoutine = null;
            }

            string msg = evt.Reason switch
            {
                "no_beacon"        => "Cần Beacon để hồi sinh!",
                "respawn_disabled" => "Hồi sinh bị khoá ở phase này",
                "beacon_destroyed" => "Beacon bị phá huỷ!",
                _                  => "Không thể hồi sinh"
            };

            if (_respawnTimerText != null)
                _respawnTimerText.text = msg;

            if (_respawnButton != null)
                _respawnButton.interactable = false;
        }

        // ── Coroutine ─────────────────────────────────────────────────────────

        private IEnumerator RespawnCountdown(float delay = -1f)
        {
            // Use server-provided delay if available, otherwise fall back to inspector value.
            float remaining = delay > 0f ? delay : _respawnDelay;

            while (remaining > 0f)
            {
                if (_respawnTimerText != null)
                    _respawnTimerText.text = $"Respawn in: {Mathf.CeilToInt(remaining)}";
                remaining -= Time.deltaTime;
                yield return null;
            }

            if (_respawnTimerText != null)
                _respawnTimerText.text = "Ready!";

            if (_respawnButton != null)
                _respawnButton.interactable = true;
        }

        // ── Button callbacks ──────────────────────────────────────────────────

        private void OnSpectateClicked()
        {
            var sm = SpectateManager.Instance;
            if (sm == null) return;

            // Cycle to next living player
            sm.SwitchSpectatedPlayer(next: true);
        }

        private void OnRespawnClicked()
        {
            if (_localPlayer != null && _respawnSystem != null)
                _respawnSystem.RequestRespawn(_localPlayer);
            else
                Debug.Log("[DeathScreen] Manual respawn requested — RespawnSystem or localPlayer not resolved.");
        }
    }
}