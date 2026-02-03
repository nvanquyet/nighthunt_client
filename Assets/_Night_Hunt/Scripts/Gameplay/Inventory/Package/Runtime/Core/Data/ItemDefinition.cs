using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Static item definition (ScriptableObject).
    /// Contains all immutable data about an item.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "Inventory/ItemDefinition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Core Info")]
        public string ItemId;           // Unique ID (e.g., "weapon_ak47")
        public ItemType ItemType;       // Weapon, Armor, Consumable, Attachment, Misc
        public Sprite Icon;
        
        [Header("Physical Properties")]
        public float Weight;            // Kilograms
        public bool IsStackable;
        public int MaxStackSize = 1;    // 1 for non-stackable, config for stackable
        
        [Header("Slot Rules")]
        public SlotLocationType[] AllowedSlotLocations; // Where can this item go?
        public EquipmentSlotType EquipmentSlot;         // If equippable
        
        [Header("Attachment System")]
        public AttachmentSlotType[] AttachmentSlots;    // Slots this item provides (defined in weapon/equipment)
        public AttachmentSlotType AttachmentType;       // If this IS an attachment
        
        [Header("Stats Modification")]
        public StatModifierConfig[] CharacterStatModifiers; // Modifies character
        public StatModifierConfig[] WeaponStatModifiers;    // Modifies weapon
        
        [Header("Durability")]
        public float MaxDurability = 100f; // 0-100%, 0 = broken (cannot repair), >0 = repairable
        
        [Header("Value/Economy")]
        public int EventValue;          // For event rewards (NOT in-game currency)
        public ItemRequirement[] Requirements; // Unlock conditions
    }
}
