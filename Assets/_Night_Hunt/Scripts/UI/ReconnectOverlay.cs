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
            EnsureRuntimeWiring();
            SetVisible(false);

            if (_returnHomeButton != null)
            {
                _returnHomeButton.onClick.RemoveListener(OnReturnHomeClicked);
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

            if (current > max)
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
            EnsureRuntimeWiring();
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

        private void EnsureRuntimeWiring()
        {
            if (_canvasGroup != null && _statusText != null && _returnHomeButton != null)
                return;

            Transform root = transform.Find("ReconnectOverlayRoot");
            RectTransform rootRt;
            if (root == null)
            {
                var rootGo = new GameObject("ReconnectOverlayRoot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootGo.transform.SetParent(transform, false);
                rootRt = (RectTransform)rootGo.transform;
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;

                var bg = rootGo.GetComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.68f);
            }
            else
            {
                rootRt = (RectTransform)root;
            }

            if (_canvasGroup == null)
                _canvasGroup = rootRt.GetComponent<CanvasGroup>() ?? rootRt.gameObject.AddComponent<CanvasGroup>();

            RectTransform panelRt = rootRt.Find("Panel") as RectTransform;
            if (panelRt == null)
            {
                var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(rootRt, false);
                panelRt = (RectTransform)panelGo.transform;
                panelRt.anchorMin = new Vector2(0.5f, 0.5f);
                panelRt.anchorMax = new Vector2(0.5f, 0.5f);
                panelRt.pivot = new Vector2(0.5f, 0.5f);
                panelRt.sizeDelta = new Vector2(560f, 220f);

                var panelImage = panelGo.GetComponent<Image>();
                panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
            }

            if (_statusText == null)
            {
                var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
                statusGo.transform.SetParent(panelRt, false);
                var rt = (RectTransform)statusGo.transform;
                rt.anchorMin = new Vector2(0f, 0.45f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(28f, 0f);
                rt.offsetMax = new Vector2(-28f, -24f);

                _statusText = statusGo.GetComponent<TextMeshProUGUI>();
                _statusText.alignment = TextAlignmentOptions.Center;
                _statusText.fontSize = 30f;
                _statusText.color = Color.white;
                _statusText.enableWordWrapping = true;
                _statusText.text = "Reconnecting...";
            }

            if (_returnHomeButton == null)
            {
                var buttonGo = new GameObject("ReturnHomeButton", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonGo.transform.SetParent(panelRt, false);
                var rt = (RectTransform)buttonGo.transform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(240f, 52f);
                rt.anchoredPosition = new Vector2(0f, 28f);

                var image = buttonGo.GetComponent<Image>();
                image.color = new Color(0.18f, 0.42f, 0.88f, 1f);
                _returnHomeButton = buttonGo.GetComponent<Button>();

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(buttonGo.transform, false);
                var labelRt = (RectTransform)labelGo.transform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var label = labelGo.GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 22f;
                label.color = Color.white;
                label.text = "Return Home";
            }
        }
    }
}
