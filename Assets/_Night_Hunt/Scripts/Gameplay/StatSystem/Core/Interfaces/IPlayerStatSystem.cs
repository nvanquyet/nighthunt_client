using System;
using System.Collections.Generic;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Configs;

namespace NightHunt.StatSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for player stat management system
    /// 
    /// RESPONSIBILITIES:
    /// - Provides access to player stats, modifiers, and weight calculations
    /// - Defines contract for stat management operations
    /// - Implemented by PlayerStatSystem (NetworkBehaviour)
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All modifier operations on server
    /// - Client receives final calculated values via network sync
    /// </summary>
    public interface IPlayerStatSystem
    {
        #region Stat Getters
        
        /// <summary>
        /// Get current calculated value of a stat (base + all modifiers)
        /// This is the actual value used in gameplay
        /// </summary>
        float GetStat(PlayerStatType type);
        
        /// <summary>
        /// Get base value of a stat (without modifiers)
        /// Useful for comparing base stats
        /// </summary>
        float GetBaseStat(PlayerStatType type);
        
        /// <summary>
        /// Get total modifier value applied to a stat
        /// Returns: CurrentValue - BaseValue
        /// Useful for UI tooltips showing bonuses
        /// </summary>
        float GetStatModifier(PlayerStatType type);
        
        /// <summary>
        /// Get all current stat values as a dictionary
        /// Useful for UI display and debugging
        /// </summary>
        Dictionary<PlayerStatType, float> GetAllStats();
        
        /// <summary>
        /// Directly set the current value of a stat (server only)
        /// 
        /// PARAMETERS:
        /// - type: Stat type to set
        /// - value: New value to set
        /// 
        /// RETURNS:
        /// - None
        /// 
        /// NETWORK:
        /// - Server-only operation
        /// - Used by ItemUseSystem for consumable effects (heal, stamina restore, etc.)
        /// - Value is clamped to [MinValue, MaxValue] defined in PlayerStatConfig
        /// </summary>
        void SetCurrentStat(PlayerStatType type, float value);
        
        #endregion
        
        #region Weight System
        
        /// <summary>
        /// Get current total weight being carried
        /// Calculated from CurrentWeight stat
        /// </summary>
        float GetCurrentWeight();
        
        /// <summary>
        /// Get maximum weight capacity
        /// Calculated from WeightCapacity stat (base + modifiers from backpack, etc)
        /// </summary>
        float GetWeightCapacity();
        
        /// <summary>
        /// Get current weight as percentage of capacity (0.0 - 1.5+)
        /// Values > 1.0 indicate overweight
        /// Used for movement speed penalties
        /// </summary>
        float GetWeightPercent();
        
        /// <summary>
        /// Check if player can carry additional weight
        /// Note: This is informational - inventory can still exceed capacity
        /// </summary>
        bool CanCarryWeight(float additionalWeight);
        
        /// <summary>
        /// Get movement speed multiplier based on current weight
        /// Returns: 1.0 = normal speed, 0.1 = minimum speed at max overweight
        /// Uses GameplayConfig settings for weight penalty calculation
        /// </summary>
        float GetMovementSpeedMultiplier();
        
        #endregion
        
        #region Modifier Management (Server Only)
        
        /// <summary>
        /// Add a stat modifier
        /// If modifier with same SourceID exists, it will be replaced
        /// Server-side only, automatically syncs to clients
        /// </summary>
        /// <param name="type">Stat type to modify</param>
        /// <param name="modifier">Modifier to add</param>
        void AddModifier(PlayerStatType type, StatModifier modifier);
        
        /// <summary>
        /// Remove a specific modifier by source ID
        /// Server-side only, automatically syncs to clients
        /// </summary>
        /// <param name="type">Stat type</param>
        /// <param name="sourceID">Source ID of modifier to remove</param>
        void RemoveModifier(PlayerStatType type, string sourceID);
        
        /// <summary>
        /// Remove all modifiers from a specific source
        /// Useful when unequipping items or removing buffs
        /// Server-side only, automatically syncs to clients
        /// </summary>
        /// <param name="sourceID">Source ID to remove all modifiers from</param>
        void RemoveAllModifiersFromSource(string sourceID);
        
        /// <summary>
        /// Get all active modifiers for a stat type
        /// Useful for debugging and UI tooltips
        /// </summary>
        List<StatModifier> GetModifiers(PlayerStatType type);
        
        /// <summary>
        /// Get PlayerStatConfig used by this system
        /// Useful for UI to get display metadata
        /// </summary>
        PlayerStatConfig GetStatConfig();
        
        #endregion
        
        #region Events (Fired on all clients)
        
        /// <summary>
        /// Event fired when any stat changes
        /// Parameters: (statType, oldValue, newValue)
        /// Fired on both server and clients after sync
        /// </summary>
        event Action<PlayerStatType, float, float> OnStatChanged;
        
        /// <summary>
        /// Event fired when weight changes
        /// Parameters: (currentWeight, weightCapacity)
        /// Useful for updating weight UI
        /// </summary>
        event Action<float, float> OnWeightChanged;
        
        /// <summary>
        /// Event fired when overweight status changes significantly
        /// Parameters: (weightPercent)
        /// Useful for triggering UI warnings or movement penalties
        /// </summary>
        event Action<float> OnOverweightChanged;
        
        #endregion
    }
}
