using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.Services.Party;
using NightHunt.State;
using NightHunt.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;

namespace NightHunt.UI
{
    /// <summary>
    /// PartyController — manages everything party-related on the Home screen:
    ///   • Game mode &amp; map dropdown population and validation
    ///   • Party display: bottom-left avatar row (PartyMemberListView)
    ///                    + centre model row (PartyModelListView)
    ///   • Ranked matchmaking: PLAY button (Idle → Searching timer → cancel)
    ///   • Party Custom Mode navigation shortcut
    ///   • All WS events: matchmaking (match_found / match_ready / match_cancelled)
    ///                    + party (member_joined / left / kicked / disbanded / host_changed)
    ///   • InviteFriendToParty() — called by FriendPanelView via Inspector reference
    ///
    /// Lives on the same GameObject as HomeView (Home panel root).
    /// HomeView.OnShow() calls <see cref="OnHomeShown"/> to trigger a party refresh.
    /// </summary>
    public class PartyController : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // INSPECTOR REFERENCES
        // ══════════════════════════════════════════════════════════════════════

        // ── Mode &amp; Map Selectors ─────────────────────────────────────────────
        [Header("Mode &amp; Map Selectors")]
        [Tooltip("MUIP CustomDropdown — populated from GameModeConfig.GetEnabled() on Start.")]
        [SerializeField] private CustomDropdown modeDropdown;
        [Tooltip("MUIP CustomDropdown — repopulated when mode changes (filtered by mode key).")]
        [SerializeField] private CustomDropdown mapDropdown;

        // ── Ranked Queue / Play Button ────────────────────────────────────────
        [Header("Ranked Queue")]
        [SerializeField] private Button          btn_Play;
        [Tooltip("Idle state: 'PLAY'.  Searching state: '00:00  ✕' (updated every frame).")]
        [SerializeField] private TextMeshProUGUI btn_PlayLabel;

        // ── Party Display Sub-Views ───────────────────────────────────────────
        [Header("Party Display")]
        [Tooltip("Bottom-left avatar row: one slot per party member + '+' for empty slots.")]
        [SerializeField] private PartyMemberListView memberListView;
        [Tooltip("Centre model row: full-body slot per member, placeholder when empty.")]
        [SerializeField] private PartyModelListView  modelListView;

        // ── Friend Panel Reference ────────────────────────────────────────────
        [Header("References")]
        [Tooltip("Used to: (a) show/hide invite-pending on friend rows, (b) set host mode.")]
        [SerializeField] private FriendPanelView friendPanelView;

        // ══════════════════════════════════════════════════════════════════════
        // SERVICES
        // ══════════════════════════════════════════════════════════════════════

        private PartyService         _partyService;
        private SessionState         _sessionState;

        // ══════════════════════════════════════════════════════════════════════
        // RUNTIME STATE
        // ══════════════════════════════════════════════════════════════════════

        private PartyResponse    _currentParty      = null;

        // Set to true before we intentionally disband/leave so HandlePartyDisbanded
        // suppresses the "party disbanded" toast on the PartyCustomMode panel.
        private bool _suppressDisbandToast = false;

        // Queue
        private RankedQueueState _queueState        = RankedQueueState.Idle;
        private float            _searchElapsed     = 0f;
        private string           _pendingLobbyToken;

        // Mode
        private GameModeEntry[] _enabledModes      = Array.Empty<GameModeEntry>();
        private int             _selectedModeIndex = 0;
        private GameModeEntry SelectedMode =>
            _enabledModes.Length > 0 ? _enabledModes[_selectedModeIndex] : default;

        // Map
        private MapEntry[] _currentMaps    = Array.Empty<MapEntry>();
        private int        _selectedMapIdx = 0;

        // ══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _partyService = GameManager.Instance.PartyService;
                _sessionState = GameManager.Instance.SessionState;
            }

            if (btn_Play     != null) btn_Play.onClick.AddListener(OnPlayClicked);
            if (modeDropdown != null) modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
            if (mapDropdown     != null) mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);
            NH_DropdownRuntime.NormalizeModeMapOrder(modeDropdown, mapDropdown);
        }

        private void Start()
        {
            SubscribeWSEvents();
            SetQueueState(RankedQueueState.Idle);
            RefreshGameModeSelector();
            GameModeConfig.OnConfigLoaded += RefreshGameModeSelector;
            MapConfig.OnConfigLoaded += HandleMapConfigLoaded;
        }

        private void HandleMapConfigLoaded()
        {
            if (SelectedMode.modeKey != null)
                RefreshMapDropdown(SelectedMode.modeKey);
        }

        private void Update()
        {
            if (_queueState == RankedQueueState.Searching)
            {
                _searchElapsed += Time.deltaTime;
                if (btn_PlayLabel != null)
                    btn_PlayLabel.text = $"{FormatTime(_searchElapsed)}  \u2715";
            }
        }

        private void OnDestroy()
        {
            UnsubscribeWSEvents();
            GameModeConfig.OnConfigLoaded -= RefreshGameModeSelector;
            MapConfig.OnConfigLoaded -= HandleMapConfigLoaded;
            if (modeDropdown != null) modeDropdown.onValueChanged.RemoveListener(OnModeDropdownChanged);
            if (mapDropdown  != null) mapDropdown.onValueChanged.RemoveListener(OnMapDropdownChanged);
        }

        // ══════════════════════════════════════════════════════════════════════
        // PUBLIC API — called by HomeView / FriendPanelView
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Called by HomeView.OnShow() — resets queue and refreshes party.</summary>
        public async void OnHomeShown()
        {
            await OnHomeShownAsync();
        }

        public async Task OnHomeShownAsync()
        {
            HLog($"HomeShown start mode={DescribeSelectedMode()} queue={_queueState} party={DescribeCurrentParty()} room={DescribeRoomState()}");
            // Do NOT force-reset to Idle here — let RefreshParty resolve the actual
            // server-side queue state (solo player may still be SEARCHING on server
            // after an app-resume, or may have been auto-cancelled).
            await RefreshParty(forceServer: true);
            HLog($"HomeShown done mode={DescribeSelectedMode()} queue={_queueState} party={DescribeCurrentParty()} room={DescribeRoomState()}");
        }

        /// <summary>
        /// Invite a friend to the party. Creates a new party first if none exists.
        /// Called by FriendPanelView when the user taps Invite on any friend row.
        /// </summary>
        public async void InviteFriendToParty(FriendResponse friend)
        {
            if (_partyService == null || friend == null) return;

            if (_currentParty == null)
            {
                var createResult = await _partyService.CreateParty();
                if (!createResult.Success)
                {
                    Debug.LogWarning($"[PartyController] CreateParty failed: {createResult.Message}");
                    ShowToast("Party", "Failed to create party.");
                    return;
                }
                _currentParty = createResult.Data;
                RefreshPartyDisplay();
            }

            var result = await _partyService.InviteToParty(friend.userId);
            if (result.Success)
            {
                ShowToast("Invited", $"Invite sent to {friend.username}.");
                Debug.Log($"[PartyController] Invite sent to userId={friend.userId} username='{friend.username}'");
                friendPanelView?.SetInvitePending(friend.userId, true);
                StartCoroutine(ClearInvitePendingAfter(friend.userId, 30f));
            }
            else
            {
                Debug.LogWarning($"[PartyController] InviteToParty failed: {result.Message}");
                ShowToast("Invite Error", result.Message ?? "Could not invite player.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // MODE DROPDOWN
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshGameModeSelector()
        {
            _enabledModes      = GameModeConfig.GetMatchmakingEnabled();
            if (_enabledModes == null || _enabledModes.Length == 0)
                _enabledModes = GameModeConfig.GetEnabled();
            _selectedModeIndex = 0;
            NH_DropdownRuntime.NormalizeModeMapOrder(modeDropdown, mapDropdown);

            if (modeDropdown == null) { OnGameModeChanged(0); return; }

            var names = new List<string>(_enabledModes.Length);
            foreach (var m in _enabledModes) names.Add(m.displayName);
            PopulateDropdown(modeDropdown, names, 0);
            OnGameModeChanged(0);
        }

        private async void OnModeDropdownChanged(int index)
        {
            if (this == null || gameObject == null) return;
            if (_enabledModes == null || index < 0 || index >= _enabledModes.Length) return;
            if (index == _selectedModeIndex) return;

            HLog($"ModeDropdown requested oldIndex={_selectedModeIndex} newIndex={index} oldMode={DescribeSelectedMode()} requestedMode={DescribeMode(_enabledModes[index])} queue={_queueState} partyBeforeRefresh={DescribeCurrentParty()} room={DescribeRoomState()}");
            await RefreshParty(forceServer: true);

            var newMode   = _enabledModes[index];
            int partySize = GetCurrentPartySize();
            HLog($"ModeDropdown after party refresh requestedMode={DescribeMode(newMode)} partySize={partySize} party={DescribeCurrentParty()} queue={_queueState}");

            if (_queueState == RankedQueueState.Searching)
            {
                ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex);
                HLog($"ModeDropdown blocked by active queue. requestedMode={DescribeMode(newMode)} party={DescribeCurrentParty()}");
                if (_currentParty != null && IsSoloMode(newMode))
                    PromptLeavePartyForSoloMode(index, newMode);
                else
                    PromptCancelQueueForModeChange(index, newMode);
                return;
            }

            if (_currentParty != null && IsSoloMode(newMode))
            {
                ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex);
                HLog($"ModeDropdown solo requested while in party. Prompting leave/disband. requestedMode={DescribeMode(newMode)} party={DescribeCurrentParty()}");
                PromptLeavePartyForSoloMode(index, newMode);
                return;
            }

            if (partySize > newMode.playersPerTeam)
            {
                ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex);
                HLog($"ModeDropdown blocked: partySize={partySize} > playersPerTeam={newMode.playersPerTeam}. requestedMode={DescribeMode(newMode)} party={DescribeCurrentParty()}");
                ShowToast("Mode Change",
                    $"Party has {partySize} members — reduce to {newMode.playersPerTeam} first.");
                return;
            }

            ApplyModeSelection(index);
            HLog($"ModeDropdown applied new mode={DescribeSelectedMode()} party={DescribeCurrentParty()}");
        }

        private void OnGameModeChanged(int index)
        {
            if (_enabledModes == null || index < 0 || index >= _enabledModes.Length) return;
            _selectedModeIndex = index;
            RefreshMapDropdown(SelectedMode.modeKey);
            RefreshPartyDisplay();
        }

        private void ApplyModeSelection(int index)
        {
            ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, index);
            OnGameModeChanged(index);
            HLog($"ApplyModeSelection index={index} selected={DescribeSelectedMode()} map={(_currentMaps.Length > 0 ? _currentMaps[_selectedMapIdx].mapId : "null")}");
        }

        private void PromptLeavePartyForSoloMode(int targetIndex, GameModeEntry targetMode)
        {
            if (_currentParty == null)
            {
                ApplyModeSelection(targetIndex);
                return;
            }

            string status = _currentParty.partyStatus ?? string.Empty;
            if (status != "IDLE" && status != "IN_QUEUE")
            {
                ShowToast("Mode Change", "Leave the current party room or match before switching to solo.");
                return;
            }

            bool isHost = IsCurrentUserPartyHost();
            bool willCancelQueue = _currentParty.IsInQueue;
            string modeName = string.IsNullOrWhiteSpace(targetMode.displayName) ? "Solo 1v1" : targetMode.displayName;
            string actionText = isHost ? "disband your ranked party" : "leave your ranked party";
            string queueText = willCancelQueue ? "cancel the party queue and " : string.Empty;
            HLog($"PromptLeavePartyForSoloMode target={DescribeMode(targetMode)} isHost={isHost} willCancelQueue={willCancelQueue} party={DescribeCurrentParty()}");

            if (GameModalWindow.Instance == null)
            {
                HLog("PromptLeavePartyForSoloMode modal missing; leaving/disbanding immediately.");
                _ = LeavePartyThenApplyMode(targetIndex);
                return;
            }

            GameModalWindow.Instance.ShowConfirm(
                "Switch to Solo?",
                $"{modeName} does not use a ranked party. Switching will {queueText}{actionText}.",
                onConfirm: async () => await LeavePartyThenApplyMode(targetIndex),
                onCancel: () => ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex),
                confirmText: isHost ? "Disband" : "Leave Party",
                cancelText: "Cancel");
        }

        private void PromptCancelQueueForModeChange(int targetIndex, GameModeEntry targetMode)
        {
            string modeName = string.IsNullOrWhiteSpace(targetMode.displayName) ? "the selected mode" : targetMode.displayName;
            HLog($"PromptCancelQueueForModeChange target={DescribeMode(targetMode)} queue={_queueState} party={DescribeCurrentParty()}");

            if (GameModalWindow.Instance == null)
            {
                HLog("PromptCancelQueueForModeChange modal missing; canceling immediately.");
                _ = CancelQueueThenApplyMode(targetIndex);
                return;
            }

            GameModalWindow.Instance.ShowConfirm(
                "Cancel Matchmaking?",
                $"Changing to {modeName} will cancel the current matchmaking search.",
                onConfirm: async () => await CancelQueueThenApplyMode(targetIndex),
                onCancel: () => ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex),
                confirmText: "Cancel Queue",
                cancelText: "Keep Searching");
        }

        private async Task CancelQueueThenApplyMode(int targetIndex)
        {
            await CancelQueue();
            ApplyModeSelection(targetIndex);
        }

        private async Task LeavePartyThenApplyMode(int targetIndex)
        {
            if (_partyService == null)
            {
                HLog($"LeavePartyThenApplyMode blocked: PartyService null. targetIndex={targetIndex} party={DescribeCurrentParty()}");
                ShowToast("Mode Change", "Party service is not ready.");
                ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex);
                return;
            }

            if (_currentParty == null)
            {
                HLog($"LeavePartyThenApplyMode no party; applying targetIndex={targetIndex}");
                ApplyModeSelection(targetIndex);
                return;
            }

            bool isHost = IsCurrentUserPartyHost();
            HLog($"LeavePartyThenApplyMode start targetIndex={targetIndex} isHost={isHost} party={DescribeCurrentParty()}");

            if (_currentParty.IsInQueue)
            {
                var cancel = await _partyService.CancelQueue();
                HLog($"LeavePartyThenApplyMode cancel queue result success={cancel.Success} errorCode={cancel.ErrorCode ?? "null"} msg='{cancel.Message ?? "null"}'");
                if (!cancel.Success)
                {
                    ShowToast("Mode Change", cancel.Message ?? "Could not cancel party queue.");
                    ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex);
                    await RefreshParty();
                    return;
                }
            }

            var result = isHost
                ? await _partyService.DisbandParty()
                : await _partyService.LeaveParty();
            HLog($"LeavePartyThenApplyMode party leave/disband result success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}'");

            if (!result.Success)
            {
                ShowToast("Mode Change", result.Message ?? "Could not leave party.");
                ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _selectedModeIndex);
                await RefreshParty();
                return;
            }

            _currentParty = null;
            ApplyModeSelection(targetIndex);
            await RefreshParty();
            HLog($"LeavePartyThenApplyMode done targetIndex={targetIndex} selected={DescribeSelectedMode()} party={DescribeCurrentParty()}");
        }

        // ══════════════════════════════════════════════════════════════════════
        // MAP DROPDOWN
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshMapDropdown(string modeKey)
        {
            _currentMaps    = MapConfig.GetByMode(modeKey);
            if (_currentMaps == null || _currentMaps.Length == 0)
                _currentMaps = MapConfig.GetAvailable();
            _selectedMapIdx = 0;
            if (mapDropdown == null) return;
            var names = new List<string>(_currentMaps.Length);
            foreach (var m in _currentMaps) names.Add(m.displayName);
            PopulateDropdown(mapDropdown, names, 0);
        }

        private void OnMapDropdownChanged(int index)
        {
            if (index >= 0 && index < _currentMaps.Length)
                _selectedMapIdx = index;
        }

        private string GetSelectedMapId()
        {
            if (_currentMaps == null || _selectedMapIdx < 0 || _selectedMapIdx >= _currentMaps.Length)
                return null;

            return _currentMaps[_selectedMapIdx].mapId;
        }

        // ══════════════════════════════════════════════════════════════════════
        // PARTY DISPLAY
        // ══════════════════════════════════════════════════════════════════════

        private async Task RefreshParty(bool forceServer = false)
        {
            HLog($"RefreshParty start forceServer={forceServer} current={DescribeCurrentParty()}");
            if (_partyService == null) { Debug.LogWarning("[FLOW][HOME_MODE] _partyService is null - skipping GetParty"); RefreshPartyDisplay(); return; }
            if (forceServer)
                APICache.Invalidate(APICache.KEY_PARTY_STATE);
            var result = await _partyService.GetParty();
            if (result.Success && result.Data != null)
            {
                _currentParty = result.Data;
                HLog($"RefreshParty response OK party={DescribeCurrentParty()}");
                if (_currentParty.members != null)
                {
                    for (int i = 0; i < _currentParty.members.Count; i++)
                    {
                        var m = _currentParty.members[i];
                        HLog($"RefreshParty member[{i}] userId={m.userId} username='{m.username}' isHost={m.isHost} joinOrder={m.joinOrder} onlineStatus={m.onlineStatus} selectedCharacterId='{m.selectedCharacterId}'");
                    }
                }
            }
            else
            {
                _currentParty = null;
                HLog($"RefreshParty response no active party success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}'");
            }

            if (_currentParty != null)
            {
                // Party player: trust the party's queue state
                if (_currentParty.IsInQueue)
                    SetQueueState(RankedQueueState.Searching);
                else if (_queueState == RankedQueueState.Searching && string.IsNullOrEmpty(_pendingLobbyToken))
                    SetQueueState(RankedQueueState.Idle);
            }
            else
            {
                // Solo player: check server-side queue entry to detect state drift
                // (e.g. client reconnected after crash — server already cancelled the entry
                // via WS disconnect dequeue, but client UI still shows 'Searching').
                await SyncSoloQueueStateAsync();
            }

            RefreshPartyDisplay();
            HLog($"RefreshParty done queue={_queueState} party={DescribeCurrentParty()} selectedMode={DescribeSelectedMode()}");
        }

        /// <summary>
        /// For solo (no-party) players: polls GET /api/matchmaking/queue/status and
        /// reconciles local <see cref="_queueState"/> with server reality.
        /// • Server says SEARCHING/MATCHED  → stay/enter Searching UI
        /// • Server says not in queue       → reset to Idle (removes stuck timer)
        /// Skipped when a WS event (match_found / match_cancelled) is already in-flight
        /// (<see cref="_pendingLobbyToken"/> is set).
        /// </summary>
        private async Task SyncSoloQueueStateAsync()
        {
            // Don't poll if we're mid-accept flow — the WS event already owns this state.
            if (!string.IsNullOrEmpty(_pendingLobbyToken)) return;

            try
            {
                var client = GameManager.Instance?.BackendClient;
                if (client == null) return;

                var response = await client.GetAsync<QueueStatusResponse>(Constants.API_MATCHMAKING_STATUS);
                if (!response.Success)
                {
                    // Non-fatal: network hiccup or token expired; leave UI as-is.
                    HLog($"SyncSoloQueueState: GET status failed ({response.Message})");
                    return;
                }

                bool serverActive = response.Data?.IsActive == true;
                HLog($"SyncSoloQueueState: serverActive={serverActive} localState={_queueState} serverStatus={response.Data?.status ?? "null"}");

                if (serverActive && _queueState == RankedQueueState.Idle)
                {
                    // Server still queuing for us (e.g. app resumed quickly) — restore UI.
                    HLog("SyncSoloQueueState: restoring Searching state from server");
                    SetQueueState(RankedQueueState.Searching);
                    _searchElapsed = response.Data.waitSeconds;
                }
                else if (!serverActive && _queueState == RankedQueueState.Searching)
                {
                    // Server cancelled our queue (timeout / dequeue on disconnect) — fix stuck timer.
                    HLog("SyncSoloQueueState: server has no active entry — resetting to Idle");
                    SetQueueState(RankedQueueState.Idle);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PartyController] SyncSoloQueueStateAsync failed: {ex.Message}");
            }
        }

        public void RefreshPartyDisplay()
        {
            int maxSlots = SelectedMode.modeKey != null ? SelectedMode.playersPerTeam : 3;
            if (maxSlots <= 0) maxSlots = 3;

            bool iAmHost = _currentParty != null &&
                           _currentParty.hostUserId == (_sessionState?.UserId ?? -1L);

            memberListView?.Refresh(_currentParty, maxSlots,
                iAmHost:         iAmHost,
                onInviteClicked: OnInviteSlotClicked,
                onKick: uid => GameModalWindow.Instance?.ShowConfirm(
                    "Kick Member?", "Are you sure you want to kick this member?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.KickMember(uid); },
                    confirmText: "Kick", cancelText: "Cancel"),
                onLeave: () => GameModalWindow.Instance?.ShowConfirm(
                    "Leave Party?", "Are you sure you want to leave the party?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.LeaveParty(); },
                    confirmText: "Leave Party", cancelText: "Cancel"));

            modelListView?.Refresh(_currentParty,
                iAmHost: iAmHost,
                onKick: uid => GameModalWindow.Instance?.ShowConfirm(
                    "Kick Member?", "Are you sure you want to kick this member?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.KickMember(uid); },
                    confirmText: "Kick", cancelText: "Cancel"),
                onLeave: () => GameModalWindow.Instance?.ShowConfirm(
                    "Leave Party?", "Are you sure you want to leave the party?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.LeaveParty(); },
                    confirmText: "Leave Party", cancelText: "Cancel"));

            bool canInviteFriends = _currentParty == null || IsPartyIdle(_currentParty);
            friendPanelView?.SetCanInviteToParty(canInviteFriends);
        }

        private void OnInviteSlotClicked() => friendPanelView?.ShowForInvite();

        // ══════════════════════════════════════════════════════════════════════
        // RANKED QUEUE — PLAY BUTTON
        // ══════════════════════════════════════════════════════════════════════

        private void OnPlayClicked()
        {
            if (_queueState == RankedQueueState.Idle)
                _ = StartQueue();
            else if (_queueState == RankedQueueState.Searching)
                _ = CancelQueue();
        }

        private async Task StartQueue()
        {
            HLog($"StartQueue requested mode={DescribeSelectedMode()} queue={_queueState} partyBeforeRefresh={DescribeCurrentParty()} room={DescribeRoomState()}");
            // Block if already in a custom room — must leave first.
            if (RoomState.Instance != null && RoomState.Instance.IsInRoom)
            {
                HLog($"StartQueue blocked: local custom room active. room={DescribeRoomState()}");
                ShowToast("Matchmaking", "Leave the custom room before joining matchmaking.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            await RefreshParty(forceServer: true);

            if (string.IsNullOrWhiteSpace(SelectedMode.modeKey))
            {
                HLog("StartQueue blocked: selected mode key is empty.");
                ShowToast("Matchmaking", "Select a game mode before joining matchmaking.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            bool isPartyHost = IsCurrentUserPartyHost();
            int partySize = GetCurrentPartySize();
            bool hasParty = _currentParty != null;
            HLog($"StartQueue after party refresh mode={DescribeSelectedMode()} hasParty={hasParty} isPartyHost={isPartyHost} partySize={partySize} party={DescribeCurrentParty()}");

            // Server-authoritative check: party is in a CUSTOM lobby room
            // (partyMode=CUSTOM). Must leave before starting ranked matchmaking.
            if (hasParty && _currentParty.IsCustom)
            {
                HLog($"StartQueue blocked: party is in CUSTOM mode (in a room). party={DescribeCurrentParty()}");
                ShowToast("Matchmaking", "Leave the party custom mode before starting ranked matchmaking.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            if (hasParty && _currentParty.IsInQueue)
            {
                HLog($"StartQueue blocked: party already in queue. party={DescribeCurrentParty()}");
                SetQueueState(RankedQueueState.Searching);
                ShowToast("Matchmaking", "Party is already searching.");
                return;
            }

            if (hasParty && IsSoloMode(SelectedMode))
            {
                HLog($"StartQueue blocked: solo selected while in party. mode={DescribeSelectedMode()} party={DescribeCurrentParty()}");
                ShowToast("Matchmaking", "Solo 1v1 cannot start while you are in a ranked party.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            if (hasParty && !isPartyHost)
            {
                HLog($"StartQueue blocked: non-host tried queue. party={DescribeCurrentParty()}");
                ShowToast("Matchmaking", "Only the party host can start matchmaking.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            if (hasParty && partySize > SelectedMode.playersPerTeam)
            {
                HLog($"StartQueue blocked: partySize={partySize} > playersPerTeam={SelectedMode.playersPerTeam}. party={DescribeCurrentParty()} mode={DescribeSelectedMode()}");
                ShowToast("Matchmaking",
                    $"Party has {partySize} members — select a larger mode or remove players first.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            if (hasParty && !SelectedMode.allowFill && partySize < SelectedMode.playersPerTeam)
            {
                HLog($"StartQueue blocked: partySize={partySize} < required={SelectedMode.playersPerTeam} and allowFill=false. party={DescribeCurrentParty()} mode={DescribeSelectedMode()}");
                ShowToast("Matchmaking",
                    $"This mode requires {SelectedMode.playersPerTeam} party members.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

            string modeKey   = SelectedMode.modeKey;
            string mapId     = _currentMaps.Length > 0 ? _currentMaps[_selectedMapIdx].mapId : null;
            bool   allowFill = SelectedMode.allowFill;

            SetQueueState(RankedQueueState.Searching);
            _searchElapsed = 0f;
            HLog($"StartQueue sending request isPartyHost={isPartyHost} mode={modeKey} map={mapId ?? "null"} allowFill={allowFill} party={DescribeCurrentParty()}");

            bool success;
            string failureMessage = null;
            string failureCode = null;
            if (isPartyHost)
            {
                var r = await _partyService.QueueParty(modeKey, allowFill, mapId);
                success = r.Success;
                failureMessage = r.Message;
                failureCode = r.ErrorCode;
                HLog($"StartQueue party queue response success={r.Success} errorCode={r.ErrorCode ?? "null"} msg='{r.Message ?? "null"}'");
            }
            else
            {
                var r = await GameManager.Instance.BackendClient.PostAsync<object>(
                    Constants.API_MATCHMAKING_QUEUE,
                    new MatchmakingQueueRequest { gameMode = modeKey, mapId = mapId, allowFill = allowFill, platform = GetClientPlatform() });
                success = r.Success;
                failureMessage = r.Message;
                failureCode = r.ErrorCode;
                HLog($"StartQueue solo queue response success={r.Success} errorCode={r.ErrorCode ?? "null"} msg='{r.Message ?? "null"}'");
            }

            if (!success)
            {
                SetQueueState(RankedQueueState.Idle);
                Debug.LogWarning($"[FLOW][HOME_MODE] Failed to join matchmaking queue errorCode={failureCode ?? "null"} msg='{failureMessage ?? "null"}'.");
                ShowToast("Matchmaking", failureMessage ?? "Failed to join queue. Please try again.");
                return;
            }

            HLog($"StartQueue success queue={_queueState} mode={modeKey} party={DescribeCurrentParty()}");
        }

        private async Task CancelQueue()
        {
            bool isPartyHost = _currentParty != null && _currentParty.IsInQueue && _partyService != null;
            HLog($"CancelQueue requested isPartyHost={isPartyHost} queue={_queueState} party={DescribeCurrentParty()}");
            if (isPartyHost)
            {
                var result = await _partyService.CancelQueue();
                HLog($"CancelQueue party response success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}'");
            }
            else
            {
                var result = await GameManager.Instance.BackendClient.DeleteAsync<object>(Constants.API_MATCHMAKING_QUEUE);
                HLog($"CancelQueue solo response success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}'");
            }

            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
            if (_currentParty != null)
                await RefreshParty();
            HLog($"CancelQueue done queue={_queueState} party={DescribeCurrentParty()}");
        }

        private void SetQueueState(RankedQueueState newState)
        {
            HLog($"SetQueueState {_queueState} -> {newState}");
            _queueState = newState;
            if (btn_PlayLabel == null) return;
            if (newState == RankedQueueState.Idle)
            {
                _searchElapsed    = 0f;
                btn_PlayLabel.text = "PLAY";
            }
            else
            {
                btn_PlayLabel.text = "00:00  \u2715";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // NAVIGATION — CUSTOM LOBBY
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by HomeUIActionRouter when the Custom Mode button is pressed.
        /// Checks queue/party state before navigating to PartyCustomMode.
        /// </summary>
        public async void OnPartyCustomModeClicked()
        {
            HLog($"PartyCustomMode clicked queue={_queueState} party={DescribeCurrentParty()} room={DescribeRoomState()} mode={DescribeSelectedMode()}");
            if (_queueState == RankedQueueState.Searching)
            {
                GameModalWindow.Instance?.ShowConfirm(
                    "Matchmaking Active",
                    "You are currently in the matchmaking queue. Leaving will cancel the search.",
                    onConfirm: async () => { await CancelQueue(); NavigatePartyCustomMode(); },
                    confirmText: "Leave Queue", cancelText: "Cancel");
                return;
            }

            await RefreshParty(true);
            if (_currentParty != null)
            {
                string status = string.IsNullOrWhiteSpace(_currentParty.partyStatus) ? "IDLE" : _currentParty.partyStatus;
                // Use server-authoritative partyMode field when available;
                // fall back to partyStatus string for older server versions.
                bool isRankedQueued = _currentParty.IsRanked ||
                    string.Equals(_currentParty.partyStatus, "IN_QUEUE", StringComparison.OrdinalIgnoreCase);

                if (isRankedQueued)
                {
                    GameModalWindow.Instance?.ShowConfirm(
                        "Party Queue Active",
                        "Cancel the party matchmaking queue before opening a party custom mode.",
                        onConfirm: async () => { await CancelQueue(); await LeaveOrDisbandPartyThenNavigatePartyCustomMode(); },
                        confirmText: "Cancel Queue",
                        cancelText: "Stay");
                    return;
                }

                if (!string.Equals(status, "IDLE", StringComparison.OrdinalIgnoreCase))
                {
                    ShowToast("Party", "Finish or leave the current party room before opening a party custom mode.");
                    HLog($"PartyCustomMode blocked currentParty={DescribeCurrentParty()}");
                    return;
                }

                string desc = IsCurrentUserPartyHost()
                    ? "Custom lobbies are separate from party matchmaking. Disband your current party before opening Party Custom Mode?"
                    : "Custom lobbies are separate from party matchmaking. Leave your current party before opening Party Custom Mode?";
                string confirmText = IsCurrentUserPartyHost() ? "Disband & Open" : "Leave & Open";

                if (GameModalWindow.Instance != null)
                {
                    GameModalWindow.Instance.ShowConfirm(
                        "Leave Party?",
                        desc,
                        onConfirm: async () => await LeaveOrDisbandPartyThenNavigatePartyCustomMode(),
                        confirmText: confirmText,
                        cancelText: "Cancel");
                    return;
                }

                await LeaveOrDisbandPartyThenNavigatePartyCustomMode();
                return;
            }

            NavigatePartyCustomMode();
        }

        private async Task LeaveOrDisbandPartyThenNavigatePartyCustomMode()
        {
            if (_partyService == null)
            {
                ShowToast("Party", "Party service is not ready.");
                return;
            }

            bool isHost = IsCurrentUserPartyHost();
            HLog($"LeaveOrDisbandPartyThenNavigatePartyCustomMode start isHost={isHost} party={DescribeCurrentParty()}");
            _suppressDisbandToast = true;
            var result = isHost
                ? await _partyService.DisbandParty()
                : await _partyService.LeaveParty();

            if (!result.Success)
            {
                _suppressDisbandToast = false;
                ShowToast("Party", result.Message ?? "Failed to leave current party.");
                await RefreshParty(true);
                return;
            }

            _currentParty = null;
            RefreshPartyDisplay();
            NavigatePartyCustomMode();
        }

        private void NavigatePartyCustomMode()
        {
            if (UINavigator.Instance != null)
            {
                string modeKey = SelectedMode.modeKey;
                string mapId = GetSelectedMapId();
                var payload = new PartyCustomModeView.NavigationPayload(modeKey, mapId);
                HLog($"NavigatePartyCustomMode mode={DescribeSelectedMode()} map={mapId ?? "null"} queue={_queueState} party={DescribeCurrentParty()} room={DescribeRoomState()}");
                _ = UINavigator.Instance.ShowPanelAsync(PanelType.PartyCustomMode, reason: "HomePartyCustomMode", payload: payload);
                return;
            }

            Debug.LogWarning("[FLOW][HOME_MODE] Cannot open Party Custom Mode: UINavigator is missing.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // WS — SUBSCRIPTIONS
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeWSEvents()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;

            bus.OnMatchFound              += HandleMatchFound;
            bus.OnMatchReady              += HandleMatchReady;
            bus.OnMatchCancelled          += HandleMatchCancelled;

            bus.OnPartyInvitationReceived += HandlePartyInvitationReceived;
            bus.OnPartyInvitationDeclined  += HandlePartyInvitationDeclined;
            bus.OnPartyInvitationCancelled += HandlePartyInvitationCancelled;
            bus.OnPartyInvitationExpired   += HandlePartyInvitationExpired;
            bus.OnPartyMemberJoined       += HandlePartyMemberJoined;
            bus.OnPartyMemberLeft         += HandlePartyMemberLeft;
            bus.OnPartyMemberKicked       += HandlePartyMemberKicked;
            bus.OnPartyDisbanded          += HandlePartyDisbanded;
            bus.OnPartyHostChanged        += HandlePartyHostChanged;
            bus.OnPartyStatusChanged      += HandlePartyStatusChanged;
        }

        private void UnsubscribeWSEvents()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.OnMatchFound              -= HandleMatchFound;
            bus.OnMatchReady              -= HandleMatchReady;
            bus.OnMatchCancelled          -= HandleMatchCancelled;
            bus.OnPartyInvitationReceived -= HandlePartyInvitationReceived;
            bus.OnPartyInvitationDeclined  -= HandlePartyInvitationDeclined;
            bus.OnPartyInvitationCancelled -= HandlePartyInvitationCancelled;
            bus.OnPartyInvitationExpired   -= HandlePartyInvitationExpired;
            bus.OnPartyMemberJoined       -= HandlePartyMemberJoined;
            bus.OnPartyMemberLeft         -= HandlePartyMemberLeft;
            bus.OnPartyMemberKicked       -= HandlePartyMemberKicked;
            bus.OnPartyDisbanded          -= HandlePartyDisbanded;
            bus.OnPartyHostChanged        -= HandlePartyHostChanged;
            bus.OnPartyStatusChanged      -= HandlePartyStatusChanged;
        }

        // ── Matchmaking ───────────────────────────────────────────────────────

        private async void HandleMatchFound(GameWebSocketService.MatchFoundEvent e)
        {
            Debug.Log($"[SPAWN][FLOW] MatchFound received — gameMode={e?.gameMode} playerCount={e?.playerIds?.Length ?? 0} lobbyToken={e?.lobbyToken ?? "null"}");
            if (e == null || string.IsNullOrEmpty(e.lobbyToken)) return;

            _pendingLobbyToken = e.lobbyToken;

            // Hiện overlay "DS đang khởi động" — informational only, không có nút xác nhận.
            // Overlay này ẩn đi khi nhận match_ready (HandleMatchReady) hoặc match_cancelled.
            MatchFoundOverlay.Instance?.Show(e.gameMode, e.playerIds, _sessionState?.UserId ?? 0L);

            // Auto-accept ngay lập tức — không cần user xác nhận.
            try
            {
                var result = await GameManager.Instance.BackendClient.PostAsync<object>(
                    Constants.API_MATCHMAKING_ACCEPT,
                    new MatchmakingAcceptRequest { lobbyToken = e.lobbyToken });

                if (!result.Success && _pendingLobbyToken == e.lobbyToken)
                {
                    _pendingLobbyToken = null;
                    MatchFoundOverlay.Instance?.Hide();
                    SetQueueState(RankedQueueState.Idle);
                    ShowToast("Matchmaking", result.Message ?? "Failed to join match.");
                }
                // Success → chờ match_ready WS. MatchFlowCoordinator sẽ handle scene loading.
            }
            catch (Exception ex)
            {
                if (_pendingLobbyToken == e.lobbyToken)
                {
                    _pendingLobbyToken = null;
                    MatchFoundOverlay.Instance?.Hide();
                    SetQueueState(RankedQueueState.Idle);
                }
                Debug.LogError($"[PartyController] Auto-accept failed lobby={e.lobbyToken}: {ex.Message}");
            }
        }

        private void HandleMatchReady(GameWebSocketService.MatchReadyEvent e)
        {
            MatchFoundOverlay.Instance?.Hide();
            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
            Debug.Log($"[PartyController] match_ready UI reset only. MatchFlowCoordinator owns scene loading. matchId={e.matchId}");
        }

        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent e)
        {
            // Ẩn overlay "tìm thấy trận" (nếu đang mở)
            MatchFoundOverlay.Instance?.Hide();

            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
            Debug.Log($"[PartyController] MatchCancelled — reason={e.reason}");
        }

        // ── Party ─────────────────────────────────────────────────────────────

        private static string GetClientPlatform()
        {
            return Application.isMobilePlatform ? "MOBILE" : "PC";
        }

        private void HandlePartyInvitationReceived(GameWebSocketService.PartyInvitationEvent e)
        {
            string inviterName  = string.IsNullOrEmpty(e.inviterUsername) ? "Someone" : e.inviterUsername;
            long   invitationId = e.invitationId;
            GameModalWindow.Instance?.ShowCountdown(
                title:        "Party Invitation",
                desc:         $"<b>{inviterName}</b> invited you to join their party.",
                seconds:      30,
                onConfirm:    () => _ = AcceptPartyInvitation(invitationId),
                onExpire:     () => _ = DeclinePartyInvitation(invitationId),
                showConfirm:  true,
                confirmText:  "Accept",
                cancelText:   "Decline",
                invitationId: invitationId);
        }

        // Invitee declined our invite — clear the pending spinner on their friend row.
        private void HandlePartyInvitationDeclined(GameWebSocketService.PartyInvitationResponseEvent e)
        {
            friendPanelView?.SetInvitePending(e.inviteeUserId, false);
            Debug.Log($"[PartyController] PartyInvitationDeclined \u2014 inviteeUserId={e.inviteeUserId} invitationId={e.invitationId}");
        }

        // The inviter withdrew their invite — close the countdown popup.
        private void HandlePartyInvitationCancelled(GameWebSocketService.PartyInvitationResponseEvent e)
        {
            GameModalWindow.Instance?.DismissIfMatchingInvitation(e.invitationId);
            Debug.Log($"[PartyController] PartyInvitationCancelled \u2014 inviterUserId={e.inviterUserId} invitationId={e.invitationId}");
        }

        // Invitation timed out — inviter clears spinner; invitee popup already handled by countdown.
        private void HandlePartyInvitationExpired(GameWebSocketService.PartyInvitationResponseEvent e)
        {
            long myId = _sessionState?.UserId ?? 0L;
            if (myId == e.inviterUserId)
            {
                friendPanelView?.SetInvitePending(e.inviteeUserId, false);
                Debug.Log($"[PartyController] PartyInvitationExpired (I'm inviter) \u2014 inviteeUserId={e.inviteeUserId}");
            }
            else
            {
                GameModalWindow.Instance?.DismissIfMatchingInvitation(e.invitationId);
                Debug.Log($"[PartyController] PartyInvitationExpired (I'm invitee) \u2014 inviterUserId={e.inviterUserId}");
            }
        }

        private async Task AcceptPartyInvitation(long invitationId)
        {
            if (_partyService == null) return;

            var partyResult = await _partyService.GetParty();
            if (partyResult.Success && partyResult.Data != null)
            {
                _currentParty = partyResult.Data;
                string confirmTitle = "Switch Party?";
                string confirmMessage = "Accepting this invite will leave your current party and join the inviter's party.";
                string confirmText = "Leave & Join";

                if (GameModalWindow.Instance != null)
                {
                    GameModalWindow.Instance.ShowConfirm(
                        confirmTitle,
                        confirmMessage,
                        onConfirm: async () => await AcceptPartyInvitationNow(invitationId),
                        confirmText: confirmText,
                        cancelText: "Cancel");
                    return;
                }

                await AcceptPartyInvitationNow(invitationId);
                return;
            }

            await AcceptPartyInvitationNow(invitationId);
        }

        private async Task AcceptPartyInvitationNow(long invitationId)
        {
            if (_partyService == null) return;
            var result = await _partyService.AcceptInvitation(invitationId);
            if (result.Success)
            {
                _currentParty = result.Data;
                RefreshPartyDisplay();
                HLog($"AcceptPartyInvitation success party={DescribeCurrentParty()} invitationId={invitationId}");
            }
            else
            {
                ShowToast("Party", result.Message ?? "Failed to join party.");
                Debug.LogWarning($"[PartyController] AcceptInvitation failed: {result.Message}");
            }
        }

        private async Task DeclinePartyInvitation(long invitationId)
        {
            if (_partyService == null) return;
            await _partyService.DeclineInvitation(invitationId);
        }

        private void HandlePartyMemberJoined(GameWebSocketService.PartyMemberJoinedEvent e)
        {
            friendPanelView?.SetInvitePending(e.userId, false);
            string name = string.IsNullOrEmpty(e.username) ? "A player" : e.username;
            ShowToast("Party", $"{name} joined the party.");
            Debug.Log($"[PartyController] PartyMemberJoined — userId={e.userId} username='{e.username}'");
            _ = RefreshParty();
        }

        private void HandlePartyMemberLeft(GameWebSocketService.PartyMemberLeftEvent e)
        {
            bool isSelf = e.userId == (_sessionState?.UserId ?? -1L);
            if (isSelf) _currentParty = null;
            ShowToast("Party", isSelf ? "You left the party." : "A party member left.");
            Debug.Log($"[PartyController] PartyMemberLeft — userId={e.userId} isSelf={isSelf}");
            _ = RefreshParty();
        }

        private void HandlePartyMemberKicked(GameWebSocketService.PartyMemberKickedEvent e)
        {
            if (e.kickedUserId == (_sessionState?.UserId ?? -1L))
            {
                _currentParty = null;
                ShowToast("Party", "You were kicked from the party.");
            }
            Debug.Log($"[PartyController] PartyMemberKicked — kickedUserId={e.kickedUserId} kickerUserId={e.kickerUserId}");
            _ = RefreshParty();
        }

        private void HandlePartyDisbanded(GameWebSocketService.PartyDisbandedEvent e)
        {
            _currentParty = null;
            SetQueueState(RankedQueueState.Idle);
            RefreshPartyDisplay();
            if (!_suppressDisbandToast)
                ShowToast("Party", "The party has been disbanded.");
            _suppressDisbandToast = false;
            Debug.Log($"[PartyController] PartyDisbanded — partyId={e.partyId}");
        }

        private void HandlePartyHostChanged(GameWebSocketService.PartyHostChangedEvent e)
        {
            if (e.newHostUserId == (_sessionState?.UserId ?? -1L))
                ShowToast("Party", "You are now the party host.");
            Debug.Log($"[PartyController] PartyHostChanged — new hostUserId={e.newHostUserId}");
            _ = RefreshParty();
        }

        private void HandlePartyStatusChanged(GameWebSocketService.PartyStatusChangedEvent e)
            => _ = RefreshParty();

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator ClearInvitePendingAfter(long userId, float delay)
        {
            yield return new WaitForSeconds(delay);
            friendPanelView?.SetInvitePending(userId, false);
        }

        private void ShowToast(string title, string message)
        {
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            HLog($"ShowToast title='{title}' message='{message}' service={(toast == null ? "null" : toast.name)}");
            if (toast == null)
            {
                Debug.LogError("[FLOW][HOME_MODE] ShowToast aborted: ToastService not found.");
                return;
            }
            toast.Show(title: title, message: message);
        }

        private void HLog(string message)
        {
            Debug.Log($"[FLOW][HOME_MODE] {message}");
        }

        private string DescribeSelectedMode()
        {
            if (_enabledModes == null || _enabledModes.Length == 0 || _selectedModeIndex < 0 || _selectedModeIndex >= _enabledModes.Length)
                return "none";

            return DescribeMode(_enabledModes[_selectedModeIndex]);
        }

        private static string DescribeMode(GameModeEntry mode)
        {
            return
                $"key={mode.modeKey ?? "null"},name={mode.displayName ?? "null"},playersPerTeam={mode.playersPerTeam},allowFill={mode.allowFill}";
        }

        private string DescribeCurrentParty()
        {
            if (_currentParty == null)
                return "none";

            int members = _currentParty.members != null ? _currentParty.members.Count : _currentParty.currentMemberCount;
            return
                $"id={_currentParty.partyId},host={_currentParty.hostUserId},status={_currentParty.partyStatus},members={members}/{_currentParty.maxMembers}";
        }

        private static string DescribeRoomState()
        {
            var room = RoomState.Instance;
            if (room == null)
                return "roomState=null";

            return
                $"isInRoom={room.IsInRoom},roomId={room.RoomId},code={room.RoomCode},status={room.Status},players={room.PlayerCount}";
        }

        private bool IsCurrentUserPartyHost()
        {
            return _currentParty != null &&
                   _currentParty.hostUserId == (_sessionState?.UserId ?? -1L);
        }

        private int GetCurrentPartySize()
        {
            if (_currentParty == null)
                return 0;

            if (_currentParty.members != null)
                return _currentParty.members.Count;

            return _currentParty.currentMemberCount;
        }

        private static bool IsPartyIdle(PartyResponse party)
        {
            return party == null ||
                   string.IsNullOrWhiteSpace(party.partyStatus) ||
                   string.Equals(party.partyStatus, "IDLE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSoloMode(GameModeEntry mode)
        {
            return mode.playersPerTeam <= 1 ||
                   string.Equals(mode.modeKey, "1v1", StringComparison.OrdinalIgnoreCase);
        }

        private static void PopulateDropdown(CustomDropdown dd, List<string> names, int selectIndex)
        {
            try
            {
                NH_DropdownRuntime.Populate(dd, names, selectIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PartyController] Safe population of dropdown failed: {ex.Message}");
            }
        }

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60);
            int s = (int)(seconds % 60);
            return $"{m:00}:{s:00}";
        }

        private enum RankedQueueState { Idle, Searching }
    }
}
