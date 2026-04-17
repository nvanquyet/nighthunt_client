using System;
using System.Collections.Generic;
using NightHunt.Data.DTOs;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// PartyMemberListView - bottom-left party avatar row on the Home screen.
    ///
    /// Shows exactly <c>maxSlots - 1</c> slots for OTHER party members (self is displayed
    /// separately in the UI as a fixed element).
    ///   Occupied → <see cref="PartyMemberAvatarView.SetMember"/> (avatar + host crown).
    ///   Empty    → <see cref="PartyMemberAvatarView.SetEmpty"/> ("+" invite button → FriendPanelView).
    ///
    /// e.g. 2v2 → 1 slot (1 other); 4v4 → 3 slots (3 others).
    ///
    /// <c>selfHostBadge</c>: assign the crown/badge GameObject on the self-player's fixed
    /// UI element. It will be shown/hidden depending on whether the local player is host.
    ///
    /// Reuses existing slot GameObjects across refreshes (EnsureSlotCount strategy).
    ///
    /// SETUP (Prefab hierarchy):
    ///   PartyMemberList (this script)
    ///   +-- Container  <- HorizontalLayoutGroup, set in Inspector as <c>container</c>
    /// </summary>
    public class PartyMemberListView : MonoBehaviour
    {
        [Header("Spawning")]
        [Tooltip("Prefab with PartyMemberAvatarView component on root or child.")]
        [SerializeField] private GameObject memberAvatarPrefab;

        [Tooltip("Parent transform for spawned avatars (HorizontalLayoutGroup recommended).")]
        [SerializeField] private Transform container;

        [Header("Self Display")]
        [Tooltip("Host crown/badge on the local player's fixed UI slot. Shown when iAmHost is true.")]
        [SerializeField] private GameObject selfHostBadge;

        [Header("Shared Context Menu")]
        [Tooltip("Assign the SharedPartyContextMenu instance that lives as last sibling of the Home panel root.")]
        [SerializeField] private SharedPartyContextMenu sharedContextMenu;

        // -- Runtime ---------------------------------------------------------

        private readonly List<PartyMemberAvatarView> _views = new();
        private Action<long> _onKick;
        private Action<long> _onTransferLeader;
        private Action       _onLeave;

        // -- Public API -------------------------------------------------------

        /// <summary>
        /// Refresh the bottom-left avatar row.
        /// Spawns <c>maxSlots - 1</c> slots — one per possible OTHER party member (self excluded).
        /// </summary>
        /// <param name="party">Current party, or null if solo.</param>
        /// <param name="maxSlots">Team size of the selected mode (e.g. 2 for 2v2, 4 for 4v4).</param>
        /// <param name="iAmHost">True if the local player is the party host.</param>
        /// <param name="onInviteClicked">Fires when an empty "+" slot is tapped (open FriendPanelView).</param>
        /// <param name="onKick">Fires with userId when host kicks a member.</param>
        /// <param name="onTransferLeader">Fires with userId when host transfers leadership.</param>
        /// <param name="onLeave">Fires when local player taps Leave.</param>
        public void Refresh(PartyResponse party, int maxSlots,
                            bool         iAmHost           = false,
                            Action       onInviteClicked    = null,
                            Action<long> onKick             = null,
                            Action<long> onTransferLeader   = null,
                            Action       onLeave            = null)
        {
            if (maxSlots <= 0) maxSlots = 1;

            _onKick           = onKick;
            _onTransferLeader = onTransferLeader;
            _onLeave          = onLeave;

            // Show/hide host crown on the local player's fixed self-slot
            selfHostBadge?.SetActive(iAmHost);

            if (memberAvatarPrefab == null || container == null) return;

            // Exclude self — this row shows OTHER members only
            long localId = SessionState.Instance?.UserId ?? -1L;
            var members = new List<PartyMemberResponse>();
            if (party?.members != null)
            {
                foreach (var m in party.members)
                    if (m.userId != localId) members.Add(m);
                members.Sort((a, b) => a.joinOrder.CompareTo(b.joinOrder));
            }

            int otherSlots = maxSlots - 1;  // slots for other members
            if (otherSlots < 0) otherSlots = 0;

            EnsureSlotCount(otherSlots);

            for (int i = 0; i < _views.Count; i++)
            {
                if (i < members.Count)
                    _views[i].SetMember(members[i], iAmHost, OnAvatarSlotClicked);
                else
                    _views[i].SetEmpty(onInviteClicked);
            }
        }

        /// <summary>Destroy all spawned avatar objects.</summary>
        public void Clear()
        {
            sharedContextMenu?.Hide();
            foreach (var v in _views)
                if (v != null) Destroy(v.gameObject);
            _views.Clear();
        }

        // -- Private ----------------------------------------------------------

        private void EnsureSlotCount(int count)
        {
            // Remove excess
            while (_views.Count > count)
            {
                var last = _views[_views.Count - 1];
                _views.RemoveAt(_views.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }

            // Add missing
            while (_views.Count < count)
            {
                var go   = Instantiate(memberAvatarPrefab, container);
                var view = go.GetComponentInChildren<PartyMemberAvatarView>(includeInactive: true)
                           ?? go.GetComponent<PartyMemberAvatarView>();

                if (view == null) { Destroy(go); break; }
                _views.Add(view);
            }
        }

        private void OnAvatarSlotClicked(PartyMemberAvatarView view)
        {
            if (view == null) return;

            var anchor             = view.GetComponent<RectTransform>();
            bool showKick          = view.IAmHost && !view.IsLocalPlayer;
            bool showTransfer      = view.IAmHost && !view.IsLocalPlayer;
            bool showLeave         = view.IsLocalPlayer;

            sharedContextMenu?.Show(anchor, showKick, showLeave,
                                    view.MemberId, _onKick, _onLeave,
                                    showTransferLeader: showTransfer,
                                    onTransferLeader:  _onTransferLeader);
        }

        private void OnDestroy() => Clear();
    }
}

