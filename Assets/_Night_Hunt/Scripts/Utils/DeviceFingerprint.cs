using UnityEngine;
using System.Security.Cryptography;
using System.Text;

namespace NightHunt.Utils
{
    /// <summary>
    /// Device fingerprint utility
    /// Generates a unique fingerprint for the device
    /// </summary>
    public static class DeviceFingerprint
    {
        private static string cachedFingerprint = null;

        /// <summary>
        /// Get device fingerprint
        /// Combines multiple device identifiers to create a unique fingerprint
        /// </summary>
        public static string GetFingerprint()
        {
            if (!string.IsNullOrEmpty(cachedFingerprint))
            {
                return cachedFingerprint;
            }

            // Collect device information
            StringBuilder sb = new StringBuilder();
            
            // Device unique identifier (primary)
            sb.Append(SystemInfo.deviceUniqueIdentifier);
            
            // Operating system
            sb.Append(SystemInfo.operatingSystem);
            
            // Device model
            sb.Append(SystemInfo.deviceModel);
            
            // Processor type
            sb.Append(SystemInfo.processorType);
            
            // Graphics device name
            sb.Append(SystemInfo.graphicsDeviceName);
            
            // Screen resolution
            sb.Append(Screen.width);
            sb.Append("x");
            sb.Append(Screen.height);
            
            // System language
            sb.Append(Application.systemLanguage);
            
            // Create hash from collected information
            string combined = sb.ToString();
            cachedFingerprint = HashString(combined);
            
            return cachedFingerprint;
        }

        /// <summary>
        /// Hash string using SHA256
        /// </summary>
        private static string HashString(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Clear cached fingerprint (for testing)
        /// </summary>
        public static void ClearCache()
        {
            cachedFingerprint = null;
        }
    }
}

