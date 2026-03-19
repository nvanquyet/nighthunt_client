using System;
using NightHunt.Data.DTOs;
using NightHunt.Gameplay.Character.Data;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// PartyModelSlotView — single centre slot in the party model display.
    ///
    /// Always represents a real party member (no empty/placeholder slots).
    /// PartyModelListView only instantiates one slot per actual member.
    ///
    /// Design: ONE Button covers the entire slot.
    ///   characterImage + nameText visible when bound via SetMember().
    ///   Button click fires onSlotClicked(this) → parent shows context menu.
    ///
    /// Context-menu logic lives in PartyModelListView (SharedPartyContextMenu).
    ///
    /// SETUP (Prefab):
    ///   PartyModelSlot (this script)
    ///   ├── SlotButton      (Button — covers whole slot)
    ///   ├── CharacterImage  (Image)
    ///   └── NameText        (TMP)
    /// </summary>
    public class PartyModelSlotView : MonoBehaviour
    {
        [Header("Slot Button (whole slot — always present)")]
        [SerializeField] private Button slotButton;

        [Header("Occupied State")]
        [SerializeField] private Image           characterImage;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("Fallback")]
        [Tooltip("Shown when no character thumbnail can be resolved.")]
        [SerializeField] private Sprite defaultThumbnail;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Action<PartyModelSlotView> _onSlotClicked;

        // ── Exposed for PartyModelListView (context-menu decisions) ───────────
        public bool IAmHost       { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public long MemberId      { get; private set; }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(OnButtonClicked);
            SetEmpty();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Show the occupied state for <paramref name="member"/>.</summary>
        public void SetMember(PartyMemberResponse member, bool iAmHost,
                              Action<PartyModelSlotView> onSlotClicked)
        {
            if (member == null) { SetEmpty(); return; }

            IAmHost        = iAmHost;
            IsLocalPlayer  = member.userId == (SessionState.Instance?.UserId ?? -1L);
            MemberId       = member.userId;
            _onSlotClicked = onSlotClicked;

            Debug.Log($"[PartyModelSlotView] SetMember — userId={member.userId} username='{member.username}' isLocal={IsLocalPlayer} isHost={member.isHost} charId='{member.selectedCharacterId}'");

            bool isLocal = IsLocalPlayer;
            if (nameText != null)
            {
                string display = isLocal
                    ? (SessionState.Instance?.Username ?? member.username ?? "Me")
                    : (member.username ?? "Unknown");
                nameText.text = $"Welcome,\n<b>{display}</b>";
            }

            if (characterImage != null)
            {
                characterImage.sprite = ResolveThumbnail(member);
                characterImage.gameObject.SetActive(true);
            }
        }

        /// <summary>Reset to unbound state (called on Awake before first SetMember).</summary>
        public void SetEmpty()
        {
            IAmHost        = false;
            IsLocalPlayer  = false;
            MemberId       = 0L;
            _onSlotClicked = null;

            if (characterImage != null) characterImage.gameObject.SetActive(false);
            if (nameText       != null) nameText.text = "";
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void OnButtonClicked() => _onSlotClicked?.Invoke(this);

        private Sprite ResolveThumbnail(PartyMemberResponse member)
        {
            // 1. Server supplied selectedCharacterId — highest priority
            if (!string.IsNullOrEmpty(member.selectedCharacterId))
            {
                var def = CharacterDatabase.Instance?.GetById(member.selectedCharacterId);
                if (def?.Thumbnail != null)
                {
                    Debug.Log($"[PartyModelSlotView] userId={member.userId} — resolved thumbnail from member.selectedCharacterId='{member.selectedCharacterId}'");
                    return def.Thumbnail;
                }
                Debug.LogWarning($"[PartyModelSlotView] userId={member.userId} — selectedCharacterId='{member.selectedCharacterId}' not found in CharacterDatabase");
            }
            else
            {
                Debug.Log($"[PartyModelSlotView] userId={member.userId} — selectedCharacterId is null/empty (server Phase 2 not done yet)");
            }

            // 2. Local player fallback — use SessionState
            bool isLocal = member.userId == (SessionState.Instance?.UserId ?? -1L);
            if (isLocal)
            {
                string charId = SessionState.Instance?.SelectedCharacterId;
                Debug.Log($"[PartyModelSlotView] Local player fallback — SessionState.SelectedCharacterId='{charId}'");
                if (!string.IsNullOrEmpty(charId))
                {
                    var def = CharacterDatabase.Instance?.GetById(charId);
                    if (def?.Thumbnail != null)
                    {
                        Debug.Log($"[PartyModelSlotView] userId={member.userId} — resolved thumbnail from SessionState charId='{charId}'");
                        return def.Thumbnail;
                    }
                    Debug.LogWarning($"[PartyModelSlotView] userId={member.userId} — SessionState charId='{charId}' not found in CharacterDatabase");
                }
            }

            // Last resort: use the first character in the database so the slot is never completely blank.
            var indexFallback = CharacterDatabase.Instance?.GetByIndex(0);
            if (indexFallback?.Thumbnail != null)
            {
                Debug.LogWarning($"[PartyModelSlotView] userId={member.userId} \u2014 falling back to CharacterDatabase index 0 (isLocal={isLocal})");
                return indexFallback.Thumbnail;
            }

            Debug.LogWarning($"[PartyModelSlotView] userId={member.userId} \u2014 using defaultThumbnail (isLocal={isLocal}, CharacterDatabase empty or no thumbnail)");
            return defaultThumbnail;
        }
    }
}
