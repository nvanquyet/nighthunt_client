using System.Collections;
using System.Collections.Generic;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// Cac panel chinh trong 01_Home scene.
    /// Loading boot (DDOL) va MatchLoadingOverlay nam tren PersistentUICanvas — khong o day.
    /// </summary>
    public enum PanelType
    {
        Login,
        Home,
        Lobby
    }

    /// <summary>
    /// UINavigator - Thay the SceneLoader trong single-scene setup.
    /// Quan ly show/hide panel bang CanvasGroup thay vi load scene.
    /// Gan vao UIRoot GameObject trong scene.
    ///
    /// Sau khi chuyen panel, UINavigator goi INavigableView.OnShow() tren panel moi
    /// va INavigableView.OnHide() tren panel cu — vi CanvasGroup khong goi SetActive
    /// nen OnEnable/OnDisable cua cac view KHONG tuong ung voi trang thai "hien/an".
    /// </summary>
    public class UINavigator : Singleton<UINavigator>
    {
        [System.Serializable]
        public class PanelEntry
        {
            public PanelType type;
            public CanvasGroup canvasGroup;

            [Tooltip("Thoi gian fade in/out (giay). 0 = instant")]
            public float fadeDuration = 0.25f;
        }

        [Header("Panels")]
        [SerializeField] private List<PanelEntry> panels = new();

        [Header("Settings")]
        [SerializeField] private bool useFade = true;

        // null = chua hien panel nao
        private PanelType? _currentPanel = null;
        private Coroutine _fadeCoroutine;

        public PanelType? CurrentPanel => _currentPanel;
        public event System.Action<PanelType> OnPanelChanged;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            foreach (var p in panels)
                ApplyCanvasGroup(p.canvasGroup, 0f, false);
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        public void ShowPanel(PanelType target, bool forceInstant = false)
        {
            if (_currentPanel.HasValue && _currentPanel.Value == target) return;

            PanelType? previous = _currentPanel;
            _currentPanel = target;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            if (useFade && !forceInstant)
                _fadeCoroutine = StartCoroutine(TransitionPanels(previous, target));
            else
                TransitionInstant(previous, target);

            OnPanelChanged?.Invoke(target);
            Debug.Log($"[UINavigator] {previous?.ToString() ?? "none"} -> {target}");
        }

        public void GoLogin() => ShowPanel(PanelType.Login);
        public void GoHome()  => ShowPanel(PanelType.Home);
        public void GoLobby() => ShowPanel(PanelType.Lobby);

        // ─────────────────────────────────────────────
        // Internal
        // ─────────────────────────────────────────────

        private void TransitionInstant(PanelType? hide, PanelType show)
        {
            if (hide.HasValue)
            {
                var hideEntry = GetEntry(hide.Value);
                if (hideEntry != null)
                {
                    NotifyHide(hideEntry.canvasGroup);
                    ApplyCanvasGroup(hideEntry.canvasGroup, 0f, false);
                }
            }
            var showEntry = GetEntry(show);
            if (showEntry != null)
            {
                NotifyShow(showEntry.canvasGroup);
                ApplyCanvasGroup(showEntry.canvasGroup, 1f, true);
            }
        }

        private IEnumerator TransitionPanels(PanelType? hidePanelType, PanelType showPanelType)
        {
            var hideEntry = hidePanelType.HasValue ? GetEntry(hidePanelType.Value) : null;
            var showEntry = GetEntry(showPanelType);

            float duration = showEntry?.fadeDuration ?? 0.25f;

            // Notify hide truoc khi fade out
            if (hideEntry != null)
                NotifyHide(hideEntry.canvasGroup);

            // Fade out panel cu (neu co)
            if (hideEntry?.canvasGroup != null)
                yield return StartCoroutine(FadeCanvasGroup(hideEntry.canvasGroup, 1f, 0f, duration * 0.5f));

            if (hideEntry != null)
                ApplyCanvasGroup(hideEntry.canvasGroup, 0f, false);

            // Notify show truoc khi fade in
            if (showEntry != null)
                NotifyShow(showEntry.canvasGroup);

            // Fade in panel moi
            if (showEntry?.canvasGroup != null)
            {
                ApplyCanvasGroup(showEntry.canvasGroup, 0f, true); // bat interaction truoc khi fade
                yield return StartCoroutine(FadeCanvasGroup(showEntry.canvasGroup, 0f, 1f, duration));
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                cg.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            cg.alpha = to;
        }

        private static void ApplyCanvasGroup(CanvasGroup cg, float alpha, bool interactable)
        {
            if (cg == null) return;
            cg.alpha          = alpha;
            cg.interactable   = interactable;
            cg.blocksRaycasts = interactable;
        }

        // ─────────────────────────────────────────────
        // INavigableView callbacks
        // ─────────────────────────────────────────────

        /// <summary>
        /// Goi OnShow() tren tat ca INavigableView gan tren CanvasGroup GameObject.
        /// </summary>
        private static void NotifyShow(CanvasGroup cg)
        {
            if (cg == null) return;
            foreach (var v in cg.GetComponents<INavigableView>())
                v.OnShow();
        }

        /// <summary>
        /// Goi OnHide() tren tat ca INavigableView gan tren CanvasGroup GameObject.
        /// </summary>
        private static void NotifyHide(CanvasGroup cg)
        {
            if (cg == null) return;
            foreach (var v in cg.GetComponents<INavigableView>())
                v.OnHide();
        }

        private PanelEntry GetEntry(PanelType type)
        {
            return panels.Find(p => p.type == type);
        }
    }
}
