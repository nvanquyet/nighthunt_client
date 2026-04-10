using System.Collections;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Gameplay.Core.Events;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// MatchLoadingOverlay — Full-screen overlay hiển thị trong khi kết nối server
    /// và spawn players, trước khi load gameplay scene thật sự.
    ///
    /// Vòng đời:
    ///   1. Gắn vào PersistentUICanvas (DontDestroyOnLoad).
    ///   2. Ẩn mặc định khi khởi động.
    ///   3. <see cref="Show"/> được gọi khi match bắt đầu (từ CustomLobbyView / HomeView).
    ///   4. External systems gọi <see cref="MarkConnected"/> / <see cref="MarkSpawning"/>.
    ///   5. Khi nhận <see cref="AllPlayersReadyEvent"/> → delay ngắn → <see cref="SceneLoader.LoadGame"/>.
    ///
    /// Khác với LoadingManager (bootstrap startup):
    ///   • LoadingManager    = app startup, auto-login check.
    ///   • MatchLoadingOverlay = match connecting, chỉ show khi có match.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchLoadingOverlay : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        private static MatchLoadingOverlay _instance;
        public static MatchLoadingOverlay Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (PersistentUICanvas.Instance != null)
                        _instance = PersistentUICanvas.Instance.MatchLoadingOverlay;

                    if (_instance == null)
                        _instance = FindFirstObjectByType<MatchLoadingOverlay>();
                }
                return _instance;
            }
        }

        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Panel")]
        [SerializeField] private GameObject panel;           // Root panel GameObject
        [SerializeField] private CanvasGroup canvasGroup;    // Để fade in/out

        [Header("Team Info")]
        [SerializeField] private TextMeshProUGUI teamALabel;
        [SerializeField] private TextMeshProUGUI teamBLabel;
        [SerializeField] private TextMeshProUGUI vsLabel;

        [Header("Progress")]
        [SerializeField] private Slider          progressBar;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI progressPercentText;

        [Header("Tips")]
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private string[]        tips = { };

        [Header("Settings")]
        [SerializeField] private float fadeDuration   = 0.3f;
        [SerializeField] private float delayAfterReady = 1.5f;
        [SerializeField] private NightHunt.Config.SceneId targetMapId = NightHunt.Config.SceneId.GameMap_01;

        // ── Default tips ───────────────────────────────────────────────────────

        private static readonly string[] DefaultTips =
        {
            "Plant beacons to secure respawn points for your team.",
            "The boss spawns at Phase 2 — focus it for powerful loot.",
            "Capture zones generate score every second.",
            "Phase 3 respawns have a delay — protect your last teammate.",
        };

        // ── State ──────────────────────────────────────────────────────────────

        private MatchLoadStage _stage  = MatchLoadStage.Connecting;
        private float          _progressTarget;
        private float          _progressCurrent;
        private Coroutine      _fadeCoroutine;
        private bool           _isVisible;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Ẩn ngay khi khởi động
            SetVisibleImmediate(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!_isVisible) return;

            // Smooth progress bar animation
            _progressCurrent = Mathf.MoveTowards(_progressCurrent, _progressTarget, Time.deltaTime * 0.4f);

            if (progressBar          != null) progressBar.value        = _progressCurrent;
            if (progressPercentText  != null) progressPercentText.text = $"{Mathf.RoundToInt(_progressCurrent * 100f)}%";
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Hiện overlay và bắt đầu flow kết nối server.</summary>
        public void Show(NightHunt.Config.SceneId mapId)
        {
            targetMapId = mapId;
            ShowInternal();
        }

        /// <summary>Hiện overlay với map ID hiện tại (default hoặc đã set).</summary>
        public void Show()
        {
            targetMapId = ResolveTargetMap();
            ShowInternal();
        }

        /// <summary>Ẩn overlay (dùng khi cancel hoặc error).</summary>
        public void Hide()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>Gọi khi transport kết nối xong với server.</summary>
        public void MarkConnected() => SetStage(MatchLoadStage.ServerReady);

        /// <summary>Gọi khi server bắt đầu spawn characters.</summary>
        public void MarkSpawning() => SetStage(MatchLoadStage.Spawning);

        /// <summary>Override map sẽ được load. Gọi trước Show().</summary>
        public void SetTargetMap(NightHunt.Config.SceneId mapId) => targetMapId = mapId;

        // ── Internal ───────────────────────────────────────────────────────────

        private void ShowInternal()
        {
            SetStage(MatchLoadStage.Connecting);
            RefreshTeamInfo();
            ShowRandomTip();

            SubscribeEvents();

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());

            // Load game scene immediately so MatchNetworkConnector (placed in the game map
            // scene) can Start() and connect to the relay/DS. The overlay persists through
            // the scene transition (DontDestroyOnLoad via PersistentUICanvas). Once all
            // players have spawned the server fires AllPlayersReadyEvent → we hide.
            SceneLoader.LoadGame(targetMapId);
        }

        private void SetStage(MatchLoadStage stage)
        {
            _stage = stage;
            switch (stage)
            {
                case MatchLoadStage.Connecting:
                    SetStatus("Connecting to server…");
                    _progressTarget = 0.1f;
                    break;

                case MatchLoadStage.ServerReady:
                    SetStatus("Server ready. Spawning players…");
                    _progressTarget = 0.5f;
                    break;

                case MatchLoadStage.Spawning:
                    SetStatus("Spawning players…");
                    _progressTarget = 0.75f;
                    break;

                case MatchLoadStage.AllReady:
                    SetStatus("All players ready! Starting…");
                    _progressTarget = 1f;
                    break;
            }
        }

        private void OnSpawningStarted(SpawningStartedEvent _)
        {
            MarkSpawning();
        }

        private void OnAllPlayersReady(AllPlayersReadyEvent _)
        {
            SetStage(MatchLoadStage.AllReady);
            // Game scene is already loaded (triggered inside ShowInternal).
            // Wait for progress-bar animation then hide overlay and unsubscribe.
            Invoke(nameof(HideOnReady), delayAfterReady);
        }

        private void HideOnReady()
        {
            UnsubscribeEvents();
            Debug.Log("[MatchLoadingOverlay] All players ready — hiding overlay.");
            Hide();
        }

        private static SceneId ResolveTargetMap()
        {
            // Prefer DsMapId (set directly from match_ready WS event, most reliable)
            string mapId = RoomState.Instance?.DsMapId;
            // Fallback to current room's mapId
            if (string.IsNullOrWhiteSpace(mapId))
                mapId = RoomState.Instance?.CurrentRoom?.mapId;

            if (!string.IsNullOrWhiteSpace(mapId)
                && MapConfig.TryGetById(mapId, out MapEntry entry))
            {
                return entry.sceneId;
            }

            return SceneId.GameMap_01;
        }

        private void RefreshTeamInfo()
        {
            var room = RoomState.Instance?.CurrentRoom;
            if (room?.players == null || room.players.Count == 0)
            {
                // Fallback khi không có data (dev mode)
                if (teamALabel != null) teamALabel.text = "Team A";
                if (teamBLabel != null) teamBLabel.text = "Team B";
                if (vsLabel    != null) vsLabel.text    = "VS";
                return;
            }

            // Gộp tên player theo team. RoomPlayerResponse.team = 1 hoặc 2.
            var teamANames = new System.Collections.Generic.List<string>();
            var teamBNames = new System.Collections.Generic.List<string>();

            foreach (var p in room.players)
            {
                string name = string.IsNullOrEmpty(p.username) ? $"Player {p.userId}" : p.username;
                if (p.team == 1) teamANames.Add(name);
                else             teamBNames.Add(name);
            }

            if (teamALabel != null)
                teamALabel.text = teamANames.Count > 0
                    ? string.Join("\n", teamANames)
                    : "Team A";

            if (teamBLabel != null)
                teamBLabel.text = teamBNames.Count > 0
                    ? string.Join("\n", teamBNames)
                    : "Team B";

            if (vsLabel != null)
                vsLabel.text = "VS";
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void ShowRandomTip()
        {
            string[] pool = (tips != null && tips.Length > 0) ? tips : DefaultTips;
            if (tipText != null && pool.Length > 0)
                tipText.text = pool[Random.Range(0, pool.Length)];
        }

        // ── Event subscription ─────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            GameplayEventBus.Instance?.Subscribe<SpawningStartedEvent>(OnSpawningStarted);
            GameplayEventBus.Instance?.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
        }

        private void UnsubscribeEvents()
        {
            GameplayEventBus.Instance?.Unsubscribe<SpawningStartedEvent>(OnSpawningStarted);
            GameplayEventBus.Instance?.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
        }

        // ── Fade helpers ───────────────────────────────────────────────────────

        private IEnumerator FadeIn()
        {
            SetVisibleImmediate(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }
        }

        private IEnumerator FadeOut()
        {
            if (canvasGroup != null)
            {
                float t = fadeDuration;
                while (t > 0f)
                {
                    t -= Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }
            SetVisibleImmediate(false);
        }

        private void SetVisibleImmediate(bool visible)
        {
            _isVisible = visible;
            if (panel != null) panel.SetActive(visible);
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = visible ? 1f : 0f;
                canvasGroup.interactable   = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }
    }

    // ── Stage enum ─────────────────────────────────────────────────────────────

    public enum MatchLoadStage
    {
        Connecting,   // Đang kết nối transport
        ServerReady,  // Server đã ready, chờ spawn
        Spawning,     // Đang spawn characters
        AllReady,     // Tất cả players ready → load scene
    }
}
