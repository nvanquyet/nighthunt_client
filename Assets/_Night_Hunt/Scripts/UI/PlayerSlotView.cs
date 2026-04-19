using NightHunt.Data.DTOs;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Player Slot View — single slot in the lobby team grid.
    ///
    /// States:
    ///   Empty    → dataContainer hidden; slotText shows "+"
    ///   Occupied → dataContainer visible; shows username + ready indicator
    ///
    /// Context menu lives in CustomLobbyView (one shared menu, not per-slot prefab).
    /// Clicking any interactable slot calls onSlotClicked(team, slot).
    /// CustomLobbyView decides: empty → ChangeTeam, occupied → show context menu.
    ///
    /// Interactability:
    ///   Empty slot          → interactable (move self to this slot)
    ///   Own occupied slot   → NOT interactable
    ///   Other player's slot → interactable (opens context menu in manager)
    ///
    /// SETUP (Prefab hierarchy):
    ///   PlayerSlot (this script)
    ///   ├── SlotText       (TMP — "+" when empty, "Slot N" when occupied)
    ///   ├── SlotButton     (Button — covers whole slot)
    ///   └── DataContainer  (GameObject — active only when occupied)
    ///       ├── UsernameText   (TMP)
    ///       └── ReadyIndicator (Image — green/red)
    /// </summary>
    public class PlayerSlotView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI slotText;
        [SerializeField] private Image           readyIndicator;
        [SerializeField] private Button          slotButton;
        [Tooltip("Kick button — only visible to the host on other players' slots.\n"
               + "Assign in prefab; controlled entirely by code.")]
        [SerializeField] private Button          kickButton;
        [Tooltip("Container for occupied-state visuals (username, ready indicator).\n"
               + "Active when slot is occupied; hidden when empty.")]
        [SerializeField] private GameObject      dataContainer;

        private RoomPlayerResponse      _player;
        private int                     _team;
        private int                     _slot;
        private System.Action<int, int> _onSlotClicked;
        private System.Action<long>     _onKickClicked;

        private void Awake()
        {
            if (slotButton != null) slotButton.onClick.AddListener(OnSlotClicked);
            if (kickButton != null) kickButton.onClick.AddListener(OnKickButtonClicked);
        }

        /// <summary>
        /// Bind slot data. Pass <c>null</c> for <paramref name="player"/> to render as empty.
        /// </summary>
        /// <param name="isOwner">True when the LOCAL player is the room host.</param>
        /// <param name="onKickClicked">Called with the target player's userId when the kick button is pressed.
        /// Only wired when <paramref name="isOwner"/> is true and the slot belongs to another player.</param>
        public void SetSlot(int team, int slot, RoomPlayerResponse player, bool isOwner,
                            System.Action<int, int> onSlotClicked,
                            System.Action<long>     onKickClicked = null)
        {
            _team          = team;
            _slot          = slot;
            _player        = player;
            _onSlotClicked = onSlotClicked;
            _onKickClicked = onKickClicked;

            bool isEmpty = player == null;
            bool isSelf  = !isEmpty && player.userId == GetCurrentUserId();

            // dataContainer visible only when occupied
            if (dataContainer != null) dataContainer.SetActive(!isEmpty);

            if (slotText != null) slotText.text = isEmpty ? "+" : $"Slot {slot + 1}";

            if (!isEmpty)
            {
                if (usernameText   != null) usernameText.text    = player.username;
                if (readyIndicator != null) readyIndicator.color = player.isReady ? Color.green : Color.red;
            }

            // Kick button: visible only to the host, only on OTHER players' occupied slots.
            if (kickButton != null)
                kickButton.gameObject.SetActive(isOwner && !isEmpty && !isSelf);

            if (slotButton != null)
                slotButton.interactable = isEmpty || !isSelf;
        }

        /// <summary>Refresh only the ready indicator without full rebind.</summary>
        public void UpdateReadyStatus(bool isReady)
        {
            if (_player == null) return;
            _player.isReady = isReady;
            if (readyIndicator != null)
                readyIndicator.color = isReady ? Color.green : Color.red;
        }

        private void OnSlotClicked() => _onSlotClicked?.Invoke(_team, _slot);

        private void OnKickButtonClicked()
        {
            if (_player != null) _onKickClicked?.Invoke(_player.userId);
        }

        private long GetCurrentUserId()
            => SessionState.Instance != null ? SessionState.Instance.UserId : 0L;

        public bool               IsEmpty => _player == null;
        public RoomPlayerResponse Player  => _player;
        public int                Team    => _team;
        public int                Slot    => _slot;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (slotButton == null)
                slotButton = ComponentResolver.Find<Button>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] SlotButton not found")
                    .Resolve();
            // kickButton intentionally NOT auto-resolved — it is an optional child
            // that must be explicitly added to the prefab and assigned here.
        }
#endif
    }
}
