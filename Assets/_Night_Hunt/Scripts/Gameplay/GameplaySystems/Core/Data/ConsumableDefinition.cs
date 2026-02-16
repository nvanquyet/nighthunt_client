using UnityEngine;
using GameplaySystems.Stat;
using GameplaySystems.Inventory;

namespace GameplaySystems.Core.Data
{
    /// <summary>
    /// Consumable item definition (food, medkits, potions)
    /// 
    /// Key Features:
    /// - Usage effects (restore health, stamina, buffs)
    /// - Usage duration (instant or over time)
    /// - Stackable by default
    /// - Can be assigned to QuickSlots
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable_", menuName = "GameplaySystems/Items/Consumable Definition")]
    public class ConsumableDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Consumable;
        
        #region Usage Effects
        
        [Header("Usage Effects")]
        [Tooltip("Effects applied when consumed")]
        public ConsumableEffect[] Effects;
        
        #endregion
        
        #region Editor Setup
        
#if UNITY_EDITOR
        [ContextMenu("Setup Default Medkit")]
        private void SetupDefaultMedkit()
        {
            DisplayName = "Medkit";
            Description = "Restores 50 health over 3 seconds";
            
            IsStackable = true;
            MaxStackSize = 5;
            Weight = 0.3f;
            
            UsageDuration = 3f;
            CanCancelUsage = true;
            CanUseWhileMoving = false;
            
            ValidSlots = new SlotLocationType[] 
            { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            
            Effects = new ConsumableEffect[]
            {
                new ConsumableEffect
                {
                    StatType = PlayerStatType.Health,
                    Value = 50f,
                    IsInstant = false,
                    Description = "Restore 50 health"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        
        #endregion
    }
    
    #region Supporting Types
    
    [System.Serializable]
    public struct ConsumableEffect
    {
        [Tooltip("Stat to affect")]
        public PlayerStatType StatType;
        
        [Tooltip("Value to add/set")]
        public float Value;
        
        [Tooltip("Apply instantly or over usage duration")]
        public bool IsInstant;
        
        [Tooltip("Description")]
        public string Description;
    }
    
    #endregion
}