using System;
using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Single row in the Requests tab.
    /// Supports both incoming (Accept / Decline) and outgoing (Cancel) layouts.
    ///
    /// SETUP (Prefab — reuse one prefab, hide either Accept/Decline or Cancel via SetupXxx):
    ///   FriendRequestItem (this script)
    ///   ├── SenderNameText   (TMP)
    ///   ├── StatusText       (TMP — "Pending" / expires info)
    ///   ├── Btn_Accept
    ///   ├── Btn_Decline
    ///   └── Btn_Cancel
    /// </summary>
    public class FriendRequestItemView : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TextMeshProUGUI senderNameText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Incoming Actions")]
        [SerializeField] private Button btn_Accept;
        [SerializeField] private Button btn_Decline;

        [Header("Outgoing Actions")]
        [SerializeField] private Button btn_Cancel;

        private long _requestId;

        private void Awake()
        {
            // Listeners wired via SetupXxx
        }

        /// <summary>Configure as an incoming request row (Accept + Decline).</summary>
        public void SetupIncoming(FriendRequestResponse req,
                                   Action<long> onAccept,
                                   Action<long> onDecline)
        {
            _requestId = req.requestId;

            if (senderNameText != null) senderNameText.text = req.requesterUsername;
            if (statusText     != null) statusText.text     = req.requestStatus;

            if (btn_Accept != null)
            {
                btn_Accept.gameObject.SetActive(true);
                btn_Accept.onClick.RemoveAllListeners();
                btn_Accept.onClick.AddListener(() => onAccept?.Invoke(_requestId));
            }
            if (btn_Decline != null)
            {
                btn_Decline.gameObject.SetActive(true);
                btn_Decline.onClick.RemoveAllListeners();
                btn_Decline.onClick.AddListener(() => onDecline?.Invoke(_requestId));
            }
            if (btn_Cancel != null) btn_Cancel.gameObject.SetActive(false);
        }

        /// <summary>Configure as an outgoing request row (Cancel only).</summary>
        public void SetupOutgoing(FriendRequestResponse req,
                                   Action<long> onCancel)
        {
            _requestId = req.requestId;

            if (senderNameText != null) senderNameText.text = req.addresseeUsername;
            if (statusText     != null) statusText.text     = req.requestStatus;

            if (btn_Cancel != null)
            {
                btn_Cancel.gameObject.SetActive(true);
                btn_Cancel.onClick.RemoveAllListeners();
                btn_Cancel.onClick.AddListener(() => onCancel?.Invoke(_requestId));
            }
            if (btn_Accept  != null) btn_Accept.gameObject.SetActive(false);
            if (btn_Decline != null) btn_Decline.gameObject.SetActive(false);
        }
    }
}
