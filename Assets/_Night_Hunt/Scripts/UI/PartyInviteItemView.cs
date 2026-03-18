using System;
using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Single incoming party invite row in the No-Party panel.
    ///
    /// SETUP (Prefab):
    ///   PartyInviteItem (this script)
    ///   ├── InviterNameText  (TMP — "{name} invited you to party")
    ///   ├── AcceptButton     (Button)
    ///   └── RejectButton     (Button)
    /// </summary>
    public class PartyInviteItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI inviterNameText;
        [SerializeField] private Button          acceptButton;
        [SerializeField] private Button          rejectButton;

        private PartyInviteResponse _invite;
        private Action<long>        _onAccept;
        private Action<long>        _onReject;

        public void Setup(PartyInviteResponse invite,
                          Action<long> onAccept, Action<long> onReject)
        {
            _invite   = invite;
            _onAccept = onAccept;
            _onReject = onReject;

            if (inviterNameText != null)
                inviterNameText.text = $"{invite.inviterUsername} invited you to party";

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(() => _onAccept?.Invoke(invite.invitationId));
            }

            if (rejectButton != null)
            {
                rejectButton.onClick.RemoveAllListeners();
                rejectButton.onClick.AddListener(() => _onReject?.Invoke(invite.invitationId));
            }
        }
    }
}
