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
    ///   If in a party → ShowConfirm "Leave/Disband party?" before any action.
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
    ///   Navigating away (UINavigator → GoHome/GoLogin) triggers OnDisable.
    ///   OnDisable auto-disbands (host) or auto-leaves (member).
    ///   Guard: skipped when game status = IN_GAME (match started).
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
            public Button    button;
            [Tooltip("Fired sau khi room đã rời/giải tán (hoặc ngay lập tức nếu chưa đủ trong room).\nWire trong Inspector: ví dụ UINavigator.GoHome().")]
            public UnityEvent onConfirmed;
        }

        // ══════════════════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        // ── Join / Create Panel ───────────────────────────────────────────────
        [Header("Join / Create Panel")]
        [SerializeField] private GameObject     joinCreatePanel;
        [SerializeField] private Button         btnCreateRoom;
        [SerializeField] private Button         btnQuickJoin;
        [SerializeField] private GameObject     codeInputContainer;   // hidden until btn click
        [SerializeField] private TMP_InputField joinCodeInput;
        [Tooltip("Two-state action button:\n  1st click → reveals joinCodeInput container, label changes to 'Tham gia'.\n  2nd click → submits the code (calls JoinByCode).")]
        [SerializeField] private Button         btn_CodeAction;
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
        [SerializeField] private GameObject     inRoomPanel;

        // ── Settings (left side — visible to all, editable by host only) ─────
        [Header("Settings Panel")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Button          btnCopyCode;
        // Mode selector — CustomDropdown (MUIP), options built from GameModeConfig at runtime
        [SerializeField] private CustomDropdown  modeDropdown;
        [SerializeField] private CustomDropdown  mapDropdown;

        // ── Password (SwitchManager thay thế Toggle) ─────────────────────────
        [Tooltip("SwitchManager cho bật/tắt mật khẩu phòng.")]
        [SerializeField] private Michsky.UI.Shift.SwitchManager   passwordSwitch;
        [SerializeField] private GameObject      passwordInputContainer;
        [SerializeField] private TMP_InputField  passwordInput;

        // ── Public / Private (SwitchManager thay thế Toggle) ─────────────────
        [Tooltip("SwitchManager cho public/private phòng.")]
        [SerializeField] private Michsky.UI.Shift.SwitchManager   publicSwitch;

        // Save — applies staged settings to server
        [SerializeField] private Button          btnSave;

        // ── Team Slots (right side) ───────────────────────────────────────────
        [Header("Team Slots")]
        [SerializeField] private Transform  team1Container;
        [SerializeField] private Transform  team2Container;
        [SerializeField] private GameObject playerSlotPrefab;

        // ── Action Buttons ────────────────────────────────────────────────────
        [Header("Actions")]
        [SerializeField] private Button          btnReady;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Button          btnStart;
        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private Button          btnLeaveOrDisband;
        [SerializeField] private TextMeshProUGUI leaveOrDisbandText;

        // ── Slot Context Menu (shared — one per CustomLobbyView, not per prefab) ─
        [Header("Slot Context Menu")]
        [Tooltip("Root GameObject of the shared context menu panel. Shown/hidden by code.")]
        [SerializeField] private GameObject      slotContextMenu;
        [SerializeField] private Button          btn_CM_ViewProfile;
        [Tooltip("Non-host: always shown on others\u2019 slots. Initiates slot-swap countdown.")]
        [SerializeField] private Button          btn_CM_RequestSwap;
        [SerializeField] private Button          btn_CM_Kick;
        [SerializeField] private Button          btn_CM_TransferOwner;
        [SerializeField] private Button          btn_CM_Close;

        // ── Status ────────────────────────────────────────────────────────────
        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;

        // ══════════════════════════════════════════════════════════════════════
        // SERVICES
        // ══════════════════════════════════════════════════════════════════════

        private RoomService  _roomService;
        private PartyService _partyService;
        private RoomState    _roomState;
        private SessionState _sessionState;

        // ══════════════════════════════════════════════════════════════════════
        // RUNTIME STATE
        // ══════════════════════════════════════════════════════════════════════

        private readonly Dictionary<string, PlayerSlotView> _slotViews = new();
        private int   _maxSlotsPerTeam = 2;
        private string _lastStatus     = null;
        private bool   _refreshPending = false;
        private float  _lastRefreshTime = 0f;
        private const float REFRESH_THROTTLE = 0.1f;

        // Mode selector — populated from GameModeConfig (deduplicated by modeKey)
        private string[] _modeModeKeys     = Array.Empty<string>();
        private string[] _modeDisplayNames = Array.Empty<string>();
        private int _currentModeIdx = 0;
        private string[] _mapIds           = Array.Empty<string>();
        private string[] _mapDisplayNames  = Array.Empty<string>();
        private int _currentMapIdx = 0;

        // Staged settings — only sent to server when user clicks btnSave
        private string _pendingMode     = "2v2";
        private string _pendingMapId    = "map_01";
        private bool   _pendingIsPublic = true;
        private bool   _pendingIsLocked = false;
        private bool   _pendingPasswordChanged = false;
        private bool   _settingsDirty   = false;

        // Swap request tracking (requester side)
        private long _pendingSwapRequestId = 0L;

        // Prevents OnDisable from firing leave/disband when game has started
        // or when we already left intentionally
        private bool _hasAutoLeft = false;

        // Tracks whether code input is visible
        private bool _codeInputVisible = false;

        // Shared slot context menu — stores target while menu is open
        private PlayerSlotView _contextMenuTargetSV  = null;
        private bool _closeContextMenuNextFrame = false;
        private float _menuShownTime = -1f;  // prevents same-frame close on menu open

        // ══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _roomService  = GameManager.Instance.RoomService;
                _partyService = GameManager.Instance.PartyService;
                _sessionState = GameManager.Instance.SessionState;
            }
            _roomState = RoomState.Instance;

            // Join/Create buttons
            if (btnCreateRoom != null) btnCreateRoom.onClick.AddListener(OnCreateRoomClicked);
            if (btnQuickJoin  != null) btnQuickJoin.onClick.AddListener(OnQuickJoinClicked);
            if (btn_CodeAction != null) btn_CodeAction.onClick.AddListener(OnEnterCodeClicked);

            // Navigation buttons — each shows leave/disband confirm before calling its own onConfirmed event
            if (navigationButtons != null)
                foreach (var entry in navigationButtons)
                    if (entry?.button != null)
                    {
                        var captured = entry;
                        captured.button.onClick.AddListener(() => OnNavigateAwayClicked(captured.onConfirmed));
                    }

            // Settings
            if (btnCopyCode != null) btnCopyCode.onClick.AddListener(OnCopyCodeClicked);

            BuildModeList();
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

            // ── SwitchManager listeners ───────────────────────────────────────
            // Package Michsky.UI.Shift không có onValueChanged(bool) —
            // phải dùng OnEvents (bật) và OffEvents (tắt) riêng biệt.
            // NOTE: Nhớ tắt "Invoke At Start" trên cả 2 SwitchManager trong Inspector.
            if (passwordSwitch != null)
            {
                passwordSwitch.OnEvents.AddListener(OnPasswordSwitchOn);
                passwordSwitch.OffEvents.AddListener(OnPasswordSwitchOff);
            }
            if (passwordInput != null)
                passwordInput.onValueChanged.AddListener(OnPasswordInputChanged);
            if (publicSwitch != null)
            {
                publicSwitch.OnEvents.AddListener(OnPublicSwitchOn);
                publicSwitch.OffEvents.AddListener(OnPublicSwitchOff);
            }

            if (btnSave != null) btnSave.onClick.AddListener(OnSaveClicked);

            // Action buttons
            if (btnReady          != null) btnReady.onClick.AddListener(OnReadyClicked);
            if (btnStart          != null) btnStart.onClick.AddListener(OnStartClicked);
            if (btnLeaveOrDisband != null) btnLeaveOrDisband.onClick.AddListener(OnLeaveOrDisbandClicked);

            // Slot context menu
            if (btn_CM_ViewProfile   != null) btn_CM_ViewProfile.onClick.AddListener(OnCM_ViewProfile);
            if (btn_CM_RequestSwap   != null) btn_CM_RequestSwap.onClick.AddListener(OnCM_RequestSwap);
            if (btn_CM_Kick          != null) btn_CM_Kick.onClick.AddListener(OnCM_Kick);
            if (btn_CM_TransferOwner != null) btn_CM_TransferOwner.onClick.AddListener(OnCM_TransferOwner);
            if (btn_CM_Close        != null) btn_CM_Close.onClick.AddListener(HideSlotContextMenu);
            slotContextMenu?.SetActive(false);

            // Hide code input by default
            codeInputContainer?.SetActive(false);
        }

        private void Start()
        {
            _hasAutoLeft = false;
            bool inRoom  = _roomState != null && _roomState.IsInRoom;
            ShowState(inRoom ? UIState.InRoom : UIState.JoinCreate);
            if (inRoom) RefreshRoomDisplay();
        }

        private void Update()
        {
            // Close slot context menu when clicking outside
            if (_closeContextMenuNextFrame)
            {
                _closeContextMenuNextFrame = false;
                HideSlotContextMenu();
                return;
            }
            // Only schedule a close when not in the same frame the menu was just shown
            // (avoids the slot button's onClick immediately re-opening after the close fires).
            if (slotContextMenu != null && slotContextMenu.activeSelf
                && Input.GetMouseButtonDown(0)
                && Time.unscaledTime > _menuShownTime + 0.1f)
                _closeContextMenuNextFrame = true;
        }

        private void OnEnable()
        {
            // CanvasGroup navigation does NOT call OnEnable/OnDisable.
            // Initialization on show is handled by INavigableView.OnShow().
        }

        private void OnDisable()
        {
            // CanvasGroup navigation does NOT call OnDisable.
            // Cleanup on hide is handled by INavigableView.OnHide().
        }

        // ─────────────────────────────────────────────
        // INavigableView — called by UINavigator
        // ─────────────────────────────────────────────

        public void OnShow()
        {
            _hasAutoLeft    = false;
            _lastStatus     = null;
            _refreshPending = false;
            _settingsDirty  = false;
            SubscribeEvents();

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
            joinCreatePanel?.SetActive(state == UIState.JoinCreate);
            inRoomPanel?.SetActive(state == UIState.InRoom);

            if (state == UIState.JoinCreate)
            {
                ResetCodeInput();
                SetStatus("");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // JOIN / CREATE ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private void OnCreateRoomClicked() =>
            _ = CheckPartyThenRun(async () =>
            {
                SetStatus("Đang tạo phòng…");
                var result = await _roomService.CreateRoom(
                    _pendingMode, isPublic: true, isLocked: false, password: null);

                if (result.Success && result.Data != null)
                {
                    ShowState(UIState.InRoom);
                    RefreshRoomDisplay();
                    SetStatus("Đang chờ người chơi…");
                }
                else
                {
                    SetStatus("");
                    GameModalWindow.Instance?.ShowNotice(
                        "Tạo phòng thất bại", result.Message ?? "Vui lòng thử lại.");
                }
            });

        private void OnQuickJoinClicked() =>
            _ = CheckPartyThenRun(async () =>
            {
                SetStatus("Đang tìm phòng…");
                var result = await _roomService.QuickPlay(_pendingMode);

                if (result.Success && result.Data != null)
                {
                    ShowState(UIState.InRoom);
                    RefreshRoomDisplay();
                    SetStatus("Đã vào phòng. Chờ host bắt đầu…");
                }
                else
                {
                    SetStatus("");
                    GameModalWindow.Instance?.ShowNotice(
                        "Không tìm được phòng", result.Message ?? "Thử lại hoặc tạo phòng mới.");
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
                if (codeActionLabel != null) codeActionLabel.text = "Tham gia";
                return;
            }

            string code = joinCodeInput?.text.Trim() ?? "";
            if (string.IsNullOrEmpty(code)) return;

            _ = CheckPartyThenRun(() => TryJoinByCode(code));
        }

        /// <summary>Attempt join; if password needed → show input modal.</summary>
        private async System.Threading.Tasks.Task TryJoinByCode(string code)
        {
            SetStatus($"Đang tham gia {code}…");
            var result = await _roomService.JoinByCode(code, "");

            if (result.Success && result.Data != null)
            {
                ResetCodeInput();
                ShowState(UIState.InRoom);
                RefreshRoomDisplay();
                SetStatus("Đã vào phòng. Chờ host bắt đầu…");
                return;
            }

            bool needPass = result.Message != null && (
                result.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                result.Message.IndexOf("mật khẩu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                result.Message.IndexOf("locked",   StringComparison.OrdinalIgnoreCase) >= 0);

            if (needPass)
            {
                SetStatus("");
                GameModalWindow.Instance?.ShowInput(
                    title:       "Nhập mật khẩu",
                    desc:        $"Phòng <b>{code}</b> yêu cầu mật khẩu.",
                    placeholder: "Mật khẩu…",
                    onConfirm: async pass =>
                    {
                        var r2 = await _roomService.JoinByCode(code, pass);
                        if (r2.Success && r2.Data != null)
                        {
                            ResetCodeInput();
                            ShowState(UIState.InRoom);
                            RefreshRoomDisplay();
                            SetStatus("Đã vào phòng. Chờ host bắt đầu…");
                        }
                        else
                        {
                            GameModalWindow.Instance?.ShowNotice(
                                "Sai mật khẩu", r2.Message ?? "Vui lòng kiểm tra lại.");
                        }
                    }
                );
            }
            else
            {
                SetStatus("");
                GameModalWindow.Instance?.ShowNotice(
                    "Không thể tham gia", result.Message ?? "Phòng không tồn tại hoặc đã đầy.");
            }
        }

        private void ResetCodeInput()
        {
            _codeInputVisible = false;
            codeInputContainer?.SetActive(false);
            if (joinCodeInput   != null) joinCodeInput.text = "";
            if (codeActionLabel != null) codeActionLabel.text = "Nh\u1eadp code";
        }

        // ── Navigate away with leave/disband confirm ──────────────────────────

        private void OnNavigateAwayClicked(UnityEvent onConfirmed)
        {
            if (_roomState == null || !_roomState.IsInRoom)
            {
                onConfirmed?.Invoke();
                return;
            }

            bool isHost = IsLocalPlayerHost();
            GameModalWindow.Instance?.ShowConfirm(
                title:       isHost ? "Giải tán phòng?" : "Rời phòng?",
                desc:        isHost
                    ? "Tất cả người chơi sẽ bị đưa ra ngoài."
                    : "Bạn có chắc muốn rời khỏi phòng?",
                onConfirm: async () =>
                {
                    _hasAutoLeft = true;
                    if (isHost) await DisbandSilent();
                    else        await LeaveSilent();
                    onConfirmed?.Invoke();
                },
                confirmText: isHost ? "Giải tán" : "Rời phòng",
                cancelText:  "Hủy"
            );
        }

        // ══════════════════════════════════════════════════════════════════════
        // PARTY CHECK HELPER
        // ══════════════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task CheckPartyThenRun(
            Func<System.Threading.Tasks.Task> action)
        {
            if (_partyService == null) { await action(); return; }

            var partyResult = await _partyService.GetParty();
            bool inParty = partyResult.Success && partyResult.Data != null;
            if (!inParty) { await action(); return; }

            bool   isHost = partyResult.Data.hostUserId == (_sessionState?.UserId ?? -1L);
            string title  = isHost ? "Giải tán party?" : "Rời party?";
            string desc   = isHost
                ? "Bạn là host của party. Thao tác này sẽ giải tán party hiện tại."
                : "Bạn đang trong một party. Thao tác này sẽ rời khỏi party.";

            GameModalWindow.Instance?.ShowConfirm(
                title, desc,
                onConfirm: async () =>
                {
                    if (isHost) await _partyService.DisbandParty();
                    else        await _partyService.LeaveParty();
                    await action();
                },
                onCancel:    null,
                confirmText: isHost ? "Giải tán" : "Rời party",
                cancelText:  "Hủy"
            );
        }

        // ══════════════════════════════════════════════════════════════════════
        // IN-ROOM ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private async void OnReadyClicked()
        {
            if (_roomService == null || _roomState == null) return;
            bool currentReady = GetLocalPlayerReady();
            var result = await _roomService.SetReady(_roomState.RoomId, !currentReady);
            if (!result.Success) SetStatus($"Lỗi: {result.Message}");
        }

        private async void OnStartClicked()
        {
            if (!IsLocalPlayerHost()) return;
            SetStatus("Đang bắt đầu trận…");
            var result = await _roomService.StartGame(_roomState.RoomId);
            if (!result.Success)
            {
                SetStatus("");
                GameModalWindow.Instance?.ShowNotice("Không thể bắt đầu", result.Message ?? "Vui lòng thử lại.");
            }
        }

        private void OnLeaveOrDisbandClicked()
        {
            if (IsLocalPlayerHost())
            {
                GameModalWindow.Instance?.ShowConfirm(
                    "Giải tán phòng?",
                    "Tất cả người chơi sẽ bị đưa ra ngoài.",
                    onConfirm: async () =>
                    {
                        _hasAutoLeft = true;
                        if (_roomService != null && _roomState != null)
                            await _roomService.DisbandRoom(_roomState.RoomId);
                        _roomState?.ClearRoom();
                        ShowState(UIState.JoinCreate);
                    },
                    confirmText: "Giải tán",
                    cancelText:  "Hủy"
                );
            }
            else
            {
                GameModalWindow.Instance?.ShowConfirm(
                    "Rời phòng?",
                    "Bạn có chắc muốn rời khỏi phòng?",
                    onConfirm: async () =>
                    {
                        _hasAutoLeft = true;
                        if (_roomService != null && _roomState != null)
                            await _roomService.LeaveRoom(_roomState.RoomId);
                        _roomState?.ClearRoom();
                        ShowState(UIState.JoinCreate);
                    },
                    confirmText: "Rời phòng",
                    cancelText:  "Hủy"
                );
            }
        }

        // ── Auto leave/disband when navigating away ───────────────────────────

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
        }

        // ══════════════════════════════════════════════════════════════════════
        // SETTINGS PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void OnCopyCodeClicked()
        {
            if (_roomState == null) return;
            GUIUtility.systemCopyBuffer = _roomState.RoomCode;
            SetStatus("Đã sao chép mã phòng!");
        }

        private void OnModeDropdownChanged(int idx)
        {
            if (!IsLocalPlayerHost())
            {
                if (modeDropdown != null) modeDropdown.SetDropdownIndex(_currentModeIdx);
                return;
            }
            _currentModeIdx = idx;
            _pendingMode    = _modeModeKeys.Length > idx ? _modeModeKeys[idx] : "2v2";
            BuildMapList(_pendingMode, _pendingMapId);
            SyncMapDropdown(isInteractable: true);
            SetSettingsDirty(true);
        }

        private void OnMapDropdownChanged(int idx)
        {
            if (!IsLocalPlayerHost())
            {
                if (mapDropdown != null) mapDropdown.SetDropdownIndex(_currentMapIdx);
                return;
            }
            _currentMapIdx = Mathf.Clamp(idx, 0, Math.Max(0, _mapIds.Length - 1));
            _pendingMapId  = _mapIds.Length > _currentMapIdx ? _mapIds[_currentMapIdx] : GetFallbackMapId(_pendingMode);
            SetSettingsDirty(true);
        }

        // Michsky.UI.Shift.SwitchManager dùng OnEvents/OffEvents riêng —
        // bridge về 1 handler chung để tái sử dụng logic.
        private void OnPasswordSwitchOn()  => OnPasswordSwitchChanged(true);
        private void OnPasswordSwitchOff() => OnPasswordSwitchChanged(false);
        private void OnPublicSwitchOn()    => OnPublicSwitchChanged(true);
        private void OnPublicSwitchOff()   => OnPublicSwitchChanged(false);

        /// <summary>
        /// Password SwitchManager value changed — stage locked state (host only).
        /// Does NOT call API immediately.
        /// </summary>
        private void OnPasswordSwitchChanged(bool isOn)
        {
            if (!IsLocalPlayerHost())
            {
                // Revert switch về trạng thái cũ mà không invoke callback
                SetSwitchSilent(passwordSwitch, !isOn);
                return;
            }
            passwordInputContainer?.SetActive(isOn);
            _pendingIsLocked = isOn;
            if (!isOn) _pendingPasswordChanged = true;
            SetSettingsDirty(true);
        }

        private void OnPasswordInputChanged(string _)
        {
            if (!IsLocalPlayerHost()) return;
            _pendingPasswordChanged = true;
            SetSettingsDirty(true);
        }

        /// <summary>
        /// Public SwitchManager value changed — stage public/private (host only).
        /// Does NOT call API immediately.
        /// </summary>
        private void OnPublicSwitchChanged(bool isPublic)
        {
            if (!IsLocalPlayerHost())
            {
                SetSwitchSilent(publicSwitch, !isPublic);
                return;
            }
            _pendingIsPublic = isPublic;
            SetSettingsDirty(true);
        }

        private void OnSaveClicked()
        {
            _ = SaveSettingsAsync();
        }

        private void SetSettingsDirty(bool dirty)
        {
            _settingsDirty = dirty;
            if (btnSave != null) btnSave.interactable = dirty && IsLocalPlayerHost();
        }

        private void InitPendingSettings(RoomResponse room)
        {
            int modeIdx  = Array.IndexOf(_modeModeKeys, room.mode);
            _currentModeIdx  = modeIdx >= 0 ? modeIdx : 0;
            _pendingMode     = room.mode     ?? (_modeModeKeys.Length > 0 ? _modeModeKeys[0] : "2v2");
            _pendingMapId    = NormalizeMapId(_pendingMode, room.mapId);
            _pendingIsPublic = room.isPublic;
            _pendingIsLocked = room.isLocked;
            _pendingPasswordChanged = false;
            BuildMapList(_pendingMode, _pendingMapId);
            if (passwordInput != null) passwordInput.text = "";
            SetSettingsDirty(false);
        }

        private async System.Threading.Tasks.Task<bool> ApplySetting(UpdateRoomSettingsRequest req)
        {
            if (_roomService == null || _roomState == null) return false;
            var result = await _roomService.UpdateRoomSettings(_roomState.RoomId, req);
            if (!result.Success)
            {
                SetStatus($"Lỗi cài đặt: {result.Message}");
                return false;
            }
            _pendingPasswordChanged = false;
            return true;
        }

        private async System.Threading.Tasks.Task SaveSettingsAsync()
        {
            if (!IsLocalPlayerHost() || !_settingsDirty) return;

            string passwordPayload = GetPasswordPayloadForSave();
            if (_pendingIsLocked && string.IsNullOrEmpty(passwordPayload))
            {
                GameModalWindow.Instance?.ShowNotice(
                    "Thiếu mật khẩu",
                    "Phòng đang khóa nên bạn cần nhập mật khẩu trước khi lưu.");
                return;
            }

            bool success = await ApplySetting(new UpdateRoomSettingsRequest
            {
                mode     = _pendingMode,
                mapId    = _pendingMapId,
                isPublic = _pendingIsPublic,
                isLocked = _pendingIsLocked,
                password = passwordPayload
            });

            if (success)
                SetSettingsDirty(false);
        }

        // ─────────────────────────────────────────────────────
        // SWITCH HELPERS  (Michsky.UI.Shift.SwitchManager)
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Set trạng thái switch mà KHÔNG invoke OnEvents/OffEvents —
        /// tương đương Toggle.SetIsOnWithoutNotify().
        /// switchAnimator là public [HideInInspector] nên truy cập được.
        /// </summary>
        private static void SetSwitchSilent(Michsky.UI.Shift.SwitchManager sw, bool value)
        {
            if (sw == null) return;
            sw.isOn = value;
            if (sw.switchAnimator != null)
                sw.switchAnimator.Play(value ? "Switch On" : "Switch Off");
        }

        /// <summary>
        /// switchButton là private trong package này —
        /// lấy qua GetComponent thay vì truy cập trực tiếp.
        /// </summary>
        private static void SetSwitchInteractable(Michsky.UI.Shift.SwitchManager sw, bool interactable)
        {
            if (sw == null) return;
            var btn = sw.GetComponent<Button>();
            if (btn != null) btn.interactable = interactable;
        }

        // ───────────────────────────────────────────────────────
        // MODE LIST HELPERS
        // ───────────────────────────────────────────────────────

        private void BuildModeList()
        {
            var enabled  = GameModeConfig.GetEnabled();
            var keys     = new List<string>();
            var names    = new List<string>();
            foreach (var m in enabled)
            {
                if (!keys.Contains(m.modeKey))
                {
                    keys.Add(m.modeKey);
                    names.Add(m.displayName);
                }
            }

            if (keys.Count == 0)
            {
                keys.AddRange(new[] { "2v2", "4v4" });
                names.AddRange(new[] { "2 vs 2", "4 vs 4" });
                Debug.LogWarning("[CustomLobbyView] GameModeConfig not found — using built-in fallback modes.");
            }

            _modeModeKeys     = keys.ToArray();
            _modeDisplayNames = names.ToArray();
            _pendingMode      = _modeModeKeys[0];
        }

        private void BuildMapList(string modeKey, string preferredMapId)
        {
            MapEntry[] maps = MapConfig.GetByMode(modeKey);
            if (maps.Length == 0) maps = MapConfig.GetAvailable();

            _mapIds          = maps.Select(m => m.mapId).ToArray();
            _mapDisplayNames = maps.Select(m => m.displayName).ToArray();

            _currentMapIdx = Array.IndexOf(_mapIds, preferredMapId);
            if (_currentMapIdx < 0) _currentMapIdx = 0;

            _pendingMapId = _mapIds.Length > 0
                ? _mapIds[_currentMapIdx]
                : GetFallbackMapId(modeKey);
        }

        private void SyncMapDropdown(bool isInteractable)
        {
            if (mapDropdown == null) return;
            PopulateDropdown(mapDropdown, new List<string>(_mapDisplayNames), _currentMapIdx);
            mapDropdown.Interactable(isInteractable && _mapDisplayNames.Length > 0);
        }

        private static string GetFallbackMapId(string modeKey)
        {
            MapEntry[] maps = MapConfig.GetByMode(modeKey);
            if (maps.Length == 0) maps = MapConfig.GetAvailable();
            return maps.Length > 0 ? maps[0].mapId : "map_01";
        }

        private static string NormalizeMapId(string modeKey, string mapId)
        {
            MapEntry[] maps = MapConfig.GetByMode(modeKey);
            if (maps.Length == 0) maps = MapConfig.GetAvailable();

            if (!string.IsNullOrWhiteSpace(mapId) && maps.Any(m => m.mapId == mapId))
                return mapId;

            return maps.Length > 0 ? maps[0].mapId : "map_01";
        }

        private string GetPasswordPayloadForSave()
        {
            var room = _roomState?.CurrentRoom;
            if (!_pendingIsLocked) return "";

            bool roomAlreadyLocked = room?.isLocked ?? false;
            string enteredPassword = passwordInput?.text.Trim() ?? "";

            if (_pendingPasswordChanged || !roomAlreadyLocked)
                return enteredPassword;

            return null;
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
                // Move self to this empty slot immediately — no menu needed
                var result = await _roomService.ChangeTeam(_roomState.RoomId, team, slotIdx);
                if (!result.Success) SetStatus($"Lỗi: {result.Message}");
                return;
            }

            long targetId = sv.Player?.userId ?? 0L;
            if (targetId == (_sessionState?.UserId ?? 0L)) return; // own slot — ignore

            // Toggle: clicking the same occupied slot while the menu is open closes it.
            if (slotContextMenu != null && slotContextMenu.activeSelf && _contextMenuTargetSV == sv)
            {
                HideSlotContextMenu();
                return;
            }

            // Open shared context menu — actual action chosen by player
            _contextMenuTargetSV = sv;
            bool isHost = IsLocalPlayerHost();
            if (btn_CM_RequestSwap   != null) btn_CM_RequestSwap.gameObject.SetActive(!isHost);
            if (btn_CM_Kick          != null) btn_CM_Kick.gameObject.SetActive(isHost);
            if (btn_CM_TransferOwner != null) btn_CM_TransferOwner.gameObject.SetActive(isHost);
            if (slotContextMenu      != null) slotContextMenu.SetActive(true);
            _menuShownTime = Time.unscaledTime;
        }

        private void HideSlotContextMenu()
        {
            _contextMenuTargetSV = null;
            slotContextMenu?.SetActive(false);
        }

        private void OnCM_ViewProfile()
        {
            HideSlotContextMenu();
            // TODO: open player profile panel for _contextMenuTargetSV.Player.userId
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
                SetStatus($"Lỗi gửi yêu cầu: {swapResult.Message}");
                return;
            }

            _pendingSwapRequestId = swapResult.Data?.requestId ?? 0L;
            string targetName = target.Player.username ?? "Người chơi";
            GameModalWindow.Instance?.ShowCountdown(
                title:       "Yêu cầu đổi chỗ",
                desc:        $"Đang chờ <b>{targetName}</b> chấp nhận…",
                seconds:     15,
                onConfirm:   null,
                onExpire:    () => { _ = CancelSwapSilent(_pendingSwapRequestId); _pendingSwapRequestId = 0L; },
                showConfirm: false,
                cancelText:  "Hủy yêu cầu");
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

        private async void OnKickClicked(long targetUserId)
        {
            if (_roomService == null || _roomState == null) return;
            var result = await _roomService.KickPlayer(_roomState.RoomId, targetUserId);
            if (!result.Success) SetStatus($"Lỗi kick: {result.Message}");
        }

        private void OnTransferOwnerClicked(long targetUserId)
        {
            GameModalWindow.Instance?.ShowConfirm(
                "Chuyển quyền host?",
                "Bạn sẽ không còn là host của phòng này.",
                onConfirm: async () =>
                {
                    if (_roomService == null || _roomState == null) return;
                    var result = await _roomService.TransferOwner(_roomState.RoomId, targetUserId);
                    if (!result.Success) SetStatus($"Lỗi: {result.Message}");
                },
                confirmText: "Chuyển",
                cancelText:  "Hủy"
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
            _refreshPending  = false;
            _lastRefreshTime = now;
            RefreshRoomDisplayImmediate();
        }

        private IEnumerator DelayedRefresh()
        {
            yield return new WaitForSeconds(REFRESH_THROTTLE);
            _refreshPending  = false;
            _lastRefreshTime = Time.time;
            RefreshRoomDisplayImmediate();
        }

        private void RefreshRoomDisplayImmediate()
        {
            if (_roomState == null || !_roomState.IsInRoom) return;
            var room    = _roomState.CurrentRoom;
            bool isHost = IsLocalPlayerHost();
            bool waiting = room.status == Constants.ROOM_STATUS_WAITING;
            bool usePendingSettings = isHost && waiting && _settingsDirty;

            // ── Room code ──────────────────────────────────────────────────────
            if (roomCodeText != null) roomCodeText.text = room.roomCode;

            // ── Init pending settings once per new room ────────────────────────
            if (_lastStatus == null) InitPendingSettings(room);

            string activeMode   = usePendingSettings ? _pendingMode    : room.mode;
            string activeMapId  = usePendingSettings ? _pendingMapId   : NormalizeMapId(room.mode, room.mapId);
            bool activeIsPublic = usePendingSettings ? _pendingIsPublic : room.isPublic;
            bool activeIsLocked = usePendingSettings ? _pendingIsLocked : room.isLocked;

            int modeIdx = Array.IndexOf(_modeModeKeys, activeMode);
            if (modeIdx >= 0) _currentModeIdx = modeIdx;
            BuildMapList(activeMode, activeMapId);

            if (modeDropdown != null)
            {
                modeDropdown.SetDropdownIndex(_currentModeIdx);
                modeDropdown.Interactable(isHost && waiting);
            }
            SyncMapDropdown(isHost && waiting);

            // ── Password SwitchManager ─────────────────────────────────────────
            SetSwitchSilent(passwordSwitch, activeIsLocked);
            SetSwitchInteractable(passwordSwitch, isHost && waiting);
            passwordInputContainer?.SetActive(activeIsLocked && isHost && waiting);

            // ── Public SwitchManager ───────────────────────────────────────────
            SetSwitchSilent(publicSwitch, activeIsPublic);
            SetSwitchInteractable(publicSwitch, isHost && waiting);

            // ── Save button ────────────────────────────────────────────────────
            if (btnSave != null) btnSave.interactable = isHost && waiting && _settingsDirty;

            // ── Slot count ─────────────────────────────────────────────────────
            _maxSlotsPerTeam = GetSlotsForMode(room.mode);

            // ── Start button (host) ────────────────────────────────────────────
            bool full     = (room.players?.Count ?? 0) >= _maxSlotsPerTeam * 2;
            bool allReady = full && (room.players?.All(p => p.isReady) ?? false);

            if (btnStart != null)
            {
                btnStart.gameObject.SetActive(isHost && waiting);
                btnStart.interactable = isHost && waiting && allReady;
            }
            if (startButtonText != null)
                startButtonText.text = allReady ? "BẮT ĐẦU" : "Chờ tất cả sẵn sàng…";

            // ── Ready button (non-host) ────────────────────────────────────────
            if (btnReady != null)
            {
                btnReady.gameObject.SetActive(!isHost && waiting);
                bool myReady = GetLocalPlayerReady();
                if (readyButtonText != null) readyButtonText.text = myReady ? "Hủy sẵn sàng" : "Sẵn sàng";
            }

            // ── Leave / Disband button ─────────────────────────────────────────
            if (leaveOrDisbandText != null)
                leaveOrDisbandText.text = isHost ? "Giải tán phòng" : "Rời phòng";

            // ── Team slots ─────────────────────────────────────────────────────
            UpdatePlayerSlots(room.players);
        }

        private void UpdatePlayerSlots(List<RoomPlayerResponse> players)
        {
            foreach (var sv in _slotViews.Values)
                if (sv != null) Destroy(sv.gameObject);
            _slotViews.Clear();

            bool isHost = IsLocalPlayerHost();

            for (int team = 1; team <= 2; team++)
            {
                Transform container = team == 1 ? team1Container : team2Container;
                if (container == null || playerSlotPrefab == null) continue;

                for (int slotIdx = 0; slotIdx < _maxSlotsPerTeam; slotIdx++)
                {
                    var player = players?.FirstOrDefault(p => p.team == team && p.slot == slotIdx);
                    var go     = Instantiate(playerSlotPrefab, container);
                    var sv     = ComponentResolver.Find<PlayerSlotView>(go)
                                    .OnSelf().InChildren().Resolve();

                    if (sv == null) continue;

                    sv.SetSlot(team, slotIdx, player, isHost,
                               onSlotClicked: OnSlotClicked);

                    _slotViews[$"{team}_{slotIdx}"] = sv;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // EVENT BUS
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            if (GameEventBus.Instance == null) return;
            GameEventBus.Instance.OnRoomUpdated       += HandleRoomUpdated;
            GameEventBus.Instance.OnPlayerJoined      += HandlePlayerJoined;
            GameEventBus.Instance.OnPlayerLeft        += HandlePlayerLeft;
            GameEventBus.Instance.OnPlayerReady       += HandlePlayerReady;
            GameEventBus.Instance.OnTeamChanged       += HandleTeamChanged;
            GameEventBus.Instance.OnRoomStatusChanged += HandleRoomStatusChanged;
            GameEventBus.Instance.OnSwapRequest       += HandleSwapRequest;
            GameEventBus.Instance.OnSwapRequestStatus += HandleSwapRequestStatus;
            GameEventBus.Instance.OnForceLogout       += HandleForceLogout;
            GameEventBus.Instance.OnSessionExpired    += HandleSessionExpired;
            GameEventBus.Instance.OnAppFocusGained    += HandleAppFocusGained;
            GameEventBus.Instance.OnAppResumed        += HandleAppResumed;
        }

        private void UnsubscribeEvents()
        {
            if (GameEventBus.Instance == null) return;
            GameEventBus.Instance.OnRoomUpdated       -= HandleRoomUpdated;
            GameEventBus.Instance.OnPlayerJoined      -= HandlePlayerJoined;
            GameEventBus.Instance.OnPlayerLeft        -= HandlePlayerLeft;
            GameEventBus.Instance.OnPlayerReady       -= HandlePlayerReady;
            GameEventBus.Instance.OnTeamChanged       -= HandleTeamChanged;
            GameEventBus.Instance.OnRoomStatusChanged -= HandleRoomStatusChanged;
            GameEventBus.Instance.OnSwapRequest       -= HandleSwapRequest;
            GameEventBus.Instance.OnSwapRequestStatus -= HandleSwapRequestStatus;
            GameEventBus.Instance.OnForceLogout       -= HandleForceLogout;
            GameEventBus.Instance.OnSessionExpired    -= HandleSessionExpired;
            GameEventBus.Instance.OnAppFocusGained    -= HandleAppFocusGained;
            GameEventBus.Instance.OnAppResumed        -= HandleAppResumed;
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
            if (evt.room != null) _roomState?.SetRoom(evt.room);
            RefreshRoomDisplay();
        }

        private void HandleRoomStatusChanged(GameWebSocketService.RoomStatusChangedEvent evt)
        {
            if (evt.room != null) _roomState?.SetRoom(evt.room);

            if (evt.newStatus == Constants.ROOM_STATUS_IN_GAME
                && _lastStatus  != Constants.ROOM_STATUS_IN_GAME)
            {
                _hasAutoLeft = true;
                MatchLoadingOverlay.Instance?.Show();
            }
            else if (evt.newStatus == Constants.ROOM_STATUS_CLOSED
                     && _lastStatus != Constants.ROOM_STATUS_CLOSED)
            {
                _hasAutoLeft = true;
                _roomState?.ClearRoom();
                ShowState(UIState.JoinCreate);
                GameModalWindow.Instance?.ShowNotice("Phòng đã đóng", "Host đã giải tán phòng.");
            }

            _lastStatus = evt.newStatus;
            RefreshRoomDisplay();
        }

        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt)
        {
            long myId = _sessionState?.UserId ?? 0L;
            if (evt.targetUserId != myId) return;

            long   requestId     = evt.requestId;
            string requesterName = evt.fromUsername ?? "Người chơi";

            GameModalWindow.Instance?.ShowCountdown(
                title:       "Yêu cầu đổi chỗ",
                desc:        $"<b>{requesterName}</b> muốn đổi chỗ với bạn.",
                seconds:     15,
                onConfirm:   () => _ = AcceptSwapSilent(requestId),
                onExpire:    () => _ = RejectSwapSilent(requestId),
                showConfirm: true,
                confirmText: "Chấp nhận",
                cancelText:  "Từ chối"
            );
        }

        private void HandleSwapRequestStatus(GameWebSocketService.SwapRequestStatusEvent evt)
        {
            // Only act on the modal if this event matches OUR pending request.
            // The old isAnyAccepted flag would silently close an unrelated modal whenever
            // any swap was accepted in the room (e.g. two other players swapping).
            // requestId == 0L is a wildcard from synthesized swap_accepted WS events
            // (GameWebSocketService emits requestId=0 when backend sends swap_accepted
            // without a requestId field). Treat 0L as "any pending swap of ours".
            bool isOurRequest = (evt.requestId == _pendingSwapRequestId || evt.requestId == 0L)
                                && _pendingSwapRequestId != 0L;

            if (isOurRequest)
            {
                _pendingSwapRequestId = 0L;
                GameModalWindow.Instance?.Close();   // silent — swap resolved

                if (evt.status == "REJECTED" || evt.status == "CANCELLED")
                    GameModalWindow.Instance?.ShowNotice(
                        "Yêu cầu bị từ chối", "Người chơi đã từ chối đổi chỗ.");
            }

            if (evt.room != null) _roomState?.SetRoom(evt.room);
            RefreshRoomDisplay();
        }

        private void HandleForceLogout()
        {
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            GameModalWindow.Instance?.ShowNotice(
                "Đăng xuất bắt buộc",
                "Tài khoản của bạn đã đăng nhập ở nơi khác.",
                closeText: "OK",
                onClose:   LoginView.Logout
            );
        }

        private void HandleSessionExpired()
        {
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            GameModalWindow.Instance?.ShowNotice(
                "Phiên hết hạn",
                "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                closeText: "OK",
                onClose:   LoginView.Logout
            );
        }

        private void HandleAppFocusGained() => RefreshRoomDisplay();
        private void HandleAppResumed()     => RefreshRoomDisplay();

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

        public void RefreshPlayerList() => RefreshRoomDisplay();
    }
}