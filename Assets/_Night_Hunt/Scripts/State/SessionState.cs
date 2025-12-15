using UnityEngine;
using NightHunt.Utils;

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
        
        // Instance-specific keys for PlayerPrefs (to support ParrelSync/multi-instance)
        private string GetInstanceKey(string baseKey) => InstanceHelper.GetInstanceKey(baseKey);

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
            AccessToken = accessToken;
            SessionId = sessionId;
            UserId = userId;
            Username = username;
            Email = email;
            SaveSession();
        }

        public void ClearSession()
        {
            AccessToken = null;
            SessionId = null;
            UserId = 0;
            Username = null;
            Email = null;
            ClearSavedSession();
        }

        private void SaveSession()
        {
            // Use instance-specific keys to support ParrelSync/multi-instance testing
            PlayerPrefs.SetString(GetInstanceKey(Common.Constants.PREFS_ACCESS_TOKEN), AccessToken);
            PlayerPrefs.SetString(GetInstanceKey(Common.Constants.PREFS_SESSION_ID), SessionId);
            PlayerPrefs.SetString(GetInstanceKey(Common.Constants.PREFS_USER_ID), UserId.ToString());
            PlayerPrefs.SetString(GetInstanceKey(Common.Constants.PREFS_USERNAME), Username);
            PlayerPrefs.Save();
            
            Debug.Log($"[SessionState] Session saved for instance {InstanceHelper.GetInstanceId()}");
        }

        private void LoadSession()
        {
            // Use instance-specific keys to support ParrelSync/multi-instance testing
            AccessToken = PlayerPrefs.GetString(GetInstanceKey(Common.Constants.PREFS_ACCESS_TOKEN), "");
            SessionId = PlayerPrefs.GetString(GetInstanceKey(Common.Constants.PREFS_SESSION_ID), "");
            string userIdStr = PlayerPrefs.GetString(GetInstanceKey(Common.Constants.PREFS_USER_ID), "0");
            long.TryParse(userIdStr, out long userId);
            UserId = userId;
            Username = PlayerPrefs.GetString(GetInstanceKey(Common.Constants.PREFS_USERNAME), "");
            
            Debug.Log($"[SessionState] Session loaded for instance {InstanceHelper.GetInstanceId()} - IsAuthenticated: {IsAuthenticated}");
        }

        private void ClearSavedSession()
        {
            // Use instance-specific keys to support ParrelSync/multi-instance testing
            PlayerPrefs.DeleteKey(GetInstanceKey(Common.Constants.PREFS_ACCESS_TOKEN));
            PlayerPrefs.DeleteKey(GetInstanceKey(Common.Constants.PREFS_SESSION_ID));
            PlayerPrefs.DeleteKey(GetInstanceKey(Common.Constants.PREFS_USER_ID));
            PlayerPrefs.DeleteKey(GetInstanceKey(Common.Constants.PREFS_USERNAME));
            PlayerPrefs.Save();
            
            Debug.Log($"[SessionState] Session cleared for instance {InstanceHelper.GetInstanceId()}");
        }
    }
}

