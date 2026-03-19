using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Services.Game;
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
        private GameWebSocketService _ws;

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
        }

        private void Start()
        {
            _ws = GameWebSocketService.Instance;
            SubscribeWSEvents();
            // Fallback: if UINavigator.Notify() doesn't reach this component (e.g. panelObject not
            // wired in Inspector), OnShow() is triggered directly when the user logs in.
            if (_sessionState != null)
                _sessionState.OnUserLoggedIn += OnUserLoggedInFallback;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INavigableView — called by UINavigator on panel transition
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fallback called when UINavigator.Notify() cannot reach this component (panelObject
        /// not wired in Inspector). OnShow() is effectively idempotent — safe to call twice.
        /// </summary>
        private void OnUserLoggedInFallback()
        {
            Debug.Log("[HomeView] OnUserLoggedIn fired — calling OnShow() as UINavigator fallback");
            if (gameObject.activeInHierarchy) OnShow();
        }

        /// <summary>
        /// Called by UINavigator right before the Home panel fades in.
        /// Safe to call multiple times (e.g. returning from Lobby).
        /// </summary>
        public async void OnShow()
        {
            Debug.Log("[HomeView] OnShow — starting home screen initialization");

            Debug.Log("[HomeView] Step 1: RefreshProfile (local cache — username, thumbnail)");
            RefreshProfile();

            Debug.Log("[HomeView] Step 2: RefreshProfileFromServer → GET /api/profile (rank, ELO, selectedCharacterId)");
            _ = RefreshProfileFromServer();

            Debug.Log("[HomeView] Step 3: CheckAndShowReconnectPopup");
            await CheckAndShowReconnectPopup();

            Debug.Log("[HomeView] Step 4: FriendPanelView.RefreshFriendList → GET /api/friends");
            friendPanelView?.RefreshFriendList();

            Debug.Log("[HomeView] Step 5: PartyController.OnHomeShown → GET /api/party/current");
            partyController?.OnHomeShown();

            var loading = PersistentUICanvas.Instance?.LoadingManager;
            if (loading != null && loading.IsShowing()) loading.Hide();

            Debug.Log("[HomeView] OnShow complete");
            onHomeShown?.Invoke();
        }

        /// <summary>Called by UINavigator right before the Home panel fades out.</summary>
        public void OnHide() { /* WS stays active — party invite modal works from any panel */ }

        private void OnDestroy()
        {
            UnsubscribeWSEvents();
            if (_sessionState != null)
                _sessionState.OnUserLoggedIn -= OnUserLoggedInFallback;
        }

        // ══════════════════════════════════════════════════════════════════════
        // TOP BAR
        // ══════════════════════════════════════════════════════════════════════

        private void OnExitToggleClicked()
        {
            GameModalWindow.Instance?.ShowMulti(
                title:           "Tho\u00e1t",
                desc:            "B\u1ea1n mu\u1ed1n l\u00e0m g\u00ec?",
                btn1Text:        "\u0110\u0103ng xu\u1ea5t",
                btn1Callback:    OnLogoutConfirmed,
                btn2Text:        "Tho\u00e1t game",
                btn2Callback:    OnQuitGameConfirmed,
                dismissText:     "H\u1ee7y",
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

        private async Task RefreshProfileFromServer()
        {
            Debug.Log("[HomeView] RefreshProfileFromServer → GET /api/profile ...");
            var result = await GameManager.Instance?.BackendClient
                .GetAsync<ProfileResponse>(Constants.API_PROFILE_GET);
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
                    title:       "K\u1ebft n\u1ed1i l\u1ea1i",
                    desc:        $"B\u1ea1n \u0111ang trong room <b>{result.Data.roomCode}</b>. K\u1ebft n\u1ed1i l\u1ea1i?",
                    onConfirm:   () => UINavigator.Instance?.GoLobby(),
                    onCancel:    _roomState.ClearRoom,
                    confirmText: "K\u1ebft n\u1ed1i l\u1ea1i",
                    cancelText:  "R\u1eddi ph\u00f2ng");
            }
            else
            {
                _roomState.ClearRoom();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // WS — SESSION EVENTS + FRIEND FORWARDING
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeWSEvents()
        {
            if (_ws == null) _ws = GameWebSocketService.Instance;
            if (_ws == null) return;

            // Session lifecycle
            _ws.OnForceLogout    += HandleForceLogout;
            _ws.OnSessionExpired += HandleSessionExpired;

            // Friend events — forward to FriendPanelView
            _ws.OnFriendStatusChanged   += HandleFriendStatusChanged;
            _ws.OnFriendRequestReceived += HandleFriendRequestReceived;
            _ws.OnFriendRequestAccepted += HandleFriendRequestAccepted;
        }

        private void UnsubscribeWSEvents()
        {
            if (_ws == null) return;
            _ws.OnForceLogout    -= HandleForceLogout;
            _ws.OnSessionExpired -= HandleSessionExpired;
            _ws.OnFriendStatusChanged   -= HandleFriendStatusChanged;
            _ws.OnFriendRequestReceived -= HandleFriendRequestReceived;
            _ws.OnFriendRequestAccepted -= HandleFriendRequestAccepted;
        }

        private void HandleForceLogout()
        {
            GameModalWindow.Instance?.ShowNotice(
                "\u0110\u0103ng xu\u1ea5t b\u1eaft bu\u1ed9c",
                "T\u00e0i kho\u1ea3n c\u1ee7a b\u1ea1n \u0111\u00e3 \u0111\u0103ng nh\u1eadp \u1edf n\u01a1i kh\u00e1c.",
                closeText: "OK",
                onClose:   LoginView.Logout);
        }

        private void HandleSessionExpired()
        {
            GameModalWindow.Instance?.ShowNotice(
                "Phi\u00ean h\u1ebft h\u1ea1n",
                "Phi\u00ean \u0111\u0103ng nh\u1eadp \u0111\u00e3 h\u1ebft h\u1ea1n. Vui l\u00f2ng \u0111\u0103ng nh\u1eadp l\u1ea1i.",
                closeText: "OK",
                onClose:   LoginView.Logout);
        }

        private void HandleFriendStatusChanged(GameWebSocketService.FriendStatusChangedEvent e)
            => friendPanelView?.OnFriendStatusChanged(e.userId, e.status, e.currentPartyId);

        private void HandleFriendRequestReceived(GameWebSocketService.FriendRequestEvent e)
        {
            friendPanelView?.OnFriendRequestBadge(+1);
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show(title: "K\u1ebft b\u1ea1n", message: $"{e.fromUsername} mu\u1ed1n k\u1ebft b\u1ea1n v\u1edbi b\u1ea1n.");
        }

        private void HandleFriendRequestAccepted(GameWebSocketService.FriendRequestAcceptedEvent e)
            => friendPanelView?.RefreshFriendList();
    }
}
