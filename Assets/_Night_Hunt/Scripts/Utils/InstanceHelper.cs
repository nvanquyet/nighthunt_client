using UnityEngine;
using System.IO;

namespace NightHunt.Utils
{
    /// <summary>
    /// Helper to detect and manage instance ID for ParrelSync or multi-instance testing
    /// ParrelSync sets environment variable "PARRELSYNC_CLONE_INDEX" or command line argument "-cloneIndex"
    /// Also detects from Application.dataPath which contains "_Clone" suffix for ParrelSync clones
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
            
            // Method 1: Try to get from environment variable (ParrelSync)
            // ParrelSync sets this for each clone
            string envInstanceId = System.Environment.GetEnvironmentVariable("PARRELSYNC_CLONE_INDEX");
            if (!string.IsNullOrEmpty(envInstanceId) && int.TryParse(envInstanceId, out int envId))
            {
                _instanceId = envId;
                Debug.Log($"[InstanceHelper] Detected instance ID from PARRELSYNC_CLONE_INDEX environment: {envId}");
                return envId;
            }
            
            // Method 2: Try to get from command line arguments
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
            
            // Method 3: Detect from Application.dataPath (ParrelSync clones have "_Clone" suffix)
            // Example: "C:/Project/ProjectName_Clone1" -> instance 1
            //          "C:/Project/ProjectName_Clone2" -> instance 2
            string dataPath = Application.dataPath;
            if (dataPath.Contains("_Clone"))
            {
                // Extract clone number from path
                // Path format: .../ProjectName_Clone1/Assets or .../ProjectName_Clone2/Assets
                int cloneIndex = dataPath.IndexOf("_Clone");
                if (cloneIndex >= 0)
                {
                    string afterClone = dataPath.Substring(cloneIndex + 6); // Skip "_Clone"
                    // Extract number until next '/' or '\'
                    string numberStr = "";
                    foreach (char c in afterClone)
                    {
                        if (char.IsDigit(c))
                        {
                            numberStr += c;
                        }
                        else if (c == '/' || c == '\\')
                        {
                            break;
                        }
                        else
                        {
                            break; // Non-digit, non-separator means invalid
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(numberStr) && int.TryParse(numberStr, out int cloneId))
                    {
                        _instanceId = cloneId;
                        Debug.Log($"[InstanceHelper] Detected instance ID from Application.dataPath: {cloneId} (path: {dataPath})");
                        return cloneId;
                    }
                }
            }
            
            // Method 4: Check for ParrelSync clone by checking if dataPath contains "Clones" folder
            // ParrelSync clones are usually in a "Clones" subfolder
            if (dataPath.Contains("Clones"))
            {
                // Try to extract instance number from path
                // Example: .../Clones/ProjectName_Clone1/Assets
                string[] pathParts = dataPath.Split(new char[] { '/', '\\' });
                for (int i = 0; i < pathParts.Length; i++)
                {
                    if (pathParts[i].Contains("_Clone"))
                    {
                        string clonePart = pathParts[i];
                        int cloneIndex = clonePart.IndexOf("_Clone");
                        if (cloneIndex >= 0)
                        {
                            string numberStr = clonePart.Substring(cloneIndex + 6);
                            if (int.TryParse(numberStr, out int cloneId))
                            {
                                _instanceId = cloneId;
                                Debug.Log($"[InstanceHelper] Detected instance ID from Clones folder: {cloneId} (path: {dataPath})");
                                return cloneId;
                            }
                        }
                    }
                }
            }
            
            // Default to 0 (main instance)
            _instanceId = 0;
            Debug.Log($"[InstanceHelper] No instance ID detected, using default: 0 (main instance). DataPath: {dataPath}");
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

