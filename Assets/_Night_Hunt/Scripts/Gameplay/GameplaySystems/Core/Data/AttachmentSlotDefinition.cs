using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Attachment item definition. Stats + ItemModifiers from StatConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "Attachment_", menuName = "GameplaySystems/Items/Attachment Definition")]
    public class AttachmentDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Attachment;
        
        #region Stat Config
        
        [Header("Stat Config")]
        [Tooltip("Kéo thả AttachmentStatConfig vào đây")]
        public AttachmentStatConfig StatConfig;
        
        #endregion
        
        #region Helpers
        
        public ItemStatModifier? GetModifier(ItemStatType statType)
        {
            if (StatConfig?.ItemModifiers == null) return null;
            foreach (var mod in StatConfig.ItemModifiers)
            {
                if (mod.StatType == statType) return mod;
            }
            return null;
        }
        
        public bool ModifiesStat(ItemStatType statType)
        {
            return GetModifier(statType).HasValue;
        }
        
        public ItemStatModifier[] GetItemModifiers()
        {
            return StatConfig?.ItemModifiers;
        }
        
        /// <summary>
        /// Get stat value from StatConfig
        /// </summary>
        public float GetStatValue(ItemStatType statType)
        {
            return StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;
        }
        
        /// <summary>
        /// Check if item has specific stat
        /// </summary>
        public bool HasStat(ItemStatType statType)
        {
            return StatConfig != null && StatConfig.HasStat(statType);
        }
        
        #endregion
        
        #region Validation
        
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error))
                return false;
            
            // Attachments must be able to attach to something
            if (CanAttachTo == null || CanAttachTo.Length == 0)
            {
                error = "Attachment must specify CanAttachTo slot types";
                return false;
            }
            
            // Attachments should not be stackable
            if (IsStackable)
            {
                error = "Attachments should not be stackable";
                return false;
            }
            
            error = null;
            return true;
        }
        
        #endregion
    }
}