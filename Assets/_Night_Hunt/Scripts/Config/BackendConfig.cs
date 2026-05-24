using UnityEngine;

namespace NightHunt.Config
{
    // ══════════════════════════════════════════════════════════════════════════
    // BackendConfig — static class. No .asset file, no ScriptableObject.
    // Connection endpoints baked at compile-time.
    // To switch between dev/prod: change ForceProductionInEditor below.
    // ══════════════════════════════════════════════════════════════════════════

    public static class BackendConfig
    {
        // ── Hosts ─────────────────────────────────────────────────────────────
        private const string DevApiHost  = "localhost:8443";
        private const string ProdApiHost = "vawnwuyest.me";

        // ── Toggle flags — change locally if needed; do NOT commit as true ───
        // Set ForceProductionInEditor = true to connect to prod from Editor.
        private const bool ForceProductionInEditor   = true;
        // Set ForceProductionOnDevBuild = true for Dev Builds pointing to prod.
        private const bool ForceProductionOnDevBuild = false;

        // Allow self-signed cert on cloud dev/staging with IP-direct (no domain).
        // Do NOT enable for Production. Do NOT commit as true.
        public const bool AllowSelfSignedCert = false;

        // ── WS / HTTP ─────────────────────────────────────────────────────────
        public const string WsPath              = "/api/ws/game";
        public const string OverrideWsBaseUrl   = ""; // Leave empty to use ApiHost
        public const int    RequestTimeoutSeconds = 10;

        // ── Resolved at runtime ───────────────────────────────────────────────
        /// <summary>
        /// API host resolved by build target:
        ///   UNITY_EDITOR              → DevApiHost  (unless ForceProductionInEditor)
        ///   DEVELOPMENT_BUILD         → DevApiHost  (unless ForceProductionOnDevBuild)
        ///   Release Build             → ProdApiHost
        /// </summary>
        public static string ApiHost
        {
            get
            {
#if UNITY_EDITOR
                return ForceProductionInEditor ? ProdApiHost : DevApiHost;
#elif DEVELOPMENT_BUILD
                return ForceProductionOnDevBuild ? ProdApiHost : DevApiHost;
#else
                return ProdApiHost;
#endif
            }
        }

        public static string GetApiBaseUrl()                        => $"https://{ApiHost}";
        public static bool   ShouldUseSecureConnection()            => true;
        public static bool   ShouldBypassSslCertificateValidation() => AllowSelfSignedCert;

        // Legacy compat
        public static bool useHttps => true;
    }
}
