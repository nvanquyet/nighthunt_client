using UnityEngine;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Network;
using NightHunt.Inventory.Core.Events;

namespace NightHunt.Inventory.UI.Management
{
    /// <summary>
    /// Centralized manager for Inventory UI.
    /// Handles opening/closing inventory panel and coordinating with input system.
    /// Singleton pattern.
    /// </summary>
    public class UIInventoryManager : MonoBehaviour
    {
        public static UIInventoryManager Instance { get; private set; }
        
        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private CanvasGroup inventoryCanvasGroup;
        
        [Header("Animation")]
        [SerializeField] private bool useAnimations = true;
        [SerializeField] private float fadeDuration = 0.2f;
        
        [Header("Cursor")]
        [SerializeField] private bool showCursorWhenOpen = true;
        [SerializeField] private CursorLockMode cursorLockModeWhenOpen = CursorLockMode.None;
        [SerializeField] private CursorLockMode cursorLockModeWhenClosed = CursorLockMode.Locked;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private bool isInventoryOpen = false;
        private PlayerInventoryNetwork currentPlayerInventory;
        private Coroutine fadeCoroutine;
        
        #region Lifecycle
        
        private void Awake()
        {
            // Singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Ensure inventory panel is hidden at start
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }
            
            if (inventoryCanvasGroup != null)
            {
                inventoryCanvasGroup.alpha = 0f;
                inventoryCanvasGroup.interactable = false;
                inventoryCanvasGroup.blocksRaycasts = false;
            }
        }
        
        private void Start()
        {
            // Subscribe to input events
            SubscribeToInput();
            
            // Find local player's inventory
            // This should be called after network initialization
            Invoke(nameof(FindLocalPlayerInventory), 0.5f);
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            UnsubscribeFromInput();
        }
        
        #endregion
        
        #region Input Subscription
        
        private void SubscribeToInput()
        {
            if (InputManager.Instance == null)
            {
                LogWarning("InputManager.Instance is null!");
                return;
            }
            
            var uiHandler = InputManager.Instance.UIHandler;
            if (uiHandler == null)
            {
                LogWarning("UIInputHandler is null!");
                return;
            }
            
            uiHandler.OnInventoryToggled += ToggleInventory;
            uiHandler.OnQuickSlotPressed += HandleQuickSlotPressed;
            uiHandler.OnCancelPressed += HandleCancelPressed;
            
            Log("Subscribed to input events");
        }
        
        private void UnsubscribeFromInput()
        {
            if (InputManager.Instance == null)
                return;
            
            var uiHandler = InputManager.Instance.UIHandler;
            if (uiHandler != null)
            {
                uiHandler.OnInventoryToggled -= ToggleInventory;
                uiHandler.OnQuickSlotPressed -= HandleQuickSlotPressed;
                uiHandler.OnCancelPressed -= HandleCancelPressed;
            }
        }
        
        #endregion
        
        #region Player Inventory
        
        private void FindLocalPlayerInventory()
        {
            // Find local player's inventory
            var inventories = SpectateManager.Instance.GetInventorySystem();
            if(inventories == null)
            {
                LogWarning("No PlayerInventoryNetwork found in scene");
                return;
            }
            if (inventories.IsOwner)
            {
                currentPlayerInventory = inventories;
                Log("Found local player inventory");
                return;
            }
            LogWarning("Could not find local player inventory");
        }
        
        /// <summary>
        /// Manually set the current player inventory (useful for spectating).
        /// </summary>
        public void SetCurrentInventory(PlayerInventoryNetwork inventory)
        {
            currentPlayerInventory = inventory;
            Log($"Set current inventory to {(inventory != null ? inventory.name : "null")}");
            
            // Refresh UI if inventory is open
            if (isInventoryOpen && inventory != null)
            {
                RefreshInventoryUI();
            }
        }
        
        #endregion
        
        #region Inventory Toggle
        
        /// <summary>
        /// Toggle inventory panel on/off.
        /// </summary>
        public void ToggleInventory()
        {
            if (isInventoryOpen)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }
        
        /// <summary>
        /// Open inventory panel.
        /// </summary>
        public void OpenInventory()
        {
            if (isInventoryOpen)
            {
                LogWarning("Inventory is already open");
                return;
            }
            
            if (currentPlayerInventory == null)
            {
                LogWarning("Cannot open inventory - no player inventory assigned");
                return;
            }
            
            isInventoryOpen = true;
            
            // Show panel
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
            }
            
            // Fade in
            if (useAnimations && inventoryCanvasGroup != null)
            {
                if (fadeCoroutine != null)
                    StopCoroutine(fadeCoroutine);
                
                fadeCoroutine = StartCoroutine(FadeCanvasGroup(inventoryCanvasGroup, 0f, 1f, fadeDuration));
            }
            else if (inventoryCanvasGroup != null)
            {
                inventoryCanvasGroup.alpha = 1f;
                inventoryCanvasGroup.interactable = true;
                inventoryCanvasGroup.blocksRaycasts = true;
            }
            
            // Update cursor
            if (showCursorWhenOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = cursorLockModeWhenOpen;
            }
            
            // Notify input manager
            InputManager.Instance?.OnInventoryOpened();
            
            Log("Inventory opened");
        }
        
        /// <summary>
        /// Close inventory panel.
        /// </summary>
        public void CloseInventory()
        {
            if (!isInventoryOpen)
            {
                LogWarning("Inventory is already closed");
                return;
            }
            
            isInventoryOpen = false;
            
            // Fade out
            if (useAnimations && inventoryCanvasGroup != null)
            {
                if (fadeCoroutine != null)
                    StopCoroutine(fadeCoroutine);
                
                fadeCoroutine = StartCoroutine(FadeCanvasGroup(inventoryCanvasGroup, 1f, 0f, fadeDuration, () =>
                {
                    if (inventoryPanel != null)
                        inventoryPanel.SetActive(false);
                }));
            }
            else
            {
                if (inventoryCanvasGroup != null)
                {
                    inventoryCanvasGroup.alpha = 0f;
                    inventoryCanvasGroup.interactable = false;
                    inventoryCanvasGroup.blocksRaycasts = false;
                }
                
                if (inventoryPanel != null)
                    inventoryPanel.SetActive(false);
            }
            
            // Update cursor
            Cursor.visible = false;
            Cursor.lockState = cursorLockModeWhenClosed;
            
            // Notify input manager
            InputManager.Instance?.OnInventoryClosed();
            
            Log("Inventory closed");
        }
        
        /// <summary>
        /// Force close inventory (e.g., when player dies).
        /// </summary>
        public void ForceCloseInventory()
        {
            if (!isInventoryOpen)
                return;
            
            isInventoryOpen = false;
            
            if (inventoryPanel != null)
                inventoryPanel.SetActive(false);
            
            if (inventoryCanvasGroup != null)
            {
                inventoryCanvasGroup.alpha = 0f;
                inventoryCanvasGroup.interactable = false;
                inventoryCanvasGroup.blocksRaycasts = false;
            }
            
            Cursor.visible = false;
            Cursor.lockState = cursorLockModeWhenClosed;
            
            InputManager.Instance?.OnInventoryClosed();
            
            Log("Inventory force closed");
        }
        
        #endregion
        
        #region Input Handlers
        
        private void HandleQuickSlotPressed(int slotIndex)
        {
            if (currentPlayerInventory == null)
            {
                LogWarning("No player inventory - cannot use quickslot");
                return;
            }
            
            // Get item in quickslot
            var item = currentPlayerInventory.GetQuickSlotItem(slotIndex);
            
            if (item == null)
            {
                Log($"QuickSlot {slotIndex} is empty");
                return;
            }
            
            // Use item
            currentPlayerInventory.TryUseItem(item.InstanceId);
            
            Log($"Used item from QuickSlot {slotIndex}: {item.Definition.DisplayName}");
        }
        
        private void HandleCancelPressed()
        {
            if (isInventoryOpen)
            {
                // Close inventory
                CloseInventory();
            }
            else if (currentPlayerInventory != null && currentPlayerInventory.IsUsingItem())
            {
                // Cancel item usage
                currentPlayerInventory.CancelItemUsage();
                Log("Cancelled item usage");
            }
        }
        
        #endregion
        
        #region UI Refresh
        
        /// <summary>
        /// Refresh entire inventory UI (useful when switching spectated player).
        /// </summary>
        public void RefreshInventoryUI()
        {
            // TODO: Notify all UI components to refresh
            Log("Refreshing inventory UI");
        }
        
        #endregion
        
        #region Utilities
        
        private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, System.Action onComplete = null)
        {
            float elapsed = 0f;
            
            canvasGroup.alpha = startAlpha;
            canvasGroup.interactable = endAlpha > 0.5f;
            canvasGroup.blocksRaycasts = endAlpha > 0.5f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                yield return null;
            }
            
            canvasGroup.alpha = endAlpha;
            canvasGroup.interactable = endAlpha > 0.5f;
            canvasGroup.blocksRaycasts = endAlpha > 0.5f;
            
            onComplete?.Invoke();
        }
        
        #endregion
        
        #region Public API
        
        public bool IsInventoryOpen => isInventoryOpen;
        public PlayerInventoryNetwork CurrentInventory => currentPlayerInventory;
        
        #endregion
        
        #region Logging
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                UnityEngine.Debug.Log($"[UIInventoryManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[UIInventoryManager] {message}");
        }
        
        #endregion
    }
}