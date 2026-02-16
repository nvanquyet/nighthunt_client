namespace GameplaySystems.Stat
{
    /// <summary>
    /// Defines how a modifier affects a stat
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