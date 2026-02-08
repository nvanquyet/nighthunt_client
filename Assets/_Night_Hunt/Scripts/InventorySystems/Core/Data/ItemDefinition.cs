using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// ScriptableObject that defines the static properties of an item.
    /// This is the template/blueprint for item instances.
    /// REFACTORED: Enhanced stat modifier system with Character/Weapon targeting.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "NightHunt/Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Core Info")]
        [Tooltip("Unique identifier for this item (e.g., 'weapon_ak47', 'helmet_tactical')")]
        public string ItemId;
        
        [Tooltip("Display name shown in UI")]
        public string DisplayName;
        
        [Tooltip("Item description")]
        [TextArea(3, 5)]
        public string Description;
        
        [Tooltip("Type of item (Weapon, Armor, Consumable, etc.)")]
        public ItemType ItemType;
        
        [Tooltip("Icon displayed in UI")]
        public Sprite Icon;
        
        [Header("Physical Properties")]
        [Tooltip("Weight in kilograms")]
        public float Weight = 1f;
        
        [Tooltip("Can this item be stacked?")]
        public bool IsStackable = false;
        
        [Tooltip("Maximum stack size (1 for non-stackable items)")]
        public int MaxStackSize = 1;
        
        [Header("Slot Rules")]
        [Tooltip("Where can this item be placed?")]
        public SlotLocationType[] AllowedSlotLocations;
        
        [Tooltip("Equipment slot type if this is an equippable item")]
        public EquipmentSlotType EquipmentSlot;
        
        [Header("Attachment System")]
        [Tooltip("Attachment slots this item provides (if weapon/equipment)")]
        public AttachmentSlotType[] AttachmentSlots;
        
        [Tooltip("If this IS an attachment, what type?")]
        public AttachmentSlotType AttachmentType;
        
        [Header("Stats Modification")]
        [Tooltip("Stat modifiers applied when this item is equipped/attached")]
        public StatModifierDefinition[] StatModifiers;
        
        [Header("Durability")]
        [Tooltip("Maximum durability (0-100%)")]
        public float MaxDurability = 100f;
        
        [Tooltip("Degradation rate per use (%)")]
        public float DurabilityDegradationRate = 0.1f;
        
        [Header("Value/Economy")]
        [Tooltip("Value for event rewards (NOT in-game currency)")]
        public int EventValue = 0;
        
        [Tooltip("Rarity tier")]
        public ItemRarity Rarity = ItemRarity.Common;
        
        [Header("Requirements")]
        [Tooltip("Requirements to unlock/use this item")]
        public ItemRequirement[] Requirements;
        
        [Header("3D Model")]
        [Tooltip("3D model prefab for world drops")]
        public GameObject WorldModelPrefab;
        
        [Tooltip("3D model for equipped state (optional)")]
        public GameObject EquippedModelPrefab;
        
        // === Helper Methods ===
        
        /// <summary>
        /// Get all stat modifiers as StatModifierData structs.
        /// Used by systems to apply modifiers to characters/weapons.
        /// </summary>
        public List<StatModifierData> GetStatModifiersData()
        {
            return StatModifiers.Select(mod => mod.ToStatModifierData()).ToList();
        }
        
        /// <summary>
        /// Check if this item can be equipped in the given slot type.
        /// </summary>
        public bool CanEquipInSlot(EquipmentSlotType slotType)
        {
            return EquipmentSlot == slotType && AllowedSlotLocations.Contains(SlotLocationType.Equipment);
        }
        
        /// <summary>
        /// Check if this item can be placed in the given location type.
        /// </summary>
        public bool IsAllowedInLocation(SlotLocationType location)
        {
            return AllowedSlotLocations.Contains(location);
        }
        
        /// <summary>
        /// Check if this item provides the given attachment slot.
        /// </summary>
        public bool HasAttachmentSlot(AttachmentSlotType slotType)
        {
            return AttachmentSlots.Contains(slotType);
        }
    }
    
    /// <summary>
    /// Configuration for stat modifiers applied by items.
    /// ENHANCED: Now targets either Character or Weapon stats explicitly.
    /// </summary>
    [Serializable]
    public struct StatModifierDefinition
    {
        [Header("Modifier Target")]
        [Tooltip("Does this modify Character stats or Weapon stats?")]
        public StatModifierTarget Target;
        
        [Header("Character Stat (if Target == Character)")]
        [Tooltip("Character stat to modify")]
        public CharacterStatType CharacterStat;
        
        [Header("Weapon Stat (if Target == Weapon)")]
        [Tooltip("Weapon stat to modify")]
        public WeaponStatType WeaponStat;
        
        [Header("Modifier Settings")]
        [Tooltip("Type of modifier (Flat or Percentage)")]
        public ModifierCalculationType CalculationType;
        
        [Tooltip("Value of the modifier (e.g., +10 for flat, +0.15 for 15% increase)")]
        public float Value;
        
        [Tooltip("Display name for UI (e.g., '+15% Vision Radius')")]
        public string DisplayName;
        
        /// <summary>
        /// Convert to StatModifierData struct for system usage.
        /// </summary>
        public StatModifierData ToStatModifierData()
        {
            return new StatModifierData
            {
                Target = Target,
                CharacterStat = CharacterStat,
                WeaponStat = WeaponStat,
                CalculationType = CalculationType,
                Value = Value,
                DisplayName = DisplayName
            };
        }
    }
    
    /// <summary>
    /// Defines requirements for unlocking/using an item.
    /// </summary>
    [Serializable]
    public struct ItemRequirement
    {
        [Tooltip("Type of requirement")]
        public RequirementType Type;
        
        [Tooltip("ID of the requirement (event ID, achievement ID, etc.)")]
        public string RequirementId;
        
        [Tooltip("Required amount/level")]
        public int RequiredAmount;
    }
    
    /// <summary>
    /// Item rarity tiers.
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
    
    /// <summary>
    /// Requirement types for items.
    /// </summary>
    public enum RequirementType
    {
        None,
        PlayerLevel,
        EventCompletion,
        Achievement,
        Currency
    }
}