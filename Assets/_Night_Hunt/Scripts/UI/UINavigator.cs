using NightHunt.Core;
using UnityEngine;
using UnityEngine.Events;

namespace NightHunt.UI
{
    /// <summary>
    /// Panel types used across the codebase to identify the current screen.
    /// </summary>
    public enum PanelType
    {
        None,
        Login,
        Home,
        Lobby
    }

    /// <summary>
    /// UINavigator — Event-driven navigation hub.
    ///
    /// DESIGN:
    ///   UINavigator does NOT manage any panels directly.
    ///   It only fires the corresponding UnityEvent → wire actions in the Inspector:
    ///   any action: Play animator, SetActive, call method, etc.
    ///
    /// INSPECTOR SETUP (example with Shift UI):
    ///   OnGoLogin  → SplashScreenAnimator.Play("Login")
    ///                or LoginPanel.SetActive(true)
    ///   OnGoHome   → MainPanelManager.OpenFirstTab()
    ///                + SplashScreen.SetActive(false)
    ///                + MainPanels.SetActive(true)
    ///                + MainPanelsAnimator.Play("Start")
    ///   OnGoLobby  → same as OnGoHome but navigates to the Lobby tab
    ///   OnGoNone   → hide all (optional)
    ///
    /// IN CODE:
    ///   UINavigator.Instance.GoLogin();   // fired from LoadingManager
    ///   UINavigator.Instance.GoHome();    // fired after login success
    ///   UINavigator.Instance.GoLobby();   // fired when joining a room
    ///
    /// QUERY STATE:
    ///   UINavigator.Instance.CurrentPanel  → current PanelType
    ///   UINavigator.Instance.OnPanelChanged += handler;
    /// </summary>
    public class UINavigator : Singleton<UINavigator>
    {
        // ─────────────────────────────────────────────
        // Events — wire in Inspector
        // ─────────────────────────────────────────────

        [Header("Navigation Events — Wire in Inspector")]

        [Tooltip("Fired when navigating to the Login screen.\n" +
                 "Example: SplashScreenAnimator.Play(\"Login\") or LoginPanel.SetActive(true)")]
        public UnityEvent OnGoLogin = new();

        [Tooltip("Fired when navigating to the Home screen.\n" +
                 "Example: MainPanels.SetActive(true) + MainPanelsAnimator.Play(\"Start\") + MainPanelManager.OpenFirstTab()")]
        public UnityEvent OnGoHome = new();

        [Tooltip("Fired when navigating to the Lobby screen.\n" +
                 "Example: MainPanels.SetActive(true) + MainPanelManager.OpenPanel(\"Lobby\")")]
        public UnityEvent OnGoLobby = new();

        [Tooltip("Fired when resetting / hiding all panels (optional — use on logout or returning to initial state).")]
        public UnityEvent OnGoNone = new();

        // ─────────────────────────────────────────────
        // State (read-only from outside)
        // ─────────────────────────────────────────────

        private PanelType _currentPanel = PanelType.None;

        /// <summary>The panel currently displayed.</summary>
        public PanelType CurrentPanel => _currentPanel;

        /// <summary>Fired every time the panel changes. Param = new panel.</summary>
        public event System.Action<PanelType> OnPanelChanged;

        /// <summary>
        /// Fired by HomeView after all async data fetches complete
        /// (profile, party, friends). Subscribe to know when it is safe to render
        /// server-driven content (PLAY button, invite UI, etc.).
        /// </summary>
        public event System.Action OnPlayerDataLoaded;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            // No setup required — all UI is wired via Inspector.
        }

        // ─────────────────────────────────────────────
        // Public navigation API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Navigate to the Login screen.
        /// Fires the OnGoLogin UnityEvent — wire actions in the Inspector.
        /// </summary>
        public void GoLogin()
        {
            if (_currentPanel == PanelType.Login) return;
            _currentPanel = PanelType.Login;
            Debug.Log($"[FLOW][UINavigator] ──▶ LOGIN  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            OnGoLogin?.Invoke();
            OnPanelChanged?.Invoke(PanelType.Login);
        }

        /// <summary>
        /// Navigate to the Home screen.
        /// Fires the OnGoHome UnityEvent — wire actions in the Inspector.
        /// </summary>
        public void GoHome()
        {
            if (_currentPanel == PanelType.Home) return;
            _currentPanel = PanelType.Home;
            Debug.Log($"[FLOW][UINavigator] ──▶ HOME  (data loading...)  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            OnGoHome?.Invoke();
            OnPanelChanged?.Invoke(PanelType.Home);
        }

        /// <summary>
        /// Navigate to the Lobby screen.
        /// Fires the OnGoLobby UnityEvent — wire actions in the Inspector.
        /// </summary>
        public void GoLobby()
        {
            if (_currentPanel == PanelType.Lobby) return;
            _currentPanel = PanelType.Lobby;
            Debug.Log($"[FLOW][UINavigator] ──▶ LOBBY  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            OnGoLobby?.Invoke();
            OnPanelChanged?.Invoke(PanelType.Lobby);
        }

        /// <summary>
        /// Called by HomeView after all async data fetches in OnShow() complete.
        /// Fires <see cref="OnPlayerDataLoaded"/> so subscribers (PLAY button, invite UI, etc.)
        /// know they can safely render server-driven content.
        /// </summary>
        public void NotifyPlayerDataLoaded()
        {
            Debug.Log($"[FLOW][UINavigator] ◆ OnPlayerDataLoaded — profile + party + friends ready  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            OnPlayerDataLoaded?.Invoke();
        }

        /// <summary>
        /// Force-navigate even when already on the same panel (bypasses the guard).
        /// Use when you need to refresh / re-trigger the event on the current panel.
        /// </summary>
        public void GoForce(PanelType target)
        {
            _currentPanel = PanelType.None; // reset guard
            switch (target)
            {
                case PanelType.Login: GoLogin();  break;
                case PanelType.Home:  GoHome();   break;
                case PanelType.Lobby: GoLobby();  break;
                default:
                    _currentPanel = PanelType.None;
                    OnGoNone?.Invoke();
                    OnPanelChanged?.Invoke(PanelType.None);
                    break;
            }
        }

        /// <summary>
        /// Navigate by PanelType enum.
        /// Called from LoadingManager: ShowPanel(_targetPanel).
        /// </summary>
        public void ShowPanel(PanelType target, bool forceInstant = false)
        {
            // forceInstant is not used in event-driven design — kept for API compatibility.
            switch (target)
            {
                case PanelType.Login: GoLogin();  break;
                case PanelType.Home:  GoHome();   break;
                case PanelType.Lobby: GoLobby();  break;
                default:
                    _currentPanel = PanelType.None;
                    OnGoNone?.Invoke();
                    OnPanelChanged?.Invoke(PanelType.None);
                    break;
            }
        }
    }
}
