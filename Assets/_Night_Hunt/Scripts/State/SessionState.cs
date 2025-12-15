using System;
using UnityEngine;
using NightHunt.Utils;
using NightHunt.Core;
using NightHunt.Config;

namespace NightHunt.State
{
    public class SessionState : MonoBehaviour
    {
        public static SessionState Instance { get; private set; }

        public string AccessToken { get; private set; }
        public string SessionId { get; private set; }
        public long UserId { get; private set; }
        public string Username { get; private set; }
        public string Email { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(SessionId);

        // Events
        public event Action OnSessionStarted;
        public event Action OnSessionEnded;
        public event Action OnUserLoggedIn;
        public event Action OnUserLoggedOut;
        
        // Instance-specific keys for PlayerPrefs (to support ParrelSync/multi-instance)
        // Uses InstanceConfig to determine if multi-instance support is enabled
        private InstanceConfig ResolveConfig()
        {
            if (GameManager.Instance != null && GameManager.Instance.InstanceConfig != null)
            {
                return GameManager.Instance.InstanceConfig;
            }
            return null;
        }

        private string GetInstanceKey(string baseKey)
        {
            var cfg = ResolveConfig();
            if (cfg != null)
            {
                return cfg.GetInstanceKey(baseKey);
            }
            // Fallback: Auto-detect (enable in Editor, disable in Build)
            if (Application.isEditor)
            {
                return InstanceHelper.GetInstanceKey(baseKey);
            }
            return baseKey; // Standard keys in Build
        }
        
        private int GetInstanceId()
        {
            var cfg = ResolveConfig();
            if (cfg != null)
            {
                return cfg.GetInstanceId();
            }
            // Fallback: Auto-detect
            if (Application.isEditor)
            {
                return InstanceHelper.GetInstanceId();
            }
            return 0; // Always 0 in Build
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSession();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetSession(string accessToken, string sessionId, long userId, string username, string email)
        {
            bool wasAuthenticated = IsAuthenticated;
            
            AccessToken = accessToken;
            SessionId = sessionId;
            UserId = userId;
            Username = username;
            Email = email;
            SaveSession();
            
            // Trigger events
            if (!wasAuthenticated && IsAuthenticated)
            {
                OnSessionStarted?.Invoke();
                OnUserLoggedIn?.Invoke();
            }
        }

        public void ClearSession()
        {
            bool wasAuthenticated = IsAuthenticated;
            
            AccessToken = null;
            SessionId = null;
            UserId = 0;
            Username = null;
            Email = null;
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
            string accessTokenKey = GetInstanceKey(Common.Constants.PREFS_ACCESS_TOKEN);
            string sessionIdKey = GetInstanceKey(Common.Constants.PREFS_SESSION_ID);
            string userIdKey = GetInstanceKey(Common.Constants.PREFS_USER_ID);
            string usernameKey = GetInstanceKey(Common.Constants.PREFS_USERNAME);
            
            PlayerPrefs.SetString(accessTokenKey, AccessToken ?? "");
            PlayerPrefs.SetString(sessionIdKey, SessionId ?? "");
            PlayerPrefs.SetString(userIdKey, UserId.ToString());
            PlayerPrefs.SetString(usernameKey, Username ?? "");
            PlayerPrefs.Save();
            
            int instanceId = InstanceHelper.GetInstanceId();
            var cfg = ResolveConfig();
            bool isMultiInstance = cfg != null ? cfg.IsMultiInstanceEnabled() : Application.isEditor;
            if (isMultiInstance)
            {
                Debug.Log($"[SessionState] Session saved for instance {instanceId} (keys: {accessTokenKey}, {sessionIdKey}, {userIdKey}, {usernameKey})");
            }
            else
            {
                Debug.Log($"[SessionState] Session saved (standard keys: {accessTokenKey}, {sessionIdKey}, {userIdKey}, {usernameKey})");
            }
        }

        private void LoadSession()
        {
            int instanceId = InstanceHelper.GetInstanceId();
            string accessTokenKey = GetInstanceKey(Common.Constants.PREFS_ACCESS_TOKEN);
            string sessionIdKey = GetInstanceKey(Common.Constants.PREFS_SESSION_ID);
            string userIdKey = GetInstanceKey(Common.Constants.PREFS_USER_ID);
            string usernameKey = GetInstanceKey(Common.Constants.PREFS_USERNAME);
            
            AccessToken = PlayerPrefs.GetString(accessTokenKey, "");
            SessionId = PlayerPrefs.GetString(sessionIdKey, "");
            string userIdStr = PlayerPrefs.GetString(userIdKey, "0");
            long.TryParse(userIdStr, out long userId);
            UserId = userId;
            Username = PlayerPrefs.GetString(usernameKey, "");
            
            var cfg = ResolveConfig();
            bool isMultiInstance = cfg != null ? cfg.IsMultiInstanceEnabled() : Application.isEditor;
            if (isMultiInstance)
            {
                Debug.Log($"[SessionState] Session loaded for instance {instanceId} - IsAuthenticated: {IsAuthenticated} (keys: {accessTokenKey}, {sessionIdKey}, {userIdKey}, {usernameKey})");
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
            int instanceId = InstanceHelper.GetInstanceId();
            string accessTokenKey = GetInstanceKey(Common.Constants.PREFS_ACCESS_TOKEN);
            string sessionIdKey = GetInstanceKey(Common.Constants.PREFS_SESSION_ID);
            string userIdKey = GetInstanceKey(Common.Constants.PREFS_USER_ID);
            string usernameKey = GetInstanceKey(Common.Constants.PREFS_USERNAME);
            
            PlayerPrefs.DeleteKey(accessTokenKey);
            PlayerPrefs.DeleteKey(sessionIdKey);
            PlayerPrefs.DeleteKey(userIdKey);
            PlayerPrefs.DeleteKey(usernameKey);
            PlayerPrefs.Save();
            
            var cfg = ResolveConfig();
            bool isMultiInstance = cfg != null ? cfg.IsMultiInstanceEnabled() : Application.isEditor;
            if (isMultiInstance)
            {
                Debug.Log($"[SessionState] Session cleared for instance {instanceId} (keys: {accessTokenKey}, {sessionIdKey}, {userIdKey}, {usernameKey})");
            }
            else
            {
                Debug.Log($"[SessionState] Session cleared (standard keys)");
            }
        }
    }
}

