using UnityEngine;
using UnityEngine.UI;
using NightHunt.Core;

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

        // Public accessors
        public new static PersistentUICanvas Instance => (PersistentUICanvas)PersistentObject.Instance;
        public Canvas Canvas => canvas;
        public LoadingManager LoadingManager => loadingManager;
        public ReconnectPopup ReconnectPopup => reconnectPopup;
        public PingDisplay PingDisplay => pingDisplay;
        public NoticePopup NoticePopup => noticePopup;

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
                canvas = GetComponent<Canvas>();
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
                canvasScaler = GetComponent<CanvasScaler>();
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
                graphicRaycaster = GetComponent<GraphicRaycaster>();
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
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references nếu chưa có
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (canvasScaler == null)
            {
                canvasScaler = GetComponent<CanvasScaler>();
            }

            if (graphicRaycaster == null)
            {
                graphicRaycaster = GetComponent<GraphicRaycaster>();
            }

            if (loadingManager == null)
            {
                loadingManager = GetComponentInChildren<LoadingManager>();
            }

            if (reconnectPopup == null)
            {
                reconnectPopup = GetComponentInChildren<ReconnectPopup>();
            }

            if (pingDisplay == null)
            {
                pingDisplay = GetComponentInChildren<PingDisplay>();
            }

            if (noticePopup == null)
            {
                noticePopup = GetComponentInChildren<NoticePopup>();
            }
        }
#endif
    }
}
