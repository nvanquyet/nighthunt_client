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
    ///   3. Overlay hiện thông tin trận + danh sách player — không cần xác nhận.
    ///   4. <see cref="Hide"/> được gọi sau khi nhận match_ready hoặc match_cancelled.
    ///
    /// Inspector slots:
    ///   panel            — Root GameObject
    ///   canvasGroup      — Fade in/out
    ///   gameModeText     — "2v2 — ngẫu nhiên"
    ///   playerListParent — ScrollRect content chứa các MatchFoundPlayerRow
    ///   playerRowPrefab  — Prefab có MatchFoundPlayerRow component
    ///   statusText       — "Đã ghép trận — đang vào..."
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

        [Header("Player List")]
        [SerializeField] private Transform  playerListParent;
        [SerializeField] private GameObject playerRowPrefab;        // must have MatchFoundPlayerRow

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.25f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Coroutine _fadeCoroutine;
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
            // No buttons to wire — overlay is informational only.
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Hiện overlay thông tin ghép trận (không có nút xác nhận).
        /// </summary>
        public void Show(string gameMode, long[] playerIds, long localUserId)
        {
            // Populate match info
            if (gameModeText != null) gameModeText.text = FormatMode(gameMode);
            if (mapNameText  != null) mapNameText.text  = "Đang vào máy chủ...";

            // Populate player rows
            PopulatePlayers(playerIds, localUserId);

            // Status
            if (statusText != null) statusText.text = "Đã ghép trận — đang khởi động server...";

            // Fade in
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>Ẩn overlay (gọi sau match_ready hoặc match_cancelled).</summary>
        public void Hide()
        {
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
