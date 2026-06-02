using System;

namespace NightHunt.Data.DTOs
{
    [Serializable]
    public class RegisterRequest
    {
        public string username;
        public string email;
        public string password;
        public string confirmPassword;
    }

    [Serializable]
    public class LoginRequest
    {
        public string identifier; // username or email
        public string password;
        public string deviceFingerprint; // Optional: device fingerprint for ban tracking
    }

    /// <summary>
    /// Sent to POST /auth/refresh-token.
    /// The client persists the refresh token locally and sends it on each app startup
    /// instead of asking the user to re-enter credentials.
    /// </summary>
    [Serializable]
    public class RefreshTokenRequest
    {
        public string refreshToken;
    }

    /// <summary>
    /// Legacy auto-login via stored access token + session ID.
    /// Kept for backward compat. New flow uses RefreshTokenRequest.
    /// </summary>
    [Serializable]
    public class AutoLoginRequest
    {
        public string accessToken;
        public string sessionId;
        public string deviceFingerprint; // Optional: device fingerprint for ban tracking
    }

    [Serializable]
    public class ChangePasswordRequest
    {
        public string oldPassword;
        public string newPassword;
        public string confirmNewPassword;
    }

    /// <summary>
    /// Returned by /auth/login, /auth/auto-login, and /auth/refresh-token.
    /// </summary>
    [Serializable]
    public class AuthResponse
    {
        public string accessToken;
        /// <summary>
        /// Long-lived token. Persisted locally (PlayerPrefs) for auto-login on next app start.
        /// Store in KEY_REFRESH_TOKEN. Delete on explicit logout.
        /// </summary>
        public string refreshToken;
        public string sessionId;
        public long   userId;
        public string username;
        public string email;
        /// <summary>
        /// String ID of the player's selected character model (e.g. "character_01").
        /// Matches CharacterDefinition.CharacterId in CharacterDatabase.
        /// NULL/empty if the player has never chosen a character → client uses index 0.
        /// </summary>
        public string selectedCharacterId;
    }

    /// <summary>
    /// Short-lived one-time credential returned by POST /api/realtime/tickets.
    /// It replaces long-lived JWT credentials in WebSocket query strings.
    /// </summary>
    [Serializable]
    public class RealtimeTicketResponse
    {
        public string ticket;
        public int expiresInSeconds;
        public string wsPath;
    }

    // ── Profile DTOs ─────────────────────────────────────────────────────────

    /// <summary>Response from GET /api/profile.</summary>
    [Serializable]
    public class ProfileResponse
    {
        public long   userId;
        public string username;
        /// <summary>Matches CharacterDefinition.CharacterId — single field, no skin ID needed.</summary>
        public string selectedCharacterId;
        public int    elo;
        public string tier;
        public int    totalWins;
        public int    totalLosses;
        /// <summary>Current in-game coin balance.</summary>
        public long   coins;
        /// <summary>Device platform: "MOBILE" | "PC" | null.</summary>
        public string platform;
    }

    /// <summary>Body for PUT /api/profile/character.</summary>
    [Serializable]
    public class UpdateCharacterRequest
    {
        public string selectedCharacterId;
    }
}
