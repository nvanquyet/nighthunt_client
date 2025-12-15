using NightHunt.Data.DTOs;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Player Slot View - Represents a single slot in lobby
    /// Can be empty (shows +) or occupied (shows player info)
    /// </summary>
    public class PlayerSlotView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI slotText;
        [SerializeField] private Image readyIndicator;
        [SerializeField] private Button slotButton; // Clickable slot
        [SerializeField] private Button kickButton; // Only for owner
        [SerializeField] private Button transferOwnerButton; // Only for owner, to transfer ownership
        [SerializeField] private GameObject emptySlotIndicator; // Shows "+" when empty

        private RoomPlayerResponse player; // null if empty
        private bool isOwner;
        private int team;
        private int slot;
        private System.Action<int, int> onSlotClicked; // Callback: (team, slot)
        private System.Action<long> onTransferOwnerClicked; // Callback: (targetUserId)

        private void Awake()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(OnSlotClicked);
            
            if (transferOwnerButton != null)
                transferOwnerButton.onClick.AddListener(OnTransferOwnerClicked);
        }

        /// <summary>
        /// Set slot data - can be empty or occupied
        /// </summary>
        public void SetSlot(int team, int slot, RoomPlayerResponse player, bool isOwner, 
                           System.Action<int, int> onSlotClicked, System.Action<long> onTransferOwnerClicked = null)
        {
            this.team = team;
            this.slot = slot;
            this.player = player;
            this.isOwner = isOwner;
            this.onSlotClicked = onSlotClicked;
            this.onTransferOwnerClicked = onTransferOwnerClicked;

            bool isEmpty = player == null;

            // Show/hide empty indicator
            if (emptySlotIndicator != null)
                emptySlotIndicator.SetActive(isEmpty);

            // Show/hide player info
            if (usernameText != null)
            {
                usernameText.gameObject.SetActive(!isEmpty);
                usernameText.text = isEmpty ? "" : player.username;
            }

            if (slotText != null)
            {
                slotText.text = isEmpty ? "+" : $"Slot {slot + 1}";
            }

            if (readyIndicator != null)
            {
                readyIndicator.gameObject.SetActive(!isEmpty);
                if (!isEmpty)
                    readyIndicator.color = player.isReady ? Color.green : Color.red;
            }

            // Kick button only for owner and occupied slots (not own slot)
            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(!isEmpty && isOwner && player.userId != GetCurrentUserId());
            }
            
            // Transfer Owner button only for owner and occupied slots (not own slot)
            if (transferOwnerButton != null)
            {
                transferOwnerButton.gameObject.SetActive(!isEmpty && isOwner && player.userId != GetCurrentUserId());
            }

            // Slot button: disable if this is current player's slot
            if (slotButton != null)
            {
                long currentUserId = GetCurrentUserId();
                bool isCurrentPlayerSlot = !isEmpty && player.userId == currentUserId;
                slotButton.interactable = !isCurrentPlayerSlot; // Disable if it's own slot
            }
        }

        private void OnSlotClicked()
        {
            onSlotClicked?.Invoke(team, slot);
        }
        
        private void OnTransferOwnerClicked()
        {
            if (player != null)
            {
                onTransferOwnerClicked?.Invoke(player.userId);
            }
        }

        private long GetCurrentUserId()
        {
            if (SessionState.Instance != null)
                return SessionState.Instance.UserId;
            return 0;
        }

        /// <summary>
        /// Update ready status without full refresh
        /// </summary>
        public void UpdateReadyStatus(bool isReady)
        {
            if (player != null)
            {
                player.isReady = isReady;
                
                if (readyIndicator != null)
                {
                    readyIndicator.color = isReady ? Color.green : Color.red;
                }
            }
        }

        public bool IsEmpty => player == null;
        public RoomPlayerResponse Player => player;
        public int Team => team;
        public int Slot => slot;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (slotButton == null)
                slotButton = GetComponent<Button>();
        }
#endif
    }
}
