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
                ShowToast("Mode Change",
                    $"Party has {partySize} members — reduce to {newMode.playersPerTeam} first.");
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
            // Block if already in a custom room — must leave first.
            if (RoomState.Instance != null && RoomState.Instance.IsInRoom)
            {
                ShowToast("Xếp hạng", "Hãy rời phòng custom trước khi vào xếp hạng.");
                SetQueueState(RankedQueueState.Idle);
                return;
            }

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
                    new MatchmakingQueueRequest { gameMode = modeKey, mapId = mapId, allowFill = allowFill });
                success = r.Success;
            }

            if (!success)
            {
                SetQueueState(RankedQueueState.Idle);
                Debug.LogWarning("[PartyController] Failed to join matchmaking queue.");
                ShowToast("Matchmaking", "Failed to join queue. Please try again.");
            }
        }

        private async Task CancelQueue()
        {
            bool isPartyHost = _currentParty != null && _currentParty.IsInQueue && _partyService != null;
            if (isPartyHost)
                await _partyService.CancelQueue();
            else
                await GameManager.Instance.BackendClient.DeleteAsync<object>(Constants.API_MATCHMAKING_QUEUE);

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
            _ws.OnDsReady                 += HandleDsReady;

            _ws.OnPartyInvitationReceived += HandlePartyInvitationReceived;
            _ws.OnPartyInvitationDeclined  += HandlePartyInvitationDeclined;
            _ws.OnPartyInvitationCancelled += HandlePartyInvitationCancelled;
            _ws.OnPartyInvitationExpired   += HandlePartyInvitationExpired;
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
            _ws.OnDsReady                 -= HandleDsReady;
            _ws.OnPartyInvitationReceived -= HandlePartyInvitationReceived;
            _ws.OnPartyInvitationDeclined  -= HandlePartyInvitationDeclined;
            _ws.OnPartyInvitationCancelled -= HandlePartyInvitationCancelled;
            _ws.OnPartyInvitationExpired   -= HandlePartyInvitationExpired;
            _ws.OnPartyMemberJoined       -= HandlePartyMemberJoined;
            _ws.OnPartyMemberLeft         -= HandlePartyMemberLeft;
            _ws.OnPartyMemberKicked       -= HandlePartyMemberKicked;
            _ws.OnPartyDisbanded          -= HandlePartyDisbanded;
            _ws.OnPartyHostChanged        -= HandlePartyHostChanged;
            _ws.OnPartyStatusChanged      -= HandlePartyStatusChanged;
        }

        // ── Matchmaking ───────────────────────────────────────────────────────

        private void HandleMatchFound(GameWebSocketService.MatchFoundEvent e)
        {
            SetQueueState(RankedQueueState.Idle);   // stop search timer

            // Show overlay with player names — server starts DS immediately, no accept needed.
            MatchFoundOverlay.Instance?.Show(
                gameMode:    e.gameMode,
                playerIds:   e.playerIds,
                localUserId: _sessionState?.UserId ?? 0L);
        }

        private void HandleDsReady(GameWebSocketService.DsReadyEvent e)
        {
            Debug.Log($"[PartyController] ds_ready: DS at {e.dsIp}:{e.dsPort} matchId={e.matchId}");
            NetworkGameManager.Instance?.NotifyDsReady();
        }

        private void HandleMatchReady(GameWebSocketService.MatchReadyEvent e)
        {
            // Ẩn overlay "tìm thấy trận" (nếu đang mở)
            MatchFoundOverlay.Instance?.Hide();

            SetQueueState(RankedQueueState.Idle);

            // Lưu DS info vào RoomState để NetworkGameManager dùng khi scene load
            if (ushort.TryParse(e.dsPort.ToString(), out ushort dsPort))
                RoomState.Instance?.SetDedicatedServer(e.dsIp, dsPort, e.matchId, e.mapId);

            // Resolve scene đúng từ mapId
            NightHunt.Config.SceneId sceneId = NightHunt.Config.SceneId.GameMap_01;
            if (!string.IsNullOrEmpty(e.mapId)
                && MapConfig.TryGetById(e.mapId, out MapEntry mapEntry))
            {
                sceneId = mapEntry.sceneId;
            }

            // Guard: never load the Home scene as a game scene.
            // This can happen if MapConfig.asset has a map entry with sceneId = 0 (Home)
            // because the ScriptableObject was created but the inspector value was not set.
            if (sceneId == NightHunt.Config.SceneId.Home)
            {
                Debug.LogError($"[PartyController] match_ready resolved sceneId=Home for mapId='{e.mapId}' — this is a misconfiguration. Falling back to GameMap_01.");
                sceneId = NightHunt.Config.SceneId.GameMap_01;
            }

            Debug.Log($"[PartyController] match_ready mapId={e.mapId} → sceneId={sceneId}");
            MatchLoadingOverlay.Instance?.Show(sceneId);
        }

        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent e)
        {
            // Ẩn overlay "tìm thấy trận" (nếu đang mở)
            MatchFoundOverlay.Instance?.Hide();

            SetQueueState(RankedQueueState.Idle);
            string reason = !string.IsNullOrEmpty(e.reason) ? e.reason : "Match was cancelled.";
            ShowToast("Matchmaking", $"Match cancelled: {reason}");
            Debug.Log($"[PartyController] MatchCancelled — reason={e.reason}");
        }

        // ── Party ─────────────────────────────────────────────────────────────

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
            var result = await _partyService.AcceptInvitation(invitationId);
            if (result.Success)
            {
                _currentParty = result.Data;
                RefreshPartyDisplay();
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
            RefreshPartyDisplay();
            ShowToast("Party", "The party has been disbanded.");
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
