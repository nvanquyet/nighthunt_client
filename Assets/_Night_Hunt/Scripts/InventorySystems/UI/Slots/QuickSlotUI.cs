using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Data;
using TMPro;

namespace NightHunt.Inventory.UI.Slots
{
    /// <summary>
    /// UI component for quick slots (1-9).
    /// Displays quick slot items and handles quick slot interactions.
    /// </summary>
    public class QuickSlotUI : ItemSlotUI
    {
        [Header("Quick Slot Specific")]
        [SerializeField] private int quickSlotIndex = -1;
        
        [Header("Visual")]
        [SerializeField] private TextMeshProUGUI slotNumberText;
        
        [Header("References")]
        [SerializeField] private InventoryUIDataProvider dataProvider;
        
        // === Public API ===
        
        /// <summary>
        /// Get quick slot index.
        /// </summary>
        public int GetQuickSlotIndex() => quickSlotIndex;
        
        /// <summary>
        /// Set quick slot index.
        /// </summary>
        public void SetQuickSlotIndex(int index)
        {
            quickSlotIndex = index;
            
            // Update slot number text
            if (slotNumberText != null)
            {
                slotNumberText.text = (index + 1).ToString();
            }
            
            RefreshFromQuickSlots();
        }
        
        /// <summary>
        /// Refresh slot data from quick slots.
        /// </summary>
        public void RefreshFromQuickSlots()
        {
            if (dataProvider == null)
            {
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            }
            
            if (dataProvider == null || quickSlotIndex < 0)
                return;
            
            var item = dataProvider.GetQuickSlotItem(quickSlotIndex);
            SetItem(item);
        }
        
        // === Override Methods ===
        
        protected override void HandleClick()
        {
            base.HandleClick();
            
            // Quick slot click = use item (handled by gameplay code)
            if (currentItem != null)
            {
                Log($"Quick slot {quickSlotIndex} clicked: {currentItem.Definition.DisplayName}");
            }
        }
        
        protected override void HandleDoubleClick()
        {
            base.HandleDoubleClick();
            
            if (currentItem == null)
                return;
            
            // Clear quick slot
            var quickSlotSync = dataProvider?.GetQuickSlotNetworkSync();
            if (quickSlotSync != null && dataProvider.CanInteract())
            {
                quickSlotSync.RequestClearQuickSlot(quickSlotIndex);
                Log($"Double clicked to clear quick slot: {quickSlotIndex}");
            }
        }
        
        // === Lifecycle ===
        
        protected override void Start()
        {
            base.Start();
            
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            // Refresh from quick slots
            RefreshFromQuickSlots();
        }
    }
}
