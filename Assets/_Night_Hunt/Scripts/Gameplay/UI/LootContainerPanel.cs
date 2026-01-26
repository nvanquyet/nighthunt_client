using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Loot container panel - displayed on the right in Loot Mode
    /// Shows items from container (chest, player corpse, etc.)
    /// </summary>
    public class LootContainerPanel : MonoBehaviour
    {
        [Header("Container Info")]
        [SerializeField] private TextMeshProUGUI containerTitleText;

        [Header("Loot Grid")]
        [SerializeField] private Transform lootGridParent;
        [SerializeField] private GameObject lootSlotPrefab;
        [SerializeField] private GridLayoutGroup gridLayout;

        private InventoryPanel inventoryPanel;
        private List<InventorySlotUI> lootSlots = new List<InventorySlotUI>();
        private object currentContainerData;
        private bool isInitialized = false;

        /// <summary>
        /// Initialize loot container panel
        /// </summary>
        public void Initialize(InventoryPanel panel)
        {
            inventoryPanel = panel;
            isInitialized = true;
            // Hide initially
            Hide();
        }

        /// <summary>
        /// Load container data and display items
        /// </summary>
        public void LoadContainer(object containerData)
        {
            currentContainerData = containerData;

            // TODO: Get items from container system
            // This would depend on the container system implementation
            // Example:
            // var container = containerData as IContainer;
            // if (container != null)
            // {
            //     var items = container.GetItems();
            //     DisplayItems(items);
            // }

            // For now, create empty grid
            RefreshLootGrid();
        }

        /// <summary>
        /// Display items in loot grid
        /// </summary>
        private void DisplayItems(List<InventorySlot> items)
        {
            ClearLootGrid();

            if (items == null || lootGridParent == null || lootSlotPrefab == null)
                return;

            foreach (var slot in items)
            {
                if (slot == null || slot.IsEmpty)
                    continue;

                GameObject slotObj = Instantiate(lootSlotPrefab, lootGridParent);
                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<InventorySlotUI>();
                }

                // Use negative coordinates to distinguish from inventory slots
                slotUI.Initialize(slot, inventoryPanel, -1, lootSlots.Count);
                lootSlots.Add(slotUI);
            }
        }

        /// <summary>
        /// Refresh loot grid display
        /// </summary>
        public void RefreshLootGrid()
        {
            // TODO: Reload items from container
            // DisplayItems(GetContainerItems());
        }

        /// <summary>
        /// Clear loot grid
        /// </summary>
        private void ClearLootGrid()
        {
            foreach (var slot in lootSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            lootSlots.Clear();
        }

        /// <summary>
        /// Set container title
        /// </summary>
        public void SetContainerTitle(string title)
        {
            if (containerTitleText != null)
            {
                containerTitleText.text = title ?? "Container";
            }
        }

        /// <summary>
        /// Show loot container panel
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide loot container panel
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Clear container data
        /// </summary>
        public void ClearContainer()
        {
            currentContainerData = null;
            ClearLootGrid();

            if (containerTitleText != null)
            {
                containerTitleText.text = "";
            }
        }

        /// <summary>
        /// Get container items (for drag & drop)
        /// </summary>
        public List<InventorySlot> GetContainerItems()
        {
            // TODO: Get items from container system
            // var container = currentContainerData as IContainer;
            // return container?.GetItems() ?? new List<InventorySlot>();
            return new List<InventorySlot>();
        }

        /// <summary>
        /// Add item to container
        /// </summary>
        public void AddItemToContainer(InventorySlot slot)
        {
            // TODO: Add item to container via container system
            RefreshLootGrid();
        }

        /// <summary>
        /// Remove item from container
        /// </summary>
        public void RemoveItemFromContainer(string itemId, int quantity)
        {
            // TODO: Remove item from container via container system
            RefreshLootGrid();
        }
    }
}
