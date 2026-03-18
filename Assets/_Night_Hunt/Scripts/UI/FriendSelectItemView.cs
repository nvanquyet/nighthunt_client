using System;
using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Friend selection row inside the Invite Friend dialog on the Party Panel.
    ///
    /// SETUP (Prefab):
    ///   FriendSelectItem (this script)
    ///   ├── UsernameText  (TMP)
    ///   ├── StatusText    (TMP — online status)
    ///   └── InviteButton  (Button)
    /// </summary>
    public class FriendSelectItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button          inviteButton;

        private FriendResponse _friend;
        private Action<long>   _onInvite;

        public void Setup(FriendResponse friend, Action<long> onInvite)
        {
            _friend   = friend;
            _onInvite = onInvite;

            if (usernameText != null) usernameText.text = friend.username;
            if (statusText   != null) statusText.text   = friend.onlineStatus;

            if (inviteButton != null)
            {
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(() => _onInvite?.Invoke(friend.userId));
            }
        }
    }
}
