using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Domain.QuickSlot;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.UI.Core;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Inventory.UI.Buttons
{
    /// <summary>
    /// Individual quick slot button for HUD.
    /// Handles select state, double-click to use item, displays item icon and key binding.
    /// First click = select, second click (within time) = use.
    /// </summary>
    public class QuickSlotHUDButton : MonoBehaviour, IUISlotStateManager
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image selectedBorder;
        [SerializeField] private TextMeshProUGUI keyText;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.5f, 0.7f, 0.5f, 1f);
        
        [Header("Settings")]
        [SerializeField] private float selectTimeout = 2f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private int slotIndex;
        private QuickSlotManager quickSlotManager;
        private float lastClickTime;
        private float selectTime;
        private const float DoubleClickTime = 0.3f;
        private UISlotState currentState = UISlotState.Empty;
        private ItemInstance currentItem;
        
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
            
            // Subscribe to events
            QuickSlotEvents.OnQuickSlotChanged += OnQuickSlotChanged;
            QuickSlotEvents.OnQuickSlotSelected += OnQuickSlotSelected;
            QuickSlotEvents.OnQuickSlotUnselected += OnQuickSlotUnselected;
            
            // Initial refresh
            var item = quickSlotManager?.GetItem(slotIndex);
            RefreshUI(item);
            UpdateStateFromItem(item);
        }
        
        void OnDestroy()
        {
            QuickSlotEvents.OnQuickSlotChanged -= OnQuickSlotChanged;
            QuickSlotEvents.OnQuickSlotSelected -= OnQuickSlotSelected;
            QuickSlotEvents.OnQuickSlotUnselected -= OnQuickSlotUnselected;
            
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
        }
        
        void Update()
        {
            // Auto-unselect after timeout
            if (currentState == UISlotState.Selected && Time.time - selectTime > selectTimeout)
            {
                OnUnselect();
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void OnButtonClicked()
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            
            if (currentState == UISlotState.Empty)
            {
                // Empty slot - log and do nothing
                InventoryLogger.Log("QuickSlotHUDButton", $"Slot {slotIndex} is empty", enableDebugLogs);
                return;
            }
            
            if (currentState == UISlotState.Selected && timeSinceLastClick < DoubleClickTime)
            {
                // Double click on selected slot → use
                UseQuickSlot();
            }
            else
            {
                // First click or timeout → select
                OnSelect();
                lastClickTime = Time.time;
            }
        }
        
        private void UseQuickSlot()
        {
            // Check if inventory is open (block usage when inventory is open)
            if (InputManager.Instance?.InventoryHandler?.IsInventoryOpen() == true)
            {
                InventoryLogger.Log("QuickSlotHUDButton", "Blocked - inventory is open", enableDebugLogs);
                return;
            }
            
            if (quickSlotManager == null)
            {
                InventoryLogger.LogError("QuickSlotHUDButton", "QuickSlotManager not assigned!");
                return;
            }
            
            var item = quickSlotManager.GetItem(slotIndex);
            if (item == null)
            {
                InventoryLogger.Log("QuickSlotHUDButton", $"Slot {slotIndex} is empty", enableDebugLogs);
                return;
            }
            
            // Log usage
            InventoryLogger.Log("QuickSlotHUDButton", $"Sử dụng item: {item.Definition.ItemId}", enableDebugLogs);
            
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
                    InventoryLogger.LogWarning("QuickSlotHUDButton", $"Item type {item.Definition.ItemType} not supported for quick slots", enableDebugLogs);
                    return;
            }
            
            // Fire analytics event
            QuickSlotEvents.InvokeQuickSlotDoubleClicked(item, slotIndex);
        }
        
        #endregion
        
        #region State Management
        
        public void SetState(UISlotState state)
        {
            currentState = state;
            UpdateVisualState();
        }
        
        public UISlotState GetCurrentState() => currentState;
        
        public void OnPointerEnter()
        {
            if (currentState != UISlotState.Empty && currentState != UISlotState.Selected)
            {
                SetState(UISlotState.Hover);
            }
        }
        
        public void OnPointerExit()
        {
            if (currentState == UISlotState.Hover)
            {
                SetState(currentItem != null ? UISlotState.Occupied : UISlotState.Empty);
            }
        }
        
        public void OnSelect()
        {
            if (currentItem != null)
            {
                SetState(UISlotState.Selected);
                selectTime = Time.time;
                QuickSlotEvents.InvokeQuickSlotSelected(slotIndex);
            }
        }
        
        public void OnUnselect()
        {
            if (currentState == UISlotState.Selected)
            {
                SetState(currentItem != null ? UISlotState.Occupied : UISlotState.Empty);
                QuickSlotEvents.InvokeQuickSlotUnselected(slotIndex);
            }
        }
        
        private void UpdateStateFromItem(ItemInstance item)
        {
            currentItem = item;
            if (item == null)
            {
                SetState(UISlotState.Empty);
            }
            else
            {
                SetState(UISlotState.Occupied);
            }
        }
        
        private void UpdateVisualState()
        {
            if (backgroundImage != null)
            {
                switch (currentState)
                {
                    case UISlotState.Empty:
                        backgroundImage.color = emptyColor;
                        break;
                        
                    case UISlotState.Occupied:
                        backgroundImage.color = occupiedColor;
                        break;
                        
                    case UISlotState.Hover:
                        backgroundImage.color = hoverColor;
                        break;
                        
                    case UISlotState.Selected:
                        backgroundImage.color = selectedColor;
                        break;
                        
                    case UISlotState.Unselected:
                        backgroundImage.color = occupiedColor;
                        break;
                }
            }
            
            if (selectedBorder != null)
            {
                selectedBorder.enabled = (currentState == UISlotState.Selected);
            }
        }
        
        private void OnQuickSlotSelected(int index)
        {
            if (index == slotIndex)
            {
                SetState(UISlotState.Selected);
                selectTime = Time.time;
            }
            else if (currentState == UISlotState.Selected)
            {
                // Another slot was selected, unselect this one
                OnUnselect();
            }
        }
        
        private void OnQuickSlotUnselected(int index)
        {
            if (index == slotIndex && currentState == UISlotState.Selected)
            {
                OnUnselect();
            }
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
            
            UpdateStateFromItem(item);
        }
        
        private void OnQuickSlotChanged(int index, ItemInstance item)
        {
            if (index == slotIndex)
            {
                RefreshUI(item);
            }
        }
        
        #endregion
        
        #region Public API
        
        public int GetSlotIndex() => slotIndex;
        
        public void SelectSlot()
        {
            OnSelect();
        }
        
        public void UseSlot()
        {
            UseQuickSlot();
        }
        
        #endregion
    }
}
