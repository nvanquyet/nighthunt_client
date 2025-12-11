using UnityEngine;

namespace NightHunt.Data
{
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "NightHunt/Backend Config")]
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

