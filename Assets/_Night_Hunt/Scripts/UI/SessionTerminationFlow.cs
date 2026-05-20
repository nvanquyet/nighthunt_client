using NightHunt.State;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// Single UI path for force logout and session expiry. Multiple systems may observe
    /// the same backend/socket event; this guard prevents duplicate modal chains.
    /// </summary>
    public static class SessionTerminationFlow
    {
        private static bool s_handling;

        public static void ShowAndLogout(string title, string message)
        {
            if (s_handling)
                return;

            s_handling = true;
            RoomState.Instance?.ClearRoom();
            RoomState.Instance?.ClearNetworkSession();

            var modal = GameModalWindow.Instance;
            if (modal != null)
            {
                modal.ShowNotice(
                    title,
                    message,
                    closeText: "OK",
                    onClose: CompleteLogout);
            }
            else
            {
                Debug.LogWarning("[SessionTerminationFlow] GameModalWindow missing; logging out immediately.");
                CompleteLogout();
            }
        }

        public static void ResetGuard()
        {
            s_handling = false;
        }

        private static void CompleteLogout()
        {
            s_handling = false;
            LoginView.Logout();
        }
    }
}
