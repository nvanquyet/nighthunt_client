using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using System;
using NightHunt.Inventory.Core.Interfaces;
using NightHunt.Inventory.Stats;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// ScriptableObject that defines the static properties of an item.
    /// This is the template/blueprint for item instances.
    /// ENHANCED: Equip behavior, weight modifiers, consumable configs.
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
        [Tooltip("Weight per unit in kilograms (for stacking calculations)")]
        public float WeightPerUnit = 1f;
        
        [Tooltip("Can this item be stacked?")]
        public bool IsStackable = false;
        
        [Tooltip("Maximum stack size (1 for non-stackable items)")]
        public int MaxStackSize = 1;
        
        [Header("Resource System")]
        public ItemResourceType ResourceType = ItemResourceType.None;
        public float MaxResource = 0f;
        
        [Header("Usage Configuration")]
        [Tooltip("Time in seconds to use this item (0 = instant, e.g., grenades)")]
        public float UsageDuration = 0f;
        
        [Tooltip("Can player cancel usage before completion?")]
        public bool CanCancelUsage = true;
        
        [Tooltip("Does usage prevent movement?")]
        public bool BlocksMovementWhileUsing = false;
        
        [Tooltip("Does usage interrupt other actions?")]
        public bool InterruptsOtherActions = true;
        
        [Header("Double-Click Behavior")]
        [Tooltip("What happens when double-clicking this item in inventory?")]
        public DoubleClickBehavior InventoryDoubleClickBehavior = DoubleClickBehavior.AutoDetermine;
        
        [Header("Weight When Equipped")]
        [Tooltip("Does this item add weight when equipped? (Override global config)")]
        public WeightBehavior WeightWhenEquipped = WeightBehavior.UseGlobalConfig;
        
        [Header("Slot Rules")]
        [Tooltip("Equipment slot type if this is an equippable item")]
        public EquipmentSlotType EquipmentSlot;
        
        [Header("Equip Behavior")]
        [Tooltip("How this item equips: Single (1 item) or StackAll (entire stack)")]
        public EquipMode EquipMode = EquipMode.Single;
        
        [Tooltip("Does equipping reduce carried weight? (e.g., backpack increases capacity)")]
        public bool ReducesWeightWhenEquipped = false;
        
        [Tooltip("Weight modifier when equipped (negative = reduces weight, positive = increases)")]
        public float EquippedWeightModifier = 0f;
        
        [Header("Consumable Settings")]
        [Tooltip("Is this a consumable item?")]
        public bool IsConsumable = false;
        
        [Tooltip("Time in seconds to use this consumable (default 3s)")]
        public float UseTime = 3f;
        
        [Tooltip("Can use be canceled? (default true)")]
        public bool IsCancelable = true;
        
        [Tooltip("Resource amount consumed per use")]
        public float ConsumeAmountPerUse = 1f;
        
        [Header("Attachment System")]
        [Tooltip("Attachment slots this item provides (if weapon/equipment)")]
        public AttachmentSlotType[] AttachmentSlots;
        
        [Tooltip("If this IS an attachment, what type?")]
        public AttachmentSlotType AttachmentType;
        
        [Header("Stats Modification")]
        [Tooltip("Stat modifiers applied when this item is equipped/attached")]
        public StatModifierDefinition[] StatModifiers;
        
        [Tooltip("Rarity tier")]
        public ItemRarity Rarity = ItemRarity.Common;
        
        [Header("3D Model")]
        [Tooltip("3D model prefab for world drops")]
        public GameObject WorldModelPrefab;
        
        [Tooltip("3D model for equipped state (optional)")]
        public GameObject EquippedModelPrefab;
    }
    
    // === SUPPORTING STRUCTS ===
    
    /// <summary>
    /// Configuration for stat modifiers applied by items.
    /// ENHANCED: Now targets either Character or Weapon stats explicitly.
    /// Attachments can modify ANY item they're attached to (not just weapons).
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
    
    
    // === ENUMS ===
    public enum ItemResourceType
    {
        None,
        Durability,
        Ammo,
        Energy,
        Fuel,
        Charge
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
    
}