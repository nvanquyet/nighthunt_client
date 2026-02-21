using System.Collections.Generic;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.StatSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for item stat calculations
    /// 
    /// RESPONSIBILITIES:
    /// - Defines contract for calculating stats for individual items
    /// - Handles calculating stats including attachments
    /// - Non-networked - used for local calculations and tooltips
    /// 
    /// DESIGN:
    /// - Static utility class implementation
    /// - Pure calculation logic, no network sync
    /// </summary>
    public interface IItemStatSystem
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
