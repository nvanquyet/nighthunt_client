using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Domain.QuickSlot;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Inventory.UI.Buttons
{
    /// <summary>
    /// Individual quick slot button for HUD.
    /// Handles double-click to use item, displays item icon and key binding.
    /// </summary>
    public class QuickSlotHUDButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI keyText;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private int slotIndex;
        private QuickSlotManager quickSlotManager;
        private float lastClickTime;
        private const float DoubleClickTime = 0.3f;
        
        #region Initialization
        
        public void Initialize(int index, QuickSlotManager manager, string displayKey)
        {
            slotIndex = index;
            quickSlotManager = manager;
            
            // Setup button
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }
            
            // Display key binding
            if (keyText != null)
            {
                keyText.text = displayKey;
            }
            
            // Subscribe to quick slot changes
            QuickSlotEvents.OnQuickSlotChanged += OnQuickSlotChanged;
            
            // Initial refresh
            RefreshUI(quickSlotManager?.GetItem(slotIndex));
        }
        
        void OnDestroy()
        {
            QuickSlotEvents.OnQuickSlotChanged -= OnQuickSlotChanged;
            
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void OnButtonClicked()
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            
            if (timeSinceLastClick < DoubleClickTime)
            {
                // Double click → use quick slot
                UseQuickSlot();
            }
            else
            {
                // Single click → just record time for double-click detection
                lastClickTime = Time.time;
            }
        }
        
        private void UseQuickSlot()
        {
            // Check if inventory is open (block usage when inventory is open)
            if (InputManager.Instance?.InventoryHandler?.IsInventoryOpen() == true)
            {
                if (enableDebugLogs)
                    Debug.Log("[QuickSlotHUDButton] Blocked - inventory is open");
                return;
            }
            
            if (quickSlotManager == null)
            {
                Debug.LogError("[QuickSlotHUDButton] QuickSlotManager not assigned!");
                return;
            }
            
            var item = quickSlotManager.GetItem(slotIndex);
            if (item == null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[QuickSlotHUDButton] Slot {slotIndex} is empty");
                return;
            }
            
            // Call same events as hotkey (unified logic)
            switch (item.Definition.ItemType)
            {
                case ItemType.Consumable:
                    QuickSlotEvents.InvokeRequestConsume(item);
                    break;
                    
                case ItemType.Throwable:
                    QuickSlotEvents.InvokeRequestEquipThrowable(item);
                    break;
                    
                default:
                    Debug.LogWarning($"[QuickSlotHUDButton] Item type {item.Definition.ItemType} not supported for quick slots");
                    return;
            }
            
            // Log usage (for analytics/debugging)
            QuickSlotEvents.InvokeQuickSlotDoubleClicked(item, slotIndex);
            
            if (enableDebugLogs)
                Debug.Log($"[QuickSlotHUDButton] Used quick slot {slotIndex}: {item.Definition.ItemId}");
        }
        
        #endregion
        
        #region UI Updates
        
        public void RefreshUI(ItemInstance item)
        {
            if (itemIcon != null)
            {
                if (item != null && item.Definition != null)
                {
                    itemIcon.sprite = item.Definition.Icon;
                    itemIcon.enabled = true;
                }
                else
                {
                    itemIcon.enabled = false;
                }
            }
        }
        
        private void OnQuickSlotChanged(int index, ItemInstance item)
        {
            if (index == slotIndex)
            {
                RefreshUI(item);
            }
        }
        
        #endregion
    }
}
