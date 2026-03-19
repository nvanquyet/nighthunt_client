using UnityEngine;

namespace NightHunt.Config
{
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "NightHunt/Config/Backend Config")]
    public class BackendConfig : ScriptableObject
    {
        [Header("Environment")]
        [Tooltip("Chỉ để hiển thị / debug — URL được quyết định bởi build target")]
        public EnvironmentProfile environment = EnvironmentProfile.Development;

        public enum EnvironmentProfile
        {
            Development,
            Staging,
            Production
        }

        // ── Dev settings (Editor / Development Build) ────────────────────────
        [Header("Dev Settings (Editor / Development Build)")]
        [Tooltip("Host khi chạy Editor hoặc Development build — localhost với mkcert cert")]
        public string devApiHost = "localhost:8443";

        // ── Production settings ──────────────────────────────────────────────
        [Header("Production Settings")]
        [Tooltip("Host khi build Release — domain thật với Let's Encrypt cert")]
        public string prodApiHost = "api.nighthunt.com";

        // ── Common Settings ──────────────────────────────────────────────────
        [Header("Common")]
        [Tooltip("WS endpoint path — phải khớp với server config")]
        public string wsPath = "/api/ws/game";

        [Tooltip("(Optional) Override toàn bộ WebSocket base URL, ví dụ: wss://custom-host:9000. Để trống để dùng giá trị tự động từ apiHost.")]
        public string overrideWsBaseUrl = "";

        [Tooltip("Timeout HTTP request (giây)")]
        public int requestTimeoutSeconds = 10;

        // ─────────────────────────────────────────────────────────────────────
        // Runtime resolved properties
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// API host được resolve tự động theo build target:
        ///   UNITY_EDITOR / DEVELOPMENT_BUILD → devApiHost (localhost:8443 với mkcert)
        ///   Release Build                    → prodApiHost (api.nighthunt.com Let's Encrypt)
        /// KHÔNG cần đổi asset khi build. Tự động.
        /// </summary>
        public string apiHost
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return devApiHost;
#else
                return prodApiHost;
#endif
            }
        }

        // Legacy compat — một số code cũ có thể đọc useHttps trực tiếp
        public bool useHttps => true;

        /// <summary>
        /// Full API base URL với protocol.
        /// Luôn HTTPS — Dev dùng mkcert cert, Production dùng Let's Encrypt.
        /// </summary>
        public string GetApiBaseUrl() => $"https://{apiHost}";

        /// <summary>Luôn dùng secure connection (HTTPS/WSS).</summary>
        public bool ShouldUseSecureConnection() => true;

        /// <summary>
        /// Không bao giờ bypass SSL validation.
        /// Dev:  mkcert -install đảm bảo cert được trust bởi Windows/macOS
        /// Prod: Let's Encrypt cert được trust bởi tất cả OS
        /// </summary>
        public bool ShouldBypassSslCertificateValidation() => false;

#if UNITY_EDITOR
        [ContextMenu("Log Current Config")]
        private void LogCurrentConfig()
        {
            Debug.Log($"[BackendConfig] Environment: EDITOR");
            Debug.Log($"[BackendConfig] Resolved apiHost: {apiHost}");
            Debug.Log($"[BackendConfig] Resolved URL: {GetApiBaseUrl()}");
            Debug.Log($"[BackendConfig] devApiHost: {devApiHost}");
            Debug.Log($"[BackendConfig] prodApiHost: {prodApiHost}");
        }
#endif
    }
}
