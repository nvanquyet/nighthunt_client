using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Utils;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Services.Game;
using NightHunt.Services.Profile;
using NightHunt.Services.Room;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// HomeView — deliberately narrow scope:
    ///   • Profile display: character thumbnail, username, rank/ELO.
    ///   • Top-bar exit toggle (Logout / Quit modal).
    ///   • INavigableView: OnShow refreshes profile, triggers party refresh, hides loading.
    ///   • WS session events: force_logout, session_expired.
    ///   • WS friend events: forwarded to FriendPanelView (status change, request badge, refresh).
    ///   • Reconnect check: if previously in a room → offer to reconnect.
    ///
    /// Everything party-related (mode/map dropdowns, play button, party display,
    /// matchmaking WS events) lives in <see cref="PartyController"/> on the same GameObject.
    ///
    /// Prefab hierarchy (top-level, all siblings):
    ///   HomePanel (HomeView + PartyController)
    ///   ├── TopBar             — btn_ExitToggle
    ///   ├── Profile area       — characterThumbnail, profileNameText, rankText
    ///   ├── PartyMemberListView  (bottom-left avatars)
    ///   ├── PartyModelListView   (centre models)
    ///   ├── FriendPanelView      (right panel — animation-driven)
    ///   └── SharedPartyContextMenu (last sibling — renders above all party slots)
    /// </summary>
    public class HomeView : MonoBehaviour, INavigableView
    {
        // ── Top Bar ───────────────────────────────────────────────────────────
        [Header("Top Bar")]
        [SerializeField] private Button btn_ExitToggle;

        // ── Profile ───────────────────────────────────────────────────────────
        [Header("Profile")]
        [SerializeField] private Image           characterThumbnail;
        [SerializeField] private TextMeshProUGUI profileNameText;
        [Tooltip("Shown as 'GOLD | 1450 ELO'. Filled after async profile fetch from server.")]
        [SerializeField] private TextMeshProUGUI rankText;

        // ── Sub-Controllers ───────────────────────────────────────────────────
        [Header("Sub-Controllers")]
        [Tooltip("Handles party display, mode/map dropdowns, play button, matchmaking WS events.")]
        [SerializeField] private PartyController partyController;
        [Tooltip("Right-side slide-in friend list panel (animation-driven).")]
        [SerializeField] private FriendPanelView friendPanelView;

        // ── Events ────────────────────────────────────────────────────────────
        [Header("Events")]
        [Tooltip("Fired after HomeView fully finishes showing (profile loaded, party refreshed).")]
        public UnityEvent onHomeShown;

        // ══════════════════════════════════════════════════════════════════════
        // SERVICES
        // ══════════════════════════════════════════════════════════════════════

        private SessionState         _sessionState;
        private RoomService          _roomService;
        private RoomState            _roomState;

        // ══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _sessionState = GameManager.Instance.SessionState;
                _roomService  = GameManager.Instance.RoomService;
            }
            _roomState = RoomState.Instance;

            if (btn_ExitToggle != null)
                btn_ExitToggle.onClick.AddListener(OnExitToggleClicked);

            // Disable static Welcome text next to username if present
            if (profileNameText != null)
            {
                Transform parent = profileNameText.transform.parent;
                if (parent != null)
                {
                    Transform welcome = parent.Find("Welcome");
                    if (welcome != null)
                    {
                        welcome.gameObject.SetActive(false);
                        Debug.Log("[HomeView] Disabled static 'Welcome' text GameObject next to username.");
                    }
                }
            }
        }

        private void Start()
        {
        }

        // ─────────────────────────────────────────────────────────────────────
        // INavigableView — called by UINavigator on panel transition
        // ─────────────────────────────────────────────────────────────────────

        // Debounce: prevent accidental repeated navigation calls from causing duplicate
        // profile/friends/party API requests.
        private float _lastOnShowTime = float.MinValue;
        private const float ON_SHOW_MIN_INTERVAL = 0.1f;

        // Track whether the WS was ever connected so reconnect toasts only show after the first drop.
        private bool _wsWasConnected;

        // Set by PreloadDataAsync() after LoginView has already fetched all home data.
        // OnShow() checks this flag to skip the redundant network calls on the first show.
        private bool _homeDataPreloaded;
        private bool _wsSubscribed;

        // Static flag that survives Unity scene loads (unlike instance fields).
        // Set by ResultsView.NavigatePostMatch() so the new HomeView instance spawned in
        // the next scene knows profile data was just refreshed — skips the second profile fetch.
        private static bool s_profileJustRefreshed;

        /// <summary>
        /// Called by ResultsView.NavigatePostMatch() after it has already awaited FetchProfile().
        /// Survives the scene load so the new HomeView instance knows profile is fresh and can
        /// skip the redundant RefreshProfileFromServer() call on the first OnShow() after a match.
        /// </summary>
        public static void MarkProfileJustRefreshed() => s_profileJustRefreshed = true;

        /// <summary>
        /// Core home-data fetch: profile from server + friends + party.
        /// Called by both <see cref="PreloadDataAsync"/> and <see cref="OnShow"/> so the logic
        /// lives in exactly one place.
        /// </summary>
        private async System.Threading.Tasks.Task FetchHomeDataAsync(bool skipProfile = false, bool requireCritical = false)
        {
            // Profile
            System.Threading.Tasks.Task profileTask =
                skipProfile ? System.Threading.Tasks.Task.CompletedTask : RefreshProfileFromServer(requireSuccess: requireCritical);

            // Friends + party can run concurrently with the profile fetch.
            Task friendTask = friendPanelView != null
                ? friendPanelView.RefreshFriendListAndBadgeAsync(requireSuccess: requireCritical)
                : Task.CompletedTask;
            Task partyTask = partyController != null
                ? partyController.OnHomeShownAsync()
                : Task.CompletedTask;

            // Wait for profile to complete last (it was started first, already in-flight).
            await Task.WhenAll(profileTask, friendTask, partyTask);
            partyController?.RefreshPartyDisplay();
        }

        /// <summary>
        /// Pre-fetches all home panel data (profile, friends, party) while the login loading
        /// overlay is still visible.  Call this from LoginView BEFORE UINavigator.GoHome() so
        /// the panel opens instantly with fresh data.  Sets <see cref="_homeDataPreloaded"/> so
        /// OnShow() skips the redundant network calls on the first show after login.
        /// </summary>
        public async System.Threading.Tasks.Task PreloadDataAsync()
        {
            Debug.Log("[HomeView] PreloadDataAsync — fetching profile / friends / party before Home transition.");
            RefreshProfile(); // populate UI from cached session data immediately
            await FetchHomeDataAsync(requireCritical: true);
            _homeDataPreloaded = true;
            Debug.Log("[HomeView] PreloadDataAsync complete.");
        }

        /// <summary>
        /// Called by UINavigator right before the Home panel fades in.
        /// Safe to call multiple times (e.g. returning from Lobby).
        /// </summary>
        public bool CanLeave(NavigationContext context) => true;

        public void OnShow() => _ = OnShowAsync(new NavigationContext(
            UINavigator.Instance != null ? UINavigator.Instance.CurrentPanel : PanelType.None,
            PanelType.Home,
            false));

        public async Task OnShowAsync(NavigationContext context)
        {
            SubscribeWSEvents();

            float now = Time.unscaledTime;
            if (now - _lastOnShowTime < ON_SHOW_MIN_INTERVAL)
            {
                Debug.Log("[FLOW][HomeView] OnShow — debounced (called too recently, skipping)");
                return;
            }
            _lastOnShowTime = now;

            Debug.Log($"[FLOW][HomeView] ── OnShow START  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            // Fast path: LoginView already fetched all home data while the loading overlay was
            // visible.  Skip redundant network calls; just refresh the local-cache UI elements
            // and run the reconnect check (always needed, cheap when !IsInRoom).
            if (_homeDataPreloaded)
            {
                _homeDataPreloaded = false;
                Debug.Log("[FLOW][HomeView] OnShow — data preloaded by login flow, skipping network refetch.");
                RefreshProfile();
                partyController?.RefreshPartyDisplay();
                await CheckAndShowReconnectPopup();
                Debug.Log($"[FLOW][HomeView] ── OnShow COMPLETE (preloaded)  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                onHomeShown?.Invoke();
                UINavigator.Instance?.NotifyPlayerDataLoaded();
                return;
            }

            // Slow path: full network refetch (auto-login, return from Lobby, NGM retry, etc.).
            Debug.Log("[FLOW][HomeView] [1/5] RefreshProfile — local cache (username, thumbnail)");
            RefreshProfile();

            // Skip profile server fetch if ResultsView already refreshed it in the previous scene.
            // s_profileJustRefreshed is a static flag that survives scene loads.
            bool skipProfile = s_profileJustRefreshed;
            s_profileJustRefreshed = false;
            if (skipProfile)
                Debug.Log("[FLOW][HomeView] profile skipped — just refreshed by post-match flow.");

            Debug.Log("[FLOW][HomeView] [2/5] RefreshProfileFromServer → GET /api/profile");
            Debug.Log("[FLOW][HomeView] [3/5] CheckAndShowReconnectPopup");
            await CheckAndShowReconnectPopup();

            Debug.Log("[FLOW][HomeView] [4/5] FriendPanelView.RefreshFriendListAndBadge → GET /api/friends + requests");
            Debug.Log("[FLOW][HomeView] [5/5] PartyController.OnHomeShown → GET /api/party/current");
            await FetchHomeDataAsync(skipProfile: skipProfile);

            Debug.Log($"[FLOW][HomeView] ── OnShow COMPLETE — all data fetched  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            onHomeShown?.Invoke();

            // Notify UINavigator that all player data is ready — enables interactive elements.
            UINavigator.Instance?.NotifyPlayerDataLoaded();
        }

        /// <summary>Called by UINavigator right before the Home panel fades out.</summary>
        public void OnHide() => _ = OnHideAsync(new NavigationContext(PanelType.Home, PanelType.None, false));

        public Task OnHideAsync(NavigationContext context)
        {
            // Keep WS subscriptions alive when navigating to sibling panels within the home
            // flow (PartyCustomMode, Settings). Friend/connection events must still be
            // delivered while the user is on those panels (toasts, badge updates).
            // Only fully unsubscribe when leaving the home flow entirely (Login / scene change).
            bool leavingHomeFlow = context.To == PanelType.Login || context.To == PanelType.None;
            if (leavingHomeFlow)
                UnsubscribeWSEvents();
            return Task.CompletedTask;
        }

        private void OnDestroy()
        {
            UnsubscribeWSEvents();
        }

        // ══════════════════════════════════════════════════════════════════════
        // TOP BAR
        // ══════════════════════════════════════════════════════════════════════

        private void OnExitToggleClicked()
        {
            GameModalWindow.Instance?.ShowMulti(
                title:           "Exit",
                desc:            "Choose an action.",
                btn1Text:        "Sign Out",
                btn1Callback:    OnLogoutConfirmed,
                btn2Text:        "Quit Game",
                btn2Callback:    OnQuitGameConfirmed,
                dismissText:     "Cancel",
                dismissCallback: null);
        }

        private void OnLogoutConfirmed()   => LoginView.Logout();
        private void OnQuitGameConfirmed() => Application.Quit();

        // ══════════════════════════════════════════════════════════════════════
        // PROFILE
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshProfile()
        {
            if (_sessionState == null) return;
            if (profileNameText != null) profileNameText.text = _sessionState.Username ?? "";
            RefreshCharacterThumbnail();
            if (rankText != null) rankText.text = "---";
        }

        private void RefreshCharacterThumbnail()
        {
            if (characterThumbnail == null) return;
            string charId = _sessionState?.SelectedCharacterId;
            if (!string.IsNullOrEmpty(charId))
            {
                var def = CharacterDatabase.Instance?.GetById(charId);
                if (def?.Thumbnail != null) { characterThumbnail.sprite = def.Thumbnail; return; }
            }
            var fallback = CharacterDatabase.Instance?.GetByIndex(0);
            if (fallback?.Thumbnail != null) characterThumbnail.sprite = fallback.Thumbnail;
        }

        private async Task RefreshProfileFromServer(bool requireSuccess = false)
        {
            Debug.Log("[HomeView] RefreshProfileFromServer → GET /api/profile ...");
            var profileManager = GameManager.Instance != null
                ? GameManager.Instance.ProfileManager
                : FindFirstObjectByType<ProfileManager>(FindObjectsInactive.Include);

            ApiResult<ProfileResponse> result = null;
            try
            {
                if (profileManager != null)
                    result = await profileManager.FetchProfile();
                else if (GameManager.Instance?.BackendClient != null)
                    result = await GameManager.Instance.BackendClient.GetAsync<ProfileResponse>(Constants.API_PROFILE_GET);
                else
                    Debug.LogWarning("[HomeView] Profile fetch skipped: ProfileManager and BackendClient are not available.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HomeView] /api/profile exception: {ex}");
                if (requireSuccess)
                    throw;
            }

            if (result?.Success == true && result.Data != null)
            {
                Debug.Log($"[HomeView] /api/profile response — userId={result.Data.userId} username='{result.Data.username}' tier={result.Data.tier} elo={result.Data.elo} selectedCharacterId='{result.Data.selectedCharacterId}'");
                if (rankText != null)
                    rankText.text = $"{result.Data.tier} | {result.Data.elo} ELO";
                if (!string.IsNullOrEmpty(result.Data.selectedCharacterId))
                {
                    _sessionState?.SetSelectedCharacterId(result.Data.selectedCharacterId);
                    Debug.Log($"[HomeView] SessionState.SelectedCharacterId updated to '{result.Data.selectedCharacterId}'");
                    RefreshCharacterThumbnail();
                    // Re-render party model slots now that the character ID is known
                    partyController?.RefreshPartyDisplay();
                }
            }
            else
            {
                Debug.LogWarning($"[HomeView] /api/profile failed — Success={result?.Success} msg='{result?.Message}'");
                if (requireSuccess)
                    throw new System.InvalidOperationException($"Profile preload failed: {result?.Message ?? "No profile response"}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // RECONNECT CHECK
        // ══════════════════════════════════════════════════════════════════════

        private async Task CheckAndShowReconnectPopup()
        {
            if (_roomState == null || !_roomState.IsInRoom || _roomService == null) return;
            var result = await _roomService.Reconnect(_roomState.RoomId);
            if (result.Success && result.Data != null)
            {
                GameModalWindow.Instance?.ShowConfirm(
                    title:       "Reconnect",
                    desc:        $"You are currently in room <b>{result.Data.roomCode}</b>. Reconnect?",
                    onConfirm:   () => UINavigator.Instance?.GoLobby(),
                    onCancel:    () => _ = LeaveRecoveredRoomAsync(result.Data),
                    confirmText: "Reconnect",
                    cancelText:  "Leave Room");
            }
            else
            {
                _roomState.ClearRoom();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // WS — SESSION EVENTS + FRIEND FORWARDING
        // ══════════════════════════════════════════════════════════════════════

        private async Task LeaveRecoveredRoomAsync(RoomResponse room)
        {
            if (room == null)
            {
                _roomState?.ClearRoom();
                return;
            }

            if (_roomService == null)
            {
                Debug.LogWarning("[HomeView] Cannot leave recovered room: RoomService is missing. Clearing local room state only.");
                _roomState?.ClearRoom();
                return;
            }

            bool isHost = room.ownerId == (_sessionState?.UserId ?? -1L);
            var leaveResult = isHost
                ? await _roomService.DisbandRoom(room.roomId)
                : await _roomService.LeaveRoom(room.roomId);

            if (!leaveResult.Success)
            {
                GameModalWindow.Instance?.ShowNotice(
                    isHost ? "Disband Room Failed" : "Leave Room Failed",
                    leaveResult.Message ?? "Please try again.");
                return;
            }

            _roomState?.ClearRoom();
        }

        private void SubscribeWSEvents()
        {
            if (_wsSubscribed) return;

            var bus = GameEventBus.Instance;
            if (bus == null) return;

            // Connection state
            bus.OnWebSocketConnected       += HandleWsConnected;
            bus.OnWebSocketDisconnected    += HandleWsDisconnected;
            bus.OnWebSocketReconnectFailed += HandleWsReconnectFailed;

            // Session lifecycle
            bus.OnForceLogout    += HandleForceLogout;
            bus.OnSessionExpired += HandleSessionExpired;

            // Friend events — forward to FriendPanelView
            bus.OnFriendStatusChanged   += HandleFriendStatusChanged;
            bus.OnFriendRequestReceived += HandleFriendRequestReceived;
            bus.OnFriendRequestAccepted += HandleFriendRequestAccepted;
            bus.OnFriendRequestDeclined += HandleFriendRequestDeclined;
            bus.OnFriendRequestCancelled += HandleFriendRequestCancelled;
            bus.OnFriendRemoved += HandleFriendRemoved;

            // Session state character change
            if (_sessionState == null) _sessionState = SessionState.Instance;
            if (_sessionState != null) _sessionState.OnCharacterChanged += HandleCharacterChanged;

            _wsSubscribed = true;
        }

        private void UnsubscribeWSEvents()
        {
            if (!_wsSubscribed) return;

            var bus = GameEventBus.Instance;
            if (_sessionState != null)
            {
                _sessionState.OnCharacterChanged -= HandleCharacterChanged;
            }

            if (bus == null)
            {
                _wsSubscribed = false;
                return;
            }
            bus.OnWebSocketConnected       -= HandleWsConnected;
            bus.OnWebSocketDisconnected    -= HandleWsDisconnected;
            bus.OnWebSocketReconnectFailed -= HandleWsReconnectFailed;
            bus.OnForceLogout    -= HandleForceLogout;
            bus.OnSessionExpired -= HandleSessionExpired;
            bus.OnFriendStatusChanged   -= HandleFriendStatusChanged;
            bus.OnFriendRequestReceived -= HandleFriendRequestReceived;
            bus.OnFriendRequestAccepted -= HandleFriendRequestAccepted;
            bus.OnFriendRequestDeclined -= HandleFriendRequestDeclined;
            bus.OnFriendRequestCancelled -= HandleFriendRequestCancelled;
            bus.OnFriendRemoved -= HandleFriendRemoved;
            _wsSubscribed = false;
        }

        // ── WS Connection ─────────────────────────────────────────────────

        private void HandleWsConnected()
        {
            Debug.Log("[HomeView] WS OnConnected — wsWasConnected=" + _wsWasConnected);
            if (_wsWasConnected)
            {
                var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
                toast?.Show("Connected", "Connection restored.");
            }
            _wsWasConnected = true;
        }

        private void HandleWsDisconnected()
        {
            Debug.Log("[HomeView] WS OnDisconnected — wsWasConnected=" + _wsWasConnected);
            if (_wsWasConnected)
            {
                var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
                toast?.Show("Connection Lost", "Lost connection to server. Reconnecting...");
            }
        }

        private void HandleWsReconnectFailed()
        {
            Debug.LogWarning("[HomeView] WS reconnect failed — max attempts reached");
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Connection Error", "Unable to reconnect to server. Please restart the game.");
        }

        private void HandleForceLogout()
        {
            SessionTerminationFlow.ShowAndLogout(
                "Forced Logout",
                "Your account has been logged in from another location.");
        }

        private void HandleSessionExpired()
        {
            SessionTerminationFlow.ShowAndLogout(
                "Session Expired",
                "Your session has expired. Please log in again.");
        }

        private void HandleFriendStatusChanged(GameWebSocketService.FriendStatusChangedEvent e)
            => friendPanelView?.OnFriendStatusChanged(e.userId, e.status, e.currentPartyId, e.currentRoomId);

        private void HandleFriendRequestReceived(GameWebSocketService.FriendRequestEvent e)
        {
            friendPanelView?.OnFriendRequestBadge(+1);
            friendPanelView?.ForceRefreshFriendRequests(); // bypass cooldown cache — data changed on server
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Friend Request", $"{e.fromUsername} wants to be your friend.");
            Debug.Log($"[HomeView] FriendRequestReceived from {e.fromUsername} ({e.fromUserId})");
        }

        private void HandleFriendRequestAccepted(GameWebSocketService.FriendRequestAcceptedEvent e)
        {
            // Belt-and-suspenders: invalidate before reload in case the WS handler's invalidation
            // fired just before a background refresh re-filled the cache.
            APICache.InvalidateFriends();
            // Refresh friend list (new friend) AND request list (outgoing request is now gone)
            friendPanelView?.RefreshFriendList();
            friendPanelView?.ForceRefreshFriendRequests(); // remove the accepted request from "Sent" tab
            string name = string.IsNullOrEmpty(e.addresseeUsername) ? "Someone" : e.addresseeUsername;
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Friends", $"{name} accepted your friend request.");
            Debug.Log($"[HomeView] FriendRequestAccepted — addressee={e.addresseeUsername} ({e.addresseeUserId})");
        }

        // Requester's outgoing request was declined — refresh requests list.
        private void HandleFriendRequestDeclined(GameWebSocketService.FriendRequestDeclinedEvent e)
        {
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Friend Request", "Your friend request was declined.");
            friendPanelView?.ForceRefreshFriendRequests(); // bypass cooldown cache — outgoing request is now gone
            Debug.Log($"[HomeView] FriendRequestDeclined by userId={e.addresseeUserId}");
        }

        // Sender cancelled their outgoing request before we accepted — decrement badge and refresh.
        private void HandleFriendRequestCancelled(GameWebSocketService.FriendRequestCancelledEvent e)
        {
            friendPanelView?.OnFriendRequestBadge(-1);
            friendPanelView?.ForceRefreshFriendRequests(); // bypass cooldown cache — incoming request is now gone
            Debug.Log($"[HomeView] FriendRequestCancelled by userId={e.requesterUserId}");
        }

        // A friend removed us (or we removed them via another device) — reload friend list.
        private void HandleFriendRemoved(GameWebSocketService.FriendRemovedEvent e)
        {
            friendPanelView?.RefreshFriendList();
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Friends", "A friend was removed from your list.");
            Debug.Log($"[HomeView] FriendRemoved — userId={e.userId} friendUserId={e.friendUserId}");
        }

        private void HandleCharacterChanged(string characterId)
        {
            Debug.Log($"[HomeView] Character selection changed to '{characterId}', refreshing UI.");
            RefreshCharacterThumbnail();
            partyController?.RefreshPartyDisplay();
        }
    }
}
