using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// Panel types used by the 01_Home UI flow.
    /// Lobby is normalized to CustomLobby for old serialized values.
    /// </summary>
    public enum PanelType
    {
        None = 0,
        Login = 1,
        Home = 2,
        Lobby = 3,
        CustomLobby = 4,
        Settings = 5
    }

    /// <summary>
    /// Code-first navigation hub for 01_Home.
    /// Buttons call controller/navigator methods in code; this class owns the visual
    /// transition order so gameplay/state logic never calls Shift package panels directly.
    /// </summary>
    public class UINavigator : Singleton<UINavigator>
    {
        [Serializable]
        public sealed class NavigationRoute
        {
            public PanelType panel = PanelType.None;
            [Tooltip("Optional route root. Navigator activates this before OnShowAsync.")]
            public GameObject rootObject;
            [Tooltip("Optional view implementing INavigableView. Auto-resolved when empty.")]
            public MonoBehaviour view;
        }

        [Header("Code-First Routes")]
        [SerializeField] private List<NavigationRoute> routes = new();

        [Header("Visual State")]
        [Tooltip("Animator on the splash/login screen root.")]
        [SerializeField] private Animator splashScreenAnimator;
        [Tooltip("Animator on the main Shift panel root.")]
        [SerializeField] private Animator mainPanelsAnimator;
        [SerializeField] private string loginAnimatorState = "Login";
        [SerializeField] private string splashHiddenState = "Invisible";
        [SerializeField] private string mainPanelsStartState = "Start";
        [SerializeField] private string mainPanelsHiddenState = "Invisible";
        [SerializeField] private string routePanelInState = "Panel In";

        private readonly Dictionary<PanelType, NavigationRoute> _routeLookup = new();
        private readonly Dictionary<PanelType, INavigableView> _viewCache = new();
        private PanelType _currentPanel = PanelType.None;
        private PanelType _settingsReturnPanel = PanelType.Home;
        private int _navigationVersion;
        private bool _isNavigating;

        public PanelType CurrentPanel => _currentPanel;
        public bool IsNavigating => _isNavigating;

        public event Action<PanelType> OnPanelChanged;
        public event Action OnPlayerDataLoaded;

        protected override void OnSingletonAwake()
        {
            RebuildRouteLookup();
        }

        private void OnValidate()
        {
            RebuildRouteLookup();
        }

        private void RebuildRouteLookup()
        {
            _routeLookup.Clear();
            if (routes == null) return;

            foreach (var route in routes)
            {
                if (route == null) continue;
                var panel = Normalize(route.panel);
                if (panel == PanelType.None) continue;
                _routeLookup[panel] = route;
            }

            _viewCache.Clear();
        }

        public void GoLogin() => ShowPanel(PanelType.Login);
        public void GoHome() => ShowPanel(PanelType.Home);
        public void GoLobby() => ShowPanel(PanelType.CustomLobby);
        public void GoCustomLobby() => ShowPanel(PanelType.CustomLobby);
        public void GoSettings() => ShowPanel(PanelType.Settings);

        public void GoBack()
        {
            switch (_currentPanel)
            {
                case PanelType.Settings:
                    ShowPanel(GetSettingsReturnPanel(), "Back");
                    break;
                case PanelType.CustomLobby:
                    ShowPanel(PanelType.Home, "Back");
                    break;
                case PanelType.Home:
                    // Maybe show exit confirm
                    break;
            }
        }

        public void GoForce(PanelType target)
        {
            ShowPanel(target, forceInstant: true, bypassCanLeave: true);
        }

        public void ShowPanel(PanelType target, bool forceInstant = false)
        {
            _ = ShowPanelAsync(target, forceInstant);
        }

        public void ShowPanel(PanelType target, string reason)
        {
            _ = ShowPanelAsync(target, reason: reason);
        }

        public void ShowPanel(PanelType target, bool forceInstant, bool bypassCanLeave)
        {
            _ = ShowPanelAsync(target, forceInstant, bypassCanLeave);
        }

        public async Task ShowPanelAsync(
            PanelType target,
            bool forceInstant = false,
            bool bypassCanLeave = false,
            string reason = null,
            object payload = null)
        {
            var normalizedTarget = Normalize(target);
            if (_currentPanel == normalizedTarget && !forceInstant)
                return;

            int version = ++_navigationVersion;
            _isNavigating = true;

            var from = _currentPanel;
            var context = new NavigationContext(from, normalizedTarget, forceInstant, bypassCanLeave, reason, payload);
            var currentView = ResolveView(from);

            if (normalizedTarget == PanelType.Settings && from != PanelType.Settings && from != PanelType.None)
                _settingsReturnPanel = from;

            if (!bypassCanLeave && currentView != null && !currentView.CanLeave(context))
            {
                if (version == _navigationVersion)
                    _isNavigating = false;
                return;
            }

            try
            {
                if (currentView != null)
                {
                    // Disable interaction immediately on the outgoing panel
                    var fromRoute = GetRoute(from);
                    if (fromRoute?.rootObject != null)
                        SetRouteCanvasGroupInteractive(fromRoute.rootObject, false);

                    await currentView.OnHideAsync(context);
                }

                if (version != _navigationVersion)
                    return;

                _currentPanel = normalizedTarget;
                Debug.Log($"[FLOW][UINavigator] {from} -> {_currentPanel} reason='{context.Reason}'  t={DateTime.UtcNow:HH:mm:ss.fff}");

                // Activate the target route before OnShowAsync so inactive UI can be populated,
                // but defer visible shell animation until the view finishes its data lifecycle.
                ApplyRouteRootState(normalizedTarget);

                var nextView = ResolveView(normalizedTarget);
                if (nextView != null)
                {
                    // Ensure the new panel is not interactable yet
                    var nextRoute = GetRoute(normalizedTarget);
                    if (nextRoute?.rootObject != null)
                        SetRouteCanvasGroupInteractive(nextRoute.rootObject, false);

                    await nextView.OnShowAsync(context);
                }

                if (version != _navigationVersion)
                    return;

                ApplyActiveRouteVisualState(normalizedTarget);
                ApplyShellVisualState(normalizedTarget);

                // Final re-enable of interaction after everything is shown
                var finalRoute = GetRoute(normalizedTarget);
                if (finalRoute?.rootObject != null)
                    SetRouteCanvasGroupInteractive(finalRoute.rootObject, true);

                OnPanelChanged?.Invoke(normalizedTarget);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UINavigator] Navigation {from} -> {normalizedTarget} failed: {ex}");
            }
            finally
            {
                if (version == _navigationVersion)
                    _isNavigating = false;
            }
        }

        private PanelType GetSettingsReturnPanel()
        {
            return _settingsReturnPanel == PanelType.None || _settingsReturnPanel == PanelType.Settings
                ? PanelType.Home
                : _settingsReturnPanel;
        }

        public void NotifyPlayerDataLoaded()
        {
            Debug.Log($"[FLOW][UINavigator] OnPlayerDataLoaded  t={DateTime.UtcNow:HH:mm:ss.fff}");
            OnPlayerDataLoaded?.Invoke();
        }

        private void InvokeVisualRoute(PanelType panel)
        {
            ApplyRouteRootState(panel);
            ApplyActiveRouteVisualState(panel);
            ApplyShellVisualState(panel);
        }

        private void ApplyRouteRootState(PanelType panel)
        {
            var activePanel = Normalize(panel);
            if (routes == null)
                return;

            foreach (var route in routes)
            {
                if (route?.rootObject == null)
                    continue;

                var routePanel = Normalize(route.panel);
                if (routePanel == PanelType.None)
                    continue;

                bool shouldBeActive = routePanel == activePanel;

                // Shell roots are animated by ApplyShellVisualState. Keep them alive so
                // the intro/login fade states can run instead of being hard-disabled.
                if (IsShellRoot(route.rootObject))
                {
                    if (shouldBeActive && !route.rootObject.activeSelf)
                        route.rootObject.SetActive(true);
                    continue;
                }

                if (route.rootObject.activeSelf != shouldBeActive)
                    route.rootObject.SetActive(shouldBeActive);
            }
        }

        private bool IsShellRoot(GameObject root)
        {
            return root != null &&
                   (root == splashScreenAnimator?.gameObject ||
                    root == mainPanelsAnimator?.gameObject);
        }

        private void ApplyActiveRouteVisualState(PanelType panel)
        {
            var route = GetRoute(panel);
            if (route?.rootObject == null || IsShellRoot(route.rootObject))
                return;

            if (!route.rootObject.activeSelf)
                route.rootObject.SetActive(true);

            bool animated = PlayAnimator(route.rootObject.GetComponent<Animator>(), routePanelInState);
            SetRouteCanvasGroupInteractive(route.rootObject, true);

            if (!animated)
                SetRouteCanvasGroupVisible(route.rootObject, true);
        }

        private static void SetRouteCanvasGroupInteractive(GameObject root, bool interactive)
        {
            if (root == null || !root.TryGetComponent<CanvasGroup>(out var canvasGroup))
                return;

            canvasGroup.interactable = interactive;
            canvasGroup.blocksRaycasts = interactive;
        }

        private static void SetRouteCanvasGroupVisible(GameObject root, bool visible)
        {
            if (root == null || !root.TryGetComponent<CanvasGroup>(out var canvasGroup))
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void ApplyShellVisualState(PanelType panel)
        {
            switch (Normalize(panel))
            {
                case PanelType.Login:
                    PlayAnimator(mainPanelsAnimator, mainPanelsHiddenState);
                    PlayAnimator(splashScreenAnimator, loginAnimatorState);
                    break;
                case PanelType.Home:
                case PanelType.CustomLobby:
                case PanelType.Settings:
                    PlayAnimator(mainPanelsAnimator, mainPanelsStartState);
                    PlayAnimator(splashScreenAnimator, splashHiddenState);
                    break;
                default:
                    PlayAnimator(mainPanelsAnimator, mainPanelsHiddenState);
                    PlayAnimator(splashScreenAnimator, splashHiddenState);
                    break;
            }
        }

        private static bool PlayAnimator(Animator animator, string stateName)
        {
            return ShiftUIBridge.PlayAnimatorState(animator, stateName);
        }

        private INavigableView ResolveView(PanelType panel)
        {
            panel = Normalize(panel);
            if (panel == PanelType.None)
                return null;

            if (_viewCache.TryGetValue(panel, out var cached))
                return cached;

            INavigableView view = null;
            var route = GetRoute(panel);
            if (route?.view is INavigableView routeView)
                view = routeView;

            view ??= panel switch
            {
                PanelType.Login => FindFirstObjectByType<LoginView>(FindObjectsInactive.Include),
                PanelType.Home => FindFirstObjectByType<HomeView>(FindObjectsInactive.Include),
                PanelType.CustomLobby => FindFirstObjectByType<CustomLobbyView>(FindObjectsInactive.Include),
                PanelType.Settings => FindFirstObjectByType<NightHunt.UI.Settings.SettingsView>(FindObjectsInactive.Include),
                _ => null
            };

            if (view != null)
                _viewCache[panel] = view;

            return view;
        }

        private NavigationRoute GetRoute(PanelType panel)
        {
            panel = Normalize(panel);
            if (_routeLookup.Count == 0)
                RebuildRouteLookup();
            _routeLookup.TryGetValue(panel, out var route);
            return route;
        }

        private static PanelType Normalize(PanelType panel)
        {
            return panel == PanelType.Lobby ? PanelType.CustomLobby : panel;
        }
    }
}
