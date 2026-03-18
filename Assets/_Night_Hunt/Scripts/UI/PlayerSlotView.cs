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
        [Tooltip("Container for occupied-state visuals (username, ready indicator).\n"
               + "Active when slot is occupied; hidden when empty.")]
        [SerializeField] private GameObject      dataContainer;

        private RoomPlayerResponse      _player;
        private int                     _team;
        private int                     _slot;
        private System.Action<int, int> _onSlotClicked;

        private void Awake()
        {
            if (slotButton != null) slotButton.onClick.AddListener(OnSlotClicked);
        }

        /// <summary>
        /// Bind slot data. Pass <c>null</c> for <paramref name="player"/> to render as empty.
        /// </summary>
        public void SetSlot(int team, int slot, RoomPlayerResponse player, bool isOwner,
                            System.Action<int, int> onSlotClicked)
        {
            _team          = team;
            _slot          = slot;
            _player        = player;
            _onSlotClicked = onSlotClicked;

            bool isEmpty = player == null;

            // dataContainer visible only when occupied
            if (dataContainer != null) dataContainer.SetActive(!isEmpty);

            if (slotText != null) slotText.text = isEmpty ? "+" : $"Slot {slot + 1}";

            if (!isEmpty)
            {
                if (usernameText   != null) usernameText.text        = player.username;
                if (readyIndicator != null) readyIndicator.color     = player.isReady ? Color.green : Color.red;
            }

            if (slotButton != null)
            {
                bool isSelf = !isEmpty && player.userId == GetCurrentUserId();
                slotButton.interactable = isEmpty || !isSelf;
            }
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
                    .OrLogWarning("[Auto] Button not found")
                    .Resolve();
        }
#endif
    }
}
