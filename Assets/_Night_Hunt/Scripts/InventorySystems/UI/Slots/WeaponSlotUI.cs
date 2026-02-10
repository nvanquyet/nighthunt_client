using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Data;

namespace NightHunt.Inventory.UI.Slots
{
    /// <summary>
    /// UI component for weapon slots (primary, secondary, melee).
    /// Displays equipped weapons and handles weapon-specific interactions.
    /// </summary>
    public class WeaponSlotUI : ItemSlotUI
    {
        [Header("Weapon Specific")]
        [SerializeField] private WeaponSlotType slotType;
        
        [Header("Visual")]
        [SerializeField] private Image activeWeaponIndicator;
        
        [Header("References")]
        [SerializeField] private InventoryUIDataProvider dataProvider;
        
        // === Public API ===
        
        /// <summary>
        /// Get weapon slot type.
        /// </summary>
        public WeaponSlotType GetSlotType() => slotType;
        
        /// <summary>
        /// Set weapon slot type.
        /// </summary>
        public void SetSlotType(WeaponSlotType type)
        {
            slotType = type;
            RefreshFromWeapons();
        }
        
        /// <summary>
        /// Refresh slot data from weapons.
        /// </summary>
        public void RefreshFromWeapons()
        {
            if (dataProvider == null)
            {
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            }
            
            if (dataProvider == null)
                return;
            
            var weapon = dataProvider.GetEquippedWeapon(slotType);
            SetItem(weapon);
            
            // Update active weapon indicator
            UpdateActiveWeaponIndicator();
        }
        
        /// <summary>
        /// Update active weapon indicator.
        /// </summary>
        public void UpdateActiveWeaponIndicator()
        {
            if (activeWeaponIndicator == null)
                return;
            
            if (dataProvider == null)
                return;
            
            bool isActive = dataProvider.GetActiveWeaponSlot() == slotType;
            activeWeaponIndicator.gameObject.SetActive(isActive);
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
                // Get config from WeaponPanel
                var weaponPanel = FindObjectOfType<Panels.WeaponPanel>();
                if (weaponPanel != null && weaponPanel.slotLayoutConfig != null)
                {
                    Sprite iconSprite = weaponPanel.slotLayoutConfig.GetWeaponEmptyIcon(slotType);
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
            
            if (currentItem == null)
                return;
            
            // Switch to this weapon
            var weaponSync = dataProvider?.GetWeaponNetworkSync();
            if (weaponSync != null && dataProvider.CanInteract())
            {
                weaponSync.RequestSwitchWeapon(slotType);
                Log($"Clicked to switch to weapon: {slotType}");
            }
        }
        
        protected override void HandleDoubleClick()
        {
            base.HandleDoubleClick();
            
            if (currentItem == null)
                return;
            
            // Unequip weapon
            var weaponSync = dataProvider?.GetWeaponNetworkSync();
            if (weaponSync != null && dataProvider.CanInteract())
            {
                weaponSync.RequestUnequipWeaponToInventory(slotType);
                Log($"Double clicked to unequip weapon: {currentItem.Definition.DisplayName}");
            }
        }
        
        // === Lifecycle ===
        
        protected override void Start()
        {
            base.Start();
            
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            // Refresh from weapons
            RefreshFromWeapons();
        }
        
        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();
            UpdateActiveWeaponIndicator();
        }
    }
}
