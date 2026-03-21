using System;
using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Single friend row in the Friends tab (Online or Offline list).
    ///
    /// Click anywhere on the row fires <c>onSelected(this)</c> so FriendPanelView
    /// can position and show its shared dropdown.
    ///
    /// SETUP (Prefab):
    ///   FriendItem (this script)
    ///   ├── UsernameText       (TMP)
    ///   ├── StatusText         (TMP — online status badge)
    ///   ├── InvitePendingIcon  (GameObject — spinning icon, hidden by default)
    ///   └── RowButton          (Button — covers whole row, fires onSelected)
    /// </summary>
    public class FriendItemView : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Invite Pending")]
        [SerializeField] private GameObject invitePendingIcon;

        [Header("Interaction")]
        [SerializeField] private Button rowButton;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Action<FriendItemView> _onSelected;

        /// <summary>The current friend data for this row.</summary>
        public FriendResponse Friend { get; private set; }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (rowButton != null)
                rowButton.onClick.AddListener(() => _onSelected?.Invoke(this));

            if (invitePendingIcon != null)
                invitePendingIcon.SetActive(false);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Initialise the row with friend data.</summary>
        public void Setup(FriendResponse friend, Action<FriendItemView> onSelected)
        {
            Friend     = friend;
            _onSelected = onSelected;

            if (usernameText == null)
            {
                Debug.LogWarning($"[FriendItemView] usernameText is not assigned on '{name}'. Trying fallback lookup.");
                usernameText = ResolveFallbackText("name");
            }
            else if (!usernameText.transform.IsChildOf(transform))
            {
                Debug.LogWarning($"[FriendItemView] usernameText on '{name}' points outside row prefab ('{usernameText.gameObject.name}'). Rebinding locally.");
                usernameText = ResolveFallbackText("name");
            }

            if (statusText == null)
            {
                statusText = ResolveFallbackText("status");
            }
            else if (!statusText.transform.IsChildOf(transform))
            {
                Debug.LogWarning($"[FriendItemView] statusText on '{name}' points outside row prefab ('{statusText.gameObject.name}'). Rebinding locally.");
                statusText = ResolveFallbackText("status");
            }

            if (usernameText != null)
                usernameText.text = string.IsNullOrEmpty(friend?.username) ? "Unknown" : friend.username;

            ApplyStatus(friend.onlineStatus);

            Debug.Log($"[FriendItemView] Bind row='{gameObject.name}' friendUserId={friend?.userId} username='{friend?.username}' displayedName='{usernameText?.text}' status='{friend?.onlineStatus}'");
        }

        /// <summary>
        /// Update status badge in-place when a WS friend_status_changed event arrives.
        /// Also updates the cached Friend data so later reads see the new state.
        /// </summary>
        public void UpdateStatus(string newStatus, long newPartyId)
        {
            if (Friend == null) return;
            Friend.onlineStatus   = newStatus;
            Friend.currentPartyId = newPartyId;
            ApplyStatus(newStatus);
        }

        /// <summary>Show or hide the pending-invite spinner/icon.</summary>
        public void SetInvitePending(bool pending)
        {
            if (invitePendingIcon != null)
                invitePendingIcon.SetActive(pending);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void ApplyStatus(string status)
        {
            if (statusText == null) return;
            statusText.text = status switch
            {
                "ONLINE"  => "Online",
                "IN_GAME" => "In Game",
                "AWAY"    => "Away",
                _         => "Offline"
            };
            statusText.color = status switch
            {
                "ONLINE"  => new Color(0.2f, 0.9f, 0.2f),
                "IN_GAME" => new Color(0.2f, 0.6f, 1f),
                "AWAY"    => new Color(1f,   0.8f, 0.2f),
                _         => new Color(0.5f, 0.5f, 0.5f)
            };
        }

        private TextMeshProUGUI ResolveFallbackText(string keyword)
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts)
            {
                if (t == null) continue;
                if (t.gameObject.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
            return null;
        }
    }
}
