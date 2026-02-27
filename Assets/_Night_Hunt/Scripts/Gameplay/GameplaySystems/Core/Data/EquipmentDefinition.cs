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
        [Tooltip("Durability loss per damage taken (multiplier applied to incoming damage)")]
        [Min(0f)]
        public float DurabilityLossRate = 1f;
        // MaxDurability / DefaultDurability → StatConfig.Stats[ItemStatType.MaxDurability]
        // Start value set by InventorySystem via GetDefaultCurrentValue()
        // instance.CurrentResource = runtime current durability

        #endregion

        #region Override Methods

        /// <summary>
        /// Returns StatConfig[MaxDurability] as the starting CurrentResource value.
        /// InventorySystem.CreateItemInstance() calls this to initialize instance.CurrentResource.
        /// </summary>
        public override float GetDefaultCurrentValue()
            => StatConfig != null ? StatConfig.GetStatValue(ItemStatType.MaxDurability) : 0f;

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