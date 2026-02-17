using UnityEngine;
using GameplaySystems.Stat;
using GameplaySystems.Inventory;

namespace GameplaySystems.Core.Data
{
    /// <summary>
    /// Armor/Equipment item definition
    /// Extends ItemDefinition with armor-specific properties
    /// 
    /// Key Features:
    /// - Armor stats (armor value, durability) - defined HERE in Stats array
    /// - Player stat modifiers (armor, weight capacity, movement speed)
    /// - Equipment slot assignment (head, chest, back, etc.)
    /// - Attachment support (lights, pouches, plates)
    /// 
    /// NOTE: Armor stats are item-specific, NOT global config
    /// Each armor definition has its own Stats[] array
    /// </summary>
    [CreateAssetMenu(fileName = "Armor_", menuName = "GameplaySystems/Items/Armor Definition")]
    public class EquipmentDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Equipment;
        
        #region Armor Stats
        
        [Header("Armor Stats")]
        [Tooltip("Array of armor stats")]
        public ArmorStatValue[] Stats;
        
        #endregion
        
        #region Equipment Slot
        
        [Header("Equipment Slot")]
        [Tooltip("Which equipment slot this item goes into")]
        public EquipmentSlotType EquipmentSlot = EquipmentSlotType.Chest;
        
        #endregion
        
        #region Durability
        
        [Header("Durability")]
        [Tooltip("Max durability")]
        [Min(0f)]
        public float MaxDurability = 100f;
        
        [Tooltip("Default durability when spawned")]
        [Min(0f)]
        public float DefaultDurability = 100f;
        
        [Tooltip("Durability loss per damage taken")]
        [Min(0f)]
        public float DurabilityLossRate = 1f;
        
        #endregion
        
        #region Player Modifiers
        
        [Header("Player Modifiers")]
        [Tooltip("Player stat modifiers when equipped")]
        public PlayerStatModifier[] PlayerModifiers;
        
        #endregion
        
        #region Override Methods
        
        public override float GetMaxResource()
        {
            // Armor uses Durability as resource (clothing may use None)
            return ResourceType == ItemResourceType.Durability ? MaxDurability : 0f;
        }
        
        public override float GetDefaultResource()
        {
            return ResourceType == ItemResourceType.Durability ? DefaultDurability : 0f;
        }
        
        #endregion
        
        #region Stat Helpers
        
        public float GetStatValue(ItemStatType statType)
        {
            if (Stats == null)
                return 0f;
            
            foreach (var stat in Stats)
            {
                if (stat.StatType == statType)
                    return stat.Value;
            }
            
            return 0f;
        }
        
        public bool HasStat(ItemStatType statType)
        {
            if (Stats == null)
                return false;
            
            foreach (var stat in Stats)
            {
                if (stat.StatType == statType)
                    return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Editor Setup
        
#if UNITY_EDITOR
        [ContextMenu("Setup Default Vest Stats")]
        private void SetupDefaultVestStats()
        {
            Stats = new ArmorStatValue[]
            {
                new ArmorStatValue { StatType = ItemStatType.ArmorValue, Value = 80 },
                new ArmorStatValue { StatType = ItemStatType.MovementSpeedPenalty, Value = 10 },
                new ArmorStatValue { StatType = ItemStatType.StaminaPenalty, Value = 5 }
            };
            
            EquipmentSlot = EquipmentSlotType.Chest;
            
            MaxDurability = 150f;
            DefaultDurability = 150f;
            DurabilityLossRate = 1f;
            
            ResourceType = ItemResourceType.Durability;
            MaxResource = MaxDurability;
            DefaultResource = DefaultDurability;
            
            Weight = 8f;
            ModifyWeightWhenEquipped = true;
            EquippedWeightModifier = -3f;  // Giảm 3kg khi equipped (distributed weight)
            
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            AttachmentSlots = new AttachmentSlotType[] 
            { 
                AttachmentSlotType.Light, 
                AttachmentSlotType.Pouch, 
                AttachmentSlotType.Pouch,
                AttachmentSlotType.Plate 
            };
            
            PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.Armor,
                    Value = 80,
                    ModifierType = ModifierType.Flat,
                    Description = "Vest armor protection"
                },
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.MovementSpeed,
                    Value = -10f,
                    ModifierType = ModifierType.Percentage,
                    Description = "Heavy vest penalty"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ArmorDefinition] Setup default vest stats complete!");
        }
        
        [ContextMenu("Setup Default Backpack Stats")]
        private void SetupDefaultBackpackStats()
        {
            Stats = new ArmorStatValue[]
            {
                new ArmorStatValue { StatType = ItemStatType.WeightCapacityBonus, Value = 30 },
                new ArmorStatValue { StatType = ItemStatType.MovementSpeedPenalty, Value = 2 }
            };
            
            EquipmentSlot = EquipmentSlotType.Back;
            
            ResourceType = ItemResourceType.None; // Backpack không có durability
            MaxResource = 0f;
            DefaultResource = 0f;
            
            Weight = 2f;
            ModifyWeightWhenEquipped = true;
            EquippedWeightModifier = -1f;  // Giảm 1kg khi equipped
            
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            AttachmentSlots = new AttachmentSlotType[0]; // No attachments
            
            PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.WeightCapacity,
                    Value = 30,
                    ModifierType = ModifierType.Flat,
                    Description = "Backpack storage capacity"
                },
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.MovementSpeed,
                    Value = -2f,
                    ModifierType = ModifierType.Percentage,
                    Description = "Backpack weight penalty"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ArmorDefinition] Setup default backpack stats complete!");
        }
        
        [ContextMenu("Setup Default Helmet Stats")]
        private void SetupDefaultHelmetStats()
        {
            Stats = new ArmorStatValue[]
            {
                new ArmorStatValue { StatType = ItemStatType.ArmorValue, Value = 40 },
                new ArmorStatValue { StatType = ItemStatType.MovementSpeedPenalty, Value = 1 }
            };
            
            EquipmentSlot = EquipmentSlotType.Head;
            
            MaxDurability = 100f;
            DefaultDurability = 100f;
            DurabilityLossRate = 1f;
            
            ResourceType = ItemResourceType.Durability;
            MaxResource = MaxDurability;
            DefaultResource = DefaultDurability;
            
            Weight = 1.5f;
            
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            AttachmentSlots = new AttachmentSlotType[] 
            { 
                AttachmentSlotType.Light  // Headlamp
            };
            
            PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.Armor,
                    Value = 40,
                    ModifierType = ModifierType.Flat,
                    Description = "Helmet protection"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ArmorDefinition] Setup default helmet stats complete!");
        }
#endif
        
        #endregion
    }
    
    #region Supporting Types
    
    [System.Serializable]
    public struct ArmorStatValue
    {
        [Tooltip("Loại stat")]
        public ItemStatType StatType;
        
        [Tooltip("Giá trị")]
        public float Value;
    }
    
    #endregion
}