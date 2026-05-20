using UnityEngine;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class HomeUIActionRouter : MonoBehaviour
    {
        [SerializeField] private UINavigator navigator;
        [SerializeField] private FriendPanelView friendPanelView;

        private UINavigator Navigator
        {
            get
            {
                if (navigator == null)
                    navigator = UINavigator.Instance ?? FindFirstObjectByType<UINavigator>(FindObjectsInactive.Include);
                return navigator;
            }
        }

        public void ShowLogin() => Navigator?.ShowPanel(PanelType.Login, "Button");
        public void ShowHome() => Navigator?.ShowPanel(PanelType.Home, "Button");
        public void ShowCustomLobby() => Navigator?.ShowPanel(PanelType.CustomLobby, "Button");
        public void ShowSettings() => Navigator?.ShowPanel(PanelType.Settings, "Button");
        public void ShowMultiplayer() => ShowHome();
        public void OpenFriendsPanel() => FriendPanel?.OpenPanel();
        public void CloseFriendsPanel() => FriendPanel?.ClosePanel();
        public void ShowCampaignUnavailable() => ShowUnavailable("Campaign");
        public void Logout() => LoginView.Logout();
        public void QuitGame() => Application.Quit();

        private FriendPanelView FriendPanel
        {
            get
            {
                if (friendPanelView == null)
                    friendPanelView = FindFirstObjectByType<FriendPanelView>(FindObjectsInactive.Include);
                return friendPanelView;
            }
        }

        private static void ShowUnavailable(string featureName)
        {
            var title = string.IsNullOrWhiteSpace(featureName) ? "Unavailable" : featureName;
            ToastService.Instance?.Show(title, "This section is not part of the active NightHunt home flow.");
            Debug.LogWarning($"[HomeUIActionRouter] '{title}' is not wired into the active home flow.");
        }

        private void Reset()
        {
            navigator = FindFirstObjectByType<UINavigator>(FindObjectsInactive.Include);
            friendPanelView = FindFirstObjectByType<FriendPanelView>(FindObjectsInactive.Include);
        }
    }
}
