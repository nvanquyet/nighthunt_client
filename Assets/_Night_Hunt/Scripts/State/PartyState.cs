using System;
using System.Threading.Tasks;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Utils;
using UnityEngine;

namespace NightHunt.State
{
    /// <summary>
    /// Singleton that owns the canonical party state, mirroring RoomState's pattern.
    ///
    /// SOURCE OF TRUTH for "am I in a party?" across all UI views.
    ///
    /// Updated by two sources:
    ///   1. PartyService after successful API calls → SetParty / ClearParty
    ///   2. GameEventBus party WS handlers
    ///        party_member_joined/left/kicked, party_host_changed, party_status_changed
    ///          → APICache invalidated + background RefreshAsync
    ///        party_disbanded → ClearParty immediately
    ///
    /// Consumers (PartyController, PartyCustomModeView) subscribe to
    ///   OnPartyUpdated / OnPartyCleared
    /// instead of caching their own PartyResponse field and calling GetParty() everywhere.
    ///
    /// This eliminates:
    ///   • _currentParty in PartyController
    ///   • Repeated RefreshParty() calls from every WS event handler
    ///   • _partyService.GetParty() calls inside PartyCustomModeView.CheckPartyThenRun
    ///   • Race conditions where two views hold different stale party snapshots
    /// </summary>
    public class PartyState : SingletonPersistent<PartyState>
    {
        // ══════════════════════════════════════════════════════════════════════
        // STATE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Current party. Null when the player is not in any party.</summary>
        public PartyResponse CurrentParty { get; private set; }

        public bool IsInParty  => CurrentParty != null;
        public bool IsInQueue  => CurrentParty?.IsInQueue ?? false;
        public bool IsInCustom => CurrentParty?.IsCustom  ?? false;

        // ══════════════════════════════════════════════════════════════════════
        // EVENTS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Fired whenever party state is set or updated (member join/leave, host change, etc.).</summary>
        public event Action<PartyResponse> OnPartyUpdated;

        /// <summary>Fired whenever we leave, are kicked, or the party disbands.</summary>
        public event Action OnPartyCleared;

        // ══════════════════════════════════════════════════════════════════════
        // INTERNALS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// When the local player intentionally triggers a DisbandParty, set this to true
        /// before the API call so HandlePartyDisbanded in PartyController skips the toast.
        /// Consumed (reset to false) by ConsumeSuppressDisbandToast().
        /// </summary>
        private bool _suppressNextDisbandToast;

        /// <summary>Guard: only one server refresh runs at a time.</summary>
        private bool _refreshInFlight;

        // ══════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set or update the active party.
        /// Called by PartyService after successful API calls (CreateParty, AcceptInvitation,
        /// GetParty) and after background RefreshAsync.
        /// </summary>
        public void SetParty(PartyResponse party)
        {
            if (party == null) { ClearParty(); return; }
            CurrentParty = party;
            int members = party.members != null ? party.members.Count : party.currentMemberCount;
            Debug.Log($"[PartyState] SetParty id={party.partyId} status={party.partyStatus} mode={party.partyMode} members={members}/{party.maxMembers}");
            OnPartyUpdated?.Invoke(party);
        }

        /// <summary>
        /// Clear party (left / kicked / disbanded).
        /// Called directly by GameEventBus when party_disbanded arrives,
        /// and by PartyService after successful leave/disband.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        public void ClearParty()
        {
            if (CurrentParty == null) return;
            Debug.Log($"[PartyState] ClearParty (was id={CurrentParty.partyId})");
            CurrentParty = null;
            OnPartyCleared?.Invoke();
        }

        /// <summary>
        /// Set before calling DisbandParty() intentionally.
        /// Consumed by HandlePartyDisbanded in PartyController to skip the toast.
        /// </summary>
        public void SuppressNextDisbandToast() => _suppressNextDisbandToast = true;

        /// <summary>
        /// Consume (read and clear) the suppress flag.
        /// Returns true if the next disbanded toast should be suppressed.
        /// </summary>
        public bool ConsumeSuppressDisbandToast()
        {
            bool v = _suppressNextDisbandToast;
            _suppressNextDisbandToast = false;
            return v;
        }

        /// <summary>
        /// Invalidate the party cache and schedule a background refresh.
        /// Called by GameEventBus handlers that receive partial WS events
        /// (member_joined, member_left, host_changed, status_changed) — the
        /// WS payload only has userId/partyId, not the full PartyResponse, so
        /// we need to re-fetch.
        ///
        /// Safe to call multiple times concurrently — only one request runs at a time.
        /// </summary>
        public void InvalidateAndScheduleRefresh()
        {
            APICache.Invalidate(APICache.KEY_PARTY_STATE);
            _ = RefreshAsync(forceNetwork: false);
        }

        /// <summary>
        /// Fetch the current party from the server (bypassing or using the cache)
        /// and update state. Fires OnPartyUpdated or OnPartyCleared when complete.
        ///
        /// This replaces the old RefreshParty(forceServer) method in PartyController.
        /// Views should await this where they need fresh data before acting.
        /// </summary>
        public async Task RefreshAsync(bool forceNetwork = false)
        {
            if (_refreshInFlight) return;
            _refreshInFlight = true;
            try
            {
                var partyService = GameManager.Instance?.PartyService;
                if (partyService == null) return;

                if (forceNetwork)
                    APICache.Invalidate(APICache.KEY_PARTY_STATE);

                var result = await partyService.GetParty();
                if (result.Success && result.Data != null)
                    SetParty(result.Data);
                else
                    ClearParty();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PartyState] RefreshAsync failed: {ex.Message}");
            }
            finally
            {
                _refreshInFlight = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>True if the given userId is the current party host.</summary>
        public bool IsHost(long userId) =>
            CurrentParty != null && CurrentParty.hostUserId == userId;

        /// <summary>Number of current party members (uses member list if available, else currentMemberCount).</summary>
        public int MemberCount =>
            CurrentParty == null ? 0
            : CurrentParty.members != null ? CurrentParty.members.Count
            : CurrentParty.currentMemberCount;
    }
}
