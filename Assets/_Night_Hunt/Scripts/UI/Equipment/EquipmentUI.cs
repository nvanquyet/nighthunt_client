using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using NightHunt.Gameplay.Equipment;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.UI.Equipment
{
    /// <summary>
    /// Equipment UI panel
    /// Shows player equipment slots and nested sockets
    /// Follows MVP pattern
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        [Header("Equipment Slots")]
        [SerializeField] private Transform equipmentSlotsParent;
        [SerializeField] private GameObject equipmentSlotPrefab;

        [Header("Selected Item Info")]
        [SerializeField] private GameObject selectedItemPanel;
        [SerializeField] private TextMeshProUGUI selectedItemNameText;
        [SerializeField] private TextMeshProUGUI selectedItemDescriptionText;
        [SerializeField] private Transform socketsParent;
        [SerializeField] private GameObject socketSlotPrefab;

        [Header("Compatible Items")]
        [SerializeField] private GameObject compatibleItemsPanel;
        [SerializeField] private Transform compatibleItemsList;
        [SerializeField] private GameObject compatibleItemPrefab;

        private EquipmentManager equipmentManager;
        private IInventoryProvider inventoryProvider;
        private EquipmentSlotUI[] equipmentSlotUIs;
        private string selectedSlotId;
        private string selectedSocketId;

        private void Awake()
        {
            if (selectedItemPanel != null)
            {
                selectedItemPanel.SetActive(false);
            }

            if (compatibleItemsPanel != null)
            {
                compatibleItemsPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Initialize with equipment manager
        /// </summary>
        public void Initialize(EquipmentManager manager, IInventoryProvider inventory)
        {
            equipmentManager = manager;
            inventoryProvider = inventory;

            // Create equipment slot UIs
            CreateEquipmentSlots();
        }

        /// <summary>
        /// Create equipment slot UIs
        /// </summary>
        private void CreateEquipmentSlots()
        {
            if (equipmentSlotsParent == null || equipmentSlotPrefab == null) return;

            // Common equipment slots
            string[] slotIds = { "PrimaryWeapon", "SecondaryWeapon", "Head", "Body", "Backpack" };
            equipmentSlotUIs = new EquipmentSlotUI[slotIds.Length];

            for (int i = 0; i < slotIds.Length; i++)
            {
                GameObject slotObj = Instantiate(equipmentSlotPrefab, equipmentSlotsParent);
                EquipmentSlotUI slotUI = slotObj.GetComponent<EquipmentSlotUI>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<EquipmentSlotUI>();
                }

                slotUI.Initialize(slotIds[i], this);
                equipmentSlotUIs[i] = slotUI;
            }
        }

        /// <summary>
        /// On equipment slot selected
        /// </summary>
        public void OnSlotSelected(string slotId)
        {
            selectedSlotId = slotId;
            selectedSocketId = null;

            // Show selected item info
            ShowSelectedItemInfo(slotId);
        }

        /// <summary>
        /// On socket selected
        /// </summary>
        public void OnSocketSelected(string slotId, string socketId)
        {
            selectedSlotId = slotId;
            selectedSocketId = socketId;

            // Show compatible items for this socket
            ShowCompatibleItems(slotId, socketId);
        }

        /// <summary>
        /// Show selected item info with sockets
        /// </summary>
        private void ShowSelectedItemInfo(string slotId)
        {
            if (selectedItemPanel == null) return;

            // TODO: Get item from equipment manager
            // For now, placeholder
            selectedItemPanel.SetActive(true);

            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = $"Slot: {slotId}";
            }

            // Show sockets if item has them
            // TODO: Get sockets from equipment manager
        }

        /// <summary>
        /// Show compatible items for socket
        /// </summary>
        private void ShowCompatibleItems(string slotId, string socketId)
        {
            if (compatibleItemsPanel == null || inventoryProvider == null) return;

            compatibleItemsPanel.SetActive(true);

            // Clear existing items
            foreach (Transform child in compatibleItemsList)
            {
                Destroy(child.gameObject);
            }

            // Get all items from inventory
            var inventoryItems = inventoryProvider.GetItems();
            List<InventorySlot> compatibleSlots = new List<InventorySlot>();

            // TODO: Filter by socket compatibility
            // For now, show all items
            foreach (var slot in inventoryItems)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    // Check if item is compatible with socket
                    // Would need to check SocketDefinition.AllowedCategories vs ItemConfig.Tags
                    compatibleSlots.Add(slot);
                }
            }

            // Create UI for compatible items
            foreach (var slot in compatibleSlots)
            {
                GameObject itemObj = Instantiate(compatibleItemPrefab, compatibleItemsList);
                CompatibleItemUI itemUI = itemObj.GetComponent<CompatibleItemUI>();
                if (itemUI == null)
                {
                    itemUI = itemObj.AddComponent<CompatibleItemUI>();
                }

                itemUI.Initialize(slot, this);
            }
        }

        /// <summary>
        /// On compatible item clicked (attach to socket)
        /// </summary>
        public void OnAttachItem(InventorySlot slot)
        {
            if (equipmentManager == null || selectedSlotId == null || selectedSocketId == null) return;

            // TODO: Get itemInstanceId from slot
            // For now, placeholder
            Debug.Log($"[EquipmentUI] Attach item {slot.Item.ItemId} to socket {selectedSocketId} on slot {selectedSlotId}");
        }
    }

    /// <summary>
    /// Equipment slot UI component
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Button slotButton;
        [SerializeField] private Transform socketsParent;
        [SerializeField] private GameObject socketButtonPrefab;

        private string slotId;
        private EquipmentUI equipmentUI;

        public void Initialize(string id, EquipmentUI ui)
        {
            slotId = id;
            equipmentUI = ui;

            if (slotNameText != null)
            {
                slotNameText.text = id;
            }

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => ui.OnSlotSelected(slotId));
            }
        }

        /// <summary>
        /// Update with equipped item
        /// </summary>
        public void UpdateItem(ItemInstance itemInstance)
        {
            if (itemInstance == null || itemInstance.Config == null)
            {
                // Clear slot
                if (itemIcon != null)
                {
                    itemIcon.sprite = null;
                }
                return;
            }

            // Update icon
            if (itemIcon != null)
            {
                // TODO: Load sprite from config.IconSpriteId
            }

            // Show sockets
            if (itemInstance.Config.Sockets != null && socketsParent != null)
            {
                foreach (var socket in itemInstance.Config.Sockets)
                {
                    GameObject socketObj = Instantiate(socketButtonPrefab, socketsParent);
                    Button socketButton = socketObj.GetComponent<Button>();
                    if (socketButton != null)
                    {
                        socketButton.onClick.AddListener(() => equipmentUI.OnSocketSelected(slotId, socket.SocketId));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Compatible item UI component
    /// </summary>
    public class CompatibleItemUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private Button itemButton;

        private InventorySlot slot;
        private EquipmentUI equipmentUI;

        public void Initialize(InventorySlot slotData, EquipmentUI ui)
        {
            slot = slotData;
            equipmentUI = ui;

            if (slot == null || slot.IsEmpty) return;

            var config = GameConfigLoader.Instance?.GetItemConfig(slot.Item.ItemId);
            if (config != null)
            {
                if (itemNameText != null)
                {
                    itemNameText.text = config.DisplayName;
                }

                // TODO: Load icon
            }

            if (itemButton != null)
            {
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(() => ui.OnAttachItem(slot));
            }
        }
    }
}

