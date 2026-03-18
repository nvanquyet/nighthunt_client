using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NightHunt.Utils
{
    /// <summary>
    /// SecureStorage - AES-256 encrypted PlayerPrefs alternative.
    /// 
    /// Security Features:
    /// - AES-256-CBC encryption for all stored values
    /// - Device-unique key derivation (prevents token copy to other devices)
    /// - Base64 encoding for safe storage
    /// 
    /// Usage:
    ///   SecureStorage.SetString("RefreshToken", token);
    ///   string token = SecureStorage.GetString("RefreshToken");
    /// 
    /// ⚠️ Security Note:
    /// - This protects against casual inspection (plaintext file reading)
    /// - Advanced attackers can still decompile code to extract key derivation logic
    /// - For production-grade security, use iOS Keychain / Android Keystore
    /// </summary>
    public static class SecureStorage
    {
        private static byte[] _key;
        private static byte[] _iv;
        private static bool _initialized;

        private static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Derive encryption key from device unique identifier
                // This ensures tokens encrypted on Device A cannot be decrypted on Device B
                string deviceId = SystemInfo.deviceUniqueIdentifier;
                
                // Key derivation: SHA-256(deviceId + salt)
                using (SHA256 sha256 = SHA256.Create())
                {
                    // Key: 256-bit (32 bytes)
                    byte[] keyHash = sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(deviceId + "NightHunt_AES_Key_v1"));
                    _key = new byte[32];
                    Array.Copy(keyHash, _key, 32);

                    // IV: 128-bit (16 bytes) - first 16 bytes of different hash
                    byte[] ivHash = sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(deviceId + "NightHunt_AES_IV_v1"));
                    _iv = new byte[16];
                    Array.Copy(ivHash, _iv, 16);
                }

                _initialized = true;
                Debug.Log("[SecureStorage] Initialized with device-unique keys");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Store encrypted string value
        /// </summary>
        public static void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[SecureStorage] Key cannot be null or empty");
                return;
            }

            try
            {
                Initialize();

                if (string.IsNullOrEmpty(value))
                {
                    // Store empty string as-is (no encryption needed)
                    PlayerPrefs.SetString(key, "");
                    PlayerPrefs.Save();
                    return;
                }

                // Encrypt value
                byte[] encrypted = EncryptStringToBytes(value, _key, _iv);
                string base64 = Convert.ToBase64String(encrypted);

                // Store encrypted value
                PlayerPrefs.SetString(key, base64);
                PlayerPrefs.Save();

                Debug.Log($"[SecureStorage] Encrypted and stored: {key}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Failed to encrypt {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieve and decrypt string value
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[SecureStorage] Key cannot be null or empty");
                return defaultValue;
            }

            try
            {
                Initialize();

                string base64 = PlayerPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(base64))
                    return defaultValue;

                // Decrypt value
                byte[] encrypted = Convert.FromBase64String(base64);
                string decrypted = DecryptStringFromBytes(encrypted, _key, _iv);

                return decrypted;
            }
            catch (FormatException)
            {
                // Invalid Base64 → corrupted data or old plaintext value
                Debug.LogWarning($"[SecureStorage] Invalid encrypted data for {key} (corrupted or plaintext legacy data)");
                return defaultValue;
            }
            catch (CryptographicException)
            {
                // Decryption failed → wrong key or corrupted data
                Debug.LogWarning($"[SecureStorage] Decryption failed for {key} (key mismatch or corruption)");
                return defaultValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Failed to decrypt {key}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Delete key from secure storage
        /// </summary>
        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log($"[SecureStorage] Deleted: {key}");
        }

        /// <summary>
        /// Check if key exists
        /// </summary>
        public static bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        // ────────────────────────────────────────────────────────────────────
        // Encryption / Decryption (AES-256-CBC)
        // ────────────────────────────────────────────────────────────────────

        private static byte[] EncryptStringToBytes(string plainText, byte[] key, byte[] iv)
        {
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException(nameof(iv));

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (MemoryStream msEncrypt = new MemoryStream())
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    csEncrypt.Write(plainBytes, 0, plainBytes.Length);
                    csEncrypt.FlushFinalBlock();
                    return msEncrypt.ToArray();
                }
            }
        }

        private static string DecryptStringFromBytes(byte[] cipherText, byte[] key, byte[] iv)
        {
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException(nameof(cipherText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException(nameof(iv));

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
    }
}
