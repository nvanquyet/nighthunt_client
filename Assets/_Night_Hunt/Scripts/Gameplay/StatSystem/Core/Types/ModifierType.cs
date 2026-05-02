
namespace NightHunt.Gameplay.StatSystem.Core.Types
{
    /// <summary>
    /// Defines how a modifier affects a stat
    /// 
    /// RESPONSIBILITIES:
    /// - Enumeration of modifier calculation types
    /// - Used by StatModifier for stat calculations
    /// - Determines calculation order and method
    /// </summary>
    public enum ModifierType
    {
        /// <summary>
        /// Flat value addition (e.g., +10)
        /// Applied first in calculation order
        /// </summary>
        Flat,
        
        /// <summary>
        /// Percentage value (e.g., +10%)
        /// Applied after flat modifiers
        /// Value should be in percentage (10 for 10%, not 0.1)
        /// </summary>
        Percentage,
        
        /// <summary>
        /// Override base value completely
        /// Highest priority, ignores other modifiers
        /// </summary>
        Override
    }
}
