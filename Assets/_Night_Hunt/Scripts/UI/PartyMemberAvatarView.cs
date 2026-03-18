using System;
using NightHunt.Data.DTOs;
using NightHunt.Gameplay.Character.Data;
using NightHunt.State;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// PartyMemberAvatarView — single avatar slot in the bottom-left party row.
    ///
    /// Represents ONE other party member (never the local player — self is shown separately).
    ///
    /// Design: ONE Button covers the entire slot background.
    ///   Occupied: avatarIcon visible + hostBadge if member.isHost.
    ///             Button click fires onSlotClicked(this) → parent shows context menu.
    ///   Empty:    avatarIcon hidden (background shows '+' via Inspector/animation).
    ///             Button click fires onInvite() (opens FriendPanelView).
    ///
    /// Context-menu logic lives in PartyMemberListView (SharedPartyContextMenu).
    ///
    /// SETUP (Prefab):
    ///   PartyMemberAvatar (this script)
    ///   ├── SlotButton  (Button — covers whole slot background)
    ///   ├── AvatarIcon  (Image  — visible when occupied)
    ///   └── HostBadge   (GameObject — shown when member.isHost is true)
    /// </summary>
    public class PartyMemberAvatarView : MonoBehaviour
    {
        [Header("Slot Button (whole background — always present)")]
        [SerializeField] private Button slotButton;

        [Header("Occupied State")]
        [SerializeField] private Image      avatarIcon;
        [Tooltip("Host crown badge. Visible when this member is the party host.")]
        [SerializeField] private GameObject hostBadge;

        [Header("Fallback")]
        [SerializeField] private Sprite defaultAvatar;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Action<PartyMemberAvatarView> _onSlotClicked;
        private Action                        _onInvite;

        // ── Exposed for PartyMemberListView (context-menu decisions) ──────────
        public bool IAmHost       { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public long MemberId      { get; private set; }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(OnButtonClicked);

            // Start in empty visual state
            if (avatarIcon != null) avatarIcon.gameObject.SetActive(false);
            if (hostBadge  != null) hostBadge.SetActive(false);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Show occupied state for <paramref name="member"/>.</summary>
        public void SetMember(PartyMemberResponse member, bool iAmHost,
                              Action<PartyMemberAvatarView> onSlotClicked)
        {
            if (member == null) { SetEmpty(null); return; }

            IAmHost        = iAmHost;
            IsLocalPlayer  = member.userId == (SessionState.Instance?.UserId ?? -1L);
            MemberId       = member.userId;
            _onSlotClicked = onSlotClicked;
            _onInvite      = null;

            if (hostBadge != null) hostBadge.SetActive(member.isHost);

            if (avatarIcon != null)
            {
                var sprite = ResolveAvatar(member);
                avatarIcon.sprite = sprite != null ? sprite : defaultAvatar;
                avatarIcon.gameObject.SetActive(true);
            }
        }

        /// <summary>Show empty state ('+' invite slot).</summary>
        public void SetEmpty(Action onInvite)
        {
            _onInvite      = onInvite;
            _onSlotClicked = null;
            IAmHost        = false;
            IsLocalPlayer  = false;
            MemberId       = 0L;

            if (avatarIcon != null) avatarIcon.gameObject.SetActive(false);
            if (hostBadge  != null) hostBadge.SetActive(false);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void OnButtonClicked()
        {
            if (_onSlotClicked != null) _onSlotClicked.Invoke(this);
            else                        _onInvite?.Invoke();
        }

        private Sprite ResolveAvatar(PartyMemberResponse member)
        {
            bool isLocalPlayer = member.userId == (SessionState.Instance?.UserId ?? -1L);
            if (!string.IsNullOrEmpty(member.selectedCharacterId))
            {
                var def = CharacterDatabase.Instance?.GetById(member.selectedCharacterId);
                if (def?.Icon != null) return def.Icon;
            }
            if (isLocalPlayer)
            {
                string charId = SessionState.Instance?.SelectedCharacterId;
                if (!string.IsNullOrEmpty(charId))
                {
                    var def = CharacterDatabase.Instance?.GetById(charId);
                    if (def?.Icon != null) return def.Icon;
                }
            }
            return defaultAvatar;
        }
    }
}
