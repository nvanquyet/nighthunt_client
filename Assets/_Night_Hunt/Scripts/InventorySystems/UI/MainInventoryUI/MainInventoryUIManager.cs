using UnityEngine;
using NightHunt.Inventory.UI.Data;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Systems;
using NightHunt.Networking;
using System;
using NightHunt.Inventory.UI.Attachments;
using NightHunt.Inventory.UI.Panels;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Main controller for inventory UI system.
    /// Handles open/close toggle (Tab key), manages all panels visibility,
    /// subscribes to events, coordinates UI updates.
    /// </summary>
    public class MainInventoryUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryUIDataProvider dataProvider;
        
        [Header("UI Panels")]
        [SerializeField] private InventoryPanel inventoryPanel;
        [SerializeField] private EquipmentPanel equipmentPanel;
        [SerializeField] private WeaponPanel weaponPanel;
        [SerializeField] private QuickSlotPanel quickSlotPanel;
        [SerializeField] private LeftAttachmentPanel leftAttachmentPanel;
        [SerializeField] private RightAttachmentPanel rightAttachmentPanel;


        [SerializeField] private GameObject mainPanel;
        
        [Header("Settings")]
        [SerializeField] private bool startClosed = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private bool isOpen = false;
        private ItemInstance selectedInventoryItem;
        private ItemInstance hoveredEquippedItem;
        
        // Events
        public event Action<ItemInstance> OnInventoryItemSelected;
        public event Action<ItemInstance> OnEquippedItemHovered;
        public event Action OnInventoryItemDeselected;
        public event Action OnEquippedItemUnhovered;
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (dataProvider == null)
                dataProvider = GetComponent<InventoryUIDataProvider>();
            
            if (dataProvider == null)
            {
                LogError("InventoryUIDataProvider not assigned!");
            }
        }
        
        void Start()
        {
            // Set initial state
            if (startClosed)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
            
            // Subscribe to events
            SubscribeToEvents();
        }
        
        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        // === Event Subscription ===
        
        private void SubscribeToEvents()
        {
            // Subscribe to inventory events for UI updates
            InventoryEvents.OnItemAdded += OnInventoryItemAdded;
            InventoryEvents.OnItemRemoved += OnInventoryItemRemoved;
            InventoryEvents.OnItemMoved += OnInventoryItemMoved;
            InventoryEvents.OnItemsSwapped += OnInventoryItemsSwapped;
            
            // Subscribe to equipment events
            EquipmentEvents.OnItemEquipped += OnEquipmentItemEquipped;
            EquipmentEvents.OnItemUnequipped += OnEquipmentItemUnequipped;
            EquipmentEvents.OnEquipmentSwapped += OnEquipmentSwapped;
            
            // Subscribe to weapon events
            WeaponEvents.OnWeaponEquipped += OnWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped += OnWeaponUnequipped;
            WeaponEvents.OnActiveWeaponChanged += OnActiveWeaponChanged;
            
            // Subscribe to quick slot events
            QuickSlotEvents.OnQuickSlotAssigned += OnQuickSlotAssigned;
            QuickSlotEvents.OnQuickSlotCleared += OnQuickSlotCleared;
            
            // Subscribe to attachment events
            AttachmentEvents.OnAttachmentAttached += OnAttachmentAttached;
            AttachmentEvents.OnAttachmentDetached += OnAttachmentDetached;
            AttachmentEvents.OnAttachmentSwapped += OnAttachmentSwapped;
            
            // Subscribe to spectate manager for player changes
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            InventoryEvents.OnItemAdded -= OnInventoryItemAdded;
            InventoryEvents.OnItemRemoved -= OnInventoryItemRemoved;
            InventoryEvents.OnItemMoved -= OnInventoryItemMoved;
            InventoryEvents.OnItemsSwapped -= OnInventoryItemsSwapped;
            
            EquipmentEvents.OnItemEquipped -= OnEquipmentItemEquipped;
            EquipmentEvents.OnItemUnequipped -= OnEquipmentItemUnequipped;
            EquipmentEvents.OnEquipmentSwapped -= OnEquipmentSwapped;
            
            WeaponEvents.OnWeaponEquipped -= OnWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped -= OnWeaponUnequipped;
            WeaponEvents.OnActiveWeaponChanged -= OnActiveWeaponChanged;
            
            QuickSlotEvents.OnQuickSlotAssigned -= OnQuickSlotAssigned;
            QuickSlotEvents.OnQuickSlotCleared -= OnQuickSlotCleared;
            
            AttachmentEvents.OnAttachmentAttached -= OnAttachmentAttached;
            AttachmentEvents.OnAttachmentDetached -= OnAttachmentDetached;
            AttachmentEvents.OnAttachmentSwapped -= OnAttachmentSwapped;
            
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged -= OnCurrentPlayerChanged;
            }
        }
        
        // === Public API ===
        
        /// <summary>
        /// Open inventory UI.
        /// </summary>
        public void OpenInventory()
        {
            if (isOpen)
                return;
            isOpen = true;
            SetPanelsActive(true);
            
            // Refresh UI data
            RefreshAllPanels();
            mainPanel?.gameObject.SetActive(isOpen);

            Log("Inventory UI opened");
        }
        
        /// <summary>
        /// Close inventory UI.
        /// </summary>
        public void CloseInventory()
        {
            if (!isOpen)
                return;
            isOpen = false;
            SetPanelsActive(false);
            
            // Clear selection
            DeselectInventoryItem();
            UnhoverEquippedItem();
            
            mainPanel?.gameObject.SetActive(isOpen);
            Log("Inventory UI closed");
        }
        
        /// <summary>
        /// Select inventory item (for left attachment panel).
        /// </summary>
        public void SelectInventoryItem(ItemInstance item)
        {
            if (selectedInventoryItem == item)
                return;
            
            selectedInventoryItem = item;
            OnInventoryItemSelected?.Invoke(item);
            
            Log($"Selected inventory item: {item?.Definition?.DisplayName ?? "null"}");
        }
        
        /// <summary>
        /// Deselect inventory item.
        /// </summary>
        public void DeselectInventoryItem()
        {
            if (selectedInventoryItem == null)
                return;
            
            selectedInventoryItem = null;
            OnInventoryItemDeselected?.Invoke();
            
            Log("Deselected inventory item");
        }
        
        /// <summary>
        /// Hover equipped item (for right attachment panel).
        /// </summary>
        public void HoverEquippedItem(ItemInstance item)
        {
            if (hoveredEquippedItem == item)
                return;
            
            hoveredEquippedItem = item;
            OnEquippedItemHovered?.Invoke(item);
            
            Log($"Hovered equipped item: {item?.Definition?.DisplayName ?? "null"}");
        }
        
        /// <summary>
        /// Unhover equipped item.
        /// </summary>
        public void UnhoverEquippedItem()
        {
            if (hoveredEquippedItem == null)
                return;
            
            hoveredEquippedItem = null;
            OnEquippedItemUnhovered?.Invoke();
            
            Log("Unhovered equipped item");
        }
        
        /// <summary>
        /// Get currently selected inventory item.
        /// </summary>
        public ItemInstance GetSelectedInventoryItem() => selectedInventoryItem;
        
        /// <summary>
        /// Get currently hovered equipped item.
        /// </summary>
        public ItemInstance GetHoveredEquippedItem() => hoveredEquippedItem;
        
        /// <summary>
        /// Check if inventory UI is open.
        /// </summary>
        public bool IsOpen() => isOpen;
        
        // === Event Handlers ===
        
        private void OnInventoryItemAdded(ItemInstance item, int slotIndex)
        {
            if (!isOpen)
                return;
            
            RefreshInventoryPanel();
            Log($"Inventory item added: {item.Definition.DisplayName} at slot {slotIndex}");
        }
        
        private void OnInventoryItemRemoved(ItemInstance item, int slotIndex)
        {
            if (!isOpen)
                return;
            
            // If removed item was selected, deselect it
            if (selectedInventoryItem == item)
            {
                DeselectInventoryItem();
            }
            
            RefreshInventoryPanel();
            Log($"Inventory item removed from slot {slotIndex}");
        }
        
        private void OnInventoryItemMoved(ItemInstance item, int fromSlot, int toSlot)
        {
            if (!isOpen)
                return;
            
            RefreshInventoryPanel();
            Log($"Inventory item moved: {fromSlot} → {toSlot}");
        }
        
        private void OnInventoryItemsSwapped(ItemInstance itemA, int slotA, ItemInstance itemB, int slotB)
        {
            if (!isOpen)
                return;
            
            RefreshInventoryPanel();
            Log($"Inventory items swapped: {slotA} ↔ {slotB}");
        }
        
        private void OnEquipmentItemEquipped(ItemInstance item, EquipmentSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshEquipmentPanel();
            Log($"Equipment item equipped: {item.Definition.DisplayName} in {slotType}");
        }
        
        private void OnEquipmentItemUnequipped(ItemInstance item, EquipmentSlotType slotType)
        {
            if (!isOpen)
                return;
            
            // If unequipped item was hovered, unhover it
            if (hoveredEquippedItem == item)
            {
                UnhoverEquippedItem();
            }
            
            RefreshEquipmentPanel();
            Log($"Equipment item unequipped from {slotType}");
        }
        
        private void OnEquipmentSwapped(ItemInstance oldItem, ItemInstance newItem, EquipmentSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshEquipmentPanel();
            Log($"Equipment swapped in {slotType}");
        }
        
        private void OnWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshWeaponPanel();
            Log($"Weapon equipped: {weapon.Definition.DisplayName} in {slotType}");
        }
        
        private void OnWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshWeaponPanel();
            Log($"Weapon unequipped from {slotType}");
        }
        
        private void OnActiveWeaponChanged(ItemInstance previous, ItemInstance newWeapon, WeaponSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshWeaponPanel();
            Log($"Active weapon changed to {slotType}");
        }
        
        private void OnQuickSlotAssigned(ItemInstance item, int quickSlotIndex)
        {
            if (!isOpen)
                return;
            
            RefreshQuickSlotPanel();
            Log($"Quick slot assigned: {item.Definition.DisplayName} to slot {quickSlotIndex}");
        }
        
        private void OnQuickSlotCleared(int quickSlotIndex)
        {
            if (!isOpen)
                return;
            
            RefreshQuickSlotPanel();
            Log($"Quick slot cleared: {quickSlotIndex}");
        }
        
        private void OnAttachmentAttached(ItemInstance parentItem, ItemInstance attachment, AttachmentSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshAttachmentPanels();
            Log($"Attachment attached: {attachment.Definition.DisplayName} to {parentItem.Definition.DisplayName}");
        }
        
        private void OnAttachmentDetached(ItemInstance parentItem, ItemInstance attachment, AttachmentSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshAttachmentPanels();
            Log($"Attachment detached from {parentItem.Definition.DisplayName}");
        }
        
        private void OnAttachmentSwapped(ItemInstance parentItem, ItemInstance oldAttachment, ItemInstance newAttachment, AttachmentSlotType slotType)
        {
            if (!isOpen)
                return;
            
            RefreshAttachmentPanels();
            Log($"Attachment swapped on {parentItem.Definition.DisplayName}");
        }
        
        private void OnCurrentPlayerChanged(NetworkPlayer player)
        {
            // Refresh all panels when player changes (spectate mode)
            if (isOpen)
            {
                RefreshAllPanels();
            }
            
            Log($"Current player changed: {player?.DisplayName ?? "null"}");
        }
        
        // === Panel Management ===
        
        private void SetPanelsActive(bool active)
        {
            if (inventoryPanel != null)
                inventoryPanel.gameObject.SetActive(active);
            
            if (equipmentPanel != null)
                equipmentPanel.gameObject.SetActive(active);
            
            if (weaponPanel != null)
                weaponPanel.gameObject.SetActive(active);
            
            if (quickSlotPanel != null)
                quickSlotPanel.gameObject.SetActive(active);
            
            if (leftAttachmentPanel != null)
                leftAttachmentPanel.gameObject.SetActive(active);
            
            if (rightAttachmentPanel != null)
                rightAttachmentPanel.gameObject.SetActive(active);
        }
        
        private void RefreshAllPanels()
        {
            RefreshInventoryPanel();
            RefreshEquipmentPanel();
            RefreshWeaponPanel();
            RefreshQuickSlotPanel();
            RefreshAttachmentPanels();
        }
        
        private void RefreshInventoryPanel()
        {
            // Panel controllers will handle this via events
            // This is a placeholder for future implementation
        }
        
        private void RefreshEquipmentPanel()
        {
            // Panel controllers will handle this via events
        }
        
        private void RefreshWeaponPanel()
        {
            // Panel controllers will handle this via events
        }
        
        private void RefreshQuickSlotPanel()
        {
            // Panel controllers will handle this via events
        }
        
        private void RefreshAttachmentPanels()
        {
            // Attachment panel controllers will handle this
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[MainInventoryUIManager] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[MainInventoryUIManager] {message}");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure data provider is assigned
            if (dataProvider == null)
                dataProvider = GetComponent<InventoryUIDataProvider>();
            inventoryPanel = GetComponentInChildren<InventoryPanel>();
            equipmentPanel = GetComponentInChildren<EquipmentPanel>();
            weaponPanel = GetComponentInChildren<WeaponPanel>();
            quickSlotPanel = GetComponentInChildren<QuickSlotPanel>();
            leftAttachmentPanel = GetComponentInChildren<LeftAttachmentPanel>();
            rightAttachmentPanel = GetComponentInChildren<RightAttachmentPanel>();
        }
#endif
    }
}
