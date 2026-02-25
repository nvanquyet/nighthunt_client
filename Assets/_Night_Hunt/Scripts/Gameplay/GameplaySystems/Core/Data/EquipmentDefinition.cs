using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Equipment item definition. Stats + PlayerModifiers from StatConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "Armor_", menuName = "GameplaySystems/Items/Armor Definition")]
    public class EquipmentDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Equipment;
        
        #region Stat Config
        
        [Header("Stat Config")]
        [Tooltip("Kéo thả EquipmentStatConfig vào đây")]
        public EquipmentStatConfig StatConfig;
        
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
            return StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;
        }
        
        public bool HasStat(ItemStatType statType)
        {
            return StatConfig != null && StatConfig.HasStat(statType);
        }
        
        public PlayerStatModifier[] GetPlayerModifiers()
        {
            return StatConfig?.PlayerModifiers;
        }
        
        #endregion
    }
}