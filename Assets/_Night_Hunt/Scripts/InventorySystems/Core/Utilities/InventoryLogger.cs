using UnityEngine;

namespace NightHunt.Inventory.Core.Utilities
{
    /// <summary>
    /// Centralized logging utility for inventory system.
    /// Provides consistent log formatting and conditional logging based on debug flags.
    /// </summary>
    public static class InventoryLogger
    {
        /// <summary>
        /// Logs an informational message if debug logging is enabled.
        /// </summary>
        /// <param name="className">Name of the class logging the message</param>
        /// <param name="message">Message to log</param>
        /// <param name="enableDebug">Whether debug logging is enabled for this component</param>
        public static void Log(string className, string message, bool enableDebug = false)
        {
            if (enableDebug)
            {
                Debug.Log($"[{className}] {message}");
            }
        }
        
        /// <summary>
        /// Logs a warning message if debug logging is enabled.
        /// </summary>
        /// <param name="className">Name of the class logging the message</param>
        /// <param name="message">Warning message to log</param>
        /// <param name="enableDebug">Whether debug logging is enabled for this component</param>
        public static void LogWarning(string className, string message, bool enableDebug = false)
        {
            if (enableDebug)
            {
                Debug.LogWarning($"[{className}] {message}");
            }
        }
        
        /// <summary>
        /// Logs an error message. Errors are always logged regardless of debug flag.
        /// </summary>
        /// <param name="className">Name of the class logging the message</param>
        /// <param name="message">Error message to log</param>
        public static void LogError(string className, string message)
        {
            Debug.LogError($"[{className}] {message}");
        }
    }
}
