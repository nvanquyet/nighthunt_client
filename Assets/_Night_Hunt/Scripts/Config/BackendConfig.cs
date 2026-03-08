using UnityEngine;

namespace NightHunt.Config
{
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "NightHunt/Config/Backend Config")]
    public class BackendConfig : ScriptableObject
    {
        [Header("API Configuration")]
        [Tooltip("API Base URL (without protocol). Example: localhost:8080 or api.yourdomain.com")]
        public string apiHost = "localhost:8080";
        
        [Tooltip("Use HTTPS instead of HTTP")]
        public bool useHttps = false;
        
        [Header("Development Settings")]
        [Tooltip("Ignore SSL certificate validation (only for local development)")]
        public bool ignoreSslCertificate = true;
        
        [Header("Timeout Settings")]
        public int requestTimeoutSeconds = 30;

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
        /// </summary>
        public string GetApiBaseUrl()
        {
            string protocol = useHttps ? "https" : "http";
            return $"{protocol}://{apiHost}";
        }
    }
}

