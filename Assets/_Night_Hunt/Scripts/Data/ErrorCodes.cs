namespace NightHunt.Data
{
    /// <summary>
    /// Error code constants matching backend ErrorCodes.java.
    /// Use these instead of magic strings when checking errorCode fields.
    /// </summary>
    public static class ErrorCodes
    {
        // Auth
        public const string AUTH_SESSION_EXPIRED   = "AUTH_007";
        public const string AUTH_FORCE_LOGOUT      = "AUTH_008";
        public const string AUTH_ACCOUNT_BANNED    = "AUTH_010";
        public const string AUTH_IP_BANNED         = "AUTH_011";
        public const string AUTH_DEVICE_BANNED     = "AUTH_012";
        /// <summary>
        /// Fired when a login attempt is made while an existing session is active.
        /// The old session is terminated; the new client must retry once.
        /// </summary>
        public const string AUTH_SESSION_CONFLICT  = "AUTH_014";

        // Rate limiting
        public const string RATE_LIMIT_EXCEEDED   = "RATE_001";
    }
}
