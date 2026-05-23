using System;
using UnityEngine;
using NightHunt.Utils;
using NightHunt.Core;
using NightHunt.Config;

namespace NightHunt.State
{
    public class SessionState : SingletonPersistent<SessionState>
    {

        public string AccessToken { get; private set; }
        public string SessionId { get; private set; }
        public long UserId { get; private set; }
        public string Username { get; private set; }
        // ⚠️ REMOVED: Email removed for security (PII should not be stored client-side)
        // Use ProfileService.GetAccountInfo() when needed (e.g., Account Settings screen)

        /// <summary>
        /// String ID of the player's selected character model (e.g. "character_01").
        /// Matches CharacterDefinition.CharacterId in CharacterDatabase.
        /// Persisted to PlayerPrefs["SelectedCharacterId"] so ClientNetworkHandler can read it.
        /// </summary>
        public string SelectedCharacterId { get; private set; }

        public long   Coins { get; private set; }
        public int    Elo   { get; private set; }
        public string Tier  { get; private set; }

        public event Action OnProfileUpdated;

        public bool IsAuthenticated
        {
            get
            {
                if (string.IsNullOrEmpty(AccessToken) || string.IsNullOrEmpty(SessionId))
                    return false;

                // SEC-FIX: Validate JWT expiry client-side (avoid API calls with expired tokens)
                if (JwtUtil.IsExpired(AccessToken))
                {
                    Debug.LogWarning("[SessionState] Access token expired");
                    return false;
                }

                return true;
            }
        }

        // Events
        public event Action OnSessionStarted;
        public event Action OnSessionEnded;
        public event Action OnUserLoggedIn;
        public event Action OnUserLoggedOut;
        
        // Instance-specific keys for PlayerPrefs (to support ParrelSync/multi-instance)
        private string GetInstanceKey(string baseKey) => InstanceConfig.GetInstanceKey(baseKey);
        private int    GetInstanceId()                => InstanceConfig.GetInstanceId();

        protected override void OnSingletonAwake()
        {
            LoadSession();
        }

        public void SetSession(string accessToken, string sessionId, long userId, string username,
                               string selectedCharacterId = null)
        {
            bool wasAuthenticated = IsAuthenticated;

            AccessToken          = accessToken;
            SessionId            = sessionId;
            UserId               = userId;
            Username             = username;
            // Email removed (see property comment above)
            SelectedCharacterId  = selectedCharacterId;
            SaveSession();

            // Sync selectedCharacterId to the PlayerPrefs key that ClientNetworkHandler reads
            SyncCharacterIdToPrefs(selectedCharacterId);

            // Trigger events
            if (!wasAuthenticated && IsAuthenticated)
            {
                OnSessionStarted?.Invoke();
                OnUserLoggedIn?.Invoke();
            }
        }

        /// <summary>
        /// Updates only the selected character without touching auth tokens.
        /// Called by ProfileManager after a successful PUT /api/profile/character.
        /// </summary>
        public void SetSelectedCharacterId(string characterId)
        {
            SelectedCharacterId = characterId;
            SyncCharacterIdToPrefs(characterId);
        }

        /// <summary>
        /// Syncs full profile data from a ProfileResponse.
        /// Called by ProfileManager after GET /api/profile.
        /// </summary>
        public void SetProfileData(long coins, int elo, string tier, string selectedCharacterId = null)
        {
            Coins = coins;
            Elo   = elo;
            Tier  = tier ?? Tier;
            if (selectedCharacterId != null)
                SetSelectedCharacterId(selectedCharacterId);
            OnProfileUpdated?.Invoke();
        }

        /// <summary>Directly updates the coin balance (e.g. after match_ended WS event).</summary>
        public void SetCoins(long coins)
        {
            Coins = coins;
            OnProfileUpdated?.Invoke();
        }

        public void ClearSession()
        {
            bool wasAuthenticated = IsAuthenticated;

            AccessToken          = null;
            SessionId            = null;
            UserId               = 0;
            Username             = null;
            // Email removed
            SelectedCharacterId  = null;
            Coins                = 0;
            Elo                  = 0;
            Tier                 = null;
            ClearSavedSession();

            // Trigger events
            if (wasAuthenticated)
            {
                OnSessionEnded?.Invoke();
                OnUserLoggedOut?.Invoke();
            }
        }

        private void SaveSession()
        {
            // SEC-FIX: DO NOT persist AccessToken (short-lived, memory only)
            // Only persist: SessionId, UserId, Username for reconnect
            string sessionIdKey   = GetInstanceKey(Common.Constants.PREFS_SESSION_ID);
            string userIdKey      = GetInstanceKey(Common.Constants.PREFS_USER_ID);
            string usernameKey    = GetInstanceKey(Common.Constants.PREFS_USERNAME);

            // AccessToken stays in memory only (expires in 1h anyway)
            PlayerPrefs.SetString(sessionIdKey,   SessionId   ?? "");
            PlayerPrefs.SetString(userIdKey,       UserId.ToString());
            PlayerPrefs.SetString(usernameKey,     Username    ?? "");
            PlayerPrefs.Save();
            
            int instanceId = InstanceHelper.GetInstanceId();
            bool isMultiInstance = InstanceConfig.IsMultiInstanceEnabled();
            if (isMultiInstance)
            {
                Debug.Log($"[SessionState] Session saved for instance {instanceId} (keys: {sessionIdKey}, {userIdKey}, {usernameKey})");
            }
            else
            {
                Debug.Log($"[SessionState] Session saved (standard keys: {sessionIdKey}, {userIdKey}, {usernameKey})");
            }
        }

        private void LoadSession()
        {
            int instanceId = InstanceHelper.GetInstanceId();
            // SEC-FIX: AccessToken no longer loaded from PlayerPrefs (not persisted)
            string sessionIdKey = GetInstanceKey(Common.Constants.PREFS_SESSION_ID);
            string userIdKey = GetInstanceKey(Common.Constants.PREFS_USER_ID);
            string usernameKey = GetInstanceKey(Common.Constants.PREFS_USERNAME);
            
            // AccessToken = null (will be set by AutoLogin via refresh token)
            AccessToken = null;
            SessionId = PlayerPrefs.GetString(sessionIdKey, "");
            string userIdStr = PlayerPrefs.GetString(userIdKey, "0");
            long.TryParse(userIdStr, out long userId);
            UserId = userId;
            Username = PlayerPrefs.GetString(usernameKey, "");
            // Restore last-known character so UI can show avatar immediately (before profile API returns)
            SelectedCharacterId = PlayerPrefs.GetString(Common.Constants.PREFS_SELECTED_CHARACTER_ID, "");
            
            bool isMultiInstance = InstanceConfig.IsMultiInstanceEnabled();
            if (isMultiInstance)
            {
                Debug.Log($"[SessionState] Session loaded for instance {instanceId} - IsAuthenticated: {IsAuthenticated} (keys: {sessionIdKey}, {userIdKey}, {usernameKey})");
                if (IsAuthenticated)
                {
                    Debug.Log($"[SessionState] Instance {instanceId} has valid session - UserId: {UserId}, Username: {Username}");
                }
            }
            else
            {
                Debug.Log($"[SessionState] Session loaded - IsAuthenticated: {IsAuthenticated} (standard keys)");
                if (IsAuthenticated)
                {
                    Debug.Log($"[SessionState] Valid session - UserId: {UserId}, Username: {Username}");
                }
            }
        }

        private void ClearSavedSession()
        {
            int    instanceId    = InstanceHelper.GetInstanceId();
            // SEC-FIX: No accessTokenKey to delete (not persisted anymore)
            string sessionIdKey   = GetInstanceKey(Common.Constants.PREFS_SESSION_ID);
            string userIdKey      = GetInstanceKey(Common.Constants.PREFS_USER_ID);
            string usernameKey    = GetInstanceKey(Common.Constants.PREFS_USERNAME);

            // No accessToken to clear (not persisted)
            PlayerPrefs.DeleteKey(sessionIdKey);
            PlayerPrefs.DeleteKey(userIdKey);
            PlayerPrefs.DeleteKey(usernameKey);
            // Also clear the character selection so next session starts fresh
            PlayerPrefs.DeleteKey(Common.Constants.PREFS_SELECTED_CHARACTER_ID);
            PlayerPrefs.Save();

            bool isMultiInstance = InstanceConfig.IsMultiInstanceEnabled();
            if (isMultiInstance)
                Debug.Log($"[SessionState] Session cleared for instance {instanceId}");
            else
                Debug.Log("[SessionState] Session cleared (standard keys)");
        }

        /// <summary>
        /// Writes selectedCharacterId to the flat PlayerPrefs key that
        /// ClientNetworkHandler.GetLocalPlayerData() reads at connect time.
        /// Uses the non-instanced key name intentionally (both clone instances
        /// should use the same character unless you want per-instance overrides).
        /// </summary>
        private static void SyncCharacterIdToPrefs(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId))
            {
                PlayerPrefs.SetString(Common.Constants.PREFS_SELECTED_CHARACTER_ID, characterId);
                Debug.Log($"[SessionState] SelectedCharacterId synced to PlayerPrefs: {characterId}");
            }
            // If null/empty, leave existing value so the last-known character is still used.
            PlayerPrefs.Save();
        }
    }
}
