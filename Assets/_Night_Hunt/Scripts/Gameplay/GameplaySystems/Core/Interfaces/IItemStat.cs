using System.Collections.Generic;
using GameplaySystems.Stat;

namespace GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for item stat calculations
    /// Handles calculating stats for individual items including attachments
    /// Non-networked - used for local calculations and tooltips
    /// </summary>
    public interface IItemStat
    {
        /// <summary>
        /// Get a specific stat value for this item
        /// Includes base item stats + attachment contributions
        /// </summary>
        float GetStat(ItemStatType type);
        
        /// <summary>
        /// Get all stats for this item as a dictionary
        /// Useful for UI tooltips and comparisons
        /// </summary>
        Dictionary<ItemStatType, float> GetAllStats();
        
        /// <summary>
        /// Check if this item has a specific stat (non-zero value)
        /// </summary>
        bool HasStat(ItemStatType type);
        
        /// <summary>
        /// Recalculate all stats based on item definition and attachments
        /// Call this when attachments change
        /// </summary>
        void RecalculateStats();
    }
}