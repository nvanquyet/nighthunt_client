using NightHunt.Core;
using NightHunt.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// ReconnectOverlay — shown mid-match when the FishNet client connection drops.
    ///
    /// Lifecycle:
    ///   FishNet LocalConnectionState → Stopping/Stopped while match is active
    ///     → overlay appears with "Reconnecting... (attempt N/max)" text
    ///   NetworkGameManager.OnRetryAttempt fires per attempt → update label
    ///   Final failure (currentAttempt == maxAttempts) → show "Return to Home" button
    ///   Successful reconnect → hide overlay
    ///
    /// Setup:
    ///   - Add this component under PersistentUICanvas (DontDestroyOnLoad).
    ///   - Default: gameObject inactive (hidden).
    ///   - Wire _statusText, _returnHomeButton in Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReconnectOverlay : SingletonPersistent<ReconnectOverlay>
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("TMP label showing reconnect attempt progress.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("Button shown after max retries (or 15s); navigates to Home.")]
        [SerializeField] private Button _returnHomeButton;

        [Tooltip("Background dimmer / panel root — toggled with this GameObject.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Settings")]
        [Tooltip("Show the Return to Home button this many seconds after the first retry attempt, regardless of retry count.")]
        [SerializeField] private float _showHomeButtonAfterSeconds = 15f;

        // ── State ─────────────────────────────────────────────────────────────

        private bool  _visible;
        private float _shownAt;

        // ── Singleton lifecycle ───────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            SetVisible(false);

            if (_returnHomeButton != null)
            {
                _returnHomeButton.onClick.AddListener(OnReturnHomeClicked);
                _returnHomeButton.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            var ngm = NetworkGameManager.Instance;
            if (ngm != null)
                ngm.OnRetryAttempt += HandleRetryAttempt;
        }

        private void OnDisable()
        {
            var ngm = NetworkGameManager.Instance;
            if (ngm != null)
                ngm.OnRetryAttempt -= HandleRetryAttempt;
        }

        private void Update()
        {
            if (!_visible) return;

            // Show home button once the timeout has elapsed (even if max retries not yet hit).
            if (_returnHomeButton != null
                && !_returnHomeButton.gameObject.activeSelf
                && Time.unscaledTime - _shownAt >= _showHomeButtonAfterSeconds)
            {
                _returnHomeButton.gameObject.SetActive(true);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show overlay — called when FishNet connection drops mid-match.</summary>
        public void Show()
        {
            if (_visible) return;
            _visible = true;
            _shownAt = Time.unscaledTime;

            SetStatus("Reconnecting...");
            if (_returnHomeButton != null)
                _returnHomeButton.gameObject.SetActive(false);

            SetVisible(true);
            Debug.Log("[ReconnectOverlay] Shown.");
        }

        /// <summary>Hide overlay — called when reconnect succeeds.</summary>
        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            SetVisible(false);
            Debug.Log("[ReconnectOverlay] Hidden.");
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleRetryAttempt(int current, int max)
        {
            if (!_visible) Show();

            if (current >= max)
            {
                // Final failure.
                SetStatus("Connection failed. Returning to Home...");
                if (_returnHomeButton != null)
                    _returnHomeButton.gameObject.SetActive(true);
            }
            else
            {
                SetStatus($"Reconnecting... (attempt {current}/{max})");
            }
        }

        private void OnReturnHomeClicked()
        {
            Hide();
            // Clear stale room/network state before returning to Home.
            NightHunt.State.RoomState.Instance?.ClearRoom();
            NightHunt.State.RoomState.Instance?.ClearNetworkSession();
            // NetworkGameManager will also call LoadHome after max retries;
            // this button lets the user skip the final wait.
            SceneLoader.LoadHome();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private void SetVisible(bool show)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = show ? 1f : 0f;
                _canvasGroup.interactable   = show;
                _canvasGroup.blocksRaycasts = show;
            }
            else
            {
                gameObject.SetActive(show);
            }
        }
    }
}
