using System;
using System.Text;
using UnityEngine;

namespace NightHunt.Utils
{
    /// <summary>
    /// JwtUtil - Client-side JWT validation utility.
    /// 
    /// Features:
    /// - Parse JWT payload (Base64URL decoding)
    /// - Validate token expiry (exp claim)
    /// - Extract user data (userId, username)
    /// 
    /// ⚠️ Security Note:
    /// - Client does NOT verify signature (only backend has secret key)
    /// - This is ONLY for local expiry checking (avoid API calls with expired tokens)
    /// - Backend always validates signature + expiry (never trust client-side validation)
    /// </summary>
    public static class JwtUtil
    {
        [Serializable]
        private class JwtPayload
        {
            public string sub;     // Subject (userId)
            public string username;
            public long exp;       // Expiry timestamp (Unix seconds)
            public long iat;       // Issued at timestamp (Unix seconds)
        }

        /// <summary>
        /// Check if JWT is expired (client-side expiry check only)
        /// </summary>
        public static bool IsExpired(string jwt)
        {
            try
            {
                var payload = ParsePayload(jwt);
                if (payload == null || payload.exp == 0)
                    return true; // Invalid token → treat as expired

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return payload.exp <= now; // exp <= now → expired
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JwtUtil] Failed to parse JWT: {ex.Message}");
                return true; // Parse failure → treat as expired
            }
        }

        /// <summary>
        /// Check if JWT will expire within the specified seconds
        /// Useful for proactive token refresh (e.g., refresh 5 minutes before expiry)
        /// </summary>
        public static bool WillExpireSoon(string jwt, int thresholdSeconds = 300)
        {
            try
            {
                var payload = ParsePayload(jwt);
                if (payload == null || payload.exp == 0)
                    return true;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return (payload.exp - now) <= thresholdSeconds;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get remaining time (in seconds) until token expires
        /// Returns 0 if expired or invalid
        /// </summary>
        public static long GetRemainingSeconds(string jwt)
        {
            try
            {
                var payload = ParsePayload(jwt);
                if (payload == null || payload.exp == 0)
                    return 0;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long remaining = payload.exp - now;
                return remaining > 0 ? remaining : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Extract userId from JWT (sub claim)
        /// </summary>
        public static string GetUserId(string jwt)
        {
            try
            {
                var payload = ParsePayload(jwt);
                return payload?.sub ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Extract username from JWT
        /// </summary>
        public static string GetUsername(string jwt)
        {
            try
            {
                var payload = ParsePayload(jwt);
                return payload?.username ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Parse JWT payload (decode Base64URL)
        /// </summary>
        private static JwtPayload ParsePayload(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
                return null;

            string[] parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                Debug.LogWarning("[JwtUtil] Invalid JWT format (expected 3 parts)");
                return null;
            }

            try
            {
                // Decode Base64URL (JWT part 1 = payload)
                string payloadJson = Base64UrlDecode(parts[1]);
                JwtPayload payload = JsonUtility.FromJson<JwtPayload>(payloadJson);
                return payload;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JwtUtil] Failed to decode payload: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Base64URL decode (JWT uses Base64URL, not standard Base64)
        /// </summary>
        private static string Base64UrlDecode(string base64Url)
        {
            // Base64URL → Base64: replace - with +, _ with /, add padding
            string base64 = base64Url.Replace('-', '+').Replace('_', '/');

            // Add padding (Base64 requires length % 4 == 0)
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
