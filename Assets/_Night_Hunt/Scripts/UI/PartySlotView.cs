using System;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Services.Party;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// PartySlotView — represents one slot in the party display (up to 4 slots).
    ///
    /// States:
    ///   Empty      → shows "+" icon; click = create party (if solo) or invite (if host)
    ///   MySlot     → shows my avatar icon + username + crown if host
    ///   MemberSlot → shows member avatar icon + username + crown if host
    ///
    /// Dropdown (shown on click):
    ///   MySlot     → [Leave Party] (if member) | [Disband Party] (if host)
    ///   MemberSlot → [Kick Player] (host only)
    ///
    /// SETUP (Prefab hierarchy):
    ///   PartySlot (this script)
    ///   ├── EmptyRoot
    ///   │   └── EmptyButton (Button — "+" click area)
    ///   ├── FilledRoot
    ///   │   ├── AvatarIcon (Image)
    ///   │   ├── UsernameText (TMP)
    ///   │   ├── CrownIcon (GameObject — SetActive only)
    ///   │   └── SlotButton (Button — covers whole slot)
    ///   └── DropdownPanel (hidden by default)
    ///       ├── Btn_LeaveParty
    ///       ├── Btn_DisbandParty
    ///       ├── Btn_KickPlayer
    ///       └── Btn_Close
    /// </summary>
    public class PartySlotView : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Slot Roots")]
        [SerializeField] private GameObject emptyRoot;
        [SerializeField] private GameObject filledRoot;

        [Header("Filled State")]
        [SerializeField] private Image           avatarIcon;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private GameObject      crownIcon;    // SetActive only — content agnostic
        [SerializeField] private Button          slotButton;   // whole-slot clickable area
        [SerializeField] private Button          emptyButton;  // "+" button on empty slot

        [Header("Dropdown Panel")]
        [SerializeField] private GameObject dropdownPanel;
        [SerializeField] private Button     btn_LeaveParty;
        [SerializeField] private Button     btn_DisbandParty;
        [SerializeField] private Button     btn_KickPlayer;
        [SerializeField] private Button     btn_Close;

        [Header("Default Avatar")]
        [Tooltip("Shown when CharacterDatabase entry has no icon (or member character unknown).")]
        [SerializeField] private Sprite defaultAvatarSprite;

        // ── Runtime state ──────────────────────────────────────────────────────

        private SlotState           _state       = SlotState.Empty;
        private PartyMemberResponse _member      = null;
        private bool                _iAmHost     = false;
        private bool                _iAmThisSlot = false;
        private bool                _closeNextFrame = false;
        private PartyService        _partyService;

        /// <summary>
        /// Fired when empty slot clicked and no party exists yet — HomeView decides create or invite.
        /// </summary>
        public event Action OnCreatePartyRequested;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (slotButton  != null) slotButton.onClick.AddListener(OnSlotClicked);
            if (emptyButton != null) emptyButton.onClick.AddListener(OnEmptyClicked);

            if (btn_LeaveParty   != null) btn_LeaveParty.onClick.AddListener(OnLeavePartyClicked);
            if (btn_DisbandParty != null) btn_DisbandParty.onClick.AddListener(OnDisbandPartyClicked);
            if (btn_KickPlayer   != null) btn_KickPlayer.onClick.AddListener(OnKickClicked);
            if (btn_Close        != null) btn_Close.onClick.AddListener(CloseDropdown);

            HideDropdown();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                _partyService = GameManager.Instance.PartyService;
        }

        // ── Public API (called by HomeView) ────────────────────────────────────

        /// <summary>Set slot to Empty state.</summary>
        public void SetEmpty()
        {
            _state       = SlotState.Empty;
            _member      = null;
            _iAmThisSlot = false;
            Refresh();
        }

        /// <summary>Set slot to "my own slot" state using local session data (no party yet).</summary>
        public void SetAsMySlot(bool iAmHost)
        {
            _state       = SlotState.MySlot;
            _member      = null;
            _iAmThisSlot = true;
            _iAmHost     = iAmHost;
            Refresh();
        }

        /// <summary>Set slot to filled member state.</summary>
        public void SetMember(PartyMemberResponse member, bool localPlayerIsHost)
        {
            bool isMe    = member.userId == (SessionState.Instance?.UserId ?? -1L);
            _state       = isMe ? SlotState.MySlot : SlotState.MemberSlot;
            _member      = member;
            _iAmThisSlot = isMe;
            _iAmHost     = localPlayerIsHost;
            Refresh();
        }

        // ── Refresh display ────────────────────────────────────────────────────

        private void Refresh()
        {
            HideDropdown();

            bool isEmpty = _state == SlotState.Empty;
            if (emptyRoot  != null) emptyRoot.SetActive(isEmpty);
            if (filledRoot != null) filledRoot.SetActive(!isEmpty);

            if (isEmpty) return;

            if (avatarIcon  != null) avatarIcon.sprite = ResolveAvatar();

            if (usernameText != null)
                usernameText.text = _iAmThisSlot
                    ? (SessionState.Instance?.Username ?? "Me")
                    : (_member?.username ?? "Unknown");

            bool showCrown = (_member != null && _member.isHost) ||
                             (_iAmThisSlot && _iAmHost);
            if (crownIcon != null) crownIcon.SetActive(showCrown);
        }

        private Sprite ResolveAvatar()
        {
            // Only own slot can resolve avatar — server doesn't send selectedCharacterId for members yet.
            // Phase 2: read charId from _member.selectedCharacterId when server supports it.
            if (_iAmThisSlot)
            {
                string charId = SessionState.Instance?.SelectedCharacterId;
                if (!string.IsNullOrEmpty(charId))
                {
                    var def = CharacterDatabase.Instance?.GetById(charId);
                    if (def?.Icon != null) return def.Icon;
                }
            }

            return defaultAvatarSprite;
        }

        // ── Click handlers ─────────────────────────────────────────────────────

        private void OnEmptyClicked()
        {
            // Delegate entirely to HomeView — it decides: create party or open invite panel
            OnCreatePartyRequested?.Invoke();
        }

        private void OnSlotClicked()
        {
            if (_state == SlotState.Empty) return;

            HideDropdown();
            if (dropdownPanel == null) return;

            bool showLeave   = _iAmThisSlot && !_iAmHost;
            bool showDisband = _iAmThisSlot && _iAmHost;
            bool showKick    = !_iAmThisSlot && _iAmHost && _state == SlotState.MemberSlot;

            if (btn_LeaveParty   != null) btn_LeaveParty.gameObject.SetActive(showLeave);
            if (btn_DisbandParty != null) btn_DisbandParty.gameObject.SetActive(showDisband);
            if (btn_KickPlayer   != null) btn_KickPlayer.gameObject.SetActive(showKick);

            dropdownPanel.SetActive(true);
        }

        private void OnLeavePartyClicked()
        {
            HideDropdown();
            GameModalWindow.Instance?.ShowConfirm(
                "Rời party?",
                "Bạn có chắc muốn rời khỏi party?",
                onConfirm: async () => { if (_partyService != null) await _partyService.LeaveParty(); },
                confirmText: "Rời party", cancelText: "Hủy");
        }

        private void OnDisbandPartyClicked()
        {
            HideDropdown();
            GameModalWindow.Instance?.ShowConfirm(
                "Giải tán party?",
                "Tất cả thành viên sẽ bị đưa ra ngoài.",
                onConfirm: async () => { if (_partyService != null) await _partyService.DisbandParty(); },
                confirmText: "Giải tán", cancelText: "Hủy");
        }

        private async void OnKickClicked()
        {
            HideDropdown();
            if (_partyService == null || _member == null) return;
            await _partyService.KickMember(_member.userId);
        }

        private void CloseDropdown() => HideDropdown();

        private void HideDropdown()
        {
            if (dropdownPanel != null) dropdownPanel.SetActive(false);
        }

        // ── Outside-click close dropdown ──────────────────────────────────────

        private void Update()
        {
            if (dropdownPanel != null && dropdownPanel.activeSelf &&
                Input.GetMouseButtonDown(0))
            {
                // Delay 1 frame: button onClick fires before Update,
                // so if dropdown is still open here → click was outside
                _closeNextFrame = true;
            }

            if (_closeNextFrame)
            {
                _closeNextFrame = false;
                if (dropdownPanel != null && dropdownPanel.activeSelf)
                    HideDropdown();
            }
        }

        // ── Inner types ────────────────────────────────────────────────────────

        private enum SlotState { Empty, MySlot, MemberSlot }
    }
}