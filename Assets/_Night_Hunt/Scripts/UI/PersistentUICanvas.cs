using UnityEngine;
using UnityEngine.UI;
using NightHunt.Core;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Persistent UI Canvas - DontDestroyOnLoad canvas chứa Loading, Reconnect, Ping
    /// Tự động tạo nếu chưa tồn tại
    /// Sử dụng Singleton pattern để dễ access các UI components
    /// </summary>
    public class PersistentUICanvas : PersistentObject
    {
        [Header("Canvas Settings")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;

        [Header("UI Components")]
        [SerializeField] private LoadingManager loadingManager;
        [SerializeField] private ReconnectPopup reconnectPopup;
        [SerializeField] private PingDisplay pingDisplay;
        [SerializeField] private NoticePopup noticePopup;
        [SerializeField] private ToastService toastService;
        [SerializeField] private UINotificationService notificationService;

        // Public accessors
        public new static PersistentUICanvas Instance => (PersistentUICanvas)PersistentObject.Instance;
        public Canvas Canvas => canvas;
        public LoadingManager LoadingManager => loadingManager;
        public ReconnectPopup ReconnectPopup => reconnectPopup;
        public PingDisplay PingDisplay => pingDisplay;
        public NoticePopup NoticePopup => noticePopup;
        public ToastService ToastService => toastService;
        public UINotificationService NotificationService => notificationService;

        protected override void OnPersistentAwake()
        {
            base.OnPersistentAwake();
            
            // Đảm bảo Canvas setup đúng
            SetupCanvas();
            
            // Initialize components
            InitializeComponents();
        }

        private void SetupCanvas()
        {
            // Tự động tạo Canvas nếu chưa có
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
            canvas.sortingOrder = 9999; // High sorting order để luôn hiển thị trên cùng

            // Tự động tạo CanvasScaler nếu chưa có
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

            // Tự động tạo GraphicRaycaster nếu chưa có
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
            {
                loadingManager.gameObject.SetActive(true);
            }

            // Initialize ReconnectPopup
            if (reconnectPopup != null)
            {
                reconnectPopup.Hide();
            }

            // Initialize NoticePopup
            if (noticePopup != null)
            {
                noticePopup.Hide();
            }

            // Wire UINotificationService with all notification backends
            if (notificationService == null)
                notificationService = ComponentResolver.Find<UINotificationService>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] UINotificationService not found")
        .Resolve();
            if (notificationService == null)
            {
                var go = new GameObject("UINotificationService");
                go.transform.SetParent(transform, false);
                notificationService = go.AddComponent<UINotificationService>();
            }
            notificationService.Configure(toastService, noticePopup, loadingManager);

            // PingDisplay sẽ tự động start trong Start()
        }

        /// <summary>
        /// Tạo PersistentUICanvas nếu chưa có
        /// </summary>
        public static PersistentUICanvas GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // Tạo mới
            GameObject canvasGO = new GameObject("PersistentUICanvas");
            PersistentUICanvas persistentCanvas = canvasGO.AddComponent<PersistentUICanvas>();
            
            // Tạo các UI components
            persistentCanvas.CreateUIComponents();
            
            return persistentCanvas;
        }

        /// <summary>
        /// Tạo các UI components nếu chưa có
        /// </summary>
        private void CreateUIComponents()
        {
            // Tạo LoadingManager
            if (loadingManager == null)
            {
                GameObject loadingGO = new GameObject("LoadingManager");
                loadingGO.transform.SetParent(transform, false);
                loadingManager = loadingGO.AddComponent<LoadingManager>();
            }

            // Tạo ReconnectPopup
            if (reconnectPopup == null)
            {
                GameObject reconnectGO = new GameObject("ReconnectPopup");
                reconnectGO.transform.SetParent(transform, false);
                reconnectPopup = reconnectGO.AddComponent<ReconnectPopup>();
            }

            // Tạo PingDisplay
            if (pingDisplay == null)
            {
                GameObject pingGO = new GameObject("PingDisplay");
                pingGO.transform.SetParent(transform, false);
                pingDisplay = pingGO.AddComponent<PingDisplay>();
            }

            // Tạo NoticePopup
            if (noticePopup == null)
            {
                GameObject noticeGO = new GameObject("NoticePopup");
                noticeGO.transform.SetParent(transform, false);
                noticePopup = noticeGO.AddComponent<NoticePopup>();
            }

            // Tạo UINotificationService
            if (notificationService == null)
            {
                GameObject notifGO = new GameObject("UINotificationService");
                notifGO.transform.SetParent(transform, false);
                notificationService = notifGO.AddComponent<UINotificationService>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references nếu chưa có
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

            if (reconnectPopup == null)
            {
                reconnectPopup = ComponentResolver.Find<ReconnectPopup>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] ReconnectPopup not found")
        .Resolve();
            }

            if (pingDisplay == null)
            {
                pingDisplay = ComponentResolver.Find<PingDisplay>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] PingDisplay not found")
        .Resolve();
            }

            if (noticePopup == null)
            {
                noticePopup = ComponentResolver.Find<NoticePopup>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] NoticePopup not found")
        .Resolve();
            }

            if (notificationService == null)
            {
                notificationService = ComponentResolver.Find<UINotificationService>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] UINotificationService not found")
        .Resolve();
            }
        }
#endif
    }
}
