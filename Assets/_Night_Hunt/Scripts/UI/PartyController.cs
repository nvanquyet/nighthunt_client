using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Networking;
using NightHunt.Services.Game;
using NightHunt.Services.Party;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    ///   • Custom Lobby navigation shortcut
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

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [Tooltip("Nút mở Custom Lobby. Nếu đang ghép trận sẽ show modal xác nhận trước.")]
        [SerializeField] private Button btn_CustomLobby;
        [Tooltip("Action thực thi sau khi xác nhận (hoặc ngay lập tức nếu không đang ghép trận).\nWire trong Inspector: ví dụ MainPanelManager.OpenPanel(CustomGame).")]
        [SerializeField] private UnityEvent evt_CustomLobbyConfirmed;

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
        private GameWebSocketService _ws;

        // ══════════════════════════════════════════════════════════════════════
        // RUNTIME STATE
        // ══════════════════════════════════════════════════════════════════════

        private PartyResponse    _currentParty      = null;

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

            if (btn_Play        != null) btn_Play.onClick.AddListener(OnPlayClicked);
            if (btn_CustomLobby != null) btn_CustomLobby.onClick.AddListener(OnCustomLobbyClicked);
            if (modeDropdown    != null) modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
            if (mapDropdown     != null) mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);
        }

        private void Start()
        {
            _ws = GameWebSocketService.Instance;
            SubscribeWSEvents();
            SetQueueState(RankedQueueState.Idle);
            RefreshGameModeSelector();
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
            if (modeDropdown != null) modeDropdown.onValueChanged.RemoveListener(OnModeDropdownChanged);
            if (mapDropdown  != null) mapDropdown.onValueChanged.RemoveListener(OnMapDropdownChanged);
        }

        // ══════════════════════════════════════════════════════════════════════
        // PUBLIC API — called by HomeView / FriendPanelView
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Called by HomeView.OnShow() — resets queue and refreshes party.</summary>
        public async void OnHomeShown()
        {
            SetQueueState(RankedQueueState.Idle);
            await RefreshParty();
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
                    ShowToast("L\u1ed7i", "Kh\u00f4ng th\u1ec3 t\u1ea1o nh\u00f3m.");
                    return;
                }
                _currentParty = createResult.Data;
                RefreshPartyDisplay();
            }

            var result = await _partyService.InviteToParty(friend.userId);
            if (result.Success)
            {
                ShowToast("\u0110\u00e3 m\u1eddi", $"\u0110\u00e3 g\u1eedi l\u1eddi m\u1eddi t\u1edbi {friend.username}.");
                friendPanelView?.SetInvitePending(friend.userId, true);
                StartCoroutine(ClearInvitePendingAfter(friend.userId, 30f));
            }
            else
            {
                ShowToast("L\u1ed7i m\u1eddi", result.Message ?? "Kh\u00f4ng th\u1ec3 m\u1eddi ng\u01b0\u1eddi ch\u01a1i.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // MODE DROPDOWN
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshGameModeSelector()
        {
            _enabledModes      = GameModeConfig.GetEnabled();
            _selectedModeIndex = 0;

            if (modeDropdown == null) { OnGameModeChanged(0); return; }

            var names = new List<string>(_enabledModes.Length);
            foreach (var m in _enabledModes) names.Add(m.displayName);
            PopulateDropdown(modeDropdown, names, 0);
            OnGameModeChanged(0);
        }

        private void OnModeDropdownChanged(int index)
        {
            if (_enabledModes == null || index < 0 || index >= _enabledModes.Length) return;
            var newMode   = _enabledModes[index];
            int partySize = _currentParty?.members?.Count ?? 0;

            if (partySize > newMode.playersPerTeam)
            {
                if (modeDropdown != null) modeDropdown.SetDropdownIndex(_selectedModeIndex);
                ShowToast("Kh\u00f4ng th\u1ec3 \u0111\u1ed5i ch\u1ebf \u0111\u1ed9",
                    $"Party \u0111ang c\u00f3 {partySize} ng\u01b0\u1eddi \u2014 vui l\u00f2ng gi\u1ea3m v\u1ec1 {newMode.playersPerTeam} tr\u01b0\u1edbc.");
                return;
            }
            _selectedModeIndex = index;
            OnGameModeChanged(index);
        }

        private void OnGameModeChanged(int index)
        {
            if (_enabledModes == null || index < 0 || index >= _enabledModes.Length) return;
            _selectedModeIndex = index;
            RefreshMapDropdown(SelectedMode.modeKey);
            RefreshPartyDisplay();
        }

        // ══════════════════════════════════════════════════════════════════════
        // MAP DROPDOWN
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshMapDropdown(string modeKey)
        {
            _currentMaps    = MapConfig.GetByMode(modeKey);
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

        // ══════════════════════════════════════════════════════════════════════
        // PARTY DISPLAY
        // ══════════════════════════════════════════════════════════════════════

        private async Task RefreshParty()
        {
            Debug.Log("[PartyController] RefreshParty → calling GET /api/party/current ...");
            if (_partyService == null) { Debug.LogWarning("[PartyController] _partyService is null — skipping GetParty"); RefreshPartyDisplay(); return; }
            var result = await _partyService.GetParty();
            if (result.Success && result.Data != null)
            {
                _currentParty = result.Data;
                Debug.Log($"[PartyController] GetParty response OK — partyId={_currentParty.partyId} hostUserId={_currentParty.hostUserId} status={_currentParty.partyStatus} members={_currentParty.members?.Count ?? 0}");
                if (_currentParty.members != null)
                {
                    for (int i = 0; i < _currentParty.members.Count; i++)
                    {
                        var m = _currentParty.members[i];
                        Debug.Log($"[PartyController]   member[{i}] userId={m.userId} username='{m.username}' isHost={m.isHost} joinOrder={m.joinOrder} onlineStatus={m.onlineStatus} selectedCharacterId='{m.selectedCharacterId}'");
                    }
                }
            }
            else
            {
                _currentParty = null;
                Debug.Log($"[PartyController] GetParty response — no active party (Success={result.Success} msg='{result.Message}')");
            }
            RefreshPartyDisplay();
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
                    "Kick th\u00e0nh vi\u00ean?", "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n kick th\u00e0nh vi\u00ean n\u00e0y?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.KickMember(uid); },
                    confirmText: "Kick", cancelText: "H\u1ee7y"),
                onLeave: () => GameModalWindow.Instance?.ShowConfirm(
                    "R\u1eddi party?", "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n r\u1eddi kh\u1ecfi party?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.LeaveParty(); },
                    confirmText: "R\u1eddi party", cancelText: "H\u1ee7y"));

            modelListView?.Refresh(_currentParty,
                iAmHost: iAmHost,
                onKick: uid => GameModalWindow.Instance?.ShowConfirm(
                    "Kick th\u00e0nh vi\u00ean?", "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n kick th\u00e0nh vi\u00ean n\u00e0y?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.KickMember(uid); },
                    confirmText: "Kick", cancelText: "H\u1ee7y"),
                onLeave: () => GameModalWindow.Instance?.ShowConfirm(
                    "R\u1eddi party?", "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n r\u1eddi kh\u1ecfi party?",
                    onConfirm: async () => { if (_partyService != null) await _partyService.LeaveParty(); },
                    confirmText: "R\u1eddi party", cancelText: "H\u1ee7y"));

            friendPanelView?.SetPartyHostMode(iAmHost);
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
            string modeKey   = SelectedMode.modeKey;
            string mapId     = _currentMaps.Length > 0 ? _currentMaps[_selectedMapIdx].mapId : null;
            bool   allowFill = SelectedMode.allowFill;

            SetQueueState(RankedQueueState.Searching);
            _searchElapsed = 0f;

            bool success;
            bool isPartyHost = _currentParty != null &&
                               _currentParty.hostUserId == (_sessionState?.UserId ?? -1L);
            if (isPartyHost)
            {
                var r = await _partyService.QueueParty(modeKey, allowFill, mapId);
                success = r.Success;
            }
            else
            {
                var r = await GameManager.Instance.BackendClient.PostAsync<object>(
                    Constants.API_MATCHMAKING_QUEUE,
                    new MatchmakingQueueRequest { gameMode = modeKey, mapId = mapId });
                success = r.Success;
            }

            if (!success)
            {
                SetQueueState(RankedQueueState.Idle);
                Debug.LogWarning("[PartyController] Kh\u00f4ng th\u1ec3 v\u00e0o h\u00e0ng ch\u1edd matchmaking.");
            }
        }

        private async Task CancelQueue()
        {
            bool isPartyHost = _currentParty != null && _currentParty.IsInQueue && _partyService != null;
            if (isPartyHost)
                await _partyService.CancelQueue();
            else
                await GameManager.Instance.BackendClient.DeleteAsync<object>(Constants.API_MATCHMAKING_QUEUE);

            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
        }

        private void SetQueueState(RankedQueueState newState)
        {
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

        private void OnCustomLobbyClicked()
        {
            if (_queueState == RankedQueueState.Searching)
            {
                GameModalWindow.Instance?.ShowConfirm(
                    "\u0110ang t\u00ecm tr\u1eadn",
                    "B\u1ea1n \u0111ang trong h\u00e0ng \u0111\u1ee3i x\u1ebfp h\u1ea1ng. R\u1eddi kh\u1ecfi s\u1ebd hu\u1ef7 t\u00ecm tr\u1eadn.",
                    onConfirm: async () => { await CancelQueue(); evt_CustomLobbyConfirmed?.Invoke(); },
                    confirmText: "R\u1eddi \u0111\u1ee3i", cancelText: "H\u1ee7y");
                return;
            }
            evt_CustomLobbyConfirmed?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        // WS — SUBSCRIPTIONS
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeWSEvents()
        {
            if (_ws == null) _ws = GameWebSocketService.Instance;
            if (_ws == null) return;

            _ws.OnMatchFound              += HandleMatchFound;
            _ws.OnMatchReady              += HandleMatchReady;
            _ws.OnMatchCancelled          += HandleMatchCancelled;

            _ws.OnPartyInvitationReceived += HandlePartyInvitationReceived;
            _ws.OnPartyMemberJoined       += HandlePartyMemberJoined;
            _ws.OnPartyMemberLeft         += HandlePartyMemberLeft;
            _ws.OnPartyMemberKicked       += HandlePartyMemberKicked;
            _ws.OnPartyDisbanded          += HandlePartyDisbanded;
            _ws.OnPartyHostChanged        += HandlePartyHostChanged;
            _ws.OnPartyStatusChanged      += HandlePartyStatusChanged;
        }

        private void UnsubscribeWSEvents()
        {
            if (_ws == null) return;
            _ws.OnMatchFound              -= HandleMatchFound;
            _ws.OnMatchReady              -= HandleMatchReady;
            _ws.OnMatchCancelled          -= HandleMatchCancelled;
            _ws.OnPartyInvitationReceived -= HandlePartyInvitationReceived;
            _ws.OnPartyMemberJoined       -= HandlePartyMemberJoined;
            _ws.OnPartyMemberLeft         -= HandlePartyMemberLeft;
            _ws.OnPartyMemberKicked       -= HandlePartyMemberKicked;
            _ws.OnPartyDisbanded          -= HandlePartyDisbanded;
            _ws.OnPartyHostChanged        -= HandlePartyHostChanged;
            _ws.OnPartyStatusChanged      -= HandlePartyStatusChanged;
        }

        // ── Matchmaking ───────────────────────────────────────────────────────

        private async void HandleMatchFound(GameWebSocketService.MatchFoundEvent e)
        {
            _pendingLobbyToken = e.lobbyToken;
            if (!string.IsNullOrEmpty(_pendingLobbyToken))
                await GameManager.Instance.BackendClient.PostAsync<object>(
                    Constants.API_MATCHMAKING_ACCEPT,
                    new MatchmakingAcceptRequest { lobbyToken = _pendingLobbyToken });
        }

        private void HandleMatchReady(GameWebSocketService.MatchReadyEvent e)
        {
            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
            MatchLoadingOverlay.Instance?.Show();
        }

        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent e)
        {
            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
            if (!string.IsNullOrEmpty(e.reason))
                ShowToast("Gh\u00e9p tr\u1eadn", $"H\u1ee7y tr\u1eadn: {e.reason}");
        }

        // ── Party ─────────────────────────────────────────────────────────────

        private void HandlePartyInvitationReceived(GameWebSocketService.PartyInvitationEvent e)
        {
            string inviterName  = string.IsNullOrEmpty(e.inviterUsername) ? "Ai \u0111\u00f3" : e.inviterUsername;
            long   invitationId = e.invitationId;
            GameModalWindow.Instance?.ShowCountdown(
                title:       "L\u1eddi m\u1eddi Party",
                desc:        $"<b>{inviterName}</b> m\u1eddi b\u1ea1n v\u00e0o party",
                seconds:     30,
                onConfirm:   () => _ = AcceptPartyInvitation(invitationId),
                onExpire:    () => _ = DeclinePartyInvitation(invitationId),
                showConfirm: true,
                confirmText: "\u0110\u1ed3ng \u00fd",
                cancelText:  "T\u1eeb ch\u1ed1i");
        }

        private async Task AcceptPartyInvitation(long invitationId)
        {
            if (_partyService == null) return;
            var result = await _partyService.AcceptInvitation(invitationId);
            if (result.Success)
            {
                _currentParty = result.Data;
                RefreshPartyDisplay();
            }
            else
            {
                ShowToast("Party", result.Message ?? "Kh\u00f4ng th\u1ec3 tham gia party.");
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
            _ = RefreshParty();
        }

        private void HandlePartyMemberLeft(GameWebSocketService.PartyMemberLeftEvent e)
        {
            if (e.userId == (_sessionState?.UserId ?? -1L)) _currentParty = null;
            _ = RefreshParty();
        }

        private void HandlePartyMemberKicked(GameWebSocketService.PartyMemberKickedEvent e)
        {
            if (e.kickedUserId == (_sessionState?.UserId ?? -1L))
            {
                _currentParty = null;
                ShowToast("Party", "B\u1ea1n \u0111\u00e3 b\u1ecb kick kh\u1ecfi party.");
            }
            _ = RefreshParty();
        }

        private void HandlePartyDisbanded(GameWebSocketService.PartyDisbandedEvent e)
        {
            _currentParty = null;
            RefreshPartyDisplay();
            ShowToast("Party", "Party \u0111\u00e3 b\u1ecb gi\u1ea3i t\u00e1n.");
        }

        private void HandlePartyHostChanged(GameWebSocketService.PartyHostChangedEvent e)
            => _ = RefreshParty();

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
            toast?.Show(title: title, message: message);
        }

        private static void PopulateDropdown(CustomDropdown dd, List<string> names, int selectIndex)
        {
            dd.items.Clear();
            if (names == null || names.Count == 0) return;
            foreach (var name in names) dd.CreateNewItem(name, notify: false);
            dd.SetupDropdown();
            int safeIndex = Mathf.Clamp(selectIndex, 0, names.Count - 1);
            dd.SetDropdownIndex(safeIndex);
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
