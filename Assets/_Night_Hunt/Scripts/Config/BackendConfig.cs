using UnityEngine;

namespace NightHunt.Config
{
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "NightHunt/Config/Backend Config")]
    public class BackendConfig : ScriptableObject
    {
        [Header("Environment")]
        [Tooltip("Select environment profile")]
        public EnvironmentProfile environment = EnvironmentProfile.Development;
        
        public enum EnvironmentProfile
        {
            Development,  // localhost, HTTP, no cert validation
            Staging,      // staging server, HTTPS, cert validation
            Production    // production server, HTTPS, strict cert validation
        }
        
        [Header("API Configuration")]
        [Tooltip("API Base URL (without protocol). Example: localhost:8080 or api.yourdomain.com")]
        public string apiHost = "localhost:8080";
        
        [Tooltip("Use HTTPS instead of HTTP")]
        public bool useHttps = false;
        
        [Tooltip("Force HTTPS in production builds (ignore useHttps flag)")]
        public bool forceHttpsInProduction = true;
        
        [Header("Security Settings")]
        [Tooltip("Ignore SSL certificate validation (ONLY for local development with self-signed certs)")]
        public bool ignoreSslCertificate = true;
        
        [Tooltip("Force secure connections (HTTPS/WSS) even for localhost")]
        public bool forceSecureEverywhere = false;
        
        [Header("Development Settings")]
        [Tooltip("Allow HTTP on localhost in development")]
        public bool allowInsecureLocalhost = true;
        
        
        [Header("Timeout Settings")]
        [Tooltip("HTTP request timeout in seconds")]
        public int requestTimeoutSeconds = 10; // Changed from 30 to 10 for better UX

        [Header("WebSocket Settings")]
        [Tooltip("WS path on server")]
        public string wsPath = "/ws/game";
        [Tooltip("Override full WS base URL (e.g., wss://api.example.com). Leave empty to auto-build from apiHost/useHttps.")]
        public string overrideWsBaseUrl = "";
        [Tooltip("If true, use useHttps to decide wss/ws. If false, rely on forceSecure/allowInsecureFallback rules.")]
        public bool respectBackendHttps = true;
        [Tooltip("Force secure WebSocket (wss) even if useHttps = false (except localhost when allowInsecureFallback = true).")]
        public bool forceSecure = false;
        [Tooltip("Allow ws:// on localhost/dev even if forceSecure is true.")]
        public bool allowInsecureFallback = true;

        /// <summary>
        /// Get the full API base URL with protocol
        /// SEC-FIX: Enforce HTTPS in production builds
        /// ENV-FIX: Support environment profiles
        /// </summary>
        public string GetApiBaseUrl()
        {
            // Environment-based configuration
            bool shouldUseHttps = useHttps;
            bool isLocalhost = apiHost.Contains("localhost") || apiHost.Contains("127.0.0.1") || apiHost.Contains("0.0.0.0");
            
            // Apply environment profile
            switch (environment)
            {
                case EnvironmentProfile.Development:
                    // Development: Allow HTTP for localhost
                    if (isLocalhost && allowInsecureLocalhost)
                        shouldUseHttps = false;
                    break;
                    
                case EnvironmentProfile.Staging:
                case EnvironmentProfile.Production:
                    // Staging/Production: Force HTTPS (even for localhost if forceSecureEverywhere)
                    shouldUseHttps = true;
                    if (isLocalhost && !forceSecureEverywhere && allowInsecureLocalhost)
                        shouldUseHttps = false;
                    break;
            }
            
#if UNITY_EDITOR
            // Development build: Respect useHttps flag
            // Allow HTTP for localhost testing
#else
            // Production build: ALWAYS use HTTPS (unless explicitly allowed)
            if (forceHttpsInProduction && !isLocalhost)
            {
                shouldUseHttps = true;
                if (!useHttps)
                {
                    Debug.LogWarning("[BackendConfig] Production build - forcing HTTPS for non-localhost");
                }
            }
#endif
            
            string protocol = shouldUseHttps ? "https" : "http";
            return $"{protocol}://{apiHost}";
        }
        
        /// <summary>
        /// Check if HTTPS should be used (for WebSocket wss:// determination)
        /// </summary>
        public bool ShouldUseSecureConnection()
        {
            bool isLocalhost = apiHost.Contains("localhost") || apiHost.Contains("127.0.0.1") || apiHost.Contains("0.0.0.0");
            
            // Apply environment profile
            switch (environment)
            {
                case EnvironmentProfile.Development:
                    if (isLocalhost && allowInsecureLocalhost)
                        return false;
                    return useHttps;
                    
                case EnvironmentProfile.Staging:
                case EnvironmentProfile.Production:
                    if (isLocalhost && !forceSecureEverywhere && allowInsecureLocalhost)
                        return false;
                    return true;
            }
            
            return useHttps;
        }
    }
}

