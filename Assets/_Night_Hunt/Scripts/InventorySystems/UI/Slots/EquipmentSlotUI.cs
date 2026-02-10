using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Data;

namespace NightHunt.Inventory.UI.Slots
{
    /// <summary>
    /// UI component for equipment slots (helmet, vest, backpack, etc.).
    /// Displays equipped items and handles equipment-specific interactions.
    /// </summary>
    public class EquipmentSlotUI : ItemSlotUI
    {
        [Header("Equipment Specific")]
        [SerializeField] private EquipmentSlotType slotType;
        
        [Header("References")]
        [SerializeField] private InventoryUIDataProvider dataProvider;
         
        // === Public API ===
        
        /// <summary>
        /// Get equipment slot type.
        /// </summary>
        public EquipmentSlotType GetSlotType() => slotType;
        
        /// <summary>
        /// Set equipment slot type.
        /// </summary>
        public void SetSlotType(EquipmentSlotType type)
        {
            slotType = type;
            RefreshFromEquipment();
        }
        
        /// <summary>
        /// Refresh slot data from equipment.
        /// </summary>
        public void RefreshFromEquipment()
        {
            if (dataProvider == null)
            {
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            }
            
            if (dataProvider == null)
                return;
            
            var item = dataProvider.GetEquippedItem(slotType);
            SetItem(item);
        }
        
        // === Override Methods ===
        
        protected override void UpdateEmptySlotIcon()
        {
            if (emptySlotIcon == null)
                return;
            
            bool shouldShow = currentItem == null;
            emptySlotIcon.gameObject.SetActive(shouldShow);
            
            if (shouldShow)
            {
                // Get config from EquipmentPanel
                var equipmentPanel = FindObjectOfType<Panels.EquipmentPanel>();
                if (equipmentPanel != null && equipmentPanel.slotLayoutConfig != null)
                {
                    Sprite iconSprite = equipmentPanel.slotLayoutConfig.GetEquipmentEmptyIcon(slotType);
                    if (iconSprite != null)
                    {
                        emptySlotIcon.sprite = iconSprite;
                        emptySlotIcon.color = Color.white;
                    }
                    else
                    {
                        emptySlotIcon.color = Color.clear;
                    }
                }
            }
        }
        
        protected override void HandleClick()
        {
            base.HandleClick();
            
            // Notify MainInventoryUIManager of selection (for right attachment panel)
            var manager = FindObjectOfType<MainInventoryUIManager>();
            if (manager != null && currentItem != null)
            {
                manager.HoverEquippedItem(currentItem);
            }
        }
        
        protected override void HandleDoubleClick()
        {
            base.HandleDoubleClick();
            
            if (currentItem == null)
                return;
            
            // Unequip item
            var equipmentSync = dataProvider?.GetEquipmentNetworkSync();
            if (equipmentSync != null && dataProvider.CanInteract())
            {
                equipmentSync.RequestUnequipToInventory(slotType);
                Log($"Double clicked to unequip: {currentItem.Definition.DisplayName}");
            }
        }
        
        // === Lifecycle ===
        
        protected override void Start()
        {
            base.Start();
            
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            // Refresh from equipment
            RefreshFromEquipment();
        }
    }
}
