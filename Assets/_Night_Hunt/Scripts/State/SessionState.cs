using UnityEngine;

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
            PlayerPrefs.SetString(Common.Constants.PREFS_ACCESS_TOKEN, AccessToken);
            PlayerPrefs.SetString(Common.Constants.PREFS_SESSION_ID, SessionId);
            PlayerPrefs.SetString(Common.Constants.PREFS_USER_ID, UserId.ToString());
            PlayerPrefs.SetString(Common.Constants.PREFS_USERNAME, Username);
            PlayerPrefs.Save();
        }

        private void LoadSession()
        {
            AccessToken = PlayerPrefs.GetString(Common.Constants.PREFS_ACCESS_TOKEN, "");
            SessionId = PlayerPrefs.GetString(Common.Constants.PREFS_SESSION_ID, "");
            string userIdStr = PlayerPrefs.GetString(Common.Constants.PREFS_USER_ID, "0");
            long.TryParse(userIdStr, out long userId);
            UserId = userId;
            Username = PlayerPrefs.GetString(Common.Constants.PREFS_USERNAME, "");
        }

        private void ClearSavedSession()
        {
            PlayerPrefs.DeleteKey(Common.Constants.PREFS_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(Common.Constants.PREFS_SESSION_ID);
            PlayerPrefs.DeleteKey(Common.Constants.PREFS_USER_ID);
            PlayerPrefs.DeleteKey(Common.Constants.PREFS_USERNAME);
            PlayerPrefs.Save();
        }
    }
}

