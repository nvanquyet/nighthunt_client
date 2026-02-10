using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Data;

namespace NightHunt.Inventory.UI.Slots
{
    /// <summary>
    /// UI component for inventory grid slots.
    /// Displays inventory items and handles inventory-specific interactions.
    /// </summary>
    public class InventorySlotUI : ItemSlotUI
    {
        [Header("Inventory Specific")]
        [SerializeField] private int slotIndex = -1;
        
        [Header("References")]
        [SerializeField] private InventoryUIDataProvider dataProvider;
        
        // === Public API ===
        
        /// <summary>
        /// Get slot index in inventory.
        /// </summary>
        public int GetSlotIndex() => slotIndex;
        
        /// <summary>
        /// Set slot index.
        /// </summary>
        public void SetSlotIndex(int index)
        {
            slotIndex = index;
        }
        
        /// <summary>
        /// Refresh slot data from inventory.
        /// </summary>
        public void RefreshFromInventory()
        {
            if (dataProvider == null)
            {
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            }
            
            if (dataProvider == null || slotIndex < 0)
            {
                Log($"[InventorySlotUI] RefreshFromInventory: dataProvider={dataProvider}, slotIndex={slotIndex}");
                return;
            }
            
            // Check if slot index is beyond actual inventory size
            int actualInventorySize = dataProvider.GetInventorySlotCount();
            Log($"[InventorySlotUI] RefreshFromInventory: slotIndex={slotIndex}, actualInventorySize={actualInventorySize}");
            
            if (slotIndex >= actualInventorySize)
            {
                // Mark as empty slot (beyond actual inventory size)
                SetIsEmptySlot(true);
                Log($"[InventorySlotUI] Slot {slotIndex} is beyond inventory size, marking as empty slot");
                return;
            }
            
            // Valid slot - get item and mark as not empty slot
            SetIsEmptySlot(false);
            var item = dataProvider.GetInventoryItemAtSlot(slotIndex);
            Log($"[InventorySlotUI] RefreshFromInventory: slotIndex={slotIndex}, item={item?.Definition?.DisplayName ?? "null"}");
            SetItem(item);
        }
        
        // === Override Methods ===
        
        protected override void HandleClick()
        {
            base.HandleClick();
            
            // Notify MainInventoryUIManager of selection
            var manager = FindObjectOfType<MainInventoryUIManager>();
            if (manager != null && currentItem != null)
            {
                manager.SelectInventoryItem(currentItem);
            }
        }
        
        protected override void HandleDoubleClick()
        {
            base.HandleDoubleClick();
            
            if (currentItem == null)
                return;
            
            // Auto-equip or use item based on type
            // This will be handled by drag & drop system or specific handlers
            Log($"Double clicked inventory item: {currentItem.Definition.DisplayName} at slot {slotIndex}");
        }
        
        // === Lifecycle ===
        
        protected override void Start()
        {
            base.Start();
            
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            // Refresh from inventory
            RefreshFromInventory();
        }
    }
}
