using System.Diagnostics;
using UnityEngine;

namespace NightHunt.Utils
{
    /// <summary>
    /// ConditionalLogger - Conditional debug logging that gets stripped in production builds
    /// 
    /// Usage:
    ///   ConditionalLogger.Log("Service", "Message");         // Stripped in Release
    ///   ConditionalLogger.LogWarning("Service", "Warning");  // Stripped in Release  
    ///   ConditionalLogger.LogError("Service", "Error");      // Always logged (keep errors)
    /// 
    /// Benefits:
    /// - Zero GC allocation in production builds (code completely removed)
    /// - No performance cost in production (compiler strips calls)
    /// - Errors always logged for production debugging
    /// - Easy to enable/disable per-category
    /// </summary>
    public static class ConditionalLogger
    {
        // ════════════════════════════════════════════════════════════════════════
        // Configuration - Enable/Disable per category
        // ════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Master switch - disable ALL logging in production
        /// Default: true (logs enabled in Development builds)
        /// Set to false to completely disable all logs
        /// </summary>
        public static bool EnableLogging = true;

        /// <summary>
        /// Category enable flags - fine-grained control
        /// Set to false to disable specific categories
        /// </summary>
        public static bool EnableWebSocketLogs = true;
        public static bool EnableServiceLogs = true;
        public static bool EnableNetworkLogs = true;
        public static bool EnableUILogs = true;
        public static bool EnableGameplayLogs = true;

        // ════════════════════════════════════════════════════════════════════════
        // Log Methods - Stripped in Production Builds
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Standard log - STRIPPED in production builds (DEVELOPMENT_BUILD not defined)
        /// Use for verbose logging that should not appear in production
        /// Zero cost in production - compiler completely removes these calls
        /// </summary>
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Log(string category, string message)
        {
            if (!EnableLogging || !ShouldLog(category)) return;
            UnityEngine.Debug.Log($"[{category}] {message}");
        }

        /// <summary>
        /// Warning log - STRIPPED in production builds
        /// Use for non-critical warnings (e.g., "already connected", "invalid state")
        /// </summary>
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void LogWarning(string category, string message)
        {
            if (!EnableLogging || !ShouldLog(category)) return;
            UnityEngine.Debug.LogWarning($"[{category}] {message}");
        }

        /// <summary>
        /// Error log - ALWAYS LOGGED (even in production)
        /// Use for critical errors that need to be tracked in production
        /// NOT stripped - errors are important for crash reports and analytics
        /// </summary>
        public static void LogError(string category, string message)
        {
            if (!EnableLogging) return;
            UnityEngine.Debug.LogError($"[{category}] {message}");
        }

        /// <summary>
        /// Error log with exception - ALWAYS LOGGED
        /// Includes full exception stack trace
        /// </summary>
        public static void LogError(string category, string message, System.Exception ex)
        {
            if (!EnableLogging) return;
            UnityEngine.Debug.LogError($"[{category}] {message}\nException: {ex.Message}\n{ex.StackTrace}");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Category Filtering
        // ════════════════════════════════════════════════════════════════════════

        private static bool ShouldLog(string category)
        {
            // Map category to flag
            switch (category?.ToLower())
            {
                case "websocket":
                case "gamewebsocketservice":
                    return EnableWebSocketLogs;

                case "partyservice":
                case "friendservice":
                case "authservice":
                case "roomservice":
                case "matchmakingservice":
                    return EnableServiceLogs;

                case "network":
                case "backendclient":
                case "backendhttpclient":
                    return EnableNetworkLogs;

                case "ui":
                case "loadingoverlay":
                case "partypanel":
                case "friendpanel":
                    return EnableUILogs;

                case "gameplay":
                case "gamemanager":
                case "sessionstate":
                    return EnableGameplayLogs;

                default:
                    return true; // Unknown categories always enabled
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Runtime Configuration Methods
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Disable ALL logging (including errors)
        /// Useful for performance testing
        /// </summary>
        public static void DisableAll()
        {
            EnableLogging = false;
        }

        /// <summary>
        /// Enable all logging
        /// </summary>
        public static void EnableAll()
        {
            EnableLogging = true;
            EnableWebSocketLogs = true;
            EnableServiceLogs = true;
            EnableNetworkLogs = true;
            EnableUILogs = true;
            EnableGameplayLogs = true;
        }

        /// <summary>
        /// Errors-only mode - only log errors, suppress all other logs
        /// Recommended for production builds
        /// </summary>
        public static void ErrorsOnly()
        {
            EnableLogging = true;
            EnableWebSocketLogs = false;
            EnableServiceLogs = false;
            EnableNetworkLogs = false;
            EnableUILogs = false;
            EnableGameplayLogs = false;
        }

        /// <summary>
        /// Configure logging based on build type
        /// Call this from GameManager.Awake()
        /// </summary>
        public static void ConfigureForBuild()
        {
#if UNITY_EDITOR
            // Editor: Enable all logs
            EnableAll();
#elif DEVELOPMENT_BUILD
            // Development build: Enable all logs
            EnableAll();
#else
            // Production build: Errors only
            ErrorsOnly();
#endif
        }
    }
}
