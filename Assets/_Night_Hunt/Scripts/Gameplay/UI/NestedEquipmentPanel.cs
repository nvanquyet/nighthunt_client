using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Nested Equipment Panel - Shows equipment slots of the currently selected item
    /// Only 1 level deep: item can have equipment slots (e.g., weapon has grip slot)
    /// Activated when an item with equipment slots is selected in inventory
    /// </summary>
    public class NestedEquipmentPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private GameObject slotPrefab; // Default prefab for nested equipment slots

        [Header("Settings")]
        [SerializeField] private string titleFormat = "Equipment Slots: {0}";

        private InventoryPanel inventoryPanel;
        private InventorySlot currentItemSlot; // Item that has these nested equipment slots
        private List<ItemCell> nestedSlotUIs = new List<ItemCell>();

        /// <summary>
        /// Initialize nested equipment panel
        /// </summary>
        public void Initialize(InventoryPanel panel)
        {
            inventoryPanel = panel;
            Hide();
        }

        /// <summary>
        /// Show nested equipment panel for selected item
        /// </summary>
        public void ShowForItem(InventorySlot itemSlot)
        {
            if (itemSlot == null || itemSlot.IsEmpty)
            {
                Hide();
                return;
            }

            currentItemSlot = itemSlot;
            var item = itemSlot.Item;
            if (item == null)
            {
                Hide();
                return;
            }

            // Get item config to check for equipment slots
            ItemConfigData itemConfig = GetItemConfig(item.ItemId);
            if (itemConfig == null)
            {
                Hide();
                return;
            }

            // Check if item has equipment slots (nested equipment)
            // This would come from ItemConfigData or ItemDataBase
            // For now, we'll check ExtraParamsJson or a future property
            var equipmentSlots = GetItemEquipmentSlots(itemConfig);
            if (equipmentSlots == null || equipmentSlots.Count == 0)
            {
                Hide();
                return;
            }

            // Show panel and create slots
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
            CreateNestedSlots(equipmentSlots, itemSlot);
        }

        /// <summary>
        /// Hide nested equipment panel
        /// </summary>
        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            ClearSlots();
            currentItemSlot = null;
        }

        /// <summary>
        /// Check if panel is showing
        /// </summary>
        public bool IsShowing()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        /// <summary>
        /// Get item config from registry or config loader
        /// </summary>
        private ItemConfigData GetItemConfig(string itemId)
        {
            // Try ItemDataRegistry first (ScriptableObject)
            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            if (registry != null)
            {
                var itemData = registry.GetById(itemId);
                if (itemData != null)
                {
                    // Convert ItemDataBase to ItemConfigData (adapter)
                    return ConvertToItemConfigData(itemData);
                }
            }

            // TODO: Implement ItemConfigData system using ScriptableObject if needed
            // For now, return null - ItemDataBase should be used directly instead
            return null;
        }

        /// <summary>
        /// Convert ItemDataBase to ItemConfigData (adapter)
        /// </summary>
        private ItemConfigData ConvertToItemConfigData(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData)
        {
            // Create ItemConfigData from ItemDataBase
            // This is a simplified conversion - you may need to map more fields
            return new ItemConfigData
            {
                ItemId = itemData.ItemId,
                DisplayName = itemData.DisplayName,
                // Category is ItemCategory enum, not string - skip for now
                Weight = itemData.Weight,
                MaxStack = itemData.MaxStack,
                // Map other fields as needed
            };
        }

        /// <summary>
        /// Get equipment slots for item (from config)
        /// </summary>
        private List<ItemEquipmentSlotData> GetItemEquipmentSlots(ItemConfigData itemConfig)
        {
            List<ItemEquipmentSlotData> slots = new List<ItemEquipmentSlotData>();

            // Get from ItemConfigData.equipmentSlots (new field)
            if (itemConfig.equipmentSlots != null && itemConfig.equipmentSlots.Count > 0)
            {
                foreach (var slotConfig in itemConfig.equipmentSlots)
                {
                    slots.Add(new ItemEquipmentSlotData
                    {
                        slotId = slotConfig.slotId,
                        displayName = slotConfig.displayName,
                        allowedItemCategory = slotConfig.allowedItemCategory,
                        slotPrefab = slotPrefab // Use default prefab or get from config
                    });
                }
            }

            return slots;
        }

        /// <summary>
        /// Create nested equipment slots
        /// </summary>
        private void CreateNestedSlots(List<ItemEquipmentSlotData> equipmentSlots, InventorySlot parentItemSlot)
        {
            ClearSlots();

            if (slotsContainer == null)
            {
                Debug.LogError("[NestedEquipmentPanel] slotsContainer is null!");
                return;
            }

            // Update title
            if (titleText != null)
            {
                var item = parentItemSlot.Item;
                string displayName = item?.ItemData?.DisplayName ?? item?.ItemId ?? "Item";
                titleText.text = string.Format(titleFormat, displayName);
            }

            // Create slot for each equipment slot
            foreach (var slotData in equipmentSlots)
            {
                if (slotData.slotPrefab == null)
                {
                    Debug.LogWarning($"[NestedEquipmentPanel] Slot prefab is null for slot: {slotData.slotId}");
                    continue;
                }

                GameObject slotObj = Instantiate(slotData.slotPrefab, slotsContainer);
                slotObj.SetActive(true);

                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Create empty slot for nested equipment
                InventorySlot nestedSlot = new InventorySlot();
                // TODO: Check if parent item has this slot filled
                // nestedSlot.SetItem(...);

                // Initialize as nested equipment (marked so it won't show in detail panel)
                slotUI.Initialize(nestedSlot, inventoryPanel, ItemCellLocation.Attachment, nestedSlotUIs.Count);
                slotUI.SetNestedEquipment(true);
                nestedSlotUIs.Add(slotUI);
            }
        }

        /// <summary>
        /// Clear all nested slots
        /// </summary>
        private void ClearSlots()
        {
            foreach (var slotUI in nestedSlotUIs)
            {
                if (slotUI != null)
                {
                    Destroy(slotUI.gameObject);
                }
            }
            nestedSlotUIs.Clear();
        }

        /// <summary>
        /// Data structure for item equipment slot
        /// </summary>
        [System.Serializable]
        public class ItemEquipmentSlotData
        {
            public string slotId; // "Grip", "Scope", "Magazine", etc.
            public string displayName;
            public string allowedItemCategory; // Category of items that can be attached
            public GameObject slotPrefab; // Prefab for the slot UI
        }
    }
}
