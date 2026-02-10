using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using System;

namespace NightHunt.Inventory.Core.Config
{
    /// <summary>
    /// Configures slot layouts for different container types.
    /// Example: Equipment slots can be expanded from 4 to 10 (adding rings, trinkets, etc.)
    ///          Weapon slots can go from 2 to 3 (Primary, Secondary, Melee)
    ///          QuickSlots can be configured as 4, 6, or 8 slots
    /// </summary>
    [CreateAssetMenu(fileName = "SlotLayoutConfig", menuName = "NightHunt/Config/Slot Layout Config")]
    public class SlotLayoutConfig : ScriptableObject 
    {
        [Header("Equipment Slots Configuration")]
        [Tooltip("Defines all equipment slots available to player")]
        public EquipmentSlotDefinition[] EquipmentSlots = new EquipmentSlotDefinition[]
        {
            new EquipmentSlotDefinition { SlotType = EquipmentSlotType.Helmet, DisplayName = "Helmet", IsRequired = false },
            new EquipmentSlotDefinition { SlotType = EquipmentSlotType.Armor, DisplayName = "Armor", IsRequired = false },
            new EquipmentSlotDefinition { SlotType = EquipmentSlotType.Backpack, DisplayName = "Backpack", IsRequired = false },
            new EquipmentSlotDefinition { SlotType = EquipmentSlotType.Boots, DisplayName = "Boots", IsRequired = false }
            // Future: Ring1, Ring2, Trinket, etc.
        };
         
        [Header("Weapon Slots Configuration")]
        [Tooltip("Defines weapon slots (can be expanded for melee)")]
        public WeaponSlotDefinition[] WeaponSlots = new WeaponSlotDefinition[]
        {
            new WeaponSlotDefinition { SlotType = WeaponSlotType.Primary, DisplayName = "Primary Weapon", AllowedTypes = new[] { ItemType.Weapon } },
            new WeaponSlotDefinition { SlotType = WeaponSlotType.Secondary, DisplayName = "Secondary Weapon", AllowedTypes = new[] { ItemType.Weapon } }
            // Future: Melee slot
        };
        
        [Header("Quick Slot Configuration")]
        [Tooltip("Number of quick access slots (default: 4, can expand to 6-8)")]
        [Range(2, 10)]
        public int QuickSlotCount = 4;
        
        [Header("Inventory Configuration")]
        [Tooltip("Main inventory slot count")]
        [Range(10, 100)]
        public int MainInventorySlotCount = 20;
        
        [Tooltip("Can inventory size be expanded via backpacks?")]
        public bool AllowDynamicInventoryExpansion = true;
        
        [Tooltip("Max inventory slots after all expansions")]
        [Range(10, 200)]
        public int MaxInventorySlotCount = 50;
        
        [Header("Empty Slot Icons")]
        [Tooltip("Icon for empty inventory slots")]
        public Sprite inventoryEmptyIcon;
        
        [Tooltip("Icon for empty quick slots")]
        public Sprite quickSlotEmptyIcon;
        
        [Header("Attachment Empty Icons")]
        [Tooltip("Icons for each attachment slot type when empty (to show what can be attached)")]
        public AttachmentSlotIcon[] attachmentEmptyIcons;
        
        // === Helper Methods ===
        
        /// <summary>
        /// Get empty icon for equipment slot type.
        /// </summary>
        public Sprite GetEquipmentEmptyIcon(EquipmentSlotType slotType)
        {
            if (EquipmentSlots == null)
                return inventoryEmptyIcon;
            
            foreach (var slotDef in EquipmentSlots)
            {
                if (slotDef.SlotType == slotType)
                {
                    // Use SlotIcon from definition if available, otherwise use inventoryEmptyIcon
                    return slotDef.SlotIcon != null ? slotDef.SlotIcon : inventoryEmptyIcon;
                }
            }
            
            return inventoryEmptyIcon; // Fallback
        }
        
        /// <summary>
        /// Get empty icon for weapon slot type.
        /// </summary>
        public Sprite GetWeaponEmptyIcon(WeaponSlotType slotType)
        {
            if (WeaponSlots == null)
                return inventoryEmptyIcon;
            
            foreach (var slotDef in WeaponSlots)
            {
                if (slotDef.SlotType == slotType)
                {
                    // Use SlotIcon from definition if available, otherwise use inventoryEmptyIcon
                    return slotDef.SlotIcon != null ? slotDef.SlotIcon : inventoryEmptyIcon;
                }
            }
            
            return inventoryEmptyIcon; // Fallback
        }
        
        /// <summary>
        /// Get empty icon for attachment slot type.
        /// </summary>
        public Sprite GetAttachmentEmptyIcon(AttachmentSlotType slotType)
        {
            if (attachmentEmptyIcons == null)
                return inventoryEmptyIcon;
            
            foreach (var icon in attachmentEmptyIcons)
            {
                if (icon.slotType == slotType)
                    return icon.emptyIcon;
            }
            
            return inventoryEmptyIcon; // Fallback
        }
    }
    
    [Serializable]
    public struct EquipmentSlotDefinition
    {
        public EquipmentSlotType SlotType;
        public string DisplayName;
        public bool IsRequired; // If true, cannot be left empty
        public Sprite SlotIcon; // Optional UI icon
    }
    
    [Serializable]
    public struct WeaponSlotDefinition
    {
        public WeaponSlotType SlotType;
        public string DisplayName;
        public ItemType[] AllowedTypes; // E.g., can allow Throwable in weapon slots
        public Sprite SlotIcon;
    }
    
    [Serializable]
    public class AttachmentSlotIcon
    {
        public AttachmentSlotType slotType;
        public Sprite emptyIcon;
        [Tooltip("Optional: Description of what this attachment slot is for")]
        public string description;
    }
}