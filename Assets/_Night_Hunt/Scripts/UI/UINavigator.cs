using NightHunt.Core;
using UnityEngine;
using UnityEngine.Events;

namespace NightHunt.UI
{
    /// <summary>
    /// Panel types được dùng trong toàn bộ codebase để identify screen hiện tại.
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
    /// THIẾT KẾ:
    ///   UINavigator KHÔNG tự manage panel nào cả.
    ///   Nó chỉ fire UnityEvent tương ứng → bạn wire trong Inspector với
    ///   bất kỳ hành động nào: Play animator, SetActive, gọi method, v.v.
    ///
    /// SETUP TRONG INSPECTOR (ví dụ với Shift UI):
    ///   OnGoLogin  → SplashScreenAnimator.Play("Login")
    ///                hoặc LoginPanel.SetActive(true)
    ///   OnGoHome   → MainPanelManager.OpenFirstTab()
    ///                + SplashScreen.SetActive(false)
    ///                + MainPanels.SetActive(true)
    ///                + MainPanelsAnimator.Play("Start")
    ///   OnGoLobby  → tương tự OnGoHome nhưng navigate đến Lobby tab
    ///   OnGoNone   → hide tất cả (optional)
    ///
    /// TRONG CODE:
    ///   UINavigator.Instance.GoLogin();   // fired từ LoadingManager
    ///   UINavigator.Instance.GoHome();    // fired sau login success
    ///   UINavigator.Instance.GoLobby();   // fired khi join room
    ///
    /// TRUY VẤN TRẠNG THÁI:
    ///   UINavigator.Instance.CurrentPanel  → PanelType hiện tại
    ///   UINavigator.Instance.OnPanelChanged += handler;
    /// </summary>
    public class UINavigator : Singleton<UINavigator>
    {
        // ─────────────────────────────────────────────
        // Events — wire trong Inspector
        // ─────────────────────────────────────────────

        [Header("Navigation Events — Wire in Inspector")]

        [Tooltip("Fired khi navigate tới Login screen.\n" +
                 "Ví dụ: SplashScreenAnimator.Play(\"Login\") hoặc LoginPanel.SetActive(true)")]
        public UnityEvent OnGoLogin = new();

        [Tooltip("Fired khi navigate tới Home screen.\n" +
                 "Ví dụ: MainPanels.SetActive(true) + MainPanelsAnimator.Play(\"Start\") + MainPanelManager.OpenFirstTab()")]
        public UnityEvent OnGoHome = new();

        [Tooltip("Fired khi navigate tới Lobby screen.\n" +
                 "Ví dụ: MainPanels.SetActive(true) + MainPanelManager.OpenPanel(\"Lobby\")")]
        public UnityEvent OnGoLobby = new();

        [Tooltip("Fired khi reset / hide tất cả (optional — dùng khi logout hoặc về trạng thái ban đầu).")]
        public UnityEvent OnGoNone = new();

        // ─────────────────────────────────────────────
        // State (read-only từ bên ngoài)
        // ─────────────────────────────────────────────

        private PanelType _currentPanel = PanelType.None;

        /// <summary>Panel hiện tại đang display.</summary>
        public PanelType CurrentPanel => _currentPanel;

        /// <summary>Fired mỗi khi panel thay đổi. Param = panel mới.</summary>
        public event System.Action<PanelType> OnPanelChanged;

        /// <summary>
        /// Fired bởi HomeView sau khi tất cả async data fetches hoàn tất
        /// (profile, party, friends). Subscribe để biết khi nào safe để render
        /// server-driven content (PLAY button, invite UI, ...).
        /// </summary>
        public event System.Action OnPlayerDataLoaded;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            // Không cần setup gì — toàn bộ UI được wire bởi Inspector
        }

        // ─────────────────────────────────────────────
        // Public navigation API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Navigate tới Login screen.
        /// Fire OnGoLogin UnityEvent — bạn wire hành động trong Inspector.
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
        /// Navigate tới Home screen.
        /// Fire OnGoHome UnityEvent — bạn wire hành động trong Inspector.
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
        /// Navigate tới Lobby screen.
        /// Fire OnGoLobby UnityEvent — bạn wire hành động trong Inspector.
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
        /// Force navigate dù đang ở cùng panel (bypass guard).
        /// Uses when needed refresh / re-trigger event trên cùng panel.
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
        /// Navigate bằng PanelType enum.
        /// Uses từ LoadingManager: ShowPanel(_targetPanel)
        /// </summary>
        public void ShowPanel(PanelType target, bool forceInstant = false)
        {
            // forceInstant không dùng trong event-driven design — giữ để tương thích API cũ
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
