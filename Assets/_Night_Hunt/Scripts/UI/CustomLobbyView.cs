using System.Collections.Generic;
using System.Linq;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.Services.Room;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Custom Lobby View (scene: 05_CustomLobby).
    ///
    /// Flow:
    ///   1. Host presses "Create Room" → backend creates relay session.
    ///   2. Backend returns relay IP/port/sessionId and room code.
    ///   3. Room code is shown; others join via "Join Room" + code.
    ///   4. All players are listed per team; owner can hit "Start Match".
    ///   5. Start → LoadMatchLoading scene; ServerGameManager takes over.
    ///
    /// WS events consumed:
    ///   PLAYER_JOINED, PLAYER_LEFT, PLAYER_READY, GAME_STARTING
    ///   (subscribed via LobbyWebSocket / GameWebSocket in GameBootstrap).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomLobbyView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Room Info")] [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI modeText;
        [SerializeField] private Button copyCodeButton;

        [Header("Team Slots")] [SerializeField]
        private Transform team1Container;

        [SerializeField] private Transform team2Container;
        [SerializeField] private GameObject playerSlotPrefab;

        [Header("Actions")] [SerializeField] private Button readyButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button startButton; // visible to host only
        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Join / Create Panels")] [SerializeField]
        private GameObject joinCreatePanel;

        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button joinConfirmButton;
        [SerializeField] private Button backToHomeButton;

        // ── Services ──────────────────────────────────────────────────────────
        private RoomService _roomService;
        private RoomState _roomState;
        private SessionState _sessionState;

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly Dictionary<string, PlayerSlotView> _slotViews = new();
        private int _maxSlotsPerTeam = 2;
        private string _lastStatus;
        private bool _refreshPending;
        private float _lastRefreshTime;
        private const float REFRESH_THROTTLE = 0.1f;

        // ──────────────────────────────────────────────────────────────────────

        #region Unity Lifecycle

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _roomService = GameManager.Instance.RoomService;
                _sessionState = GameManager.Instance.SessionState;
            }

            _roomState = RoomState.Instance;

            // Buttons
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoomPanelClicked);
            if (joinConfirmButton != null) joinConfirmButton.onClick.AddListener(OnJoinConfirmClicked);
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (backToHomeButton != null) backToHomeButton.onClick.AddListener(OnBackToHomeClicked);
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        }

        private void Start()
        {
            bool alreadyInRoom = _roomState != null && _roomState.IsInRoom;
            SetJoinCreatePanelVisible(!alreadyInRoom);

            if (alreadyInRoom)
                RefreshRoomDisplay();
        }

        private void OnEnable()
        {
            _lastStatus = null;
            _refreshPending = false;
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.OnRoomUpdated += HandleRoomUpdated;
                GameEventBus.Instance.OnPlayerJoined += HandlePlayerJoined;
                GameEventBus.Instance.OnPlayerLeft += HandlePlayerLeft;
                GameEventBus.Instance.OnPlayerReady += HandlePlayerReady;
                GameEventBus.Instance.OnTeamChanged += HandleTeamChanged;
                GameEventBus.Instance.OnRoomStatusChanged += HandleRoomStatusChanged;
                GameEventBus.Instance.OnSwapRequest += HandleSwapRequest;
                GameEventBus.Instance.OnSwapRequestStatus += HandleSwapRequestStatus;
                GameEventBus.Instance.OnForceLogout += HandleForceLogout;
                GameEventBus.Instance.OnSessionExpired += HandleSessionExpired;
                GameEventBus.Instance.OnAppFocusGained += HandleAppFocusGained;
                GameEventBus.Instance.OnAppResumed += HandleAppResumed;
            }

            if (_roomState != null && _roomState.IsInRoom)
                RefreshRoomDisplay();
        }

        private void OnDisable()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.OnRoomUpdated -= HandleRoomUpdated;
                GameEventBus.Instance.OnPlayerJoined -= HandlePlayerJoined;
                GameEventBus.Instance.OnPlayerLeft -= HandlePlayerLeft;
                GameEventBus.Instance.OnPlayerReady -= HandlePlayerReady;
                GameEventBus.Instance.OnTeamChanged -= HandleTeamChanged;
                GameEventBus.Instance.OnRoomStatusChanged -= HandleRoomStatusChanged;
                GameEventBus.Instance.OnSwapRequest -= HandleSwapRequest;
                GameEventBus.Instance.OnSwapRequestStatus -= HandleSwapRequestStatus;
                GameEventBus.Instance.OnForceLogout -= HandleForceLogout;
                GameEventBus.Instance.OnSessionExpired -= HandleSessionExpired;
                GameEventBus.Instance.OnAppFocusGained -= HandleAppFocusGained;
                GameEventBus.Instance.OnAppResumed -= HandleAppResumed;
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Room Actions

        private async void OnCreateRoomClicked()
        {
            SetStatus("Creating room…");

            // POST /api/custom/room → handled by RoomService.CreateRoom
            var result = await _roomService.CreateRoom("2v2", isPublic: true, isLocked: false, password: null);

            if (result.Success && result.Data != null)
            {
                SetJoinCreatePanelVisible(false);
                RefreshRoomDisplay();
                SetStatus("Waiting for players…");
            }
            else
            {
                SetStatus($"Error: {result.Message}");
            }
        }

        private void OnJoinRoomPanelClicked()
        {
            // Toggle join code input visibility
            if (joinCodeInput != null)
                joinCodeInput.gameObject.SetActive(true);
            if (joinConfirmButton != null)
                joinConfirmButton.gameObject.SetActive(true);
        }

        private async void OnJoinConfirmClicked()
        {
            if (joinCodeInput == null) return;
            string code = joinCodeInput.text.Trim();
            if (string.IsNullOrEmpty(code)) return;

            SetStatus($"Joining {code}…");

            var result = await _roomService.JoinByCode(code, "");
            if (result.Success)
            {
                SetJoinCreatePanelVisible(false);
                RefreshRoomDisplay();
                SetStatus("Joined. Waiting for host to start…");
            }
            else
            {
                SetStatus($"Join failed: {result.Message}");
            }
        }

        private async void OnReadyClicked()
        {
            if (_roomService == null || _roomState == null) return;

            bool currentReady = GetCurrentPlayerReady();
            var result = await _roomService.SetReady(_roomState.RoomId, !currentReady);
            if (!result.Success)
                SetStatus($"Ready failed: {result.Message}");
            // UI will refresh via OnPlayerReady WS event
        }

        private async void OnStartClicked()
        {
            if (!IsLocalPlayerHost() || _roomService == null || _roomState == null) return;

            SetStatus("Starting match…");
            var result = await _roomService.StartGame(_roomState.RoomId);
            if (!result.Success)
                SetStatus($"Start failed: {result.Message}");
            // Scene transition triggered by HandleRoomStatusChanged (GAME_STARTING WS event)
        }

        private async void OnLeaveClicked()
        {
            if (_roomService != null && _roomState != null)
                await _roomService.LeaveRoom(_roomState.RoomId);

            _roomState?.ClearRoom();
            SceneLoader.LoadHome();
        }

        private void OnBackToHomeClicked()
        {
            SceneLoader.LoadHome();
        }

        private void OnCopyCodeClicked()
        {
            if (_roomState == null) return;
            GUIUtility.systemCopyBuffer = _roomState.RoomCode;
            SetStatus("Room code copied!");
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Display

        private void RefreshRoomDisplay()
        {
            if (_roomState == null) return;

            // Throttle to avoid cascading refreshes
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

        private System.Collections.IEnumerator DelayedRefresh()
        {
            yield return new UnityEngine.WaitForSeconds(REFRESH_THROTTLE);
            _refreshPending = false;
            _lastRefreshTime = Time.time;
            RefreshRoomDisplayImmediate();
        }

        private void RefreshRoomDisplayImmediate()
        {
            if (_roomState == null || !_roomState.IsInRoom) return;
            var room = _roomState.CurrentRoom;

            if (roomCodeText != null)
                roomCodeText.text = $"Room: {room.roomCode}";

            if (modeText != null)
                modeText.text = room.mode ?? "Custom";

            _maxSlotsPerTeam = room.mode switch
            {
                "2v2" => 2, "3v3" => 3, "5v5" => 5, _ => 2
            };

            bool isHost = IsLocalPlayerHost();
            bool waiting = room.status == Constants.ROOM_STATUS_WAITING;
            bool allReady = room.players != null && room.players.Count > 0
                                                 && room.players.All(p => p.isReady);
            bool full = room.players?.Count >= _maxSlotsPerTeam * 2;

            if (startButton != null)
            {
                startButton.gameObject.SetActive(isHost && waiting);
                startButton.interactable = isHost && waiting && full && allReady;
            }

            if (readyButton != null)
                readyButton.gameObject.SetActive(!isHost && waiting);

            UpdatePlayerSlots(room.players);
        }

        private void UpdatePlayerSlots(List<RoomPlayerResponse> players)
        {
            // Destroy old slot views
            foreach (var sv in _slotViews.Values)
                if (sv != null)
                    Destroy(sv.gameObject);
            _slotViews.Clear();

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
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[Auto] PlayerSlotView not found")
                        .Resolve();
                    if (sv != null)
                    {
                        sv.SetSlot(team, slotIdx, player, isHost, OnSlotClicked, OnTransferOwnerClicked);
                        _slotViews[$"{team}_{slotIdx}"] = sv;
                    }
                }
            }
        }

        /// <summary>
        /// Public entry-point kept for external callers.
        /// Prefer relying on GameEventBus events instead.
        /// </summary>
        public void RefreshPlayerList() => RefreshRoomDisplay();

        private void SetJoinCreatePanelVisible(bool visible)
        {
            if (joinCreatePanel != null)
                joinCreatePanel.SetActive(visible);
        }

        private void SetStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }

        private bool IsLocalPlayerHost()
        {
            if (_roomState == null || _sessionState == null) return false;
            return _roomState.CurrentRoom?.ownerId == _sessionState.UserId;
        }

        private bool GetCurrentPlayerReady()
        {
            if (_roomState == null || !_roomState.IsInRoom || _sessionState == null) return false;
            long uid = _sessionState.UserId;
            return _roomState.CurrentRoom?.players?.FirstOrDefault(x => x.userId == uid)?.isReady ?? false;
        }

        private async void OnSlotClicked(int team, int slotIdx)
        {
            if (_roomService == null || _roomState == null) return;
            string key = $"{team}_{slotIdx}";
            if (!_slotViews.TryGetValue(key, out var sv)) return;

            if (sv.IsEmpty)
            {
                var result = await _roomService.ChangeTeam(_roomState.RoomId, team, slotIdx);
                if (!result.Success) SetStatus($"Move failed: {result.Message}");
            }
            else if (sv.Player?.userId != GetCurrentUserIdLong())
            {
                var result = await _roomService.RequestSwap(_roomState.RoomId, sv.Player.userId, team, slotIdx);
                if (!result.Success) SetStatus($"Swap request failed: {result.Message}");
            }
        }

        private async void OnTransferOwnerClicked(long targetUserId)
        {
            if (_roomService == null || _roomState == null) return;
            var result = await _roomService.TransferOwner(_roomState.RoomId, targetUserId);
            if (!result.Success) SetStatus($"Transfer failed: {result.Message}");
        }

        private long GetCurrentUserIdLong() => _sessionState?.UserId ?? 0L;

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region GameEventBus Handlers

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
                && _lastStatus != Constants.ROOM_STATUS_IN_GAME)
            {
                Debug.Log("[CustomLobbyView] Game started — loading MatchLoading scene.");
                SceneLoader.LoadMatchLoading();
            }

            _lastStatus = evt.newStatus;
            RefreshRoomDisplay();
        }

        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt)
        {
            // Show a basic notification; full swap UI can be added later
            if (_sessionState != null && evt.targetUserId == GetCurrentUserIdLong())
                SetStatus($"{evt.fromUsername ?? "Player"} wants to swap with you!");
        }

        private void HandleSwapRequestStatus(GameWebSocketService.SwapRequestStatusEvent evt)
        {
            RefreshRoomDisplay();
        }

        private void HandleForceLogout()
        {
            _roomState?.ClearRoom();
            SceneLoader.LoadLogin();
        }

        private void HandleSessionExpired()
        {
            _roomState?.ClearRoom();
            SceneLoader.LoadLogin();
        }

        private void HandleAppFocusGained() => RefreshRoomDisplay();
        private void HandleAppResumed() => RefreshRoomDisplay();

        #endregion
    }
}