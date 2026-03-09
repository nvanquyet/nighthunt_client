using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking;

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

        [Header("Panel")]
        [SerializeField] private GameObject _deathPanel;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _killedByText;    // "Killed by: PlayerX"
        [SerializeField] private TextMeshProUGUI _respawnTimerText; // "Respawn in: 5"

        [Header("Buttons")]
        [SerializeField] private Button _spectateButton;
        [SerializeField] private Button _respawnButton;

        [Header("Settings")]
        [Tooltip("Seconds before the Respawn button becomes active. " +
                 "Set 0 to make it available instantly.")]
        [SerializeField] private float _respawnDelay = 5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private CharacterLifecycleController _lifecycle;        private PlayerHealthSystem           _healthSystem;        private Coroutine                    _countdownRoutine;

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

            _lifecycle    = player.GetComponent<CharacterLifecycleController>();
            _healthSystem = player.GetComponent<PlayerHealthSystem>();

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
        }

        // ── Event handlers ────────────────────────────────────────────────────

        // Killer name comes from the server-authoritative kill RPC — always populated correctly.
        private void HandlePlayerHealthDied(string killerName) => Show(killerName);

        private void HandleRespawned() => Hide();

        // ── Coroutine ─────────────────────────────────────────────────────────

        private IEnumerator RespawnCountdown()
        {
            float remaining = _respawnDelay;

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
            // Respawn is server-authoritative — the RespawnSystem already requested
            // a respawn on death (if autoRequestRespawnOnDeath = true).
            // This button is provided for modes where manual respawn is required.
            // Here we just note the intent; the actual respawn is handled by RespawnSystem.
            Debug.Log("[DeathScreen] Player requested manual respawn.");
        }
    }
}
