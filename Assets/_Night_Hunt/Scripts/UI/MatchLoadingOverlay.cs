using System.Collections;
using System.Collections.Generic;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Gameplay.Core.Events;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// MatchLoadingOverlay — Full-screen overlay display trong on connection server và spawn players.
    ///
    /// Thay thế team labels / single progress bằng 2 danh sách PlayerCard (theo team).
    /// Mỗi card: avatar (CharacterDatabase), tên, ELO, rank, progress cá nhân.
    /// Overall progress bar theo dõi trạng thái connect chung.
    ///
    /// Inspector layout gợi ý:
    ///   MatchLoadingOverlay (Panel + CanvasGroup)
    ///   ├── MapNameText                    ← optional
    ///   ├── StatusText
    ///   ├── OverallProgressBar
    ///   ├── TipText
    ///   ├── VSLabel                        ← "VS"
    ///   ├── TeamAContainer (HorizontalLayoutGroup / GridLayout)
    ///   └── TeamBContainer
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchLoadingOverlay : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        private static MatchLoadingOverlay _instance;
        private static bool _authoritativeAllPlayersReady;
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
        [SerializeField] private GameObject  panel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Overall Status")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider          overallProgressBar;
        [SerializeField] private MonoBehaviour   overallProgressViewComponent;
        [SerializeField] private TextMeshProUGUI overallPercentText;
        [SerializeField] private TextMeshProUGUI mapNameText;   // optional

        [Header("VS Label")]
        [SerializeField] private TextMeshProUGUI vsLabel;

        [Header("Player Cards — prefab + containers")]
        [Tooltip("Prefab có component MatchPlayerCardView.")]
        [SerializeField] private GameObject  playerCardPrefab;
        [Tooltip("Container bên trái — Team A.")]
        [SerializeField] private Transform   teamAContainer;
        [Tooltip("Container bên phải — Team B.")]
        [SerializeField] private Transform   teamBContainer;

        [Header("Tips")]
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private string[]        tips = { };

        [Header("Settings")]
        [SerializeField] private float fadeDuration           = 0.3f;
        [SerializeField] private float minimumDisplayDuration = 2.5f;
        [SerializeField] private float delayAfterReady        = 1.5f;
        [SerializeField] private float connectionTimeout       = 45f;
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

        private MatchLoadStage _stage;
        private float          _progressTarget;
        private float          _progressCurrent;
        private Coroutine      _fadeCoroutine;
        private Coroutine      _timeoutCoroutine;
        private bool           _isVisible;
        private float          _showTime;

        // card lists indexed by userId
        private readonly Dictionary<long, MatchPlayerCardView> _cards = new();
        private int _totalExpected;
        private int _spawnedCount;
        private ILoadingProgressView _overallProgressView;

        // GameplayEventBus is scene-scoped (Singleton<T>) — it does not exist until the
        // map scene activates. SubscribeEvents() is called in ShowInternal() before the
        // scene loads, so we must retry in Update() until the bus is alive.
        private bool _eventsSubscribed;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
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
            _progressCurrent = Mathf.MoveTowards(_progressCurrent, _progressTarget, Time.deltaTime * 0.4f);
            if (overallProgressBar != null) overallProgressBar.value = _progressCurrent;
            ResolveProgressView()?.SetProgress(_progressCurrent);
            if (overallPercentText != null) overallPercentText.text  = $"{Mathf.RoundToInt(_progressCurrent * 100f)}%";

            // GameplayEventBus is scene-scoped — it does not exist until the map scene
            // activates. Retry subscription every frame until it becomes available.
            if (!_eventsSubscribed) TryLateSubscribe();
        }

        private void TryLateSubscribe()
        {
            var bus = NightHunt.Gameplay.Core.Events.GameplayEventBus.Instance;
            if (bus == null) return;

            bus.Subscribe<SpawningStartedEvent>(OnSpawningStarted);
            bus.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            bus.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
            _eventsSubscribed = true;
            Debug.Log($"[MatchLoadingOverlay] Late-subscribed to GameplayEventBus (stage={_stage})  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            if (_authoritativeAllPlayersReady)
                MarkAllPlayersReady();
        }

        private ILoadingProgressView ResolveProgressView()
        {
            if (overallProgressViewComponent is ILoadingProgressView assigned)
            {
                _overallProgressView = assigned;
                return _overallProgressView;
            }

            if (_overallProgressView != null)
                return _overallProgressView;

            if (panel != null)
            {
                _overallProgressView = panel.GetComponentInChildren<ILoadingProgressView>(true);
                if (_overallProgressView is MonoBehaviour mb)
                    overallProgressViewComponent = mb;
            }

            return _overallProgressView;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi ngay khi nhận WS "match_found" — hiện overlay trước khi có player data / scene.
        /// Overlay sẽ tiếp tục được dùng xuyên suốt đến khi game bắt đầu;
        /// <see cref="Show(NightHunt.Config.SceneId)"/> (gọi bởi match_ready) sẽ cập nhật
        /// player cards và load scene mà KHÔNG reset hoặc fade lại.
        /// </summary>
        public void ShowMatchFound(string gameMode = null)
        {
            if (_isVisible)
            {
                // Overlay đã mở (trường hợp hiếm) — chỉ cập nhật status
                SetStatus("Tìm thấy trận! Đang khởi động server...");
                return;
            }

            ResetReadinessSignal();
            CancelInvoke(nameof(HideOnReady));
            _showTime        = Time.realtimeSinceStartup;
            _progressTarget  = 0.08f;
            _progressCurrent = 0f;
            _spawnedCount    = 0;
            _eventsSubscribed = false;

            // Chưa có player data → không build cards, chỉ show status
            SetStatus("Tìm thấy trận! Đang khởi động server...");
            ShowRandomTip();
            if (vsLabel != null) vsLabel.text = "VS";

            StartTimeout();

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeInMatchFound());

            Debug.Log($"[FLOW §0] MatchLoadingOverlay.ShowMatchFound: gameMode={gameMode}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
        }

        public void Show(NightHunt.Config.SceneId mapId)
        {
            targetMapId = mapId;
            ShowInternal();
        }

        public void Show()
        {
            targetMapId = ResolveTargetMap();
            ShowInternal();
        }

        public void Hide()
        {
            StopTimeout();
            CancelInvoke(nameof(HideOnReady));
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        public void ForceHide(string reason = null)
        {
            StopTimeout();
            CancelInvoke(nameof(HideOnReady));
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            UnsubscribeEvents();
            SetVisibleImmediate(false);
            Debug.Log($"[MatchLoadingOverlay] Force hidden{(string.IsNullOrEmpty(reason) ? "" : $" ({reason})")}.");
        }

        public void MarkDsReady()    => SetStage(MatchLoadStage.Connecting);
        public void MarkWaitingRelayHost() => SetStage(MatchLoadStage.WaitingRelayHost);
        public void MarkConnected()  => SetStage(MatchLoadStage.ServerReady);
        public void MarkSpawning()   => SetStage(MatchLoadStage.Spawning);
        public void MarkAllPlayersReady() => SignalAllPlayersReady("instance");

        public static void ResetReadinessSignal()
        {
            _authoritativeAllPlayersReady = false;
        }

        public static void SignalAllPlayersReady(string source = null)
        {
            _authoritativeAllPlayersReady = true;
            var instance = Instance;
            if (instance != null)
                instance.OnAllPlayersReady(new AllPlayersReadyEvent());

            if (!string.IsNullOrEmpty(source))
                Debug.Log($"[MatchLoadingOverlay] AllPlayersReady signal source={source} instance={(instance != null ? "ok" : "null")}");
        }

        public void SetTargetMap(NightHunt.Config.SceneId mapId) => targetMapId = mapId;

        // ── Internal ───────────────────────────────────────────────────────────

        private void ShowInternal()
        {
            ResetReadinessSignal();
            CancelInvoke(nameof(HideOnReady));
            _spawnedCount    = 0;
            _eventsSubscribed = false;   // reset so TryLateSubscribe() re-runs for new match

            SetStage(MatchLoadStage.DsBooting);
            BuildPlayerCards();   // lần này đã có player data từ match_ready
            RefreshMapLabel();
            ShowRandomTip();
            if (vsLabel != null) vsLabel.text = "VS";

            Debug.Log($"[FLOW §2] MatchLoadingOverlay.ShowInternal: alreadyVisible={_isVisible} RoomState.CurrentRoom={RoomState.Instance?.CurrentRoom?.roomId} players={RoomState.Instance?.CurrentRoom?.players?.Count ?? 0}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            SubscribeEvents();
            StartTimeout();

            if (_isVisible)
            {
                // Overlay đã mở sẵn từ ShowMatchFound — KHÔNG fade lại (tránh flash alpha).
                // Chỉ cần activate scene mà SceneLoader.LoadGame() đã load ngầm.
                // _showTime giữ nguyên từ lúc ShowMatchFound để minimumDisplayDuration tính đúng.
                _progressTarget  = 0.15f;
                NightHunt.Core.SceneLoader.ActivateLoadedScene();
                return;
            }

            // Chưa visible (match_ready đến mà không có match_found trước) — flow cũ
            _showTime        = Time.realtimeSinceStartup;
            _progressTarget  = 0f;
            _progressCurrent = 0f;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());

            // NOTE: SceneLoader.LoadGame() is called by MatchFlowCoordinator AFTER Show().
            // Do NOT call it here — doing so would load the scene twice.
        }

        // ── Player Cards ───────────────────────────────────────────────────────

        /// <summary>
        /// Read RoomState.MatchReadyPlayers (from match_ready WS) → spawn cards into 2 containers by team.
        /// Falls back to RoomState.CurrentRoom.players if MatchReadyPlayers not populated.
        /// Local player uses SessionState.SelectedCharacterId for avatar.
        /// </summary>
        private void BuildPlayerCards()
        {
            ClearCards();

            if (playerCardPrefab == null)
            {
                Debug.LogWarning("[MatchLoadingOverlay] playerCardPrefab not yet gán trong Inspector.");
                return;
            }

            var session      = NightHunt.Core.GameManager.Instance?.SessionState;
            long localUserId = session?.UserId ?? 0L;

            // ── Priority 1: players from match_ready WS payload (Phase 3) ─────────────
            var matchReadyPlayers = RoomState.Instance?.MatchReadyPlayers;
            if (matchReadyPlayers != null && matchReadyPlayers.Count > 0)
            {
                Debug.Log($"[FLOW §2] BuildPlayerCards: using MatchReadyPlayers count={matchReadyPlayers.Count} localUserId={localUserId}");
                _totalExpected = matchReadyPlayers.Count;
                foreach (var p in matchReadyPlayers)
                {
                    Transform container = p.team == 1 ? teamAContainer : teamBContainer;
                    string charId = (p.userId == localUserId) ? session?.SelectedCharacterId : null;
                    SpawnCard(container, p.userId, p.username, charId, elo: p.elo, rank: p.tier, team: p.team);
                }
                return;
            }

            // ── Priority 2: players from RoomState.CurrentRoom (Custom_Relay / lobby) ──
            var room    = RoomState.Instance?.CurrentRoom;
            var players = room?.players;
            Debug.Log($"[FLOW §2] BuildPlayerCards: CurrentRoom={(room != null ? room.roomId.ToString() : "null")} players={(players?.Count.ToString() ?? "null")} localUserId={localUserId}");
            if (players != null && players.Count > 0)
            {
                _totalExpected = players.Count;
                foreach (var p in players)
                {
                    Transform container = p.team == 1 ? teamAContainer : teamBContainer;
                    string charId = (p.userId == localUserId) ? session?.SelectedCharacterId : null;
                    SpawnCard(container, p.userId, p.username, charId, elo: -1, rank: null, team: p.team);
                }
                return;
            }

            // ── Fallback: no player data at all — solo placeholder ─────────────────────
            Debug.LogWarning("[FLOW §2] BuildPlayerCards: NO player list in RoomState — showing solo placeholder. Ensure backend sends players[] in match_ready.");
            SpawnCard(teamAContainer, localUserId,
                      session?.Username ?? "Player",
                      session?.SelectedCharacterId,
                      elo: -1, rank: null, team: 1);
        }

        private void SpawnCard(Transform container, long userId, string playerName,
                               string characterId, int elo, string rank, int team)
        {
            if (container == null || playerCardPrefab == null) return;

            var go   = Instantiate(playerCardPrefab, container);
            var card = go.GetComponent<MatchPlayerCardView>();
            if (card == null)
            {
                Debug.LogError("[MatchLoadingOverlay] playerCardPrefab thiếu component MatchPlayerCardView!");
                Destroy(go);
                return;
            }

            card.Initialize(playerName, characterId, elo, rank, team);
            card.ResetToConnecting();
            _cards[userId] = card;
        }

        private void ClearCards()
        {
            _cards.Clear();
            ClearContainer(teamAContainer);
            ClearContainer(teamBContainer);
        }

        private static void ClearContainer(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        // ── Stage ──────────────────────────────────────────────────────────────

        private void SetStage(MatchLoadStage stage)
        {
            _stage = stage;
            switch (stage)
            {
                case MatchLoadStage.DsBooting:
                    SetStatus("Starting game server...");
                    _progressTarget = 0.15f;
                    foreach (var c in _cards.Values) c.SetProgress(0.1f, "Connecting...");
                    break;

                case MatchLoadStage.Connecting:
                    SetStatus("Connecting to server...");
                    _progressTarget = 0.3f;
                    break;

                case MatchLoadStage.WaitingRelayHost:
                    SetStatus("Waiting for host server...");
                    _progressTarget = 0.2f;
                    foreach (var c in _cards.Values) c.SetProgress(0.2f, "Waiting...");
                    break;

                case MatchLoadStage.ServerReady:
                    SetStatus("Connected. Loading match...");
                    _progressTarget = 0.5f;
                    // Advance all cards to "Connected" state
                    foreach (var c in _cards.Values) c.SetProgress(0.5f, "Connected");
                    break;

                case MatchLoadStage.Spawning:
                    SetStatus("Spawning players...");
                    _progressTarget = 0.75f;
                    foreach (var c in _cards.Values) c.SetProgress(0.6f, "Loading...");
                    break;

                case MatchLoadStage.AllReady:
                    SetStatus("All players ready! Preparing match...");
                    _progressTarget = 1f;
                    foreach (var c in _cards.Values) c.MarkReady();
                    break;
            }
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnSpawningStarted(SpawningStartedEvent _) => MarkSpawning();

        private void OnPlayerSpawned(PlayerSpawnedEvent e)
        {
            _spawnedCount = e.SpawnedCount;
            _totalExpected = e.ExpectedCount > 0 ? e.ExpectedCount : _totalExpected;

            // Overall progress bar: map spawn fraction to [0.75..0.98]
            if (_stage == MatchLoadStage.Spawning && _totalExpected > 0)
            {
                float f = (float)_spawnedCount / _totalExpected;
                _progressTarget = Mathf.Lerp(0.75f, 0.98f, f);
            }

            // Update status
            SetStatus($"{_spawnedCount} / {_totalExpected} players ready");

            // Mark Nth card as ready (we don't know which userId spawned, so we iterate)
            // Strategy: mark ready in order of List<cards> up to spawnedCount
            int idx = 0;
            foreach (var card in _cards.Values)
            {
                idx++;
                if (idx <= _spawnedCount)
                    card.MarkReady();
                else
                    card.SetProgress(Mathf.Lerp(0.6f, 0.9f, (float)_spawnedCount / Mathf.Max(1, _totalExpected)));
            }
        }

        private void OnAllPlayersReady(AllPlayersReadyEvent _)
        {
            _authoritativeAllPlayersReady = true;

            if (!_isVisible)
                return;

            StopTimeout();

            if (_stage == MatchLoadStage.AllReady)
            {
                if (!IsInvoking(nameof(HideOnReady)))
                    Invoke(nameof(HideOnReady), delayAfterReady);
                return;
            }

            SetStage(MatchLoadStage.AllReady);

            float elapsed    = Time.realtimeSinceStartup - _showTime;
            float remaining  = Mathf.Max(0f, minimumDisplayDuration - elapsed);
            float totalDelay = remaining + delayAfterReady;

            Debug.Log($"[FLOW §13] MatchLoadingOverlay.OnAllPlayersReady: elapsed={elapsed:F1}s minimumDisplay={minimumDisplayDuration}s → hiding overlay in {totalDelay:F1}s  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            Invoke(nameof(HideOnReady), totalDelay);
        }

        private void HideOnReady()
        {
            UnsubscribeEvents();
            Debug.Log("[MatchLoadingOverlay] All players ready — hiding overlay.");
            Hide();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static SceneId ResolveTargetMap()
        {
            string mapId = RoomState.Instance?.DsMapId;
            if (string.IsNullOrWhiteSpace(mapId))
                mapId = RoomState.Instance?.CurrentRoom?.mapId;

            if (!string.IsNullOrWhiteSpace(mapId)
                && MapConfig.TryGetById(mapId, out MapEntry entry))
                return entry.sceneId;

            return SceneId.GameMap_01;
        }

        private void RefreshMapLabel()
        {
            if (mapNameText == null) return;
            string mapId = RoomState.Instance?.DsMapId
                        ?? RoomState.Instance?.CurrentRoom?.mapId;
            if (!string.IsNullOrEmpty(mapId)
                && MapConfig.TryGetById(mapId, out MapEntry entry))
                mapNameText.text = entry.displayName;
            else
                mapNameText.text = "";
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            ResolveProgressView()?.SetMessage(msg);
        }

        private void ShowRandomTip()
        {
            string[] pool = (tips != null && tips.Length > 0) ? tips : DefaultTips;
            if (tipText != null && pool.Length > 0)
                tipText.text = pool[Random.Range(0, pool.Length)];
        }

        // ── Timeout ────────────────────────────────────────────────────────────

        private void StartTimeout()
        {
            StopTimeout();
            if (connectionTimeout > 0f)
                _timeoutCoroutine = StartCoroutine(TimeoutRoutine());
        }

        private void StopTimeout()
        {
            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }
        }

        private IEnumerator TimeoutRoutine()
        {
            yield return new WaitForSecondsRealtime(connectionTimeout);
            if (!_isVisible) yield break;

            // FishNet transport being connected is not the same as the match being
            // authoritative-ready. In Custom_Relay the host may have only spawned
            // 1/3 players while remote identity handshakes are still pending. Do
            // not synthesize AllPlayersReady here; wait for ServerGameManager RPCs.
            bool matchRunning = NightHunt.Networking.NetworkGameManager.Instance?.IsClient ?? false;
            if (matchRunning)
            {
                Debug.LogWarning($"[MatchLoadingOverlay] Timeout ({connectionTimeout}s) but FishNet IsClient=true - still waiting for authoritative AllPlayersReady.");
                SetStatus("Waiting for players...");
                StartTimeout();
                yield break;
            }

            Debug.LogWarning("[MatchLoadingOverlay] Connection timeout — returning to Home.");
            UnsubscribeEvents();
            Hide();

            yield return new WaitForSecondsRealtime(fadeDuration + 0.1f);
            var modal = GameModalWindow.Instance;
            if (modal != null)
            {
                modal.ShowNotice(
                    "Connection Failed",
                    "Unable to connect to the match server. Please try again.",
                    closeText: "Return to Home",
                    onClose: ReturnToHomeAfterTimeout);
            }
            else
            {
                ReturnToHomeAfterTimeout();
            }
        }

        private static void ReturnToHomeAfterTimeout()
        {
            var networkManager = NightHunt.Networking.NetworkGameManager.Instance;
            if (networkManager != null)
            {
                networkManager.ReturnHomeAfterConnectionFailure();
                return;
            }

            if (NightHunt.Core.SceneLoader.HasPendingSceneLoad)
            {
                NightHunt.Core.SceneLoader.ReturnHomeFromGameplayFlow();
                return;
            }

            if (UINavigator.Instance != null)
            {
                UINavigator.Instance.GoHome();
                return;
            }

            NightHunt.Core.SceneLoader.ReturnHomeFromGameplayFlow();
        }

        // ── Event Subscription ─────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            // Attempt immediate subscription. If GameplayEventBus.Instance is null
            // (map scene not yet loaded), TryLateSubscribe() in Update() will retry.
            if (!_eventsSubscribed)
                TryLateSubscribe();
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed) return;
            GameplayEventBus.Instance?.Unsubscribe<SpawningStartedEvent>(OnSpawningStarted);
            GameplayEventBus.Instance?.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            GameplayEventBus.Instance?.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
            _eventsSubscribed = false;
        }

        // ── Fade ───────────────────────────────────────────────────────────────

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

            // Overlay is now fully visible — allow the async-loaded game scene to activate.
            // SceneLoader.LoadGame() held the scene at 90% with allowSceneActivation=false
            // so this FadeIn animation could play without main-thread blocking.
            NightHunt.Core.SceneLoader.ActivateLoadedScene();
        }
        /// <summary>FadeInMatchFound: fade in overlay but do NOT activate scene (no scene loaded yet at match_found time).</summary>
        private System.Collections.IEnumerator FadeInMatchFound()
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
            // Do NOT call ActivateLoadedScene() � no scene queued yet at match_found time.
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
        DsBooting,
        WaitingRelayHost,
        Connecting,
        ServerReady,
        Spawning,
        AllReady,
    }
}
