using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.Services.Party;
using NightHunt.Services.Room;
using NightHunt.State;
using NightHunt.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Michsky.MUIP;
using Michsky.UI.Shift;

namespace NightHunt.UI
{
    /// <summary>
    /// Custom Lobby View — Panel in 01_Home scene.
    ///
    /// ── State: Not in room ──────────────────────────────────────────────────
    ///   [Create Room]  [Quick Join]  Enter Code: [_____] [Join]
    ///   If in a ranked party → ShowConfirm "Leave/Disband party?" before any action.
    ///   API result is checked: if leave/disband fails, action is NOT executed.
    ///
    /// ── State: In room ──────────────────────────────────────────────────────
    ///   LEFT  — Settings panel (all see, only host edits):
    ///             Room Code + Copy | Mode selector | Password toggle | Public toggle
    ///   RIGHT — Team slot grid (PlayerSlotView prefabs × 2 teams)
    ///             Empty slot  → click = ChangeTeam (move self there)
    ///             Occupied    → click = RequestSwap (GameModalWindow countdown)
    ///             Host on others' slot: [X] kick, [♛] transfer owner
    ///   BOTTOM— [Ready] (non-host) | [Start] (host, when full+all ready)
    ///            [Leave] (non-host) | [Disband] (host)
    ///
    /// ── Auto leave/disband ──────────────────────────────────────────────────
    ///   Navigating away (UINavigator → GoHome/GoLogin) triggers OnHide().
    ///   OnHide auto-disbands (host) or auto-leaves (member).
    ///   Guard: skipped when game status = IN_GAME (match started).
    ///   _hasAutoLeft is set BEFORE showing the confirm modal so OnHide() does
    ///   NOT double-fire; it is restored to false if the user cancels.
    ///
    /// ── Swap notification (GameModalWindow) ────────────────────────────────
    ///   Requester: ShowCountdown(showConfirm=false) + Cancel = CancelSwap
    ///   Target:    ShowCountdown(showConfirm=true)  + Confirm/Cancel = Accept/Reject
    ///   Status event ACCEPTED/REJECTED → Close() modal silently (no callback fire).
    ///
    /// WS events (via GameEventBus):
    ///   room_updated, player_joined, player_left, player_ready,
    ///   team_changed, room_status_changed, swap_request, swap_request_status,
    ///   force_logout, session_expired, app_focus_gained, app_resumed
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomLobbyView : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // NESTED — navigation button + confirmed-action pair
        // ══════════════════════════════════════════════════════════════════════

        [System.Serializable]
        public class NavigationButtonEntry
        {
            public Button button;
            [Tooltip("Fired after room đã rời/giải tán (hoặc ngay lập tức nếu chưa ở trong room).\nWire trong Inspector: ví dụ UINavigator.GoHome().")]
            public UnityEvent onConfirmed;
        }

        // ══════════════════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        // ── Join / Create Panel ───────────────────────────────────────────────
        [Header("Join / Create Panel")]
        [SerializeField] private GameObject joinCreatePanel;
        [SerializeField] private Button btnCreateRoom;
        [SerializeField] private Button btnQuickJoin;
        [SerializeField] private GameObject codeInputContainer;   // hidden until btn click
        [SerializeField] private TMP_InputField joinCodeInput;
        [Tooltip("Two-state action button:\n  1st click → reveals joinCodeInput container, label changes to 'Tham gia'.\n  2nd click → submits the code (calls JoinByCode).")]
        [SerializeField] private Button btn_CodeAction;
        [Tooltip("Label on btn_CodeAction. Default: 'Nhập code'. After revealing input: 'Tham gia'.")]
        [SerializeField] private TextMeshProUGUI codeActionLabel;

        // ── Navigation (navigating away when in room → confirm leave/disband) ─
        [Header("Navigation")]
        [Tooltip("Mỗi entry: 1 Button + 1 UnityEvent 'onConfirmed'.\n"
               + "Nếu đang trong room → hiện modal leave/disband trước, rồi gọi onConfirmed.\n"
               + "Nếu không trong room → gọi onConfirmed ngay.\n"
               + "Wire onConfirmed trong Inspector: ví dụ UINavigator.GoHome().")]
        [SerializeField] private List<NavigationButtonEntry> navigationButtons;

        // ── In-Room Panel ──────────────────────────────────────────────────────
        [Header("In-Room Panel")]
        [SerializeField] private GameObject inRoomPanel;

        // ── Settings (left side — visible to all, editable by host only) ─────
        [Header("Settings Panel")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Button btnCopyCode;
        [SerializeField] private CustomDropdown modeDropdown;
        [SerializeField] private CustomDropdown mapDropdown;

        [SerializeField] private Button btnSave;

        // ── Team Slots (right side) ───────────────────────────────────────────
        [Header("Team Slots")]
        [SerializeField] private Transform team1Container;
        [SerializeField] private Transform team2Container;
        [SerializeField] private GameObject playerSlotPrefab;

        // ── Action Buttons ────────────────────────────────────────────────────
        [Header("Actions")]
        [SerializeField] private Button btnReady;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Button btnStart;
        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private Button btnLeaveOrDisband;
        [SerializeField] private TextMeshProUGUI leaveOrDisbandText;

        // ── Slot Context Menu ──────────────────────────────────────────────────
        [Header("Slot Context Menu")]
        [Tooltip("Root GameObject of the shared context menu panel. Shown/hidden by code.")]
        [SerializeField] private GameObject slotContextMenu;
        [SerializeField] private Button btn_CM_ViewProfile;
        [Tooltip("Non-host: always shown on others' slots. Initiates slot-swap countdown.")]
        [SerializeField] private Button btn_CM_RequestSwap;
        [SerializeField] private Button btn_CM_Kick;
        [SerializeField] private Button btn_CM_TransferOwner;
        [SerializeField] private Button btn_CM_Close;

        // ── Status ────────────────────────────────────────────────────────────
        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;

        // ══════════════════════════════════════════════════════════════════════
        // SERVICES
        // ══════════════════════════════════════════════════════════════════════

        private RoomService _roomService;
        private PartyService _partyService;
        private RoomState _roomState;
        private SessionState _sessionState;

        // ══════════════════════════════════════════════════════════════════════
        // RUNTIME STATE
        // ══════════════════════════════════════════════════════════════════════

        private readonly Dictionary<string, PlayerSlotView> _slotViews = new();
        private int _maxSlotsPerTeam = 2;
        private string _lastStatus = null;
        private bool _refreshPending = false;
        private float _lastRefreshTime = 0f;
        private const float REFRESH_THROTTLE = 0.1f;

        // Mode selector
        private string[] _modeModeKeys = Array.Empty<string>();
        private string[] _modeDisplayNames = Array.Empty<string>();
        private int _currentModeIdx = 0;
        private string[] _mapIds = Array.Empty<string>();
        private string[] _mapDisplayNames = Array.Empty<string>();
        private int _currentMapIdx = 0;

        // Staged settings
        private string _pendingMode = "2v2";
        private string _pendingMapId = "map_01";
        private bool _pendingIsPublic = true;
        private bool _pendingIsLocked = false;
#pragma warning disable CS0414
        private bool _pendingPasswordChanged = false;
#pragma warning restore CS0414
        private bool _settingsDirty = false;

        // Guard: set to true when we change dropdowns programmatically to suppress OnValueChanged callbacks
        private bool _updatingDropdown = false;

        // Swap request tracking (requester side)
        private long _pendingSwapRequestId = 0L;

        /// <summary>
        /// Set to true BEFORE showing any confirm modal that would eventually leave/disband.
        /// Prevents AutoLeaveOrDisband() from double-firing when the panel fades out after
        /// the user confirms. Restored to false if the user cancels the modal.
        /// </summary>
        private bool _hasAutoLeft = false;

        private bool _codeInputVisible = false;

        // Shared slot context menu
        private PlayerSlotView _contextMenuTargetSV = null;
        private bool _closeContextMenuNextFrame = false;
        private float _menuShownTime = -1f;

        // ══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _roomService = GameManager.Instance.RoomService;
                _partyService = GameManager.Instance.PartyService;
                _sessionState = GameManager.Instance.SessionState;
            }
            _roomState = RoomState.Instance;

            if (btnCreateRoom != null) btnCreateRoom.onClick.AddListener(OnCreateRoomClicked);
            if (btnQuickJoin != null) btnQuickJoin.onClick.AddListener(OnQuickJoinClicked);
            if (btn_CodeAction != null) btn_CodeAction.onClick.AddListener(OnEnterCodeClicked);

            if (navigationButtons != null)
                foreach (var entry in navigationButtons)
                    if (entry?.button != null)
                    {
                        var captured = entry;
                        captured.button.onClick.AddListener(() => OnNavigateAwayClicked(captured.onConfirmed));
                    }

            if (btnCopyCode != null) btnCopyCode.onClick.AddListener(OnCopyCodeClicked);

            BuildModeList();
            _updatingDropdown = true;
            if (modeDropdown != null)
            {
                PopulateDropdown(modeDropdown, new List<string>(_modeDisplayNames), 0);
                modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
            }

            BuildMapList(_pendingMode, _pendingMapId);
            if (mapDropdown != null)
            {
                PopulateDropdown(mapDropdown, new List<string>(_mapDisplayNames), _currentMapIdx);
                mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);
            }
            _updatingDropdown = false;

            // Password/public switch UI removed — no listeners to register.

            if (btnSave != null) btnSave.onClick.AddListener(OnSaveClicked);

            if (btnReady != null) btnReady.onClick.AddListener(OnReadyClicked);
            if (btnStart != null) btnStart.onClick.AddListener(OnStartClicked);
            if (btnLeaveOrDisband != null) btnLeaveOrDisband.onClick.AddListener(OnLeaveOrDisbandClicked);

            if (btn_CM_ViewProfile != null) btn_CM_ViewProfile.onClick.AddListener(OnCM_ViewProfile);
            if (btn_CM_RequestSwap != null) btn_CM_RequestSwap.onClick.AddListener(OnCM_RequestSwap);
            if (btn_CM_Kick != null) btn_CM_Kick.onClick.AddListener(OnCM_Kick);
            if (btn_CM_TransferOwner != null) btn_CM_TransferOwner.onClick.AddListener(OnCM_TransferOwner);
            if (btn_CM_Close != null) btn_CM_Close.onClick.AddListener(HideSlotContextMenu);
            slotContextMenu?.SetActive(false);

            codeInputContainer?.SetActive(false);
        }

        private void Start()
        {
            // Re-acquire services in case GameManager was not ready during Awake.
            if (_sessionState == null || _roomService == null)
            {
                if (GameManager.Instance != null)
                {
                    _roomService    = GameManager.Instance.RoomService;
                    _partyService   = GameManager.Instance.PartyService;
                    _sessionState   = GameManager.Instance.SessionState;
                }
            }
            _roomState = RoomState.Instance;

            _hasAutoLeft = false;

            // Safety net: subscribe here in case OnShow() is not wired in the Inspector
            // or is called before GameEventBus finishes its DelayedSubscribe.
            // UnsubscribeEvents first so we never double-subscribe.
            SubscribeEvents();

            bool inRoom = _roomState != null && _roomState.IsInRoom;
            ShowState(inRoom ? UIState.InRoom : UIState.JoinCreate);
            if (inRoom) RefreshRoomDisplay();
        }

        private void Update()
        {
            if (_closeContextMenuNextFrame)
            {
                _closeContextMenuNextFrame = false;
                HideSlotContextMenu();
                return;
            }
            if (slotContextMenu != null && slotContextMenu.activeSelf
                && Input.GetMouseButtonDown(0)
                && Time.unscaledTime > _menuShownTime + 0.1f)
                _closeContextMenuNextFrame = true;
        }

        private void OnEnable() { /* CanvasGroup navigation does NOT call OnEnable. OnShow() handles init. */ }
        private void OnDisable() { /* CanvasGroup navigation does NOT call OnDisable. OnHide() handles cleanup. */ }

        // ─────────────────────────────────────────────
        // INavigableView — called by UINavigator (wire in Inspector)
        // ─────────────────────────────────────────────

        public void OnShow()
        {
            // Re-acquire services here too — GameManager may not have been ready during Awake/Start.
            if (_sessionState == null || _roomService == null)
            {
                if (GameManager.Instance != null)
                {
                    _roomService    = GameManager.Instance.RoomService;
                    _partyService   = GameManager.Instance.PartyService;
                    _sessionState   = GameManager.Instance.SessionState;
                }
            }
            _roomState = RoomState.Instance;

            _hasAutoLeft = false;
            _lastStatus = null;
            _refreshPending = false;
            _settingsDirty = false;
            SubscribeEvents(); // UnsubscribeEvents is called inside — safe to call multiple times.

            BuildModeList();
            _updatingDropdown = true;
            if (modeDropdown != null)
                PopulateDropdown(modeDropdown, new List<string>(_modeDisplayNames), _currentModeIdx);
            _updatingDropdown = false;

            bool inRoom = _roomState != null && _roomState.IsInRoom;
            ShowState(inRoom ? UIState.InRoom : UIState.JoinCreate);
            if (inRoom) RefreshRoomDisplay();
        }

        public void OnHide()
        {
            UnsubscribeEvents();
            AutoLeaveOrDisband();
        }

        // ══════════════════════════════════════════════════════════════════════
        // PANEL STATE
        // ══════════════════════════════════════════════════════════════════════

        private enum UIState { JoinCreate, InRoom }

        private void ShowState(UIState state)
        {
            NLog($"ShowState({state})");
            joinCreatePanel?.SetActive(state == UIState.JoinCreate);
            inRoomPanel?.SetActive(state == UIState.InRoom);

            if (state == UIState.JoinCreate)
            {
                // Re-enable join/create buttons in case they were individually disabled during InRoom state.
                // Unity SetActive(false) on individual children persists even when parent is re-shown.
                btnCreateRoom?.gameObject.SetActive(true);
                btnQuickJoin?.gameObject.SetActive(true);
                btn_CodeAction?.gameObject.SetActive(true);

                btnStart?.gameObject.SetActive(false);
                btnReady?.gameObject.SetActive(false);
                btnLeaveOrDisband?.gameObject.SetActive(false);
                ResetCodeInput();
                SetStatus("");
            }
            else // InRoom
            {
                // Hide join/create buttons explicitly in case they sit outside joinCreatePanel hierarchy
                btnCreateRoom?.gameObject.SetActive(false);
                btnQuickJoin?.gameObject.SetActive(false);
                btn_CodeAction?.gameObject.SetActive(false);
                codeInputContainer?.SetActive(false);

                btnLeaveOrDisband?.gameObject.SetActive(true);
                btnStart?.gameObject.SetActive(false);
                btnReady?.gameObject.SetActive(false);

                if (modeDropdown != null) modeDropdown.Interactable(false);
                if (mapDropdown != null) mapDropdown.Interactable(false);
                if (btnSave != null) btnSave.interactable = false;
            }
        }

        // Switch helpers removed — no SwitchManager UI in this view.

        // ══════════════════════════════════════════════════════════════════════
        // JOIN / CREATE ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private void OnCreateRoomClicked() =>
            _ = CheckPartyThenRun(async () =>
            {
                NLog($"CreateRoom mode={_pendingMode}");
                SetStatus("Creating room...");
                var result = await _roomService.CreateRoom(
                    _pendingMode, allowFill: false, isPublic: true, isLocked: false, password: null);
                NLog($"CreateRoom result: success={result.Success} msg={result.Message}");

                if (result.Success && result.Data != null)
                {
                    // ── FIX: Always reset code input when successfully entering a room
                    ResetCodeInput();
                    ShowState(UIState.InRoom);
                    RefreshRoomDisplay();
                    SetStatus("Waiting for players...");
                }
                else
                {
                    SetStatus("");
                    GameModalWindow.Instance?.ShowNotice(
                        "Create Room Failed", result.Message ?? "Please try again.");
                }
            });

        private void OnQuickJoinClicked() =>
            _ = CheckPartyThenRun(async () =>
            {
                NLog($"QuickPlay mode={_pendingMode}");
                SetStatus("Finding room...");
                var result = await _roomService.QuickPlay(_pendingMode, false);
                NLog($"QuickPlay result: success={result.Success} msg={result.Message}");

                if (result.Success && result.Data != null)
                {
                    // ── FIX: Reset code input so codeInputContainer doesn't bleed into InRoom state
                    ResetCodeInput();
                    ShowState(UIState.InRoom);
                    RefreshRoomDisplay();
                    SetStatus("Joined room. Waiting for host to start...");
                }
                else
                {
                    SetStatus("");
                    GameModalWindow.Instance?.ShowNotice(
                        "Room Not Found", result.Message ?? "Try again or create a new room.");
                }
            });

        /// <summary>
        /// First click → reveal code input.
        /// Second click → attempt join by code (party check first).
        /// </summary>
        private void OnEnterCodeClicked()
        {
            if (!_codeInputVisible)
            {
                codeInputContainer?.SetActive(true);
                _codeInputVisible = true;
                if (codeActionLabel != null) codeActionLabel.text = "Join";
                return;
            }

            string code = joinCodeInput?.text.Trim() ?? "";
            if (string.IsNullOrEmpty(code)) return;

            _ = CheckPartyThenRun(() => TryJoinByCode(code));
        }

        /// <summary>Attempt join; if password needed → show input modal.</summary>
        private async System.Threading.Tasks.Task TryJoinByCode(string code)
        {
            NLog($"JoinByCode code={code}");
            SetStatus($"Joining {code}...");
            var result = await _roomService.JoinByCode(code, "");

            NLog($"JoinByCode result: success={result.Success} msg={result.Message}");
            if (result.Success && result.Data != null)
            {
                ResetCodeInput();
                ShowState(UIState.InRoom);
                RefreshRoomDisplay();
                SetStatus("Joined room. Waiting for host to start...");
                return;
            }

            bool needPass = result.Message != null && (
                result.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                result.Message.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0);

            if (needPass)
            {
                SetStatus("");
                GameModalWindow.Instance?.ShowInput(
                    title: "Enter Password",
                    desc: $"Room <b>{code}</b> requires a password.",
                    placeholder: "Password...",
                    onConfirm: async pass =>
                    {
                        var r2 = await _roomService.JoinByCode(code, pass);
                        if (r2.Success && r2.Data != null)
                        {
                            ResetCodeInput();
                            ShowState(UIState.InRoom);
                            RefreshRoomDisplay();
                            SetStatus("Joined room. Waiting for host to start...");
                        }
                        else
                        {
                            GameModalWindow.Instance?.ShowNotice(
                                "Wrong Password", r2.Message ?? "Please check and try again.");
                        }
                    }
                );
            }
            else
            {
                SetStatus("");
                GameModalWindow.Instance?.ShowNotice(
                    "Cannot Join", result.Message ?? "Room not found or is full.");
            }
        }

        private void ResetCodeInput()
        {
            _codeInputVisible = false;
            codeInputContainer?.SetActive(false);
            if (joinCodeInput != null) joinCodeInput.text = "";
            if (codeActionLabel != null) codeActionLabel.text = "Enter Code";
        }

        // ── Navigate away with leave/disband confirm ──────────────────────────

        private void OnNavigateAwayClicked(UnityEvent onConfirmed)
        {
            if (_roomState == null || !_roomState.IsInRoom)
            {
                onConfirmed?.Invoke();
                return;
            }

            _hasAutoLeft = true;
            bool isHost = IsLocalPlayerHost();

            GameModalWindow.Instance?.ShowConfirm(
                title: isHost ? "Disband Room?" : "Leave Room?",
                desc: isHost
                    ? "All players will be removed from the room."
                    : "Are you sure you want to leave?",
                onConfirm: async () =>
                {
                    NLog($"NavigateAway confirmed — isHost={isHost}");

                    if (isHost) await DisbandSilent();
                    else await LeaveSilent();

                    // ── FIX: Chuyển về JoinCreate TRƯỚC khi navigate
                    ShowState(UIState.JoinCreate);

                    // ── FIX: Show notice để user biết đã handle xong
                    GameModalWindow.Instance?.ShowNotice(
                        title: isHost ? "Room Disbanded" : "Left Room",
                        desc: isHost ? "The room has been disbanded." : "You have left the room.",
                        closeText: "OK",
                        onClose: () => onConfirmed?.Invoke()   // Navigate SAU khi user đóng notice
                    );
                },
                onCancel: () =>
                {
                    _hasAutoLeft = false;
                },
                confirmText: isHost ? "Disband" : "Leave",
                cancelText: "Cancel"
            );
        }
        // ══════════════════════════════════════════════════════════════════════
        // PARTY CHECK HELPER
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// If the user is in a ranked party, shows a confirm modal to leave/disband first.
        /// Only proceeds with <paramref name="action"/> if leave/disband succeeds.
        /// If not in a party, <paramref name="action"/> runs immediately.
        /// </summary>
        private async System.Threading.Tasks.Task CheckPartyThenRun(
            Func<System.Threading.Tasks.Task> action)
        {
            if (_partyService == null) { await action(); return; }

            var partyResult = await _partyService.GetParty();
            bool inParty = partyResult.Success && partyResult.Data != null;
            if (!inParty) { await action(); return; }

            bool isHost = partyResult.Data.hostUserId == (_sessionState?.UserId ?? -1L);
            string partyStatus = partyResult.Data.partyStatus ?? "";

            // Party in ranked matchmaking queue — block entirely, must cancel queue first.
            if (partyStatus == "IN_QUEUE")
            {
                NLog("CreateRoom blocked: party is IN_QUEUE for ranked matchmaking");
                GameModalWindow.Instance?.ShowNotice(
                    "Cannot Create Custom Room",
                    "Your party is currently in a matchmaking queue. Cancel the queue first before creating a custom lobby.");
                return;
            }

            string title = isHost ? "Disband Party?" : "Leave Party?";
            string desc = isHost
                ? "You are the host. This will disband your current party."
                : "You are in a party. Proceeding will remove you from it.";

            GameModalWindow.Instance?.ShowConfirm(
                title, desc,
                onConfirm: async () =>
                {
                    SetStatus(isHost ? "Disbanding party..." : "Leaving party...");

                    var r = isHost
                        ? await _partyService.DisbandParty()
                        : await _partyService.LeaveParty();

                    if (!r.Success)
                    {
                        SetStatus("");
                        GameModalWindow.Instance?.ShowNotice(
                            "Error",
                            r.Message ?? "Could not leave party. Please try again.");
                        return;
                    }

                    SetStatus("");
                    await action();
                },
                onCancel: null,
                confirmText: isHost ? "Disband" : "Leave Party",
                cancelText: "Cancel"
            );
        }

        // ══════════════════════════════════════════════════════════════════════
        // IN-ROOM ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private async void OnReadyClicked()
        {
            if (_roomService == null || _roomState == null) return;
            bool currentReady = GetLocalPlayerReady();
            bool newReady = !currentReady;
            NLog($"SetReady roomId={_roomState.RoomId} ready={newReady}");
            var result = await _roomService.SetReady(_roomState.RoomId, newReady);
            NLog($"SetReady result: success={result.Success} msg={result.Message}");
            if (!result.Success) SetStatus($"Error: {result.Message}");
        }

        private async void OnStartClicked()
        {
            if (!IsLocalPlayerHost()) return;
            NLog($"StartGame roomId={_roomState.RoomId}");
            SetStatus("Starting match...");
            var result = await _roomService.StartGame(_roomState.RoomId);
            NLog($"StartGame result: success={result.Success} msg={result.Message}");
            if (!result.Success)
            {
                SetStatus("");
                GameModalWindow.Instance?.ShowNotice("Cannot Start", result.Message ?? "Please try again.");
            }
        }

        private void OnLeaveOrDisbandClicked()
        {
            if (IsLocalPlayerHost())
            {
                GameModalWindow.Instance?.ShowConfirm(
                    "Disband Room?",
                    "All players will be removed from the room.",
                    onConfirm: async () =>
                    {
                        NLog("Disbanding room from button click");
                        _hasAutoLeft = true;
                        if (_roomService != null && _roomState != null)
                            await _roomService.DisbandRoom(_roomState.RoomId);
                        _roomState?.ClearRoom();
                        ClearRoomUI();
                        ShowState(UIState.JoinCreate);
                    },
                    confirmText: "Disband",
                    cancelText: "Cancel"
                );
            }
            else
            {
                GameModalWindow.Instance?.ShowConfirm(
                    "Leave Room?",
                    "Are you sure you want to leave?",
                    onConfirm: async () =>
                    {
                        NLog("Leaving room from button click");
                        _hasAutoLeft = true;
                        if (_roomService != null && _roomState != null)
                            await _roomService.LeaveRoom(_roomState.RoomId);
                        _roomState?.ClearRoom();
                        ClearRoomUI();
                        ShowState(UIState.JoinCreate);
                    },
                    confirmText: "Leave",
                    cancelText: "Cancel"
                );
            }
        }

        // ── Auto leave/disband when panel hides ───────────────────────────────

        private void AutoLeaveOrDisband()
        {
            if (_hasAutoLeft) return;
            if (_roomState == null || !_roomState.IsInRoom) return;
            if (_roomState.Status == Constants.ROOM_STATUS_IN_GAME) return;

            _hasAutoLeft = true;
            if (IsLocalPlayerHost())
                _ = DisbandSilent();
            else
                _ = LeaveSilent();
        }

        private async System.Threading.Tasks.Task DisbandSilent()
        {
            try
            {
                if (_roomService != null && _roomState != null)
                    await _roomService.DisbandRoom(_roomState.RoomId);
            }
            catch { }
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
        }

        private async System.Threading.Tasks.Task LeaveSilent()
        {
            try
            {
                if (_roomService != null && _roomState != null)
                    await _roomService.LeaveRoom(_roomState.RoomId);
            }
            catch { }
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
        }

        private void ClearRoomUI()
        {
            ResetCodeInput();
            if (roomCodeText != null) roomCodeText.text = "";

            // ── FIX: Remove slot views + toàn bộ children còn sót trong containers
            foreach (var sv in _slotViews.Values)
                if (sv != null) DestroyImmediate(sv.gameObject);
            _slotViews.Clear();

            // Remove sạch bất kỳ child nào còn sót (edge case khi sv == null)
            ClearContainer(team1Container);
            ClearContainer(team2Container);

            _lastStatus = null;    // ← Reset để InitPendingSettings chạy lại khi vào phòng mới
            _settingsDirty = false;

            // Reset pending settings về default...
            _pendingMode = _modeModeKeys.Length > 0 ? _modeModeKeys[0] : string.Empty;
            _pendingMapId = _mapIds.Length > 0 ? _mapIds[0] : string.Empty;
            _pendingIsPublic = true;
            _pendingIsLocked = false;
            SetSettingsDirty(false);

            _updatingDropdown = true;
            if (modeDropdown != null)
                PopulateDropdown(modeDropdown, new List<string>(_modeDisplayNames), 0);
            if (mapDropdown != null)
                PopulateDropdown(mapDropdown, new List<string>(_mapDisplayNames), 0);
            _updatingDropdown = false;
        }

        private static void ClearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                DestroyImmediate(container.GetChild(i).gameObject);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SETTINGS PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void OnCopyCodeClicked()
        {
            if (_roomState == null) return;
            GUIUtility.systemCopyBuffer = _roomState.RoomCode;
            SetStatus("Room code copied!");
        }

        private void OnModeDropdownChanged(int idx)
        {
            if (_updatingDropdown) return; // Programmatic update — ignore
            if (!IsLocalPlayerHost())
            {
                if (modeDropdown != null) modeDropdown.SetDropdownIndex(_currentModeIdx);
                return;
            }
            _currentModeIdx = idx;
            _pendingMode = _modeModeKeys.Length > idx ? _modeModeKeys[idx] : "2v2";
            NLog($"ModeChanged idx={idx} key={_pendingMode}");
            BuildMapList(_pendingMode, _pendingMapId);
            // Guard _updatingDropdown so MUIP's internal SetDropdownIndex inside
            // PopulateDropdown does not re-fire OnMapDropdownChanged as a spurious user event.
            _updatingDropdown = true;
            SyncMapDropdown(isInteractable: true);
            _updatingDropdown = false;
            SetSettingsDirty(true);
        }

        private void OnMapDropdownChanged(int idx)
        {
            if (_updatingDropdown) return; // Programmatic update — ignore
            if (!IsLocalPlayerHost())
            {
                if (mapDropdown != null) mapDropdown.SetDropdownIndex(_currentMapIdx);
                return;
            }
            _currentMapIdx = Mathf.Clamp(idx, 0, Math.Max(0, _mapIds.Length - 1));
            _pendingMapId = _mapIds.Length > _currentMapIdx ? _mapIds[_currentMapIdx] : GetFallbackMapId(_pendingMode);
            SetSettingsDirty(true);
        }

        // Password/public switch handlers removed.

        private void OnSaveClicked() => _ = SaveSettingsAsync();

        private void SetSettingsDirty(bool dirty)
        {
            _settingsDirty = dirty;
            if (btnSave != null) btnSave.interactable = dirty && IsLocalPlayerHost();
        }

        private void InitPendingSettings(RoomResponse room)
        {
            int modeIdx = FindModeIndex(room.mode);
            _currentModeIdx = modeIdx >= 0 ? modeIdx : 0;
            _pendingMode = room.mode ?? (_modeModeKeys.Length > 0 ? _modeModeKeys[0] : "2v2");
            _pendingMapId = NormalizeMapId(_pendingMode, room.mapId);
            _pendingIsPublic = room.isPublic;
            _pendingIsLocked = room.isLocked;
            BuildMapList(_pendingMode, _pendingMapId);
            SetSettingsDirty(false);
        }

        private async System.Threading.Tasks.Task<bool> ApplySetting(UpdateRoomSettingsRequest req)
        {
            if (_roomService == null || _roomState == null) return false;
            var result = await _roomService.UpdateRoomSettings(_roomState.RoomId, req);
            if (!result.Success)
            {
                SetStatus($"Settings error: {result.Message}");
                return false;
            }
            return true;
        }

        private async System.Threading.Tasks.Task SaveSettingsAsync()
        {
            if (!IsLocalPlayerHost() || !_settingsDirty) return;

            string passwordPayload = null; // password input removed

            bool success = await ApplySetting(new UpdateRoomSettingsRequest
            {
                mode = _pendingMode,
                allowFill = false,
                mapId = _pendingMapId,
                isPublic = _pendingIsPublic,
                isLocked = _pendingIsLocked,
                password = passwordPayload
            });

            if (success)
                SetSettingsDirty(false);
        }

        // ── Mode List Helpers ─────────────────────────────────────────────────

        private void BuildModeList()
        {
            var enabled = GameModeConfig.GetEnabled();
            var keys = new List<string>();
            var names = new List<string>();
            foreach (var m in enabled)
            {
                keys.Add(m.modeKey);
                names.Add(m.displayName);
            }

            if (keys.Count == 0)
                Debug.LogError("[CustomLobbyView] GameModeConfig empty — ensure GameConfigService.FetchAsync() ran successfully.");

            _modeModeKeys = keys.ToArray();
            _modeDisplayNames = names.ToArray();
            _pendingMode = _modeModeKeys.Length > 0 ? _modeModeKeys[0] : string.Empty;
        }

        private int FindModeIndex(string modeKey)
        {
            return Array.IndexOf(_modeModeKeys, modeKey);
        }

        private void BuildMapList(string modeKey, string preferredMapId)
        {
            MapEntry[] maps = MapConfig.GetByMode(modeKey);
            // Do NOT fall back to built-in map ids. Use only server-provided maps.
            if (maps.Length == 0)
            {
                Debug.LogError($"[CustomLobbyView] No maps available for mode={modeKey}. Ensure server provides MapConfig.");
            }

            _mapIds = maps.Select(m => m.mapId).ToArray();
            _mapDisplayNames = maps.Select(m => m.displayName).ToArray();

            _currentMapIdx = Array.IndexOf(_mapIds, preferredMapId);
            if (_currentMapIdx < 0) _currentMapIdx = (_mapIds.Length > 0 ? 0 : -1);

            _pendingMapId = _mapIds.Length > 0 && _currentMapIdx >= 0
                ? _mapIds[_currentMapIdx]
                : string.Empty;
        }

        private void SyncMapDropdown(bool isInteractable)
        {
            if (mapDropdown == null) return;
            PopulateDropdown(mapDropdown, new List<string>(_mapDisplayNames), _currentMapIdx);
            mapDropdown.Interactable(isInteractable && _mapDisplayNames.Length > 0);
        }

        private static string GetFallbackMapId(string modeKey)
        {
            // Deprecated: fallback map id is not allowed. Return empty string to indicate none.
            MapEntry[] maps = MapConfig.GetByMode(modeKey);
            if (maps.Length == 0) maps = MapConfig.GetAvailable();
            return maps.Length > 0 ? maps[0].mapId : string.Empty;
        }

        private static string NormalizeMapId(string modeKey, string mapId)
        {
            MapEntry[] maps = MapConfig.GetByMode(modeKey);
            if (maps.Length == 0) maps = MapConfig.GetAvailable();

            if (!string.IsNullOrWhiteSpace(mapId) && maps.Any(m => m.mapId == mapId))
                return mapId;

            return maps.Length > 0 ? maps[0].mapId : string.Empty;
        }


        private static int GetSlotsForMode(string modeKey)
        {
            foreach (var m in GameModeConfig.GetEnabled())
                if (m.modeKey == modeKey) return m.playersPerTeam;
            return 2;
        }

        // ══════════════════════════════════════════════════════════════════════
        // SLOT INTERACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private async void OnSlotClicked(int team, int slotIdx)
        {
            if (_roomService == null || _roomState == null) return;
            string key = $"{team}_{slotIdx}";
            if (!_slotViews.TryGetValue(key, out var sv)) return;

            if (sv.IsEmpty)
            {
                // Move self to empty slot immediately — no context menu needed
                var result = await _roomService.ChangeTeam(_roomState.RoomId, team, slotIdx);
                if (!result.Success) SetStatus($"Error: {result.Message}");
                return;
            }

            long targetId = sv.Player?.userId ?? 0L;
            if (targetId == (_sessionState?.UserId ?? 0L)) return; // own slot — ignore

            // Toggle: clicking same occupied slot while menu is open → close it
            if (slotContextMenu != null && slotContextMenu.activeSelf && _contextMenuTargetSV == sv)
            {
                HideSlotContextMenu();
                return;
            }

            _contextMenuTargetSV = sv;
            bool isHost = IsLocalPlayerHost();
            if (btn_CM_RequestSwap != null) btn_CM_RequestSwap.gameObject.SetActive(!isHost);
            if (btn_CM_Kick != null) btn_CM_Kick.gameObject.SetActive(isHost);
            if (btn_CM_TransferOwner != null) btn_CM_TransferOwner.gameObject.SetActive(isHost);
            if (slotContextMenu != null) slotContextMenu.SetActive(true);
            _menuShownTime = Time.unscaledTime;
        }

        private void HideSlotContextMenu()
        {
            _contextMenuTargetSV = null;
            slotContextMenu?.SetActive(false);
        }

        private void OnCM_ViewProfile()
        {
            long uid = _contextMenuTargetSV?.Player?.userId ?? 0L;
            string uname = _contextMenuTargetSV?.Player?.username;
            HideSlotContextMenu();
            if (uid == 0L) return;
            NLog($"ViewProfile userId={uid} username={uname}");
            PlayerProfilePanel.Instance?.Show(uid, uname);
        }

        private async void OnCM_RequestSwap()
        {
            var target = _contextMenuTargetSV;
            HideSlotContextMenu();
            if (target?.Player == null || _roomService == null || _roomState == null) return;

            var swapResult = await _roomService.RequestSwap(
                _roomState.RoomId, target.Player.userId, target.Team, target.Slot);
            if (!swapResult.Success)
            {
                SetStatus($"Swap request failed: {swapResult.Message}");
                return;
            }

            _pendingSwapRequestId = swapResult.Data?.requestId ?? 0L;
            string targetName = target.Player.username ?? "Player";
            NLog($"SwapRequest sent to {targetName} requestId={_pendingSwapRequestId}");
            GameModalWindow.Instance?.ShowCountdown(
                title: "Swap Request",
                desc: $"Waiting for <b>{targetName}</b> to accept...",
                seconds: 15,
                onConfirm: null,
                onExpire: () => { _ = CancelSwapSilent(_pendingSwapRequestId); _pendingSwapRequestId = 0L; },
                showConfirm: false,
                cancelText: "Cancel Request");
        }

        private void OnCM_Kick()
        {
            long uid = _contextMenuTargetSV?.Player?.userId ?? 0L;
            HideSlotContextMenu();
            if (uid != 0L) OnKickClicked(uid);
        }

        private void OnCM_TransferOwner()
        {
            long uid = _contextMenuTargetSV?.Player?.userId ?? 0L;
            HideSlotContextMenu();
            if (uid != 0L) OnTransferOwnerClicked(uid);
        }

        private void OnKickWithConfirm(long targetUserId, string username)
        {
            if (targetUserId == 0L) return;
            string name = string.IsNullOrEmpty(username) ? "this player" : $"<b>{username}</b>";
            GameModalWindow.Instance?.ShowConfirm(
                "Kick Player?",
                $"Remove {name} from the room?",
                onConfirm: () => OnKickClicked(targetUserId),
                confirmText: "Kick",
                cancelText: "Cancel"
            );
        }

        private async void OnKickClicked(long targetUserId)
        {
            if (_roomService == null || _roomState == null) return;
            NLog($"KickPlayer userId={targetUserId}");
            var result = await _roomService.KickPlayer(_roomState.RoomId, targetUserId);
            NLog($"KickPlayer result: success={result.Success} msg={result.Message}");
            if (!result.Success) SetStatus($"Kick failed: {result.Message}");
        }

        private void OnTransferOwnerClicked(long targetUserId)
        {
            GameModalWindow.Instance?.ShowConfirm(
                "Transfer Host?",
                "You will no longer be the host of this room.",
                onConfirm: async () =>
                {
                    if (_roomService == null || _roomState == null) return;
                    NLog($"TransferOwner to userId={targetUserId}");
                    var result = await _roomService.TransferOwner(_roomState.RoomId, targetUserId);
                    if (!result.Success) SetStatus($"Transfer failed: {result.Message}");
                },
                confirmText: "Transfer",
                cancelText: "Cancel"
            );
        }

        private async System.Threading.Tasks.Task CancelSwapSilent(long requestId)
        {
            if (_roomService == null || _roomState == null || requestId == 0L) return;
            try { await _roomService.CancelSwapRequest(_roomState.RoomId, requestId); } catch { }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DISPLAY / REFRESH
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshRoomDisplay()
        {
            float now = Time.time;
            if (now - _lastRefreshTime < REFRESH_THROTTLE && _lastRefreshTime > 0f)
            {
                if (!_refreshPending)
                {
                    _refreshPending = true;
                    StartCoroutine(DelayedRefresh());
                }
                return;
            }
            _refreshPending = false;
            _lastRefreshTime = now;
            RefreshRoomDisplayImmediate();
        }

        private IEnumerator DelayedRefresh()
        {
            yield return new WaitForSeconds(REFRESH_THROTTLE);
            _refreshPending = false;
            _lastRefreshTime = Time.time;
            RefreshRoomDisplayImmediate();
        }

        private void RefreshRoomDisplayImmediate()
        {
            if (_roomState == null || !_roomState.IsInRoom) return;
            var room = _roomState.CurrentRoom;
            bool isHost = IsLocalPlayerHost();
            bool waiting = room.status == Constants.ROOM_STATUS_WAITING;
            bool usePendingSettings = isHost && waiting && _settingsDirty;

            // ── Room code ──────────────────────────────────────────────────────
            if (roomCodeText != null) roomCodeText.text = room.roomCode;

            // ── Init pending settings once per new room ────────────────────────
            if (_lastStatus == null) InitPendingSettings(room);

            string activeMode = usePendingSettings ? _pendingMode : room.mode;
            string activeMapId = usePendingSettings ? _pendingMapId : NormalizeMapId(room.mode, room.mapId);
            bool activeIsPublic = usePendingSettings ? _pendingIsPublic : room.isPublic;
            bool activeIsLocked = usePendingSettings ? _pendingIsLocked : room.isLocked;

            int modeIdx = FindModeIndex(activeMode);
            if (modeIdx >= 0) _currentModeIdx = modeIdx;
            BuildMapList(activeMode, activeMapId);

            _updatingDropdown = true;
            if (modeDropdown != null)
            {
                modeDropdown.SetDropdownIndex(_currentModeIdx);
                modeDropdown.Interactable(isHost && waiting);
            }
            SyncMapDropdown(isHost && waiting);
            _updatingDropdown = false;

            // Password/public switches removed — no UI to update.

            // ── Save button ────────────────────────────────────────────────────
            if (btnSave != null) btnSave.interactable = isHost && waiting && _settingsDirty;

            // ── Slot count ─────────────────────────────────────────────────────
            _maxSlotsPerTeam = GetSlotsForMode(room.mode);

            // ── Start button (host) ────────────────────────────────────────────
            bool full = (room.players?.Count ?? 0) >= _maxSlotsPerTeam * 2;
            bool allReady = full && (room.players?.All(p => p.isReady) ?? false);

            if (btnStart != null)
            {
                btnStart.gameObject.SetActive(isHost && waiting);
                btnStart.interactable = isHost && waiting && allReady;
            }
            if (startButtonText != null)
                startButtonText.text = allReady ? "START" : "Waiting for all players...";

            // ── Ready button (non-host) ────────────────────────────────────────
            if (btnReady != null)
            {
                btnReady.gameObject.SetActive(!isHost && waiting);
                bool myReady = GetLocalPlayerReady();
                if (readyButtonText != null) readyButtonText.text = myReady ? "Cancel Ready" : "Ready";
            }

            // ── Leave / Disband button ─────────────────────────────────────────
            if (leaveOrDisbandText != null)
                leaveOrDisbandText.text = isHost ? "Disband Room" : "Leave Room";

            // ── Team slots ─────────────────────────────────────────────────────
            UpdatePlayerSlots(room.players);

            // Track current status so InitPendingSettings only fires once per room entry
            _lastStatus = room.status;
        }

        private void UpdatePlayerSlots(List<RoomPlayerResponse> players)
        {
            // Use immediate destroy so containers are clean before we add new slots
            _slotViews.Clear();
            ClearContainer(team1Container);
            ClearContainer(team2Container);

            bool isHost = IsLocalPlayerHost();

            for (int team = 1; team <= 2; team++)
            {
                Transform container = team == 1 ? team1Container : team2Container;
                if (container == null || playerSlotPrefab == null) continue;

                for (int slotIdx = 0; slotIdx < _maxSlotsPerTeam; slotIdx++)
                {
                    var player = players?.FirstOrDefault(p => p.team == team && p.slot == slotIdx);
                    var go = Instantiate(playerSlotPrefab, container);
                    var sv = ComponentResolver.Find<PlayerSlotView>(go)
                                    .OnSelf().InChildren().Resolve();

                    if (sv == null) continue;

                    var capturedPlayer = player;
                    sv.SetSlot(team, slotIdx, player, isHost,
                               onSlotClicked: OnSlotClicked,
                               onKickClicked: isHost ? uid => OnKickWithConfirm(uid, capturedPlayer?.username) : (System.Action<long>)null);

                    _slotViews[$"{team}_{slotIdx}"] = sv;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // EVENT BUS
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            // Always unsubscribe first to guarantee exactly-once subscription,
            // regardless of how many times OnShow() / Start() call this.
            UnsubscribeEvents();

            if (GameEventBus.Instance == null)
            {
                Debug.LogWarning("[CustomLobbyView] SubscribeEvents: GameEventBus.Instance is null — events will not fire. " +
                                 "Ensure GameEventBus is present in the scene and initialized before CustomLobbyView.");
                return;
            }

            GameEventBus.Instance.OnRoomUpdated += HandleRoomUpdated;
            GameEventBus.Instance.OnPlayerJoined += HandlePlayerJoined;
            GameEventBus.Instance.OnPlayerLeft += HandlePlayerLeft;
            GameEventBus.Instance.OnPlayerReady += HandlePlayerReady;
            GameEventBus.Instance.OnTeamChanged += HandleTeamChanged;
            GameEventBus.Instance.OnRoomStatusChanged += HandleRoomStatusChanged;
            GameEventBus.Instance.OnRoomDisbanded += HandleRoomDisbanded;
            GameEventBus.Instance.OnYouWereKicked += HandleYouWereKicked;
            GameEventBus.Instance.OnSwapRequest += HandleSwapRequest;
            GameEventBus.Instance.OnSwapRequestStatus += HandleSwapRequestStatus;
            GameEventBus.Instance.OnForceLogout += HandleForceLogout;
            GameEventBus.Instance.OnSessionExpired += HandleSessionExpired;
            GameEventBus.Instance.OnAppFocusGained += HandleAppFocusGained;
            GameEventBus.Instance.OnAppResumed += HandleAppResumed;
        }

        private void UnsubscribeEvents()
        {
            if (GameEventBus.Instance == null) return;
            GameEventBus.Instance.OnRoomUpdated -= HandleRoomUpdated;
            GameEventBus.Instance.OnPlayerJoined -= HandlePlayerJoined;
            GameEventBus.Instance.OnPlayerLeft -= HandlePlayerLeft;
            GameEventBus.Instance.OnPlayerReady -= HandlePlayerReady;
            GameEventBus.Instance.OnTeamChanged -= HandleTeamChanged;
            GameEventBus.Instance.OnRoomStatusChanged -= HandleRoomStatusChanged;
            GameEventBus.Instance.OnRoomDisbanded -= HandleRoomDisbanded;
            GameEventBus.Instance.OnYouWereKicked -= HandleYouWereKicked;
            GameEventBus.Instance.OnSwapRequest -= HandleSwapRequest;
            GameEventBus.Instance.OnSwapRequestStatus -= HandleSwapRequestStatus;
            GameEventBus.Instance.OnForceLogout -= HandleForceLogout;
            GameEventBus.Instance.OnSessionExpired -= HandleSessionExpired;
            GameEventBus.Instance.OnAppFocusGained -= HandleAppFocusGained;
            GameEventBus.Instance.OnAppResumed -= HandleAppResumed;
        }

        // ══════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private void HandleRoomUpdated(RoomResponse room)
        {
            if (room != null) _roomState?.SetRoom(room);
            RefreshRoomDisplay();
        }

        private void HandlePlayerJoined(GameWebSocketService.PlayerJoinedEvent evt)
        {
            if (evt.room != null) _roomState?.SetRoom(evt.room);
            RefreshRoomDisplay();
        }

        private void HandlePlayerLeft(GameWebSocketService.PlayerLeftEvent evt)
        {
            if (evt.room != null) _roomState?.SetRoom(evt.room);
            RefreshRoomDisplay();
        }

        private void HandlePlayerReady(GameWebSocketService.PlayerReadyEvent evt)
        {
            if (evt.room != null) _roomState?.SetRoom(evt.room);
            RefreshRoomDisplay();
        }

        private void HandleTeamChanged(GameWebSocketService.TeamChangedEvent evt)
        {
            NLog($"[TEAM_CHANGED] userId={evt.userId} newTeam={evt.team} newSlot={evt.slot} " +
                 $"hasRoom={evt.room != null} roomPlayerCount={evt.room?.players?.Count ?? -1}");
            if (evt.room?.players != null)
                foreach (var p in evt.room.players)
                    NLog($"  player={p.username} team={p.team} slot={p.slot}");

            if (evt.room != null)
            {
                _roomState?.SetRoom(evt.room);
                RefreshRoomDisplay();
            }
            else
            {
                // room field not populated — patch local state manually and refresh
                Debug.LogWarning("[CustomLobby] team_changed: room is null — patching RoomState manually");
                var current = _roomState?.CurrentRoom;
                if (current?.players != null)
                {
                    var player = current.players.Find(p => p.userId == evt.userId);
                    if (player != null)
                    {
                        player.team = evt.team;
                        player.slot = evt.slot;
                        _roomState?.SetRoom(current);
                    }
                }
                RefreshRoomDisplay();
            }
        }

        private void HandleRoomStatusChanged(GameWebSocketService.RoomStatusChangedEvent evt)
        {
            NLog($"RoomStatusChanged: {_lastStatus} → {evt.newStatus}");
            if (evt.room != null) _roomState?.SetRoom(evt.room);

            if (evt.newStatus == Constants.ROOM_STATUS_IN_GAME
                && _lastStatus != Constants.ROOM_STATUS_IN_GAME)
            {
                _hasAutoLeft = true;
                MatchLoadingOverlay.Instance?.Show();
            }
            else if (evt.newStatus == Constants.ROOM_STATUS_CLOSED
                     && _lastStatus != Constants.ROOM_STATUS_CLOSED)
            {
                _hasAutoLeft = true;
                _roomState?.ClearRoom();
                ClearRoomUI();
                ShowState(UIState.JoinCreate);
                GameModalWindow.Instance?.ShowNotice("Room Closed", "The host has disbanded the room.");
            }

            _lastStatus = evt.newStatus;
            RefreshRoomDisplay();
        }

        /// <summary>
        /// Fired when the server removes the room entirely (host disbanded or host disconnected).
        /// room_disbanded arrives WITHOUT a preceding room_status_changed, so we handle all
        /// cleanup here. If room_status_changed CLOSED already fired first, the guard
        /// _hasAutoLeft prevents double-processing.
        /// </summary>
        private void HandleRoomDisbanded(GameWebSocketService.RoomDisbandedEvent evt)
        {
            NLog($"RoomDisbanded: roomId={evt.roomId} reason={evt.reason}");
            if (_hasAutoLeft) return; // already handled by room_status_changed CLOSED
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
            string msg = evt.reason == "owner_disconnected"
                ? "The host disconnected. The room has been closed."
                : "The room has been disbanded.";
            GameModalWindow.Instance?.ShowNotice("Room Closed", msg);
        }

        /// <summary>Fired ONLY on the kicked player when the host removes them from the room.</summary>
        private void HandleYouWereKicked(GameWebSocketService.YouWereKickedEvent evt)
        {
            NLog($"YouWereKicked: roomId={evt.roomId}");
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
            GameModalWindow.Instance?.ShowNotice("Removed", "You have been removed from the room.");
        }

        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt)
        {
            long myId = _sessionState?.UserId ?? 0L;
            if (evt.targetUserId != myId) return;

            long requestId = evt.requestId;
            string requesterName = evt.fromUsername ?? "Player";
            NLog($"SwapRequest received from {requesterName} requestId={requestId}");

            GameModalWindow.Instance?.ShowCountdown(
                title: "Swap Request",
                desc: $"<b>{requesterName}</b> wants to swap seats with you.",
                seconds: 15,
                onConfirm: () => _ = AcceptSwapSilent(requestId),
                onExpire: () => _ = RejectSwapSilent(requestId),
                showConfirm: true,
                confirmText: "Accept",
                cancelText: "Decline"
            );
        }

        private void HandleSwapRequestStatus(GameWebSocketService.SwapRequestStatusEvent evt)
        {
            // ── LOG CHI TIẾT ──
            NLog($"[SWAP_STATUS] requestId={evt.requestId} status={evt.status} " +
                 $"hasRoom={evt.room != null} " +
                 $"roomPlayerCount={evt.room?.players?.Count ?? -1}");
            if (evt.room?.players != null)
                foreach (var p in evt.room.players)
                    NLog($"  player={p.username} team={p.team} slot={p.slot} ready={p.isReady}");
            // ── END LOG ──

            bool isOurRequest = (evt.requestId == _pendingSwapRequestId || evt.requestId == 0L)
                                && _pendingSwapRequestId != 0L;
            if (isOurRequest)
            {
                _pendingSwapRequestId = 0L;
                GameModalWindow.Instance?.Close();
                if (evt.status == "REJECTED" || evt.status == "CANCELLED")
                    GameModalWindow.Instance?.ShowNotice("Swap Declined", "The player declined your swap request.");
                else if (evt.status == "EXPIRED")
                    GameModalWindow.Instance?.ShowNotice("Swap Expired", "The swap request has expired.");
            }

            if (evt.room != null) _roomState?.SetRoom(evt.room);
            RefreshRoomDisplay();
        }


        private void HandleForceLogout()
        {
            NLog("HandleForceLogout");
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            GameModalWindow.Instance?.ShowNotice(
                "Signed Out",
                "Your account was signed in from another location.",
                closeText: "OK",
                onClose: LoginView.Logout
            );
        }

        private void HandleSessionExpired()
        {
            NLog("HandleSessionExpired");
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            GameModalWindow.Instance?.ShowNotice(
                "Session Expired",
                "Your session has expired. Please log in again.",
                closeText: "OK",
                onClose: LoginView.Logout
            );
        }

        private void HandleAppFocusGained() => RefreshRoomDisplay();
        private void HandleAppResumed() => RefreshRoomDisplay();

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static void PopulateDropdown(CustomDropdown dd, List<string> names, int selectIndex)
        {
            dd.items.Clear();
            foreach (var name in names)
                dd.CreateNewItem(name, notify: false);
            if (dd.items.Count > 0)
            {
                dd.SetupDropdown();
                dd.SetDropdownIndex(Mathf.Clamp(selectIndex, 0, dd.items.Count - 1));
            }
        }

        private bool IsLocalPlayerHost()
        {
            if (_roomState == null || _sessionState == null) return false;
            return _roomState.CurrentRoom?.ownerId == _sessionState.UserId;
        }

        private bool GetLocalPlayerReady()
        {
            if (_roomState == null || _sessionState == null) return false;
            long uid = _sessionState.UserId;
            return _roomState.CurrentRoom?.players?.FirstOrDefault(p => p.userId == uid)?.isReady ?? false;
        }

        private async System.Threading.Tasks.Task AcceptSwapSilent(long requestId)
        {
            if (_roomService == null || _roomState == null) return;
            try { await _roomService.AcceptSwapRequest(_roomState.RoomId, requestId); } catch { }
        }

        private async System.Threading.Tasks.Task RejectSwapSilent(long requestId)
        {
            if (_roomService == null || _roomState == null) return;
            try { await _roomService.RejectSwapRequest(_roomState.RoomId, requestId); } catch { }
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Conditional log filtered by NightHunt.Utils.ConditionalLogger (stripped in Release).</summary>
        private static void NLog(string msg) =>
            NightHunt.Utils.ConditionalLogger.Log("CustomLobby", msg);

        public void RefreshPlayerList() => RefreshRoomDisplay();
    }
}
