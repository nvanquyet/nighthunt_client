using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Friend;
using NightHunt.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightHunt.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    // FriendPanelView
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Friend Panel — slide-in side panel on the Home screen.
    ///
    /// Tabs (animation-driven — no panel GameObject refs here):
    ///   • Friends      — online + offline friend rows, click row → context menu
    ///   • More Friends — search for new friends + incoming requests
    ///
    /// Tab switching:
    ///   Wire each tab button's onClick in the Inspector to BOTH:
    ///     1. Your animation system's Open/Switch call.
    ///     2. <see cref="OnFriendsTabClicked"/> or <see cref="OnMoreFriendsTabClicked"/>
    ///        (data refresh — badge reset, request load, etc.)
    ///   Do NOT wire btn_Close or btn_Refresh here — the animation system handles
    ///   open/close; use <see cref="RefreshAll"/> as the Inspector target for refresh.
    ///
    /// Context menu (SharedFriendContextMenu):
    ///   Clicking any friend row opens the shared context menu near that row.
    ///   Context menu buttons fire: Invite to Party → <see cref="partyController"/>.
    ///                              Remove Friend   → internal async handler.
    ///
    /// Public API (called by HomeView in response to WS events):
    ///   • <see cref="OnFriendStatusChanged"/>  — live status update
    ///   • <see cref="OnFriendRequestBadge"/>   — +1 / -1 on pending-request badge
    ///   • <see cref="RefreshFriendList"/>       — full reload
    ///   • <see cref="ShowForInvite"/>           — open panel + switch to Friends tab
    ///   • <see cref="SetPartyHostMode"/>        — toggle context-menu invite visibility
    ///   • <see cref="SetInvitePending"/>        — pending-invite spinner per row
    ///
    /// SETUP (Prefab / hierarchy):
    ///   FriendPanelView (this script)
    ///   ├── [FriendsTabContent]      ← animation system shows/hides, NOT code
    ///   │   ├── OnlineListContainer  (ScrollRect content)
    ///   │   └── OfflineListContainer (ScrollRect content)
    ///   ├── [MoreFriendsTabContent]  ← animation system shows/hides, NOT code
    ///   │   ├── SearchFriendView     (always active)
    ///   │   └── IncomingContainer    (ScrollRect content)
    ///   ├── Footer
    ///   │   ├── Btn_FriendsTab      → Inspector onClick: animation open + OnFriendsTabClicked()
    ///   │   ├── Btn_MoreFriendsTab  → Inspector onClick: animation open + OnMoreFriendsTabClicked()
    ///   │   ├── Btn_Close           → Inspector onClick: animation close (no code ref needed)
    ///   │   ├── Btn_Refresh         → Inspector onClick: RefreshAll() (direct method reference)
    ///   │   └── PendingRequestBadge (TMP — shows pending incoming request count)
    ///   └── SharedFriendContextMenu (last sibling — renders above all rows)
    /// </summary>
    public class FriendPanelView : MonoBehaviour
    {
        // ── Friend Lists ──────────────────────────────────────────────────────
        [Header("Friend Lists")]
        [SerializeField] private Transform  onlineListContainer;
        [SerializeField] private Transform  offlineListContainer;
        [SerializeField] private GameObject friendItemPrefab;   // FriendItemView prefab

        // ── Requests ──────────────────────────────────────────────────────────
        [Header("Incoming Requests")]
        [SerializeField] private Transform  incomingRequestContainer;
        [SerializeField] private Transform  outgoingRequestContainer;  // sent invitations
        [SerializeField] private GameObject requestItemPrefab;  // FriendRequestItemView prefab

        // ── Search (embedded in MoreFriends tab) ──────────────────────────────
        [Header("Search (embedded in MoreFriends tab)")]
        [SerializeField] private SearchFriendView searchFriendView;

        // ── Pending Request Badge ─────────────────────────────────────────────
        [Header("Pending Request Badge")]
        [Tooltip("Red badge on the More Friends tab button. Shows pending incoming friend request count.\n"
               + "Set active/inactive by code; wire Btn_MoreFriendsTab onClick to OnMoreFriendsTabClicked() to auto-clear on open.")]
        [SerializeField] private TextMeshProUGUI pendingRequestBadge;

        // ── Context Menu ──────────────────────────────────────────────────────
        [Header("Context Menu")]
        [Tooltip("Place as last child of FriendPanel root so it renders above all friend rows.")]
        [SerializeField] private SharedFriendContextMenu contextMenu;

        // ── Animation Callbacks ───────────────────────────────────────────────
        [Header("Animation Callbacks")]
        [Tooltip("Fires to open this panel (wire to your animation Open call in Inspector).")]
        [SerializeField] private UnityEvent onOpenRequested;

        // ── Party invite delegation ────────────────────────────────────────────
        [Header("References")]
        [Tooltip("Called when the user taps Invite to Party in the context menu.")]
        [SerializeField] private PartyController partyController;

        // ── Services ──────────────────────────────────────────────────────────
        private FriendService _friendService;

        // ── Runtime state ─────────────────────────────────────────────────────
        private FriendTab            _activeTab    = FriendTab.Friends;
        private int                  _pendingCount = 0;   // pending incoming requests
        private bool                 _canInviteToParty = false;
        private FriendItemView       _activeContextItem;
        private int                  _friendsLoadVersion;
        private int                  _requestsLoadVersion;
        private int                  _badgeLoadVersion;

        // Cached lists (online / offline split, kept in sync with WS events)
        private readonly List<FriendResponse>    _onlineFriends  = new();
        private readonly List<FriendResponse>    _offlineFriends = new();

        // Instantiated row views keyed by userId (for live status updates)
        private readonly Dictionary<long, FriendItemView> _onlineItems  = new();
        private readonly Dictionary<long, FriendItemView> _offlineItems = new();

        private enum FriendTab { Friends, MoreFriends }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (GameManager.Instance != null)
                _friendService = GameManager.Instance.FriendService;

            // Khong goi LoadFriends() o day — goi trong OnEnable()
            // de dam bao chi load khi panel duoc show (user da login).
        }

        private void OnEnable()
        {
            // Fired khi Shift UI SetActive(true) FriendPanel.
            // Guard: only load data when user is authenticated.
            // At game startup the panel may be active before login — skip to avoid
            // a pointless "Not authenticated" error and a wasted API call.
            // HomeView.RefreshFriendListAndBadge() covers first real load after login.
            if (_friendService == null && GameManager.Instance != null)
                _friendService = GameManager.Instance.FriendService;

            SwitchTabData(FriendTab.Friends);
            if (NightHunt.State.SessionState.Instance != null && NightHunt.State.SessionState.Instance.IsAuthenticated)
                LoadFriends();
        }

        // ── Public API (called by HomeView / PartyController) ─────────────────

        /// <summary>
        /// Open the panel and show the Friends tab.
        /// Visibility is handled by the animation system — fires <c>onOpenRequested</c>.
        /// </summary>
        public void ShowForInvite()
        {
            if (!UIAnimationCallbackInvoker.InvokeOpen(onOpenRequested, this))
                Debug.LogWarning("[FriendPanelView] Open animation callback is not wired and no WindowIn fallback was found.");

            SwitchTabData(FriendTab.Friends);
        }

        public void OpenPanel() => ShowForInvite();

        public void ClosePanel()
        {
            contextMenu?.Hide();
            if (!UIAnimationCallbackInvoker.InvokeClose(null, this))
                Debug.LogWarning("[FriendPanelView] Close animation fallback was not found.");
        }

        /// <summary>Full reload of the friend list from the API.</summary>
        public void RefreshFriendList() => _ = RefreshFriendListAsync();

        public Task RefreshFriendListAsync(bool requireSuccess = false)
            => LoadFriendsAsync(requireSuccess);

        /// <summary>
        /// Full reload of the friend list AND fetches incoming request count for the badge.
        /// Call this from HomeView on login/return-to-home so the badge is correct immediately.
        /// </summary>
        public void RefreshFriendListAndBadge() => _ = RefreshFriendListAndBadgeAsync();

        public async Task RefreshFriendListAndBadgeAsync(bool requireSuccess = false)
        {
            // Preload request rows at Home entry so data is available even if tab animation
            // is wired but OnMoreFriendsTabClicked() was missed in Inspector.
            await Task.WhenAll(
                LoadFriendsAsync(requireSuccess),
                LoadFriendRequestsAsync(updateBadge: true, requireSuccess: requireSuccess));
        }

        /// <summary>
        /// Refresh both friend list and pending requests.
        /// Safe to wire directly to a Refresh button's onClick in the Inspector.
        /// </summary>
        public void RefreshAll() => _ = RefreshAllAsync();

        public async Task RefreshAllAsync(bool requireSuccess = false)
        {
            await Task.WhenAll(
                LoadFriendsAsync(requireSuccess),
                LoadFriendRequestsAsync(updateBadge: true, requireSuccess: requireSuccess)); // always reload regardless of active tab
        }

        /// <summary>
        /// Bypass the short-lived cooldown cache and reload friend requests immediately.
        /// Call from HomeView on WS events that change the pending request list
        /// (friend_request_received, friend_request_cancelled, friend_request_declined).
        /// </summary>
        public void ForceRefreshFriendRequests()
        {
            _friendService?.InvalidatePendingRequestsCache();
            _ = LoadFriendRequestsAsync(updateBadge: _activeTab != FriendTab.MoreFriends);
        }

        /// <summary>Update a friend's status badge in-place (from WS friend_status_changed).</summary>
        public void OnFriendStatusChanged(long userId, string newStatus, long newPartyId, long newRoomId)
        {
            bool wasOnline = _onlineItems.ContainsKey(userId);
            // Treat ONLINE+inRoom / ONLINE+inParty / IN_GAME all as "online bucket"
            // Server sends status="ONLINE" even when entering a room — use room/party context too.
            bool isNowOnline = newStatus == "ONLINE" || newStatus == "IN_GAME"
                               || newRoomId != 0 || newPartyId != 0;

            if (wasOnline && isNowOnline)
            {
                _onlineItems[userId].UpdateStatus(newStatus, newPartyId, newRoomId);
                UpdateCachedFriendStatus(_onlineFriends, userId, newStatus, newPartyId, newRoomId);
                return;
            }
            if (!wasOnline && !isNowOnline)
            {
                if (_offlineItems.TryGetValue(userId, out var offlineItem))
                    offlineItem.UpdateStatus(newStatus, newPartyId, newRoomId);
                UpdateCachedFriendStatus(_offlineFriends, userId, newStatus, newPartyId, newRoomId);
                return;
            }
            MoveFriendBetweenLists(userId, newStatus, newPartyId, newRoomId, isNowOnline);
        }

        /// <summary>Adjust the pending-request badge by delta (+1 received, -1 accepted/declined).</summary>
        public void OnFriendRequestBadge(int delta)
        {
            _pendingCount = Mathf.Max(0, _pendingCount + delta);
            RefreshBadge();
        }

        /// <summary>Tell the panel whether the local player can invite friends to their party.</summary>
        public void SetCanInviteToParty(bool canInvite) => _canInviteToParty = canInvite;

        /// <summary>Compatibility wrapper for older PartyController wiring.</summary>
        public void SetPartyHostMode(bool canInvite) => SetCanInviteToParty(canInvite);

        /// <summary>Show or hide the pending-invite spinner on a specific friend row.</summary>
        public void SetInvitePending(long userId, bool pending)
        {
            if (_onlineItems.TryGetValue(userId, out var item))
                item.SetInvitePending(pending);
        }

        // ── Tab Navigation (public — wire to Inspector tab button onClick) ─────

        /// <summary>
        /// Wire to Btn_FriendsTab onClick (alongside animation Open call).
        /// Clears any active context menu; no data reload needed for Friends tab.
        /// </summary>
        public void OnFriendsTabClicked()
        {
            contextMenu?.Hide();
            SwitchTabData(FriendTab.Friends);
        }

        /// <summary>
        /// Wire to Btn_MoreFriendsTab onClick (alongside animation Open call).
        /// Clears context menu, loads pending requests, resets badge.
        /// </summary>
        public void OnMoreFriendsTabClicked()
        {
            contextMenu?.Hide();
            SwitchTabData(FriendTab.MoreFriends);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void SwitchTabData(FriendTab tab)
        {
            _activeTab = tab;
            if (tab == FriendTab.MoreFriends)
            {
                LoadFriendRequests();
                _pendingCount = 0;
                RefreshBadge();
            }
        }

        // ── Load — Friends ────────────────────────────────────────────────────

        private void LoadFriends() => _ = LoadFriendsAsync();

        private async Task LoadFriendsAsync(bool requireSuccess = false)
        {
            EnsureFriendService();
            if (_friendService == null)
            {
                const string message = "[FriendPanel] FriendService is not available.";
                Debug.LogWarning(message);
                if (requireSuccess)
                    throw new InvalidOperationException(message);
                return;
            }

            int version = ++_friendsLoadVersion;
            var result = await _friendService.GetFriends();
            if (version != _friendsLoadVersion) return;
            if (!result.Success || result.Data == null)
            {
                Debug.LogError($"[FriendPanel] Failed to load friends: {result.Message}");
                if (requireSuccess)
                    throw new InvalidOperationException($"Friend list preload failed: {result.Message}");
                return;
            }
            _onlineFriends.Clear();
            _offlineFriends.Clear();
            foreach (var f in result.Data)
            {
                // Mirror the same rule as OnFriendStatusChanged: room/party presence = online bucket
                bool online = f.onlineStatus == "ONLINE" || f.onlineStatus == "IN_GAME"
                              || f.currentRoomId != 0 || f.currentPartyId != 0;
                if (online) _onlineFriends.Add(f);
                else        _offlineFriends.Add(f);
            }

            Debug.Log($"[FriendPanel] Friends loaded: total={result.Data.Count}, online={_onlineFriends.Count}, offline={_offlineFriends.Count}");
            RebuildOnlineList();
            RebuildOfflineList();
        }

        private void RebuildOnlineList()
        {
            ClearContainer(onlineListContainer, _onlineItems);
            foreach (var f in _onlineFriends)
            {
                var item = SpawnFriendItem(f, onlineListContainer);
                if (item != null) _onlineItems[f.userId] = item;
            }
        }

        private void RebuildOfflineList()
        {
            ClearContainer(offlineListContainer, _offlineItems);
            foreach (var f in _offlineFriends)
            {
                var item = SpawnFriendItem(f, offlineListContainer);
                if (item != null) _offlineItems[f.userId] = item;
            }
        }

        private FriendItemView SpawnFriendItem(FriendResponse friend, Transform container)
        {
            if (friendItemPrefab == null)
            {
                Debug.LogError("[FriendPanel] friendItemPrefab is not assigned.");
                return null;
            }
            if (container == null)
            {
                Debug.LogError("[FriendPanel] Friend list container is not assigned.");
                return null;
            }

            var go   = Instantiate(friendItemPrefab, container);
            var view = go.GetComponent<FriendItemView>();
            if (view == null)
            {
                Debug.LogError("[FriendPanel] friendItemPrefab is missing FriendItemView component.");
                return null;
            }
            view?.Setup(friend, OnRowClicked);
            return view;
        }

        private void ClearContainer(Transform container, Dictionary<long, FriendItemView> items)
        {
            items.Clear();
            if (container == null) return;
            foreach (Transform child in container) Destroy(child.gameObject);
        }

        // ── Load — Friend Requests ────────────────────────────────────────────

        private void LoadFriendRequests() => _ = LoadFriendRequestsAsync(updateBadge: false);

        private async Task LoadFriendRequestsAsync(bool updateBadge = false, bool requireSuccess = false)
        {
            EnsureFriendService();
            if (_friendService == null)
            {
                const string message = "[FriendPanel] FriendService is not available.";
                Debug.LogWarning(message);
                if (requireSuccess)
                    throw new InvalidOperationException(message);
                return;
            }

            int version = ++_requestsLoadVersion;
            var result = await _friendService.GetPendingRequests();
            if (version != _requestsLoadVersion) return;
            if (!result.Success || result.Data == null)
            {
                Debug.LogError($"[FriendPanel] Failed to load requests: {result.Message}");
                if (requireSuccess)
                    throw new InvalidOperationException($"Friend request preload failed: {result.Message}");
                return;
            }

            int incoming = result.Data.received?.Count ?? 0;
            int outgoing = result.Data.sent?.Count ?? 0;
            Debug.Log($"[FriendPanel] Requests loaded: incoming={incoming}, outgoing={outgoing}");

            if (updateBadge)
            {
                _pendingCount = incoming;
                RefreshBadge();
            }

            PopulateRequests(incomingRequestContainer, result.Data.received, isIncoming: true);
            PopulateRequests(outgoingRequestContainer, result.Data.sent, isIncoming: false);
        }

        private void EnsureFriendService()
        {
            if (_friendService == null && GameManager.Instance != null)
                _friendService = GameManager.Instance.FriendService;
        }

        /// <summary>
        /// Fetch only the incoming request count and update the badge.
        /// Lightweight: called at login time so the badge is visible without opening the tab.
        /// </summary>
        private async System.Threading.Tasks.Task RefreshIncomingBadge()
        {
            if (_friendService == null) return;
            int version = ++_badgeLoadVersion;
            var result = await _friendService.GetPendingRequests();
            if (version != _badgeLoadVersion) return;
            if (result.Success && result.Data?.received != null)
            {
                _pendingCount = result.Data.received.Count;
                RefreshBadge();
            }
        }

        private void PopulateRequests(Transform container, List<FriendRequestResponse> requests, bool isIncoming)
        {
            if (container == null)
            {
                Debug.LogError($"[FriendPanel] {(isIncoming ? "incoming" : "outgoing")} request container is not assigned.");
                return;
            }

            foreach (Transform c in container) Destroy(c.gameObject);
            if (requests == null) return;

            if (requestItemPrefab == null)
            {
                Debug.LogError("[FriendPanel] requestItemPrefab is not assigned.");
                return;
            }

            foreach (var req in requests)
            {
                var go   = Instantiate(requestItemPrefab, container);
                var view = go.GetComponent<FriendRequestItemView>();
                if (view == null)
                {
                    Debug.LogError("[FriendPanel] requestItemPrefab is missing FriendRequestItemView component.");
                    Destroy(go);
                    continue;
                }
                if (isIncoming) view.SetupIncoming(req, OnAcceptRequest, OnDeclineRequest);
                else            view.SetupOutgoing(req, OnCancelRequest);
            }
        }

        // ── Context Menu ──────────────────────────────────────────────────────

        /// <summary>Called when any friend row is clicked.</summary>
        private void OnRowClicked(FriendItemView item)
        {
            // Second click on same row → close
            if (_activeContextItem == item && contextMenu != null && contextMenu.IsVisible)
            {
                contextMenu.Hide();
                _activeContextItem = null;
                return;
            }

            _activeContextItem = item;

            bool canInvite = partyController != null
                && _canInviteToParty
                && item.Friend != null
                && item.Friend.IsOnline;

            contextMenu?.Show(
                anchorPoint:   item.ContextMenuAnchor,
                canInvite:     canInvite,
                userId:        item.Friend?.userId ?? 0L,
                onViewProfile: () => OnViewProfile(item.Friend),
                onInvite:      () => OnInviteToParty(item.Friend),
                onRemove:      () => _ = OnRemoveFriend(item.Friend));
        }

        private void OnViewProfile(FriendResponse friend)
        {
            _activeContextItem = null;
            if (friend == null) return;
            ConditionalLogger.Log("FriendPanel", $"ViewProfile: userId={friend.userId} username={friend.username}");
            PlayerProfilePanel.Instance?.Show(friend.userId, friend.username);
        }

        private void OnInviteToParty(FriendResponse friend)
        {
            _activeContextItem = null;
            if (friend == null) return;
            partyController?.InviteFriendToParty(friend);
        }

        private async System.Threading.Tasks.Task OnRemoveFriend(FriendResponse friend)
        {
            _activeContextItem = null;
            if (friend == null || _friendService == null) return;
            var result = await _friendService.RemoveFriend(friend.userId);
            if (result.Success) LoadFriends();
            else Debug.LogError($"[FriendPanel] Remove failed: {result.Message}");
        }

        // ── Request Actions ───────────────────────────────────────────────────

        private async void OnAcceptRequest(long requestId)
        {
            if (_friendService == null) return;
            var result = await _friendService.AcceptRequest(requestId);
            if (result.Success)
            {
                OnFriendRequestBadge(-1);
                LoadFriendRequests();
                LoadFriends();
            }
            else Debug.LogError($"[FriendPanel] Accept failed: {result.Message}");
        }

        private async void OnDeclineRequest(long requestId)
        {
            if (_friendService == null) return;
            var result = await _friendService.DeclineRequest(requestId);
            if (result.Success) { OnFriendRequestBadge(-1); LoadFriendRequests(); }
            else Debug.LogError($"[FriendPanel] Decline failed: {result.Message}");
        }

        private async void OnCancelRequest(long requestId)
        {
            if (_friendService == null) return;
            var result = await _friendService.CancelFriendRequest(requestId);
            if (result.Success) LoadFriendRequests();
            else Debug.LogError($"[FriendPanel] Cancel failed: {result.Message}");
        }

        // ── Badge ─────────────────────────────────────────────────────────────

        private void RefreshBadge()
        {
            if (pendingRequestBadge == null) return;
            pendingRequestBadge.gameObject.SetActive(_pendingCount > 0);
            pendingRequestBadge.text = _pendingCount > 9 ? "9+" : _pendingCount.ToString();
        }

        // ── Live Update Helpers ───────────────────────────────────────────────

        private static void UpdateCachedFriendStatus(List<FriendResponse> list, long userId,
                                                     string newStatus, long newPartyId, long newRoomId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].userId != userId) continue;
                var f = list[i];
                f.onlineStatus   = newStatus;
                f.currentPartyId = newPartyId;
                f.currentRoomId  = newRoomId;
                list[i]          = f;
                break;
            }
        }

        private void MoveFriendBetweenLists(long userId, string newStatus, long newPartyId, long newRoomId, bool nowOnline)
        {
            var source = nowOnline ? _offlineFriends : _onlineFriends;
            var target = nowOnline ? _onlineFriends  : _offlineFriends;
            FriendResponse moved = default;
            bool found = false;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].userId != userId) continue;
                moved = source[i];
                moved.onlineStatus   = newStatus;
                moved.currentPartyId = newPartyId;
                moved.currentRoomId  = newRoomId;
                source.RemoveAt(i);
                found = true;
                break;
            }
            if (!found) return;
            target.Add(moved);
            RebuildOnlineList();
            RebuildOfflineList();
        }

#if UNITY_EDITOR
        // ── Editor — Context Menu: Create Friend UI Prefab Templates ──────────

        [ContextMenu("NightHunt/Create Friend Item Prefab Template")]
        private void Editor_CreateFriendItemPrefab()
        {
            Editor_CreateRowPrefab(
                "Assets/_Night_Hunt/Prefabs/UI/FriendItem_Template.prefab",
                "FriendItem_Template",
                new UnityEngine.Vector2(300f, 50f),
                ref friendItemPrefab,
                "FriendItemView",
                "[FriendPanelView] Created FriendItem_Template. Add FriendItemView component.");
        }

        [ContextMenu("NightHunt/Create Friend Request Item Prefab Template")]
        private void Editor_CreateFriendRequestItemPrefab()
        {
            Editor_CreateRowPrefab(
                "Assets/_Night_Hunt/Prefabs/UI/FriendRequestItem_Template.prefab",
                "FriendRequestItem_Template",
                new UnityEngine.Vector2(300f, 55f),
                ref requestItemPrefab,
                "FriendRequestItemView",
                "[FriendPanelView] Created FriendRequestItem_Template. Add FriendRequestItemView component.");
        }

        private void Editor_CreateRowPrefab(string path, string goName, UnityEngine.Vector2 size,
            ref GameObject field, string componentHint, string logMessage)
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[FriendPanelView] Prefab already exists at {path}");
                return;
            }

            var go = new GameObject(goName);
            go.AddComponent<UnityEngine.RectTransform>().sizeDelta = size;
            go.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.1f, 0.1f, 0.1f, 0.8f);
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.spacing = 6f;
            hlg.padding = new UnityEngine.RectOffset(6, 6, 4, 4);

            // Avatar
            var avatarGo = new GameObject("Avatar", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            avatarGo.transform.SetParent(go.transform, false);
            avatarGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 40f;

            // Name
            var nameGo  = new GameObject("NameText", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            nameGo.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            var nameTmp = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
            nameTmp.text = "Friend Name"; nameTmp.fontSize = 14f;

            // Status dot
            var dotGo = new GameObject("StatusDot", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            dotGo.transform.SetParent(go.transform, false);
            dotGo.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.green;
            dotGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 14f;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);

            if (field == null)
            {
                field = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"{logMessage} — saved at: {path}");
        }
#endif
    }
}
