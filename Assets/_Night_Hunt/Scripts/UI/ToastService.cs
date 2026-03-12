using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// ToastService — stacked, animated toast notifications.
    ///
    /// • Up to <see cref="maxVisible"/> toasts visible simultaneously.
    /// • Each toast slides up and fades out automatically.
    /// • Oldest is removed immediately when the stack is full.
    /// • Call <c>ToastService.Instance.Show("message")</c> from anywhere.
    /// </summary>
    public class ToastService : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static ToastService _instance;
        public static ToastService Instance => _instance != null ? _instance : Create();

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Container")]
        [Tooltip("Parent RectTransform where toast items are spawned. " +
                 "Add a VerticalLayoutGroup (spacing 8, child force expand H) here.")]
        [SerializeField]
        private RectTransform toastContainer;

        [Header("Toast Prefab")]
        [Tooltip("Prefab: Panel (CanvasGroup) > Background (Image) > MessageText (TMP)")]
        [SerializeField]
        private GameObject toastItemPrefab;

        [Header("Settings")] [SerializeField] private int maxVisible = 3;
        [SerializeField] private float displayTime = 2.5f;
        [SerializeField] private float slideDistance = 40f;
        [SerializeField] private float animDuration = 0.25f;

        [Header("Colors")] [SerializeField] private Color defaultColor = new Color(0.15f, 0.15f, 0.15f, 0.92f);
        [SerializeField] private Color errorColor = new Color(0.65f, 0.1f, 0.1f, 0.92f);
        [SerializeField] private Color successColor = new Color(0.1f, 0.55f, 0.2f, 0.92f);

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly Queue<ToastHandle> _active = new Queue<ToastHandle>();

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
        }

        private static ToastService Create()
        {
            var go = new GameObject("[ToastService]");
            return go.AddComponent<ToastService>();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Show(string message, float duration = -1f)
            => ShowInternal(message, defaultColor, duration < 0 ? displayTime : duration);

        public void ShowError(string message, float duration = -1f)
            => ShowInternal(message, errorColor, duration < 0 ? displayTime : duration);

        public void ShowSuccess(string message, float duration = -1f)
            => ShowInternal(message, successColor, duration < 0 ? displayTime : duration);

        // ── Internal ──────────────────────────────────────────────────────────

        private void ShowInternal(string message, Color bgColor, float duration)
        {
            // Trim oldest if at capacity
            while (_active.Count >= maxVisible)
            {
                var oldest = _active.Dequeue();
                if (oldest.go != null) Destroy(oldest.go);
            }

            GameObject item = SpawnItem(message, bgColor);
            if (item == null) return;

            var handle = new ToastHandle(item);
            _active.Enqueue(handle);
            StartCoroutine(AnimateToast(handle, duration));
        }

        private GameObject SpawnItem(string message, Color bgColor)
        {
            if (toastContainer == null) EnsureCanvas();

            GameObject item;
            if (toastItemPrefab != null)
            {
                item = Instantiate(toastItemPrefab, toastContainer);
            }
            else
            {
                // Auto-build a simple toast if no prefab assigned
                item = BuildDefaultItem(message, bgColor);
                return item;
            }

            // Assign text and background via ToastItem component if available,
            // otherwise fall back to GetComponentInChildren (for simple prefabs without ToastItem).
            var toastItem = ComponentResolver.Find<ToastItem>(item)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] ToastItem not found")
                .Resolve();
            if (toastItem != null)
            {
                if (toastItem.MessageText != null) toastItem.MessageText.text = message;
                if (toastItem.Background != null) toastItem.Background.color = bgColor;
            }
            else
            {
                var tmp = ComponentResolver.Find<TextMeshProUGUI>(item)
                    .OnSelf()
                    .InChildren()
                    .InParent()
                    .OrLogWarning("[Auto] TextMeshProUGUI not found")
                    .Resolve();
                if (tmp != null) tmp.text = message;
                var img = ComponentResolver.Find<Image>(item)
                    .OnSelf()
                    .InChildren()
                    .InParent()
                    .OrLogWarning("[Auto] Image not found")
                    .Resolve();
                if (img != null) img.color = bgColor;
            }

            return item;
        }

        private IEnumerator AnimateToast(ToastHandle handle, float duration)
        {
            if (handle.go == null) yield break;

            var cg = ComponentResolver.Find<CanvasGroup>(handle.go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CanvasGroup not found")
                .Resolve();
            var rect = ComponentResolver.Find<RectTransform>(handle.go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] RectTransform not found")
                .Resolve();
            if (cg == null) cg = handle.go.AddComponent<CanvasGroup>();

            Vector2 origin = rect != null ? rect.anchoredPosition : Vector2.zero;

            // Slide in — move from -slideDistance to 0, fade 0 → 1
            float t = 0f;
            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / animDuration);
                float ease = 1f - (1f - p) * (1f - p); // ease-out quad
                cg.alpha = ease;
                if (rect != null)
                    rect.anchoredPosition = origin + new Vector2(0f, -slideDistance * (1f - ease));
                yield return null;
            }

            cg.alpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(duration);

            // Fade out
            t = 0f;
            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / animDuration);
                cg.alpha = 1f - p;
                yield return null;
            }

            // Remove from queue and destroy
            if (handle.go != null)
            {
                // Dequeue if it's still us
                if (_active.Count > 0 && _active.Peek().go == handle.go)
                    _active.Dequeue();
                Destroy(handle.go);
            }
        }

        // ── Canvas / prefab auto-build ─────────────────────────────────────────

        private void EnsureCanvas()
        {
            if (toastContainer != null) return;

            Canvas c = ComponentResolver.Find<Canvas>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] Canvas not found")
                .Resolve();
            if (c == null)
            {
                c = gameObject.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 9997;
                var cs = gameObject.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                cs.matchWidthOrHeight = 0.5f;
            }

            // Container anchored at bottom-center
            var containerGO = new GameObject("ToastContainer", typeof(RectTransform));
            containerGO.transform.SetParent(transform, false);
            toastContainer = ComponentResolver.Find<RectTransform>(containerGO)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] RectTransform not found")
                .Resolve();
            toastContainer.anchorMin = new Vector2(0.5f, 0f);
            toastContainer.anchorMax = new Vector2(0.5f, 0f);
            toastContainer.pivot = new Vector2(0.5f, 0f);
            toastContainer.anchoredPosition = new Vector2(0f, 80f);
            toastContainer.sizeDelta = new Vector2(600f, 0f);

            var vlg = containerGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.reverseArrangement = true; // newest toast at bottom

            containerGO.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        private GameObject BuildDefaultItem(string message, Color bgColor)
        {
            var go = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(toastContainer, false);

            var bg = go.AddComponent<Image>();
            bg.color = bgColor;

            var rect = ComponentResolver.Find<RectTransform>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] RectTransform not found")
                .Resolve();
            rect.sizeDelta = new Vector2(0f, 56f);

            var textGO = new GameObject("Msg", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            var tr = ComponentResolver.Find<RectTransform>(textGO)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] RectTransform not found")
                .Resolve();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(16f, 8f);
            tr.offsetMax = new Vector2(-16f, -8f);

            go.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        // ── Helper struct ─────────────────────────────────────────────────────
        private class ToastHandle
        {
            public readonly GameObject go;

            public ToastHandle(GameObject g)
            {
                go = g;
            }
        }
    }
}