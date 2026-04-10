using System;
using System.Collections;
using System.Collections.Generic;
using NightHunt.Core;
using NightHunt.Services.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// MatchFoundOverlay — Full-screen overlay hiển thị khi backend gửi "match_found".
    ///
    /// Vòng đời:
    ///   1. Gắn vào PersistentUICanvas (DontDestroyOnLoad) — ẩn mặc định.
    ///   2. PartyController.HandleMatchFound() gọi <see cref="Show"/> với data từ WS.
    ///   3. Overlay hiện countdown + danh sách player. Player nhấn Accept → gọi <see cref="OnAcceptClicked"/>.
    ///   4. Khi all accepted  → backend gửi WS "match_ready" → PartyController xử lý.
    ///   5. <see cref="Hide"/> được gọi sau khi nhận match_ready hoặc match_cancelled.
    ///
    /// Inspector slots:
    ///   panel            — Root GameObject
    ///   canvasGroup      — Fade in/out
    ///   countdownText    — "12s" (đếm ngược accept timeout)
    ///   countdownFill    — Image radial fill (0→1)
    ///   gameModeText     — "2v2 — Industrial Zone"
    ///   playerListParent — ScrollRect content chứa các MatchFoundPlayerRow
    ///   playerRowPrefab  — Prefab có MatchFoundPlayerRow component
    ///   btn_Accept       — MUIP/standard button (xanh lá)
    ///   btn_Decline      — MUIP/standard button (đỏ)
    ///   statusText       — "Đang chờ xác nhận…" / "Đã chấp nhận"
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchFoundOverlay : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        private static MatchFoundOverlay _instance;
        public static MatchFoundOverlay Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (PersistentUICanvas.Instance != null)
                        _instance = PersistentUICanvas.Instance.MatchFoundOverlay;

                    if (_instance == null)
                        _instance = FindFirstObjectByType<MatchFoundOverlay>();
                }
                return _instance;
            }
        }

        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Panel")]
        [SerializeField] private GameObject  panel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Match Info")]
        [SerializeField] private TextMeshProUGUI gameModeText;
        [SerializeField] private TextMeshProUGUI mapNameText;

        [Header("Countdown")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private Image           countdownFill;     // radial fill — 1→0

        [Header("Player List")]
        [SerializeField] private Transform  playerListParent;
        [SerializeField] private GameObject playerRowPrefab;        // must have MatchFoundPlayerRow

        [Header("Buttons")]
        [SerializeField] private Button btn_Accept;
        [SerializeField] private Button btn_Decline;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings")]
        [SerializeField] private float fadeDuration      = 0.25f;
        [SerializeField] private int   defaultTimeoutSec = 30;      // fallback if backend sends 0

        // ── Events — bắt bởi PartyController ─────────────────────────────────

        public event Action OnAccepted;   // player nhấn Accept
        public event Action OnDeclined;   // player nhấn Decline hoặc timeout

        // ── Runtime ───────────────────────────────────────────────────────────

        private Coroutine _countdownCoroutine;
        private Coroutine _fadeCoroutine;
        private bool      _accepted;
        private bool      _isVisible;

        private readonly List<MatchFoundPlayerRow> _rows = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SetVisibleImmediate(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            if (btn_Accept  != null) btn_Accept .onClick.AddListener(OnAcceptClicked);
            if (btn_Decline != null) btn_Decline.onClick.AddListener(OnDeclineClicked);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Hiện overlay với thông tin match_found.
        /// </summary>
        /// <param name="lobbyToken">Token dùng để Accept/Decline</param>
        /// <param name="gameMode">Chuỗi mode: "2v2" / "3v3" / "5v5"</param>
        /// <param name="mapId">mapId từ WS (nullable). Nếu có, resolve displayName từ MapConfig.</param>
        /// <param name="playerIds">Mảng userId của tất cả players được ghép.</param>
        /// <param name="localUserId">UserId của client hiện tại — highlight row này.</param>
        /// <param name="timeoutSeconds">Số giây để accept. 0 = dùng defaultTimeoutSec.</param>
        public void Show(
            string   lobbyToken,
            string   gameMode,
            string   mapId,
            long[]   playerIds,
            long     localUserId,
            int      timeoutSeconds = 0)
        {
            _accepted = false;
            int secs = timeoutSeconds > 0 ? timeoutSeconds : defaultTimeoutSec;

            // Populate match info
            if (gameModeText != null) gameModeText.text = FormatMode(gameMode);

            if (mapNameText != null)
            {
                string mapDisplay = "Ngẫu nhiên";
                if (!string.IsNullOrEmpty(mapId)
                    && NightHunt.Config.MapConfig.TryGetById(mapId, out NightHunt.Config.MapEntry entry))
                    mapDisplay = entry.displayName;
                mapNameText.text = mapDisplay;
            }

            // Populate player rows
            PopulatePlayers(playerIds, localUserId);

            // Button state
            SetButtonsInteractable(true);
            if (statusText != null) statusText.text = "Đang chờ xác nhận…";

            // Fade in
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());

            // Countdown
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(CountdownRoutine(secs));
        }

        /// <summary>Ẩn overlay (gọi sau match_ready hoặc match_cancelled).</summary>
        public void Hide()
        {
            StopCountdown();
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Đánh dấu một player đã accept — cập nhật row icon.
        /// Gọi khi nhận WS event "player_accepted" (nếu backend gửi).
        /// </summary>
        public void MarkPlayerAccepted(long userId)
        {
            foreach (var row in _rows)
            {
                if (row != null && row.UserId == userId)
                    row.SetAccepted(true);
            }
        }

        // ── Button Handlers ───────────────────────────────────────────────────

        private void OnAcceptClicked()
        {
            if (_accepted) return;
            _accepted = true;
            SetButtonsInteractable(false);
            StopCountdown();
            if (statusText != null) statusText.text = "Đã chấp nhận — chờ players khác…";
            OnAccepted?.Invoke();
        }

        private void OnDeclineClicked()
        {
            StopCountdown();
            Hide();
            OnDeclined?.Invoke();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void PopulatePlayers(long[] playerIds, long localUserId)
        {
            // Clear old rows
            foreach (var row in _rows)
                if (row != null) Destroy(row.gameObject);
            _rows.Clear();

            if (playerListParent == null || playerRowPrefab == null || playerIds == null)
                return;

            foreach (long uid in playerIds)
            {
                var go  = Instantiate(playerRowPrefab, playerListParent);
                var row = go.GetComponent<MatchFoundPlayerRow>();
                if (row == null) { Destroy(go); continue; }

                row.Bind(uid, isLocalPlayer: uid == localUserId);
                _rows.Add(row);
            }
        }

        private IEnumerator CountdownRoutine(int totalSeconds)
        {
            float remaining = totalSeconds;

            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                float clamped = Mathf.Max(0f, remaining);

                if (countdownText != null)
                    countdownText.text = Mathf.CeilToInt(clamped).ToString();

                if (countdownFill != null)
                    countdownFill.fillAmount = clamped / totalSeconds;

                yield return null;
            }

            // Timeout — auto-decline
            if (countdownText != null) countdownText.text = "0";
            if (countdownFill != null) countdownFill.fillAmount = 0f;

            Debug.Log("[MatchFoundOverlay] Accept timeout — auto-declining.");
            Hide();
            OnDeclined?.Invoke();
        }

        private void StopCountdown()
        {
            if (_countdownCoroutine == null) return;
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        private void SetButtonsInteractable(bool value)
        {
            if (btn_Accept  != null) btn_Accept .interactable = value;
            if (btn_Decline != null) btn_Decline.interactable = value;
        }

        // ── Fade ──────────────────────────────────────────────────────────────

        private IEnumerator FadeIn()
        {
            SetVisibleImmediate(true);
            if (canvasGroup == null) yield break;
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

        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) { SetVisibleImmediate(false); yield break; }
            float t = fadeDuration;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            SetVisibleImmediate(false);
        }

        private void SetVisibleImmediate(bool visible)
        {
            _isVisible = visible;
            if (panel != null) panel.SetActive(visible);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FormatMode(string mode) => mode?.ToUpperInvariant() switch
        {
            "2V2" => "2v2",
            "3V3" => "3v3",
            "4V4" => "4v4",
            "5V5" => "5v5",
            _     => mode ?? "—"
        };

#if UNITY_EDITOR
        [ContextMenu("NightHunt/Create MatchFoundPlayerRow Template Prefab")]
        private void Editor_CreateRowPrefab()
        {
            const string dir  = "Assets/_Night_Hunt/Prefabs/UI";
            const string path = dir + "/MatchFoundPlayerRow_Template.prefab";

            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Night_Hunt/Prefabs", "UI");

            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[MatchFoundOverlay] MatchFoundPlayerRow_Template already exists.");
                return;
            }

            var go  = new GameObject("MatchFoundPlayerRow_Template");
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(280f, 44f);
            go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.88f);
            var hlg  = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.spacing = 6f;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            // Name
            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            nameGo.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            nameGo.GetComponent<TMPro.TextMeshProUGUI>().text = "Player";

            // Status indicator
            var statusGo = new GameObject("StatusIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            statusGo.transform.SetParent(go.transform, false);
            statusGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 20f;

            go.AddComponent<MatchFoundPlayerRow>();

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);

            if (playerRowPrefab == null)
            {
                playerRowPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[MatchFoundOverlay] Created {path}. Wire NameText + StatusIcon to MatchFoundPlayerRow.");
        }
#endif
    }
}
