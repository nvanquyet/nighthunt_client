using UnityEngine;

namespace NightHunt.Utils
{
    /// <summary>
    /// Helper to detect and manage instance ID for ParrelSync or multi-instance testing
    /// ParrelSync sets environment variable "PARRELSYNC_CLONE_INDEX" or command line argument "-cloneIndex"
    /// </summary>
    public static class InstanceHelper
    {
        private static int? _instanceId = null;
        
        /// <summary>
        /// Get instance ID (0 for main instance, 1, 2, 3... for clones)
        /// </summary>
        public static int GetInstanceId()
        {
            if (_instanceId.HasValue)
            {
                return _instanceId.Value;
            }
            
            // Try to get from environment variable (ParrelSync)
            string envInstanceId = System.Environment.GetEnvironmentVariable("PARRELSYNC_CLONE_INDEX");
            if (!string.IsNullOrEmpty(envInstanceId) && int.TryParse(envInstanceId, out int envId))
            {
                _instanceId = envId;
                Debug.Log($"[InstanceHelper] Detected instance ID from environment: {envId}");
                return envId;
            }
            
            // Try to get from command line arguments
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-cloneIndex" || args[i] == "--cloneIndex")
                {
                    if (int.TryParse(args[i + 1], out int argId))
                    {
                        _instanceId = argId;
                        Debug.Log($"[InstanceHelper] Detected instance ID from command line: {argId}");
                        return argId;
                    }
                }
            }
            
            // Default to 0 (main instance)
            _instanceId = 0;
            Debug.Log("[InstanceHelper] No instance ID detected, using default: 0 (main instance)");
            return 0;
        }
        
        /// <summary>
        /// Get instance-specific key for PlayerPrefs
        /// </summary>
        public static string GetInstanceKey(string baseKey)
        {
            int instanceId = GetInstanceId();
            if (instanceId == 0)
            {
                // Main instance uses original key
                return baseKey;
            }
            // Clone instances use key with instance ID suffix
            return $"{baseKey}_INSTANCE_{instanceId}";
        }
        
        /// <summary>
        /// Get instance-specific file path
        /// </summary>
        public static string GetInstanceFilePath(string baseFileName)
        {
            int instanceId = GetInstanceId();
            if (instanceId == 0)
            {
                return System.IO.Path.Combine(Application.persistentDataPath, baseFileName);
            }
            // Clone instances use subdirectory
            string instanceDir = System.IO.Path.Combine(Application.persistentDataPath, $"Instance_{instanceId}");
            if (!System.IO.Directory.Exists(instanceDir))
            {
                System.IO.Directory.CreateDirectory(instanceDir);
            }
            return System.IO.Path.Combine(instanceDir, baseFileName);
        }
    }
}

