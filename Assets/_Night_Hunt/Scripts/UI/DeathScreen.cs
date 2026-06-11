using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Diagnostics;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking.Player;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Shows only the respawn actions confirmed by the server for the local player.
    /// </summary>
    public class DeathScreen : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _deathPanel;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _killedByText;
        [SerializeField] private TextMeshProUGUI _respawnTimerText;

        [Header("Buttons")]
        [SerializeField] private Button _spectateButton;
        [SerializeField] private Button _respawnButton;

        private CharacterLifecycleController _lifecycle;
        private PlayerHealthSystem _healthSystem;
        private Coroutine _countdownRoutine;
        private Coroutine _statusTimeoutRoutine;
        private NetworkPlayer _localPlayer;
        private RespawnSystem _respawnSystem;
        private bool _hasRespawnDisposition;

        public void RegisterPlayer(NetworkPlayer player)
        {
            if (player == null)
                return;

            UnregisterCurrent();
            _localPlayer = player;
            _respawnSystem = FindFirstObjectByType<RespawnSystem>();

            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[Auto] CharacterLifecycleController not found").Resolve();
            _healthSystem = ComponentResolver.Find<PlayerHealthSystem>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[Auto] PlayerHealthSystem not found").Resolve();

            if (_lifecycle != null)
                _lifecycle.OnRespawned += HandleRespawned;
            if (_healthSystem != null)
                _healthSystem.OnPlayerDied += HandlePlayerHealthDied;
        }

        public void Show(string killerName = "")
        {
            if (_deathPanel != null)
                _deathPanel.SetActive(true);

            if (_killedByText != null)
            {
                _killedByText.text = string.IsNullOrEmpty(killerName)
                    ? "You were eliminated"
                    : $"You were eliminated by {killerName}";
            }

            StopCountdown();
            StopStatusTimeout();
            _hasRespawnDisposition = false;
            SetButtonVisible(_respawnButton, false);
            SetButtonVisible(_spectateButton, false);
            SetStatus("Checking respawn status...");
            if (!ApplyCachedDisposition())
                _statusTimeoutRoutine = StartCoroutine(RespawnStatusTimeout());

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "DeathScreenShow",
                $"player={_localPlayer?.DisplayName ?? "null"} killer={killerName ?? string.Empty}",
                this);
        }

        public void Hide()
        {
            if (_deathPanel != null)
                _deathPanel.SetActive(false);
            StopCountdown();
            StopStatusTimeout();
        }

        private void Awake()
        {
            Hide();
            SetupButtons();
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<RespawnDispositionEvent>(OnRespawnDisposition);
            GameplayEventBus.Instance?.Subscribe<RespawnCancelledEvent>(OnRespawnCancelled);
            ApplyCachedDisposition();
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<RespawnDispositionEvent>(OnRespawnDisposition);
            GameplayEventBus.Instance?.Unsubscribe<RespawnCancelledEvent>(OnRespawnCancelled);
            StopStatusTimeout();
        }

        private void OnDestroy()
        {
            UnregisterCurrent();
        }

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
                _lifecycle.OnRespawned -= HandleRespawned;
            if (_healthSystem != null)
                _healthSystem.OnPlayerDied -= HandlePlayerHealthDied;

            _lifecycle = null;
            _healthSystem = null;
            _localPlayer = null;
            _respawnSystem = null;
        }

        private void HandlePlayerHealthDied(string killerName)
        {
            // Wait for the server disposition before exposing respawn or spectate actions.
            Show(killerName);
        }

        private void HandleRespawned()
        {
            SpectateManager.Instance?.StopSpectating();
            _respawnSystem?.ClearLocalDisposition();
            Hide();
        }

        private void OnRespawnDisposition(RespawnDispositionEvent evt)
        {
            if (_deathPanel == null || !_deathPanel.activeSelf)
                return;

            ApplyDisposition(evt.Disposition, evt.DelaySeconds, evt.Reason);
        }

        private void OnRespawnCancelled(RespawnCancelledEvent evt)
        {
            ApplyDisposition(RespawnDisposition.Eliminated, 0f, evt.Reason);
        }

        private bool ApplyCachedDisposition()
        {
            if (_deathPanel == null || !_deathPanel.activeSelf || _respawnSystem == null)
                return false;

            if (_respawnSystem.TryGetLocalDisposition(
                    _localPlayer,
                    out RespawnDisposition disposition,
                    out float remainingDelay,
                    out string reason))
            {
                ApplyDisposition(disposition, remainingDelay, reason);
                return true;
            }

            return false;
        }

        private void ApplyDisposition(RespawnDisposition disposition, float delay, string reason)
        {
            _hasRespawnDisposition = true;
            StopStatusTimeout();
            StopCountdown();
            SetButtonVisible(_respawnButton, false);
            SetButtonVisible(_spectateButton, false);

            switch (disposition)
            {
                case RespawnDisposition.Queued:
                    SetButtonVisible(_respawnButton, true, false);
                    SetButtonVisible(_spectateButton, HasSpectateTargets(), true);
                    _countdownRoutine = StartCoroutine(RespawnCountdown(delay));
                    break;

                case RespawnDisposition.WaitingForFinalZone:
                    SetStatus("Waiting for final zone revival");
                    TryAutoSpectate();
                    break;

                default:
                    SetStatus(GetEliminatedStatus(reason));
                    TryAutoSpectate();
                    break;
            }

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "DeathScreenDisposition",
                $"player={_localPlayer?.DisplayName ?? "null"} disposition={disposition} delay={delay:F2} reason={reason}",
                this);
        }

        private void TryAutoSpectate()
        {
            SpectateManager spectateManager = SpectateManager.Instance;
            if (spectateManager != null && spectateManager.HasLivingSpectateTargets())
            {
                spectateManager.SwitchSpectatedPlayer(next: true);
                return;
            }

            // No living teammate: remain on the eliminated screen with no actions.
            SetButtonVisible(_spectateButton, false);
            SetButtonVisible(_respawnButton, false);
        }

        private bool HasSpectateTargets()
        {
            return SpectateManager.Instance != null && SpectateManager.Instance.HasLivingSpectateTargets();
        }

        private IEnumerator RespawnCountdown(float delay)
        {
            float remaining = Mathf.Max(0f, delay);
            while (remaining > 0f)
            {
                SetStatus($"Respawning in {Mathf.CeilToInt(remaining)}s...");
                remaining -= Time.deltaTime;
                yield return null;
            }

            SetStatus("Respawning...");
            _countdownRoutine = null;
        }

        private IEnumerator RespawnStatusTimeout()
        {
            yield return new WaitForSeconds(1.25f);
            _statusTimeoutRoutine = null;

            if (_hasRespawnDisposition || _deathPanel == null || !_deathPanel.activeSelf)
                yield break;

            ApplyDisposition(RespawnDisposition.Eliminated, 0f, "status_timeout");
        }

        private string GetEliminatedStatus(string reason)
        {
            return reason switch
            {
                "beacon_destroyed" => "Beacon was destroyed",
                "respawn_disabled" => "No respawn available",
                "final_zone_respawn_disabled" => "No final zone revival",
                _ => "No respawn available"
            };
        }

        private void StopCountdown()
        {
            if (_countdownRoutine == null)
                return;

            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        private void StopStatusTimeout()
        {
            if (_statusTimeoutRoutine == null)
                return;

            StopCoroutine(_statusTimeoutRoutine);
            _statusTimeoutRoutine = null;
        }

        private void SetStatus(string message)
        {
            if (_respawnTimerText != null)
                _respawnTimerText.text = message;
        }

        private static void SetButtonVisible(Button button, bool visible, bool interactable = false)
        {
            if (button == null)
                return;

            button.gameObject.SetActive(visible);
            button.interactable = visible && interactable;
        }

        private void OnSpectateClicked()
        {
            SpectateManager.Instance?.SwitchSpectatedPlayer(next: true);
        }

        private void OnRespawnClicked()
        {
            if (_localPlayer != null && _respawnSystem != null)
                _respawnSystem.RequestRespawn(_localPlayer);
        }
    }
}
