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

#if UNITY_EDITOR
        [Header("Editor Cloud Testing")]
        [Tooltip("Bật để kết nối thẳng tới cloud từ Unity Editor (dùng prodApiHost). " +
                 "Tắt để dùng localhost mkcert bình thường. KHÔNG commit flag này lên git ở trạng thái true.")]
        public bool forceProductionInEditor = false;
#endif

        [Header("Development Build Cloud Testing")]
        [Tooltip("Bật khi build Development Build mà muốn connect tới cloud thật (dùng prodApiHost thay vì localhost). " +
                 "KHÔNG bật khi test local. KHÔNG commit ở trạng thái true.")]
        public bool forceProductionOnDevBuild = false;

        // ── Production settings ──────────────────────────────────────────────
        [Header("Production Settings")]
        [Tooltip("Host khi build Release — domain thật với Let's Encrypt cert")]
        public string prodApiHost = "api.nighthunt.com";

        // ── SSL / Security ───────────────────────────────────────────────────
        [Header("SSL / Security")]
        [Tooltip("Cho phép self-signed cert (mkcert) trên cloud dev/staging server.\n" +
                 "Dùng khi server dùng IP trực tiếp (vd: 20.2.235.140) chưa có domain + Let's Encrypt.\n" +
                 "KHÔNG bật cho Production release. KHÔNG commit ở trạng thái true.")]
        public bool allowSelfSignedCert = false;

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
        ///   UNITY_EDITOR              → devApiHost  (trừ khi forceProductionInEditor = true)
        ///   DEVELOPMENT_BUILD desktop → devApiHost  (trừ khi forceProductionOnDevBuild = true)
        ///   Release Build             → prodApiHost (api.nighthunt.com Let's Encrypt)
        /// KHÔNG cần đổi asset khi build. Tự động.
        /// </summary>
        public string apiHost
        {
            get
            {
#if UNITY_EDITOR
                return forceProductionInEditor ? prodApiHost : devApiHost;
#elif DEVELOPMENT_BUILD
                return forceProductionOnDevBuild ? prodApiHost : devApiHost;
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
        /// Cloud dev/staging với IP trực tiếp: bật allowSelfSignedCert
        /// </summary>
        public bool ShouldBypassSslCertificateValidation() => allowSelfSignedCert;

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
