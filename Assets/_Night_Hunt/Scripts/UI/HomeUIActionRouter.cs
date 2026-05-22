using UnityEngine;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class HomeUIActionRouter : MonoBehaviour
    {
        [SerializeField] private UINavigator navigator;
        [SerializeField] private FriendPanelView friendPanelView;

        [Header("Party")]
        [Tooltip("Required for party/queue check before entering Custom Mode. Auto-resolved at Reset().")]
        [SerializeField] private PartyController partyController;

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
        public void ShowPartyCustomMode()
        {
            // Delegate to PartyController so the party/queue check runs before navigation.
            // PartyController.OnPartyCustomModeClicked() prompts to cancel queue / leave
            // ranked party if needed, then calls UINavigator.ShowPanelAsync with mode+map payload.
            if (partyController != null)
            {
                partyController.OnPartyCustomModeClicked();
                return;
            }

            // Fallback: direct navigate if PartyController is not wired (no party check).
            Navigator?.ShowPanel(PanelType.PartyCustomMode, "Button");
        }
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
            friendPanelView = FindFirstObjectByType<FriendPanelView>(FindObjectsInactive.Include);            partyController = FindFirstObjectByType<PartyController>(FindObjectsInactive.Include);        }
    }
}
