using UnityEngine;
using UnityEngine.UI;
using NightHunt.Core;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Persistent UI Canvas - DontDestroyOnLoad canvas chứa Loading, Reconnect, Ping
    /// Tự động tạo nếu chưa tồn tại
    /// Sử dụng SingletonPersistent generic pattern để tránh cross-class singleton collision
    /// </summary>
    public class PersistentUICanvas : SingletonPersistent<PersistentUICanvas>
    {
        [Header("Canvas Settings")] [SerializeField]
        private Canvas canvas;

        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;

        [Header("UI Components")]        [SerializeField] private LoadingManager     loadingManager;
        [SerializeField] private BootIntroView bootIntroView;
        [SerializeField] private MatchLoadingOverlay matchLoadingOverlay;
        [SerializeField] private MatchFoundOverlay   matchFoundOverlay;
        [SerializeField] private MatchFlowCoordinator matchFlowCoordinator;
        [SerializeField] private SessionTerminationListener sessionTerminationListener;
        [SerializeField] private PingDisplay         pingDisplay;
        [SerializeField] private ToastService        toastService;

        // Public accessors (use Instance instead of needing type cast)
        public Canvas              Canvas               => canvas;
        public LoadingManager      LoadingManager       => loadingManager;
        public BootIntroView       BootIntroView        => bootIntroView;
        public MatchLoadingOverlay MatchLoadingOverlay  => matchLoadingOverlay;
        public MatchFoundOverlay   MatchFoundOverlay    => matchFoundOverlay;
        public MatchFlowCoordinator MatchFlowCoordinator => matchFlowCoordinator;
        public SessionTerminationListener SessionTerminationListener => sessionTerminationListener;
        public PingDisplay         PingDisplay          => pingDisplay;
        public ToastService        ToastService         => toastService;

        protected override void OnSingletonAwake()
        {
            // Đảm bảo Canvas setup đúng
            SetupCanvas();

            // Initialize components
            InitializeComponents();
        }

        private void SetupCanvas()
        {
            // Tự động tạo Canvas nếu not yet available
            if (canvas == null)
            {
                canvas = ComponentResolver.Find<Canvas>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] Canvas not found")
                    .Resolve();
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                }
            }

            // Setup Canvas
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // High sorting order để luôn display trên cùng

            // Tự động tạo CanvasScaler nếu not yet available
            if (canvasScaler == null)
            {
                canvasScaler = ComponentResolver.Find<CanvasScaler>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] CanvasScaler not found")
                    .Resolve();
                if (canvasScaler == null)
                {
                    canvasScaler = gameObject.AddComponent<CanvasScaler>();
                }
            }

            // Setup CanvasScaler
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;

            // Tự động tạo GraphicRaycaster nếu not yet available
            if (graphicRaycaster == null)
            {
                graphicRaycaster = ComponentResolver.Find<GraphicRaycaster>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] GraphicRaycaster not found")
                    .Resolve();
                if (graphicRaycaster == null)
                {
                    graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
                }
            }
        }

        private void InitializeComponents()
        {
            // Keep loading visible on first boot to cover service init + auto-login
            if (loadingManager != null)
                loadingManager.gameObject.SetActive(true);

            EnsureBootIntroView();
            EnsurePersistentFlowComponents();

            // MatchLoadingOverlay ẩn mặc định (tự ẩn trong Awake của nó)
            // Không cần làm gì thêm ở đây

            // PingDisplay tự start trong Start()
        }

        private void EnsurePersistentFlowComponents()
        {
            if (matchFlowCoordinator == null)
            {
                matchFlowCoordinator = GetComponentInChildren<MatchFlowCoordinator>(true);
                if (matchFlowCoordinator == null)
                    matchFlowCoordinator = FindFirstObjectByType<MatchFlowCoordinator>(FindObjectsInactive.Include);
                if (matchFlowCoordinator == null)
                    matchFlowCoordinator = gameObject.AddComponent<MatchFlowCoordinator>();
            }

            if (sessionTerminationListener == null)
            {
                sessionTerminationListener = GetComponentInChildren<SessionTerminationListener>(true);
                if (sessionTerminationListener == null)
                    sessionTerminationListener = FindFirstObjectByType<SessionTerminationListener>(FindObjectsInactive.Include);
                if (sessionTerminationListener == null)
                    sessionTerminationListener = gameObject.AddComponent<SessionTerminationListener>();
            }
        }

        public BootIntroView EnsureBootIntroView()
        {
            if (bootIntroView == null)
                bootIntroView = GetComponentInChildren<BootIntroView>(true);

            if (bootIntroView == null)
                bootIntroView = BootIntroView.CreateRuntime(canvas != null ? canvas.transform : transform);

            if (bootIntroView != null)
                bootIntroView.EnsureRuntimeWiring();

            return bootIntroView;
        }

        /// <summary>
        /// Create PersistentUICanvas nếu not yet available
        /// </summary>
        public static PersistentUICanvas GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // Create mới
            GameObject canvasGO = new GameObject("PersistentUICanvas");
            PersistentUICanvas persistentCanvas = canvasGO.AddComponent<PersistentUICanvas>();

            // Create các UI components
            persistentCanvas.CreateUIComponents();

            return persistentCanvas;
        }

        /// <summary>
        /// Create các UI components nếu not yet available
        /// </summary>
        private void CreateUIComponents()
        {
            if (loadingManager == null)
            {
                GameObject loadingGO = new GameObject("LoadingManager");
                loadingGO.transform.SetParent(transform, false);
                loadingManager = loadingGO.AddComponent<LoadingManager>();
            }

            EnsureBootIntroView();

            if (matchLoadingOverlay == null)
            {
                GameObject go = new GameObject("MatchLoadingOverlay");
                go.transform.SetParent(transform, false);
                matchLoadingOverlay = go.AddComponent<MatchLoadingOverlay>();
            }

            if (pingDisplay == null)
            {
                GameObject pingGO = new GameObject("PingDisplay");
                pingGO.transform.SetParent(transform, false);
                pingDisplay = pingGO.AddComponent<PingDisplay>();
            }

            EnsurePersistentFlowComponents();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Only auto-assign in edit mode — at runtime components are created dynamically in CreateUIComponents()
            if (Application.isPlaying) return;

            // Auto-assign references nếu not yet available
            if (canvas == null)
            {
                canvas = ComponentResolver.Find<Canvas>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] Canvas not found")
                    .Resolve();
            }

            if (canvasScaler == null)
            {
                canvasScaler = ComponentResolver.Find<CanvasScaler>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] CanvasScaler not found")
                    .Resolve();
            }

            if (graphicRaycaster == null)
            {
                graphicRaycaster = ComponentResolver.Find<GraphicRaycaster>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] GraphicRaycaster not found")
                    .Resolve();
            }

            if (loadingManager == null)
            {
                loadingManager = ComponentResolver.Find<LoadingManager>(this)
                    .OnSelf()
                    .InChildren()
                    .InParent()
                    .OrLogWarning("[Auto] LoadingManager not found")
                    .Resolve();
            }

            if (bootIntroView == null)
                bootIntroView = GetComponentInChildren<BootIntroView>(true);

            if (pingDisplay == null)
            {
                pingDisplay = ComponentResolver.Find<PingDisplay>(this)
                    .OnSelf()
                    .InChildren()
                    .InParent()
                    .OrLogWarning("[Auto] PingDisplay not found")
                    .Resolve();
            }

            if (matchFlowCoordinator == null)
                matchFlowCoordinator = GetComponentInChildren<MatchFlowCoordinator>(true);

            if (sessionTerminationListener == null)
                sessionTerminationListener = GetComponentInChildren<SessionTerminationListener>(true);
        }
#endif
    }
}
