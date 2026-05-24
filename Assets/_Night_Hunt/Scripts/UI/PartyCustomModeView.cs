using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Data;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.Services.Party;
using NightHunt.Services.Room;
using NightHunt.State;
using NightHunt.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;

namespace NightHunt.UI
{
    /// <summary>
    /// Party Custom Mode View — Panel in 01_Home scene.
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
    public sealed class PartyCustomModeView : MonoBehaviour, INavigableView
    {
        // ══════════════════════════════════════════════════════════════════════
        // NESTED — navigation button + confirmed-action pair
        // ══════════════════════════════════════════════════════════════════════

        [System.Serializable]
        public class NavigationButtonEntry
        {
            public Button button;
            [Tooltip("Panel to open after any required leave/disband confirmation completes.")]
            public PanelType targetPanel = PanelType.Home;
        }

        public sealed class NavigationPayload
        {
            public NavigationPayload(string modeKey, string mapId)
            {
                ModeKey = modeKey;
                MapId = mapId;
            }

            public string ModeKey { get; }
            public string MapId { get; }
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
        [Tooltip("Mỗi entry: 1 Button + target panel. Nếu đang trong room thì phải xác nhận leave/disband trước khi UINavigator chuyển panel.")]
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

        // Swap request tracking
        private long _pendingSwapRequestId = 0L;
        private long _pendingSwapRoomId = 0L;
        private long _incomingSwapRequestId = 0L;
        private long _incomingSwapRoomId = 0L;

        /// <summary>
        /// Set to true BEFORE showing any confirm modal that would eventually leave/disband.
        /// Prevents AutoLeaveOrDisband() from double-firing when the panel fades out after
        /// the user confirms. Restored to false if the user cancels the modal.
        /// </summary>
        private bool _hasAutoLeft = false;

        private bool _codeInputVisible = false;
        private bool _joinCreateRequestInFlight = false;

        // Shared slot context menu
        private PlayerSlotView _contextMenuTargetSV = null;
        private Button _slotContextBackdrop = null;
        private RectTransform _slotContextPanel = null;

        // ══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            ResolveReferences(createFallback: true);

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
                        captured.button.onClick.AddListener(() => OnNavigateAwayClicked(captured.targetPanel));
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
            EnsureSlotContextMenuBackdrop();
            UIContextMenuRegistry.Register(this, HideSlotContextMenu);
            slotContextMenu?.SetActive(false);
            _slotContextBackdrop?.gameObject.SetActive(false);

            codeInputContainer?.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences(createFallback: false);
        }
#endif

        private void Reset()
        {
            ResolveReferences(createFallback: false);
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

            bool inRoom = _roomState != null && _roomState.IsInRoom;
            ShowState(inRoom ? UIState.InRoom : UIState.JoinCreate);
            if (inRoom) RefreshRoomDisplay();
        }

        private void Update()
        {
            // Slot context menu outside-click is handled by a fullscreen backdrop.
        }

        private void OnEnable() { /* CanvasGroup navigation does NOT call OnEnable. OnShow() handles init. */ }
        private void OnDisable() { HideSlotContextMenu(); }

        // ─────────────────────────────────────────────
        // INavigableView — called by UINavigator (wire in Inspector)
        // ─────────────────────────────────────────────

        public bool CanLeave(NavigationContext context)
        {
            if (context.BypassCanLeave || context.To == PanelType.PartyCustomMode || context.To == PanelType.Lobby)
                return true;
            if (context.To == PanelType.Settings)
                return true;
            if (_hasAutoLeft || _roomState == null || !_roomState.IsInRoom)
                return true;
            if (_roomState.Status == Constants.ROOM_STATUS_IN_GAME)
                return true;

            PromptLeaveBeforeNavigation(context);
            return false;
        }

        public void OnShow() => _ = OnShowAsync(new NavigationContext(
            UINavigator.Instance != null ? UINavigator.Instance.CurrentPanel : PanelType.None,
            PanelType.PartyCustomMode,
            false));

        public async Task OnShowAsync(NavigationContext context)
        {
            ResolveReferences(createFallback: true);

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

            GameModeConfig.OnConfigLoaded -= HandleConfigLoaded;
            MapConfig.OnConfigLoaded -= HandleConfigLoaded;
            GameModeConfig.OnConfigLoaded += HandleConfigLoaded;
            MapConfig.OnConfigLoaded += HandleConfigLoaded;

            BuildModeList();
            ApplyNavigationPayload(context.Payload as NavigationPayload);
            _updatingDropdown = true;
            if (modeDropdown != null)
                PopulateDropdown(modeDropdown, new List<string>(_modeDisplayNames), _currentModeIdx);
            BuildMapList(_pendingMode, _pendingMapId);
            if (mapDropdown != null)
                PopulateDropdown(mapDropdown, new List<string>(_mapDisplayNames), _currentMapIdx);
            _updatingDropdown = false;

            NLog($"OnShow before recover pendingMode={_pendingMode} pendingMap={_pendingMapId ?? "null"} local={DescribeLocalRoom()}");
            await TryRecoverActiveRoomFromServer("PartyCustomMode.OnShow", updateUi: false);

            bool inRoom = _roomState != null && _roomState.IsInRoom;
            NLog($"OnShow after recover inRoom={inRoom} local={DescribeLocalRoom()}");
            ShowState(inRoom ? UIState.InRoom : UIState.JoinCreate);

            if (inRoom)
            {
                // Refresh room data from server to ensure we have fresh member list/settings
                _ = RefreshRoomDataAndDisplayAsync();
            }

            return;
        }

        private async Task RefreshRoomDataAndDisplayAsync()
        {
            if (_roomService == null || _roomState == null || !_roomState.IsInRoom) return;

            var result = await _roomService.GetRoom(_roomState.RoomId);
            if (result.Success)
            {
                RefreshRoomDisplay();
            }
            else
            {
                Debug.LogWarning($"[PartyCustomModeView] Failed to refresh room data: {result.Message}");
                // If room no longer exists, we should probably leave
                if (result.Message != null && (result.Message.Contains("not found") || result.Message.Contains("404")))
                {
                    _roomState.ClearRoom();
                    ShowState(UIState.JoinCreate);
                }
            }
        }

        private async Task<bool> TryRecoverActiveRoomFromServer(string source, bool updateUi)
        {
            if (_roomState == null)
                _roomState = RoomState.Instance;

            if (_roomState != null && _roomState.IsInRoom)
            {
                NLog($"RecoverActiveRoom skipped from {source}: local already in room. local={DescribeLocalRoom()} updateUi={updateUi}");
                if (updateUi)
                {
                    ResetCodeInput();
                    ShowState(UIState.InRoom);
                    RefreshRoomDisplay();
                }
                return true;
            }

            // No local room state — do NOT call the reconnect endpoint here.
            // The authoritative room-recovery check is done in HomeView.CheckAndShowReconnectPopup
            // right after login/app resume.  If that check cleared (or never set) room state,
            // calling Reconnect() here would send roomId=0 and produce a server 500 error.
            NLog($"RecoverActiveRoom skipped from {source}: not in room, nothing to recover. local={DescribeLocalRoom()}");
            return false;
        }

        private void HandleConfigLoaded()
        {
            ResolveReferences(createFallback: true);

            string previousMode = _pendingMode;
            string previousMapId = _pendingMapId;

            BuildModeList();
            int previousModeIdx = FindModeIndex(previousMode);
            if (previousModeIdx >= 0)
            {
                _currentModeIdx = previousModeIdx;
                _pendingMode = previousMode;
            }

            BuildMapList(_pendingMode, previousMapId);
            _updatingDropdown = true;
            if (modeDropdown != null)
                PopulateDropdown(modeDropdown, new List<string>(_modeDisplayNames), _currentModeIdx);
            if (mapDropdown != null)
                PopulateDropdown(mapDropdown, new List<string>(_mapDisplayNames), _currentMapIdx);
            _updatingDropdown = false;

            RefreshRoomDisplay();
        }

        private void ApplyNavigationPayload(NavigationPayload payload)
        {
            _currentModeIdx = FindModeIndex(_pendingMode);
            if (_currentModeIdx < 0)
                _currentModeIdx = _modeModeKeys.Length > 0 ? 0 : -1;
            if (_currentModeIdx >= 0)
                _pendingMode = _modeModeKeys[_currentModeIdx];

            if (payload == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(payload.ModeKey))
            {
                int requestedModeIdx = FindModeIndex(payload.ModeKey);
                if (requestedModeIdx >= 0)
                {
                    _currentModeIdx = requestedModeIdx;
                    _pendingMode = _modeModeKeys[requestedModeIdx];
                }
                else
                {
                    NLog($"Navigation payload mode ignored because it is not enabled: mode={payload.ModeKey}");
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.MapId))
                _pendingMapId = payload.MapId;
        }

        public void OnHide() => _ = OnHideAsync(new NavigationContext(PanelType.PartyCustomMode, PanelType.None, false));

        public Task OnHideAsync(NavigationContext context)
        {
            GameModeConfig.OnConfigLoaded -= HandleConfigLoaded;
            MapConfig.OnConfigLoaded -= HandleConfigLoaded;
            HideSlotContextMenu();
            UnsubscribeEvents();
            // Safety net: fire-and-forget leave/disband when navigation bypasses CanLeave()
            // (e.g. GoForce, force-logout, bypassCanLeave=true without prior leave).
            // The normal confirm path (CanLeave → PromptLeaveBeforeNavigation) sets
            // _hasAutoLeft=true before navigating, so AutoLeaveOrDisband returns early there.
            // Settings is excluded — navigating to Settings preserves the room intentionally.
            if (context.To != PanelType.Settings)
                AutoLeaveOrDisband();
            return Task.CompletedTask;
        }

        private void OnDestroy()
        {
            UIContextMenuRegistry.Unregister(this);
            UnsubscribeEvents();
        }

        // ══════════════════════════════════════════════════════════════════════
        // PANEL STATE
        // ══════════════════════════════════════════════════════════════════════

        private enum UIState { JoinCreate, InRoom }

        private void ShowState(UIState state)
        {
            NLog($"ShowState({state})");
            // NOTE: Do NOT call joinCreatePanel.SetActive() here.
            // joinCreatePanel IS the UINavigator route rootObject for PartyCustomMode.
            // UINavigator owns its active state (ApplyRouteRootState / ApplyActiveRouteVisualState).
            // Calling SetActive(false) on it resets the Animator (m_KeepAnimatorStateOnDisable=0),
            // causing a 1-frame invisible flash when UINavigator re-enables it after OnShowAsync.
            // Instead, control only the child sub-panels and individual buttons below.
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
                // Re-enable mode/map dropdowns — they are disabled by ShowState(InRoom).
                // Must be done BEFORE ClearRoomUI() repopulates them so Populate sees a live control.
                if (modeDropdown != null) modeDropdown.Interactable(true);
                if (mapDropdown != null) mapDropdown.Interactable(true);
                ResetCodeInput();
                SetJoinCreateInteractable(!_joinCreateRequestInFlight);
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

        private void SetJoinCreateInteractable(bool interactable)
        {
            if (btnCreateRoom != null) btnCreateRoom.interactable = interactable;
            if (btnQuickJoin != null) btnQuickJoin.interactable = interactable;
            if (btn_CodeAction != null) btn_CodeAction.interactable = interactable;
        }

        private void RunJoinCreateAction(Func<Task> action)
        {
            if (_joinCreateRequestInFlight)
            {
                NLog($"RunJoinCreateAction ignored: request already in flight. local={DescribeLocalRoom()}");
                return;
            }

            _joinCreateRequestInFlight = true;
            NLog($"RunJoinCreateAction start pendingMode={_pendingMode} pendingMap={_pendingMapId ?? "null"} local={DescribeLocalRoom()}");
            SetJoinCreateInteractable(false);
            _ = RunJoinCreateActionAsync(action);
        }

        private async Task RunJoinCreateActionAsync(Func<Task> action)
        {
            try
            {
                await CheckPartyThenRun(action);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("");
                GameModalWindow.Instance?.ShowNotice("Request Failed", "Please try again.");
            }
            finally
            {
                _joinCreateRequestInFlight = false;
                NLog($"RunJoinCreateAction finish local={DescribeLocalRoom()}");
                if (_roomState == null || !_roomState.IsInRoom)
                    SetJoinCreateInteractable(true);
            }
        }

        private void OnCreateRoomClicked() =>
            RunJoinCreateAction(async () =>
            {
                NLog($"CreateRoom click mode={_pendingMode} map={_pendingMapId ?? "null"} localBefore={DescribeLocalRoom()}");
                SetStatus("Creating room...");
                var result = await _roomService.CreateRoom(
                    _pendingMode, allowFill: false, isPublic: true, isLocked: false, password: null, mapId: _pendingMapId);
                NLog($"CreateRoom result success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}' dataRoomId={result.Data?.roomId ?? 0} localAfterApi={DescribeLocalRoom()}");

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
                    if (IsAlreadyInActiveRoom(result) &&
                        await TryRecoverActiveRoomFromServer("CreateRoom conflict", updateUi: true))
                    {
                        return;
                    }

                    SetStatus("");
                    GameModalWindow.Instance?.ShowNotice(
                        "Create Room Failed", result.Message ?? "Please try again.");
                }
            });

        private void OnQuickJoinClicked() =>
            RunJoinCreateAction(async () =>
            {
                NLog($"QuickPlay click mode={_pendingMode} map={_pendingMapId ?? "null"} localBefore={DescribeLocalRoom()}");
                SetStatus("Finding room...");
                var result = await _roomService.QuickPlay(_pendingMode, false, _pendingMapId);
                NLog($"QuickPlay result success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}' dataRoomId={result.Data?.roomId ?? 0} localAfterApi={DescribeLocalRoom()}");

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
                    if (IsAlreadyInActiveRoom(result) &&
                        await TryRecoverActiveRoomFromServer("QuickJoin conflict", updateUi: true))
                    {
                        return;
                    }

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

            RunJoinCreateAction(() => TryJoinByCode(code));
        }

        /// <summary>Attempt join; if password needed → show input modal.</summary>
        private async System.Threading.Tasks.Task TryJoinByCode(string code)
        {
            NLog($"JoinByCode click code={code} localBefore={DescribeLocalRoom()}");
            SetStatus($"Joining {code}...");
            var result = await _roomService.JoinByCode(code, "");

            NLog($"JoinByCode result success={result.Success} errorCode={result.ErrorCode ?? "null"} msg='{result.Message ?? "null"}' dataRoomId={result.Data?.roomId ?? 0} localAfterApi={DescribeLocalRoom()}");
            if (result.Success && result.Data != null)
            {
                ResetCodeInput();
                ShowState(UIState.InRoom);
                RefreshRoomDisplay();
                SetStatus("Joined room. Waiting for host to start...");
                return;
            }

            bool needPass =
                string.Equals(result.ErrorCode, ErrorCodes.ROOM_LOCKED, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result.ErrorCode, ErrorCodes.ROOM_PASSWORD_INVALID, StringComparison.OrdinalIgnoreCase) ||
                (result.Message != null && (
                    result.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    result.Message.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0));

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
                if (IsAlreadyInActiveRoom(result) &&
                    await TryRecoverActiveRoomFromServer("JoinByCode conflict", updateUi: true))
                {
                    return;
                }

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

        private static bool IsAlreadyInActiveRoom(ApiResult<RoomResponse> result)
        {
            if (result == null) return false;
            if (string.Equals(result.ErrorCode, ErrorCodes.ROOM_ALREADY_IN_ROOM, StringComparison.OrdinalIgnoreCase))
                return true;

            return result.Message != null &&
                   result.Message.IndexOf("already in an active room", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnNavigateAwayClicked(PanelType targetPanel)
        {
            NavigateAfterConfirmation(targetPanel);
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
            if (_partyService == null)
            {
                NLog($"CheckPartyThenRun: PartyService null, continue to room check. local={DescribeLocalRoom()}");
                await CheckRoomThenRun(action);
                return;
            }

            var partyResult = await _partyService.GetParty();
            bool inParty = partyResult.Success && partyResult.Data != null;
            NLog(
                $"CheckPartyThenRun: getParty success={partyResult.Success} errorCode={partyResult.ErrorCode ?? "null"} " +
                $"msg='{partyResult.Message ?? "null"}' inParty={inParty} party={DescribeParty(partyResult.Data)} local={DescribeLocalRoom()}");
            if (!inParty)
            {
                await CheckRoomThenRun(action);
                return;
            }

            bool isHost = partyResult.Data.hostUserId == (_sessionState?.UserId ?? -1L);
            string partyStatus = partyResult.Data.partyStatus ?? "";

            // Party in ranked matchmaking queue — block entirely, must cancel queue first.
            if (partyStatus == "IN_QUEUE")
            {
                NLog($"CreateRoom blocked: party is IN_QUEUE for ranked matchmaking. party={DescribeParty(partyResult.Data)} local={DescribeLocalRoom()}");
                GameModalWindow.Instance?.ShowNotice(
                    "Cannot Create Custom Room",
                    "Your party is currently in a matchmaking queue. Cancel the queue first before creating a party custom mode.");
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
                    NLog($"Party leave/disband confirmed before custom room. isHost={isHost} party={DescribeParty(partyResult.Data)}");

                    var r = isHost
                        ? await _partyService.DisbandParty()
                        : await _partyService.LeaveParty();
                    NLog($"Party leave/disband result success={r.Success} errorCode={r.ErrorCode ?? "null"} msg='{r.Message ?? "null"}'");

                    if (!r.Success)
                    {
                        SetStatus("");
                        GameModalWindow.Instance?.ShowNotice(
                            "Error",
                            r.Message ?? "Could not leave party. Please try again.");
                        return;
                    }

                    SetStatus("");
                    await CheckRoomThenRun(action);
                },
                onCancel: null,
                confirmText: isHost ? "Disband" : "Leave Party",
                cancelText: "Cancel"
            );
        }

        // ══════════════════════════════════════════════════════════════════════
        // IN-ROOM ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task CheckRoomThenRun(Func<System.Threading.Tasks.Task> action)
        {
            if (action == null)
                return;

            if (_roomState == null)
                _roomState = RoomState.Instance;

            if (_roomState == null || !_roomState.IsInRoom)
            {
                NLog($"CheckRoomThenRun: no local active room, running action. local={DescribeLocalRoom()}");
                await action();
                return;
            }

            if (_roomState.Status == Constants.ROOM_STATUS_IN_GAME)
            {
                NLog($"CheckRoomThenRun blocked: local room is in game. local={DescribeLocalRoom()}");
                GameModalWindow.Instance?.ShowNotice(
                    "Room Active",
                    "You are already in an active match room.");
                return;
            }

            bool isHost = IsLocalPlayerHost();
            async System.Threading.Tasks.Task LeaveCurrentRoomAndRun()
            {
                SetStatus(isHost ? "Disbanding previous room..." : "Leaving previous room...");
                _hasAutoLeft = true;
                NLog($"CheckRoomThenRun confirmed leave current room. isHost={isHost} localBefore={DescribeLocalRoom()}");
                if (isHost) await DisbandSilent();
                else await LeaveSilent();
                _hasAutoLeft = false;
                SetStatus("");
                NLog($"CheckRoomThenRun current room cleared, running action. localAfterLeave={DescribeLocalRoom()}");
                await action();
            }

            if (GameModalWindow.Instance == null)
            {
                NLog($"CheckRoomThenRun: modal missing, auto leave/disband. isHost={isHost} local={DescribeLocalRoom()}");
                await LeaveCurrentRoomAndRun();
                return;
            }

            NLog($"CheckRoomThenRun: local active room requires confirm before action. isHost={isHost} local={DescribeLocalRoom()}");
            GameModalWindow.Instance.ShowConfirm(
                isHost ? "Disband Current Room?" : "Leave Current Room?",
                isHost
                    ? "You are already hosting a custom room. Disband it before creating or joining another room?"
                    : "You are already in a custom room. Leave it before creating or joining another room?",
                onConfirm: async () => await LeaveCurrentRoomAndRun(),
                onCancel: () => SetStatus(""),
                confirmText: isHost ? "Disband" : "Leave",
                cancelText: "Cancel");
        }

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

        private void PromptLeaveBeforeNavigation(NavigationContext context)
        {
            bool isHost = IsLocalPlayerHost();
            GameModalWindow.Instance?.ShowConfirm(
                title: isHost ? "Disband Room?" : "Leave Room?",
                desc: isHost
                    ? "All players will be removed from the room."
                    : "Are you sure you want to leave?",
                onConfirm: async () =>
                {
                    _hasAutoLeft = true;
                    if (isHost) await DisbandSilent();
                    else await LeaveSilent();

                    if (UINavigator.Instance != null)
                        await UINavigator.Instance.ShowPanelAsync(context.To, context.ForceInstant, bypassCanLeave: true);
                },
                onCancel: () => _hasAutoLeft = false,
                confirmText: isHost ? "Disband" : "Leave",
                cancelText: "Cancel");
        }

        private static void NavigateAfterConfirmation(PanelType targetPanel)
        {
            if (UINavigator.Instance != null)
            {
                UINavigator.Instance.ShowPanel(targetPanel, "PartyCustomModeNavigation");
                return;
            }

            Debug.LogWarning($"[PartyCustomModeView] Cannot navigate to {targetPanel}: UINavigator is missing.");
        }

        private async System.Threading.Tasks.Task DisbandSilent()
        {
            try
            {
                NLog($"DisbandSilent start local={DescribeLocalRoom()}");
                if (_roomService != null && _roomState != null)
                    await _roomService.DisbandRoom(_roomState.RoomId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FLOW][CUSTOM_LOBBY] DisbandSilent exception: {ex.Message}");
            }
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
            NLog($"DisbandSilent done local={DescribeLocalRoom()}");
        }

        private async System.Threading.Tasks.Task LeaveSilent()
        {
            try
            {
                NLog($"LeaveSilent start local={DescribeLocalRoom()}");
                if (_roomService != null && _roomState != null)
                    await _roomService.LeaveRoom(_roomState.RoomId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FLOW][CUSTOM_LOBBY] LeaveSilent exception: {ex.Message}");
            }
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
            NLog($"LeaveSilent done local={DescribeLocalRoom()}");
        }

        private void ClearRoomUI()
        {
            ResetCodeInput();
            if (roomCodeText != null) roomCodeText.text = "";

            // ── FIX: Remove slot views + toàn bộ children còn sót trong containers
            foreach (var sv in _slotViews.Values)
                if (sv != null) Destroy(sv.gameObject);
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
                Destroy(container.GetChild(i).gameObject);
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
                ShiftUIBridge.SetDropdownIndexSilently(modeDropdown, _currentModeIdx);
                return;
            }
            _currentModeIdx = idx;
            _pendingMode = _modeModeKeys.Length > idx ? _modeModeKeys[idx] : "2v2";
            NLog($"ModeChanged idx={idx} key={_pendingMode} pendingMapBefore={_pendingMapId ?? "null"} local={DescribeLocalRoom()}");
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
                ShiftUIBridge.SetDropdownIndexSilently(mapDropdown, _currentMapIdx);
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
            {
                SetSettingsDirty(false);
                RefreshRoomDisplay();
            }
        }

        // ── Mode List Helpers ─────────────────────────────────────────────────

        private void BuildModeList()
        {
            var enabled = GameModeConfig.GetEnabled();
            var keys = new List<string>();
            var names = new List<string>();
            var seen = new HashSet<string>();
            foreach (var m in enabled)
            {
                if (string.IsNullOrWhiteSpace(m.modeKey) || !seen.Add(m.modeKey))
                    continue;
                keys.Add(m.modeKey);
                names.Add(m.displayName);
            }

            if (keys.Count == 0)
                Debug.LogWarning("[PartyCustomModeView] GameModeConfig not yet loaded — waiting for GameConfigService.FetchAsync(). This is normal on first Awake.");

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
            if (maps.Length == 0)
            {
                maps = MapConfig.GetAvailable();
                Debug.LogWarning($"[PartyCustomModeView] No mode-specific maps for mode={modeKey}. Showing available maps.");
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

            ShowSlotContextMenu(sv);
        }

        private void ShowSlotContextMenu(PlayerSlotView slotView)
        {
            if (slotView == null || slotContextMenu == null)
                return;

            EnsureSlotContextMenuBackdrop();
            UIContextMenuRegistry.CloseAllExcept(this);

            _contextMenuTargetSV = slotView;
            bool isHost = IsLocalPlayerHost();
            if (btn_CM_RequestSwap != null) btn_CM_RequestSwap.gameObject.SetActive(!isHost);
            if (btn_CM_Kick != null) btn_CM_Kick.gameObject.SetActive(isHost);
            if (btn_CM_TransferOwner != null) btn_CM_TransferOwner.gameObject.SetActive(isHost);

            if (_slotContextBackdrop != null)
            {
                _slotContextBackdrop.transform.SetAsLastSibling();
                _slotContextBackdrop.gameObject.SetActive(true);
            }

            slotContextMenu.transform.SetAsLastSibling();
            slotContextMenu.SetActive(true);

            if (_slotContextPanel != null)
            {
                var anchor = slotView.transform as RectTransform;
                UIContextMenuPositioner.PlaceNearTopLeft(_slotContextPanel, anchor, new Vector2(8f, -4f));
            }
        }

        private void HideSlotContextMenu()
        {
            _contextMenuTargetSV = null;
            slotContextMenu?.SetActive(false);
            _slotContextBackdrop?.gameObject.SetActive(false);
        }

        private void EnsureSlotContextMenuBackdrop()
        {
            if (slotContextMenu == null)
                return;

            EnsureSlotContextMenuOverlayParent();
            _slotContextPanel = slotContextMenu.transform as RectTransform;
            if (_slotContextBackdrop != null)
                return;

            Transform parent = slotContextMenu.transform.parent != null
                ? slotContextMenu.transform.parent
                : transform;

            var backdropGo = new GameObject("Runtime Slot Context Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.layer = slotContextMenu.layer == 0 ? 5 : slotContextMenu.layer;
            backdropGo.transform.SetParent(parent, false);

            var backdropRect = backdropGo.GetComponent<RectTransform>();
            UIContextMenuPositioner.PrepareFullscreenBackdrop(backdropRect);

            var image = backdropGo.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            _slotContextBackdrop = backdropGo.GetComponent<Button>();
            _slotContextBackdrop.transition = Selectable.Transition.None;
            _slotContextBackdrop.onClick.AddListener(HideSlotContextMenu);
            backdropGo.SetActive(false);
        }

        private void EnsureSlotContextMenuOverlayParent()
        {
            if (slotContextMenu == null || slotContextMenu.transform.parent == transform)
                return;

            slotContextMenu.transform.SetParent(transform, false);
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

            long roomId = ResolveActiveRoomId();
            if (roomId <= 0L)
            {
                SetStatus("Swap request failed: no active room.");
                return;
            }

            var swapResult = await _roomService.RequestSwap(
                roomId, target.Player.userId, target.Team, target.Slot);
            if (!swapResult.Success)
            {
                SetStatus($"Swap request failed: {swapResult.Message}");
                return;
            }

            long requestId = swapResult.Data?.requestId ?? 0L;
            _pendingSwapRequestId = requestId;
            _pendingSwapRoomId = roomId;
            string targetName = target.Player.username ?? "Player";
            NLog($"SwapRequest sent to {targetName} requestId={requestId} roomId={roomId}");
            GameModalWindow.Instance?.ShowCountdown(
                title: "Swap Request",
                desc: $"Waiting for <b>{targetName}</b> to accept...",
                seconds: 15,
                onConfirm: null,
                onExpire: () => { _ = CancelSwapSilent(roomId, requestId); ClearPendingSwapIfMatches(requestId); },
                showConfirm: false,
                cancelText: "Cancel Request",
                invitationId: requestId);
        }

        private void OnCM_Kick()
        {
            long uid = _contextMenuTargetSV?.Player?.userId ?? 0L;
            string uname = _contextMenuTargetSV?.Player?.username;
            HideSlotContextMenu();
            if (uid != 0L) OnKickWithConfirm(uid, uname);
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

        private async System.Threading.Tasks.Task CancelSwapSilent(long roomId, long requestId)
        {
            if (_roomService == null || roomId <= 0L || requestId == 0L) return;
            try { await _roomService.CancelSwapRequest(roomId, requestId); }
            catch (Exception ex) { Debug.LogWarning($"[PartyCustomModeView] CancelSwap failed: roomId={roomId} requestId={requestId} error={ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DISPLAY / REFRESH
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshRoomDisplay()
        {
            if (this == null || gameObject == null) return;

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
            if (this == null || gameObject == null) yield break;

            _refreshPending = false;
            _lastRefreshTime = Time.time;
            RefreshRoomDisplayImmediate();
        }

        private void RefreshRoomDisplayImmediate()
        {
            if (this == null || gameObject == null) return;
            if (_roomState == null || !_roomState.IsInRoom) return;
            var room = _roomState.CurrentRoom;
            bool isHost = IsLocalPlayerHost();
            bool waiting = room.status == Constants.ROOM_STATUS_WAITING;
            bool usePendingSettings = isHost && waiting && _settingsDirty;
            EnsureOwnerReadyForDisplay(room);

            // ── Room code ──────────────────────────────────────────────────────
            if (roomCodeText != null) roomCodeText.text = room.roomCode;

            // ── Init pending settings once per new room ────────────────────────
            if (_lastStatus == null) InitPendingSettings(room);

            string activeMode = usePendingSettings ? _pendingMode : room.mode;
            string activeMapId = usePendingSettings ? _pendingMapId : NormalizeMapId(room.mode, room.mapId);

            // ... rest of method remains unchanged but I'll add the closing braces and checks carefully
            int modeIdx = FindModeIndex(activeMode);
            if (modeIdx >= 0) _currentModeIdx = modeIdx;
            BuildMapList(activeMode, activeMapId);

            _updatingDropdown = true;
            if (modeDropdown != null)
            {
                PopulateDropdown(modeDropdown, new List<string>(_modeDisplayNames), _currentModeIdx);
                modeDropdown.Interactable(isHost && waiting);
            }
            SyncMapDropdown(isHost && waiting);
            _updatingDropdown = false;

            if (btnSave != null) btnSave.interactable = isHost && waiting && _settingsDirty;

            _maxSlotsPerTeam = GetSlotsForMode(room.mode);

            int currentPlayers = room.players?.Count ?? 0;
            bool hasPlayers = currentPlayers > 0;
            bool allReady = hasPlayers && (room.players?.All(p => IsReadyForStart(room, p)) ?? false);

            if (btnStart != null)
            {
                btnStart.gameObject.SetActive(isHost && waiting);
                btnStart.interactable = isHost && waiting && allReady;
            }
            if (startButtonText != null)
                startButtonText.text = allReady ? "START" : "Waiting for players to ready...";

            if (btnReady != null)
            {
                btnReady.gameObject.SetActive(!isHost && waiting);
                bool myReady = GetLocalPlayerReady();
                if (readyButtonText != null) readyButtonText.text = myReady ? "Cancel Ready" : "Ready";
            }

            if (leaveOrDisbandText != null)
                leaveOrDisbandText.text = isHost ? "Disband Room" : "Leave Room";

            UpdatePlayerSlots(room.players);
            _lastStatus = room.status;
        }

        private void UpdatePlayerSlots(List<RoomPlayerResponse> players)
        {
            // Use immediate destroy so containers are clean before we add new slots
            _slotViews.Clear();
            ClearContainer(team1Container);
            ClearContainer(team2Container);

            bool isHost = IsLocalPlayerHost();

            // 1. Create standard slots for the current mode
            for (int team = 1; team <= 2; team++)
            {
                Transform container = team == 1 ? team1Container : team2Container;
                if (container == null || playerSlotPrefab == null) continue;

                for (int slotIdx = 0; slotIdx < _maxSlotsPerTeam; slotIdx++)
                {
                    CreateSlot(container, team, slotIdx, players, isHost);
                }
            }

            // 2. Identify 'out of bounds' players (e.g. in seat 3 but mode is 1v1)
            if (players != null)
            {
                foreach (var p in players)
                {
                    string key = $"{p.team}_{p.slot}";
                    if (!_slotViews.ContainsKey(key))
                    {
                        // Player is in an invalid slot for this mode.
                        // Force render them at the end of their team container so they don't vanish.
                        Transform container = p.team == 1 ? team1Container : team2Container;
                        if (container != null)
                        {
                            Debug.LogWarning($"[PartyCustomMode] Player {p.username} is in invalid slot {p.slot} for mode. Force-rendering.");
                            CreateSlot(container, p.team, p.slot, players, isHost);
                        }
                    }
                }
            }
        }

        private void CreateSlot(Transform container, int team, int slotIdx, List<RoomPlayerResponse> players, bool isHost)
        {
            var player = players?.FirstOrDefault(p => p.team == team && p.slot == slotIdx);
            var go = Instantiate(playerSlotPrefab, container);
            var sv = ComponentResolver.Find<PlayerSlotView>(go)
                            .OnSelf().InChildren().Resolve();

            if (sv == null) return;

            var capturedPlayer = player;
            sv.SetSlot(team, slotIdx, player, isHost,
                        onSlotClicked: OnSlotClicked,
                        onKickClicked: isHost ? uid => OnKickWithConfirm(uid, capturedPlayer?.username) : (System.Action<long>)null);

            _slotViews[$"{team}_{slotIdx}"] = sv;
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
                Debug.LogWarning("[PartyCustomModeView] SubscribeEvents: GameEventBus.Instance is null — events will not fire. " +
                                 "Ensure GameEventBus is present in the scene and initialized before PartyCustomModeView.");
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
            GameEventBus.Instance.OnMatchPresenceNotice += HandleMatchPresenceNotice;
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
            GameEventBus.Instance.OnMatchPresenceNotice -= HandleMatchPresenceNotice;
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
            ApplyRoomUpdate(room, "room_updated");
            RefreshRoomDisplay();
        }

        private void HandlePlayerJoined(GameWebSocketService.PlayerJoinedEvent evt)
        {
            ApplyRoomUpdate(evt.room, "player_joined");
            RefreshRoomDisplay();
        }

        private void HandlePlayerLeft(GameWebSocketService.PlayerLeftEvent evt)
        {
            ApplyRoomUpdate(evt.room, "player_left");
            RefreshRoomDisplay();
        }

        private void HandlePlayerReady(GameWebSocketService.PlayerReadyEvent evt)
        {
            ApplyRoomUpdate(evt.room, "player_ready");
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
                ApplyRoomUpdate(evt.room, "team_changed");
                RefreshRoomDisplay();
            }
            else
            {
                // room field not populated — patch local state manually and refresh
                Debug.LogWarning("[PartyCustomMode] team_changed: room is null — patching RoomState manually");
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
            if (_roomState == null)
                _roomState = RoomState.Instance;

            if (_roomState == null || !_roomState.IsInRoom || _roomState.RoomId <= 0)
            {
                Debug.LogWarning("[PartyCustomModeView] Ignoring room_status_changed: no active current room.");
                return;
            }

            bool hasCurrentRoomPayload = ApplyRoomUpdate(evt.room, "room_status_changed");

            if (evt.newStatus == Constants.ROOM_STATUS_IN_GAME
                && _lastStatus != Constants.ROOM_STATUS_IN_GAME)
            {
                _hasAutoLeft = true;
                SetStatus("Match starting...");
                // MatchFlowCoordinator owns match_ready/ds_ready and the loading overlay.
            }
            else if (evt.newStatus == Constants.ROOM_STATUS_CLOSED
                     && _lastStatus != Constants.ROOM_STATUS_CLOSED)
            {
                if (!hasCurrentRoomPayload)
                {
                    Debug.LogWarning("[PartyCustomModeView] Ignoring CLOSED status without a matching current room payload.");
                    return;
                }

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
            if (!IsCurrentRoomEvent(evt.roomId, "room_disbanded")) return;
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
            NLog($"YouWereKicked: roomId={evt.roomId} reason={evt.reason}");
            if (!IsCurrentRoomEvent(evt.roomId, "you_were_kicked")) return;
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            ClearRoomUI();
            ShowState(UIState.JoinCreate);
            string message = !string.IsNullOrEmpty(evt.message)
                ? evt.message
                : "You have been removed from the room.";
            GameModalWindow.Instance?.ShowNotice("Removed", message);
        }

        private void HandleMatchPresenceNotice(GameWebSocketService.MatchPresenceNoticeEvent evt)
        {
            if (evt == null) return;
            if (!IsCurrentRoomEvent(evt.room?.roomId ?? 0L, "match_presence_notice")) return;

            if (evt.room != null)
            {
                ApplyRoomUpdate(evt.room, "match_presence_notice");
                RefreshRoomDisplay();
            }

            string title = string.Equals(evt.state, "CONNECTED", StringComparison.OrdinalIgnoreCase)
                ? "Player Reconnected"
                : string.Equals(evt.state, "ABANDONED", StringComparison.OrdinalIgnoreCase)
                    ? "Player Removed"
                    : "Player Disconnected";
            string name = !string.IsNullOrEmpty(evt.displayName) ? evt.displayName : $"Player {evt.userId}";
            string message = !string.IsNullOrEmpty(evt.message) ? evt.message : $"{name}: {evt.state}";
            PersistentUICanvas.Instance?.ToastService?.Show(title, $"{name}: {message}");
        }

        private bool ApplyRoomUpdate(RoomResponse room, string source)
        {
            if (room == null)
                return false;

            if (room.roomId <= 0)
            {
                Debug.LogWarning($"[PartyCustomModeView] Ignoring {source}: payload roomId is invalid ({room.roomId}).");
                return false;
            }

            if (_roomState == null)
                _roomState = RoomState.Instance;

            if (_roomState == null || !_roomState.IsInRoom || _roomState.RoomId <= 0)
            {
                long localUserId = _sessionState?.UserId ?? 0L;
                bool payloadContainsLocalPlayer = localUserId > 0L &&
                    (room.players?.Any(p => p.userId == localUserId) ?? false);
                if (payloadContainsLocalPlayer)
                {
                    NLog($"Adopting {source} room payload as active room: roomId={room.roomId}");
                    _roomState?.SetRoom(room);
                    return true;
                }

                Debug.LogWarning($"[PartyCustomModeView] Ignoring {source}: no active current room.");
                return false;
            }

            if (room.roomId != _roomState.RoomId)
            {
                Debug.LogWarning($"[PartyCustomModeView] Ignoring {source}: roomId={room.roomId} does not match current roomId={_roomState.RoomId}.");
                return false;
            }

            _roomState.SetRoom(room);
            return true;
        }

        private bool IsCurrentRoomEvent(long eventRoomId, string source)
        {
            if (_roomState == null)
                _roomState = RoomState.Instance;

            if (_roomState == null || !_roomState.IsInRoom || _roomState.RoomId <= 0)
            {
                Debug.LogWarning($"[PartyCustomModeView] Ignoring {source}: no active current room.");
                return false;
            }

            if (eventRoomId <= 0)
            {
                Debug.LogWarning($"[PartyCustomModeView] Ignoring {source}: payload roomId is invalid ({eventRoomId}).");
                return false;
            }

            if (eventRoomId != _roomState.RoomId)
            {
                Debug.LogWarning($"[PartyCustomModeView] Ignoring {source}: roomId={eventRoomId} does not match current roomId={_roomState.RoomId}.");
                return false;
            }

            return true;
        }

        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt)
        {
            long myId = _sessionState?.UserId ?? 0L;
            if (evt.targetUserId != myId) return;

            long requestId = evt.requestId;
            string requesterName = evt.fromUsername ?? "Player";
            long roomId = evt.roomId > 0L ? evt.roomId : ResolveActiveRoomId();
            if (roomId <= 0L)
            {
                Debug.LogWarning($"[PartyCustomModeView] Ignoring swap request {requestId}: no active room.");
                return;
            }

            _incomingSwapRequestId = requestId;
            _incomingSwapRoomId = roomId;
            NLog($"SwapRequest received from {requesterName} requestId={requestId} roomId={roomId}");

            GameModalWindow.Instance?.ShowCountdown(
                title: "Swap Request",
                desc: $"<b>{requesterName}</b> wants to swap seats with you.",
                seconds: 15,
                onConfirm: () => _ = AcceptSwapSilent(roomId, requestId),
                onExpire: () => _ = RejectSwapSilent(roomId, requestId),
                showConfirm: true,
                confirmText: "Accept",
                cancelText: "Decline",
                invitationId: requestId
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
                long closedRequestId = _pendingSwapRequestId;
                _pendingSwapRequestId = 0L;
                _pendingSwapRoomId = 0L;
                if (evt.requestId != 0L)
                    GameModalWindow.Instance?.DismissIfMatchingInvitation(evt.requestId);
                else
                    GameModalWindow.Instance?.DismissIfMatchingInvitation(closedRequestId);
                if (evt.status == "REJECTED" || evt.status == "CANCELLED")
                    GameModalWindow.Instance?.ShowNotice("Swap Declined", "The player declined your swap request.");
                else if (evt.status == "EXPIRED")
                    GameModalWindow.Instance?.ShowNotice("Swap Expired", "The swap request has expired.");
            }

            bool isIncomingRequest = evt.requestId != 0L && evt.requestId == _incomingSwapRequestId;
            if (isIncomingRequest)
            {
                _incomingSwapRequestId = 0L;
                _incomingSwapRoomId = 0L;
                GameModalWindow.Instance?.DismissIfMatchingInvitation(evt.requestId);
            }

            if (evt.room != null) ApplyRoomUpdate(evt.room, "swap_request_status");
            RefreshRoomDisplay();
        }


        private void HandleForceLogout()
        {
            NLog("HandleForceLogout");
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            SessionTerminationFlow.ShowAndLogout(
                "Signed Out",
                "Your account was signed in from another location.");
        }

        private void HandleSessionExpired()
        {
            NLog("HandleSessionExpired");
            _hasAutoLeft = true;
            _roomState?.ClearRoom();
            SessionTerminationFlow.ShowAndLogout(
                "Session Expired",
                "Your session has expired. Please log in again.");
        }

        private void HandleAppFocusGained() => RefreshRoomDisplay();
        private void HandleAppResumed() => RefreshRoomDisplay();

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static void PopulateDropdown(CustomDropdown dd, List<string> names, int selectIndex)
        {
            NH_DropdownRuntime.Populate(dd, names, selectIndex);
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
            var room = _roomState.CurrentRoom;
            var player = room?.players?.FirstOrDefault(p => p.userId == uid);
            return IsReadyForStart(room, player);
        }

        private static void EnsureOwnerReadyForDisplay(RoomResponse room)
        {
            if (room?.players == null)
                return;

            foreach (var player in room.players)
            {
                if (player != null && player.userId == room.ownerId)
                    player.isReady = true;
            }
        }

        private static bool IsReadyForStart(RoomResponse room, RoomPlayerResponse player)
        {
            if (room == null || player == null)
                return false;

            return player.isReady || player.userId == room.ownerId;
        }

        private long ResolveActiveRoomId()
        {
            if (_roomState == null)
                _roomState = RoomState.Instance;

            long roomId = _roomState?.RoomId ?? 0L;
            return roomId > 0L ? roomId : 0L;
        }

        private void ClearPendingSwapIfMatches(long requestId)
        {
            if (requestId == 0L || _pendingSwapRequestId == requestId)
            {
                _pendingSwapRequestId = 0L;
                _pendingSwapRoomId = 0L;
            }
        }

        private void ClearIncomingSwapIfMatches(long requestId)
        {
            if (requestId == 0L || _incomingSwapRequestId == requestId)
            {
                _incomingSwapRequestId = 0L;
                _incomingSwapRoomId = 0L;
            }
        }

        private async System.Threading.Tasks.Task AcceptSwapSilent(long roomId, long requestId)
        {
            if (_roomService == null || roomId <= 0L || requestId == 0L) return;
            try { await _roomService.AcceptSwapRequest(roomId, requestId); }
            catch (Exception ex) { Debug.LogWarning($"[PartyCustomModeView] AcceptSwap failed: roomId={roomId} requestId={requestId} error={ex.Message}"); }
            finally { ClearIncomingSwapIfMatches(requestId); }
        }

        private async System.Threading.Tasks.Task RejectSwapSilent(long roomId, long requestId)
        {
            if (_roomService == null || roomId <= 0L || requestId == 0L) return;
            try { await _roomService.RejectSwapRequest(roomId, requestId); }
            catch (Exception ex) { Debug.LogWarning($"[PartyCustomModeView] RejectSwap failed: roomId={roomId} requestId={requestId} error={ex.Message}"); }
            finally { ClearIncomingSwapIfMatches(requestId); }
        }

        private void SetStatus(string msg)
        {
            ResolveReferences(createFallback: true);
            if (statusText != null) statusText.text = msg;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ResolveReferences(bool createFallback)
        {
            ResolveDropdownReferences();

            if (statusText == null)
                statusText = FindChildByName<TextMeshProUGUI>("status");

            if (btnCopyCode == null)
                btnCopyCode = FindChildByName<Button>("copy");

            if (!createFallback)
                return;

            if (statusText == null)
                statusText = CreateRuntimeStatusText();

            if (btnCopyCode == null && roomCodeText != null)
                btnCopyCode = CreateRuntimeCopyButton();
        }

        private void ResolveDropdownReferences()
        {
            if (modeDropdown != null && mapDropdown != null)
            {
                NH_DropdownRuntime.NormalizeModeMapOrder(modeDropdown, mapDropdown);
                return;
            }

            var dropdowns = GetComponentsInChildren<CustomDropdown>(true);
            if (dropdowns == null || dropdowns.Length == 0)
                return;

            if (modeDropdown == null)
                modeDropdown = FindDropdown(dropdowns, "mode", "game");

            if (mapDropdown == null)
                mapDropdown = FindDropdown(dropdowns, "map");

            NH_DropdownRuntime.NormalizeModeMapOrder(modeDropdown, mapDropdown);
        }

        private static CustomDropdown FindDropdown(CustomDropdown[] dropdowns, params string[] tokens)
        {
            if (dropdowns == null || tokens == null)
                return null;

            for (int i = 0; i < dropdowns.Length; i++)
            {
                var dropdown = dropdowns[i];
                if (dropdown == null)
                    continue;

                string name = dropdown.gameObject.name;
                for (int j = 0; j < tokens.Length; j++)
                    if (name.IndexOf(tokens[j], StringComparison.OrdinalIgnoreCase) >= 0)
                        return dropdown;
            }

            return null;
        }

        private T FindChildByName<T>(string token) where T : Component
        {
            if (string.IsNullOrEmpty(token))
                return null;

            foreach (var component in GetComponentsInChildren<T>(true))
            {
                if (component != null &&
                    component.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return component;
                }
            }

            return null;
        }

        private TextMeshProUGUI CreateRuntimeStatusText()
        {
            var parent = inRoomPanel != null ? inRoomPanel.transform : transform;
            var go = CreateRuntimeUIObject("Runtime Lobby Status", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 32f);
            rect.anchoredPosition = new Vector2(0f, 24f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 16f;
            text.color = new Color(0.82f, 0.9f, 1f, 1f);
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private Button CreateRuntimeCopyButton()
        {
            var parent = roomCodeText != null && roomCodeText.transform.parent != null
                ? roomCodeText.transform.parent
                : (inRoomPanel != null ? inRoomPanel.transform : transform);

            var go = CreateRuntimeUIObject("Runtime Copy Room Code", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 32f);
            rect.anchoredPosition = new Vector2(-8f, 0f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.13f, 0.18f, 0.22f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var labelGo = CreateRuntimeUIObject("Label", go.transform);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 14f;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "Copy";

            return button;
        }

        private GameObject CreateRuntimeUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer == 0 ? 5 : gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private string DescribeLocalRoom()
        {
            if (_roomState == null)
                _roomState = RoomState.Instance;

            if (_roomState == null)
                return "roomState=null";

            return
                $"isInRoom={_roomState.IsInRoom},roomId={_roomState.RoomId},code={_roomState.RoomCode},status={_roomState.Status},players={_roomState.PlayerCount}";
        }

        private static string DescribeParty(PartyResponse party)
        {
            if (party == null)
                return "null";

            int members = party.members != null ? party.members.Count : party.currentMemberCount;
            return
                $"id={party.partyId},host={party.hostUserId},status={party.partyStatus},members={members}/{party.maxMembers}";
        }

        /// <summary>Flow log with a stable Console filter for lobby debugging.</summary>
        private static void NLog(string msg) =>
            Debug.Log($"[FLOW][CUSTOM_LOBBY] {msg}");

        public void RefreshPlayerList() => RefreshRoomDisplay();
    }
}
