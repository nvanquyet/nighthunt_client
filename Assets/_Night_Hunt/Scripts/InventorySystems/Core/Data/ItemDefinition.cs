using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using System;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// ScriptableObject that defines the static properties of an item.
    /// This is the template/blueprint for item instances.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "NightHunt/Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Core Info")]
        [Tooltip("Unique identifier for this item (e.g., 'weapon_ak47')")]
        public string ItemId;
        
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
        [Tooltip("Modifiers applied to character when equipped")]
        public StatModifierConfig[] CharacterStatModifiers;
        
        [Tooltip("Modifiers applied to weapon when attached")]
        public StatModifierConfig[] WeaponStatModifiers;
        
        [Header("Durability")]
        [Tooltip("Maximum durability (0-100%). 0 = broken, >0 = repairable")]
        public float MaxDurability = 100f;
        
        [Header("Value/Economy")]
        [Tooltip("Value for event rewards (NOT in-game currency)")]
        public int EventValue = 0;
        
        [Tooltip("Requirements to unlock/use this item")]
        public ItemRequirement[] Requirements;
        
        [Header("3D Model (Optional)")]
        [Tooltip("3D model prefab for world drops")]
        public GameObject WorldModelPrefab;
    }
    
    /// <summary>
    /// Configuration for stat modifiers applied by items.
    /// </summary>
    [Serializable]
    public class StatModifierConfig
    {
        [Header("Target Stat")]
        [Tooltip("Character stat to modify (if applicable)")]
        public CharacterStatType CharacterStat;
        
        [Tooltip("Weapon stat to modify (if applicable)")]
        public WeaponStatType WeaponStat;
        
        [Header("Modifier Settings")]
        [Tooltip("Type of modifier (Flat or Percentage) - configured globally")]
        public ModifierCalculationType Type;
        
        [Tooltip("Value of the modifier")]
        public float Value;
        
        /// <summary>
        /// Gets a unique source ID for tracking this modifier.
        /// </summary>
        public string GetSourceId(string itemInstanceId)
        {
            return $"Attach:{itemInstanceId}";
        }
    }
    
    /// <summary>
    /// Defines requirements for unlocking/using an item.
    /// </summary>
    [Serializable]
    public class ItemRequirement
    {
        [Tooltip("Type of requirement")]
        public RequirementType Type;
        
        [Tooltip("ID of the requirement (event ID, achievement ID, etc.)")]
        public string RequirementId;
        
        [Tooltip("Required amount/level")]
        public int RequiredAmount = 1;
    }
}