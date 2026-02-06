using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.UI.Visuals
{
    /// <summary>
    /// World-space progress bar UI for consumable usage.
    /// Listens to QuickSlotEvents for progress updates.
    /// Supports spectate mode (shows for spectated player).
    /// </summary>
    public class QuickSlotProgressBarUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject progressBarRoot;
        [SerializeField] private Image progressBarFill;
        
        [Header("Settings")]
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Transform targetTransform;
        private bool isShowing = false;
        
        #region Lifecycle
        
        void Awake()
        {
            if (progressBarRoot != null)
            {
                progressBarRoot.SetActive(false);
            }
        }
        
        void OnEnable()
        {
            QuickSlotEvents.OnConsumeStarted += OnConsumeStarted;
            QuickSlotEvents.OnConsumeProgress += OnConsumeProgress;
            QuickSlotEvents.OnConsumeCompleted += OnConsumeCompleted;
            QuickSlotEvents.OnConsumeCancelled += OnConsumeCancelled;
        }
        
        void OnDisable()
        {
            QuickSlotEvents.OnConsumeStarted -= OnConsumeStarted;
            QuickSlotEvents.OnConsumeProgress -= OnConsumeProgress;
            QuickSlotEvents.OnConsumeCompleted -= OnConsumeCompleted;
            QuickSlotEvents.OnConsumeCancelled -= OnConsumeCancelled;
        }
        
        void Update()
        {
            if (isShowing && targetTransform != null && progressBarRoot != null)
            {
                // Follow target transform
                Vector3 targetPosition = targetTransform.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
                
                // Face camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    transform.LookAt(mainCamera.transform);
                    transform.Rotate(0, 180, 0);
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnConsumeStarted()
        {
            isShowing = true;
            
            if (progressBarRoot != null)
            {
                progressBarRoot.SetActive(true);
            }
            
            // Set target to current player or spectated player
            targetTransform = transform.parent;
            if (targetTransform == null)
            {
                // Try to find player transform
                var player = FindObjectOfType<NightHunt.Networking.NetworkPlayer>();
                if (player != null)
                {
                    targetTransform = player.transform;
                }
            }
            
            InventoryLogger.Log("QuickSlotProgressBarUI", "Progress bar shown", enableDebugLogs);
        }
        
        private void OnConsumeProgress(float progress)
        {
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = progress;
            }
        }
        
        private void OnConsumeCompleted()
        {
            HideProgressBar();
        }
        
        private void OnConsumeCancelled(ItemInstance item, string reason)
        {
            HideProgressBar();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Sets the target transform to follow (for spectate mode).
        /// </summary>
        /// <param name="target">The transform to follow</param>
        public void SetTarget(Transform target)
        {
            targetTransform = target;
        }
        
        #endregion
        
        #region Private Methods
        
        private void HideProgressBar()
        {
            isShowing = false;
            
            if (progressBarRoot != null)
            {
                progressBarRoot.SetActive(false);
            }
            
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = 0f;
            }
            
            InventoryLogger.Log("QuickSlotProgressBarUI", "Progress bar hidden", enableDebugLogs);
        }
        
        #endregion
    }
}
