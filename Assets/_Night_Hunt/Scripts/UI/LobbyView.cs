using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Lobby;
using NightHunt.Services.Game;
using NightHunt.Services.Room;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Lobby View - Displays lobby with fixed slots per team
    /// Logic:
    /// - Empty slot: Click to move to that slot
    /// - Occupied slot (different team): Click to switch teams
    /// - Occupied slot (same team): Click to swap positions
    /// - All slots full + click occupied slot: Send swap request
    /// </summary>
    public class LobbyView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI modeText;
        [SerializeField] private Transform team1Container;
        [SerializeField] private Transform team2Container;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private GameObject playerSlotPrefab;

        [Header("Swap Request - Target (Received)")]
        [SerializeField] private GameObject swapRequestPanel;
        [SerializeField] private TextMeshProUGUI swapRequestText;
        [SerializeField] private Button acceptSwapButton;
        [SerializeField] private Button rejectSwapButton;
        
        [Header("Swap Request - Requester (Sent)")]
        [SerializeField] private GameObject swapRequestCancelPanel;
        [SerializeField] private TextMeshProUGUI swapRequestCancelText;
        [SerializeField] private Button cancelSwapButton;

        [Header("Room Settings (Owner Only)")]
        [SerializeField] private GameObject roomSettingsPanel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Dropdown modeDropdown;
        [SerializeField] private Toggle isPublicToggle;
        [SerializeField] private Toggle isLockedToggle;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button saveSettingsButton;
        [SerializeField] private Button cancelSettingsButton;

        private LobbyController lobbyController;
        private RoomService roomService;
        private RoomState roomState;
        private SessionState sessionState;
        
        // Slot management
        private Dictionary<string, PlayerSlotView> slotViews = new Dictionary<string, PlayerSlotView>(); // Key: "team_slot"
        private int maxSlotsPerTeam = 2; // Default 2v2, will be updated based on mode
        
        // Swap request tracking
        private long? pendingSwapRequestId = null; // Track our own pending swap request
        private Coroutine swapRequestTimeoutCoroutine = null;
        private long? receivedSwapRequestId = null; // Track received swap request (for target)
        private Coroutine receivedSwapRequestTimeoutCoroutine = null; // Timeout for received request
        
        // Track owner ID to detect changes
        private long? lastOwnerId = null;
        
        // Track room status to detect game start
        private string lastStatus = null;
        private bool gameStartLogged = false; // Flag to ensure log only once
        
        // Throttle RefreshLobby to prevent too many calls
        private float lastRefreshTime = 0f;
        private const float REFRESH_THROTTLE_INTERVAL = 0.1f; // Max 10 refreshes per second
        private bool refreshPending = false;

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                roomService = GameManager.Instance.RoomService;
                sessionState = GameManager.Instance.SessionState;
            }

            lobbyController = FindFirstObjectByType<LobbyController>();
            roomState = RoomState.Instance;

            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyClicked);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeaveClicked);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);

            if (acceptSwapButton != null)
                acceptSwapButton.onClick.AddListener(OnAcceptSwapRequest);

            if (rejectSwapButton != null)
                rejectSwapButton.onClick.AddListener(OnRejectSwapRequest);

            if (cancelSwapButton != null)
                cancelSwapButton.onClick.AddListener(OnCancelSwapRequest);

            if (swapRequestPanel != null)
                swapRequestPanel.SetActive(false);
            
            if (swapRequestCancelPanel != null)
                swapRequestCancelPanel.SetActive(false);

            // Room Settings
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (saveSettingsButton != null)
                saveSettingsButton.onClick.AddListener(OnSaveSettingsClicked);
            if (cancelSettingsButton != null)
                cancelSettingsButton.onClick.AddListener(OnCancelSettingsClicked);

            if (roomSettingsPanel != null)
                roomSettingsPanel.SetActive(false);
        }

        private void OnEnable()
        {
            // Reset tracking flags
            lastStatus = null;
            gameStartLogged = false;
            
            // Subscribe to GameWebSocketService events (unified WebSocket for all events)
            if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
            {
                var ws = GameManager.Instance.GameWebSocket;
                // Room Events
                ws.OnRoomUpdated += HandleRoomUpdated;
                ws.OnPlayerJoined += HandlePlayerJoined;
                ws.OnPlayerLeft += HandlePlayerLeft;
                ws.OnPlayerReady += HandlePlayerReady;
                ws.OnTeamChanged += HandleTeamChanged;
                ws.OnRoomStatusChanged += HandleRoomStatusChanged;
                ws.OnSwapRequest += HandleSwapRequest;
                ws.OnSwapRequestStatus += HandleSwapRequestStatus;
                // Session Events
                ws.OnForceLogout += HandleForceLogout;
                ws.OnSessionExpired += HandleSessionExpired;
            }
            
            RefreshLobby();
        }

        private void OnDisable()
        {
            // Unsubscribe from GameWebSocketService events
            if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
            {
                var ws = GameManager.Instance.GameWebSocket;
                // Room Events
                ws.OnRoomUpdated -= HandleRoomUpdated;
                ws.OnPlayerJoined -= HandlePlayerJoined;
                ws.OnPlayerLeft -= HandlePlayerLeft;
                ws.OnPlayerReady -= HandlePlayerReady;
                ws.OnTeamChanged -= HandleTeamChanged;
                ws.OnRoomStatusChanged -= HandleRoomStatusChanged;
                ws.OnSwapRequest -= HandleSwapRequest;
                ws.OnSwapRequestStatus -= HandleSwapRequestStatus;
                // Session Events
                ws.OnForceLogout -= HandleForceLogout;
                ws.OnSessionExpired -= HandleSessionExpired;
            }
            
            // Cancel timeout coroutines
            if (swapRequestTimeoutCoroutine != null)
            {
                StopCoroutine(swapRequestTimeoutCoroutine);
                swapRequestTimeoutCoroutine = null;
            }
            
            if (receivedSwapRequestTimeoutCoroutine != null)
            {
                StopCoroutine(receivedSwapRequestTimeoutCoroutine);
                receivedSwapRequestTimeoutCoroutine = null;
            }
        }

        /// <summary>
        /// Show lobby view (for panel overlay mode)
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            RefreshLobby();
        }

        /// <summary>
        /// Hide lobby view (for panel overlay mode)
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void RefreshLobby()
        {
            if (roomState == null || !roomState.IsInRoom)
                return;

            // Throttle refresh calls to prevent too many updates
            float currentTime = Time.time;
            if (currentTime - lastRefreshTime < REFRESH_THROTTLE_INTERVAL)
            {
                // Too soon, schedule a delayed refresh
                if (!refreshPending)
                {
                    refreshPending = true;
                    StartCoroutine(DelayedRefreshLobby());
                }
                return;
            }

            refreshPending = false;
            lastRefreshTime = currentTime;
            RefreshLobbyImmediate();
        }

        private System.Collections.IEnumerator DelayedRefreshLobby()
        {
            yield return new WaitForSeconds(REFRESH_THROTTLE_INTERVAL);
            refreshPending = false;
            lastRefreshTime = Time.time;
            RefreshLobbyImmediate();
        }

        private void RefreshLobbyImmediate()
        {
            if (roomState == null || !roomState.IsInRoom)
                return;

            var room = roomState.CurrentRoom;

            if (roomCodeText != null)
                roomCodeText.text = $"Room Code: {room.roomCode}";

            if (modeText != null)
                modeText.text = $"Mode: {room.mode}";

            // Determine max slots per team based on mode
            maxSlotsPerTeam = GetMaxSlotsPerTeam(room.mode);

            // Check if owner changed
            bool ownerChanged = lastOwnerId.HasValue && lastOwnerId.Value != room.ownerId;
            if (ownerChanged)
            {
                Debug.Log($"[LobbyView] Owner changed from {lastOwnerId.Value} to {room.ownerId}");
                // Owner transfer detected - refresh UI to update controls
            }
            lastOwnerId = room.ownerId;
            
            // Check if game started (status changed from WAITING to IN_GAME)
            if (!string.IsNullOrEmpty(lastStatus) && 
                lastStatus == Constants.ROOM_STATUS_WAITING && 
                room.status == Constants.ROOM_STATUS_IN_GAME)
            {
                if (!gameStartLogged)
                {
                    Debug.Log($"[LobbyView] Game started! Room {room.roomCode} (ID: {room.roomId}, MatchID: {room.matchId}) - Mode: {room.mode}, Players: {room.players?.Count ?? 0}");
                    gameStartLogged = true;
                    // TODO: Here you can trigger game start event, load game scene, etc.
                    // Example: OnGameStarted?.Invoke(room);
                }
            }
            else if (room.status == Constants.ROOM_STATUS_WAITING)
            {
                // Reset flag if back to WAITING (shouldn't happen, but handle it)
                gameStartLogged = false;
            }
            lastStatus = room.status;

            // Update player slots
            UpdatePlayerSlots(room.players);

            // Update button states
            bool isOwner = lobbyController != null && lobbyController.IsOwner();
            if (startButton != null)
                startButton.gameObject.SetActive(isOwner && room.status == "WAITING");
            
            // Show settings button only for owner
            if (settingsButton != null)
                settingsButton.gameObject.SetActive(isOwner && room.status == "WAITING");
        }

        private int GetMaxSlotsPerTeam(string mode)
        {
            return mode switch
            {
                "2v2" => 2,
                "3v3" => 3,
                "5v5" => 5,
                _ => 2
            };
        }

        private void UpdatePlayerSlots(List<RoomPlayerResponse> players)
        {
            // Clear existing slots
            foreach (var slotView in slotViews.Values)
            {
                if (slotView != null)
                    Destroy(slotView.gameObject);
            }
            slotViews.Clear();

            // Create all slots (empty + occupied)
            // Team 1
            for (int slot = 0; slot < maxSlotsPerTeam; slot++)
            {
                CreateSlot(1, slot, players);
            }

            // Team 2
            for (int slot = 0; slot < maxSlotsPerTeam; slot++)
            {
                CreateSlot(2, slot, players);
            }
        }

        private void CreateSlot(int team, int slot, List<RoomPlayerResponse> players)
        {
            Transform container = team == 1 ? team1Container : team2Container;
            if (container == null) return;

            // Find player in this slot
            var player = players?.FirstOrDefault(p => p.team == team && p.slot == slot);

            GameObject slotObj = Instantiate(playerSlotPrefab, container);
            PlayerSlotView slotView = slotObj.GetComponent<PlayerSlotView>();
            if (slotView != null)
            {
                bool isOwner = lobbyController != null && lobbyController.IsOwner();
                slotView.SetSlot(team, slot, player, isOwner, OnSlotClicked, OnTransferOwnerClicked);
                
                string key = $"{team}_{slot}";
                slotViews[key] = slotView;
            }
        }

        /// <summary>
        /// Add a player slot (when player joins)
        /// </summary>
        private void AddPlayerSlot(RoomPlayerResponse player)
        {
            if (player == null) return;
            
            string key = $"{player.team}_{player.slot}";
            
            // If slot already exists, update it
            if (slotViews.ContainsKey(key))
            {
                var slotView = slotViews[key];
                if (slotView != null)
                {
                    bool isOwner = lobbyController != null && lobbyController.IsOwner();
                    slotView.SetSlot(player.team, player.slot, player, isOwner, OnSlotClicked, OnTransferOwnerClicked);
                }
            }
            else
            {
                // Create new slot
                CreateSlot(player.team, player.slot, new List<RoomPlayerResponse> { player });
            }
        }

        /// <summary>
        /// Remove a player slot (when player leaves)
        /// </summary>
        private void RemovePlayerSlot(long userId)
        {
            // Find slot containing this player
            string keyToRemove = null;
            foreach (var kvp in slotViews)
            {
                if (kvp.Value != null && kvp.Value.Player != null && kvp.Value.Player.userId == userId)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }
            
            if (keyToRemove != null)
            {
                var slotView = slotViews[keyToRemove];
                if (slotView != null)
                {
                    // Clear slot (make it empty)
                    bool isOwner = lobbyController != null && lobbyController.IsOwner();
                    slotView.SetSlot(slotView.Team, slotView.Slot, null, isOwner, OnSlotClicked, OnTransferOwnerClicked);
                }
            }
        }

        /// <summary>
        /// Update ready status for a specific player
        /// </summary>
        private void UpdatePlayerReadyStatus(long userId, bool isReady)
        {
            foreach (var kvp in slotViews)
            {
                if (kvp.Value != null && kvp.Value.Player != null && kvp.Value.Player.userId == userId)
                {
                    // Update ready status in slot view
                    kvp.Value.UpdateReadyStatus(isReady);
                    break;
                }
            }
        }

        /// <summary>
        /// Move player to new team/slot
        /// </summary>
        private void MovePlayerSlot(long userId, int newTeam, int newSlot)
        {
            // Find old slot
            string oldKey = null;
            RoomPlayerResponse player = null;
            
            foreach (var kvp in slotViews)
            {
                if (kvp.Value != null && kvp.Value.Player != null && kvp.Value.Player.userId == userId)
                {
                    oldKey = kvp.Key;
                    player = kvp.Value.Player;
                    break;
                }
            }
            
            if (player == null) return;
            
            // Update player's team/slot
            player.team = newTeam;
            player.slot = newSlot;
            
            // Clear old slot
            if (oldKey != null && slotViews.ContainsKey(oldKey))
            {
                var oldSlotView = slotViews[oldKey];
                if (oldSlotView != null)
                {
                    bool isOwner = lobbyController != null && lobbyController.IsOwner();
                    oldSlotView.SetSlot(oldSlotView.Team, oldSlotView.Slot, null, isOwner, OnSlotClicked, OnTransferOwnerClicked);
                }
            }
            
            // Add/Update new slot
            string newKey = $"{newTeam}_{newSlot}";
            if (slotViews.ContainsKey(newKey))
            {
                var newSlotView = slotViews[newKey];
                if (newSlotView != null)
                {
                    bool isOwner = lobbyController != null && lobbyController.IsOwner();
                    newSlotView.SetSlot(newTeam, newSlot, player, isOwner, OnSlotClicked, OnTransferOwnerClicked);
                }
            }
            else
            {
                // Create new slot if doesn't exist
                CreateSlot(newTeam, newSlot, new List<RoomPlayerResponse> { player });
            }
        }

        /// <summary>
        /// Update button states based on room status and ownership
        /// </summary>
        private void UpdateButtonStates(RoomResponse room)
        {
            if (room == null) return;
            
            bool isOwner = lobbyController != null && lobbyController.IsOwner();
            
            if (startButton != null)
                startButton.gameObject.SetActive(isOwner && room.status == "WAITING");
            
            if (settingsButton != null)
                settingsButton.gameObject.SetActive(isOwner && room.status == "WAITING");
        }

        /// <summary>
        /// Called when user clicks on a slot
        /// Logic:
        /// 1. Slot empty -> Move to this slot
        /// 2. Slot occupied by current player -> Do nothing (should be disabled, but double-check)
        /// 3. Slot occupied by other player -> Send swap request
        /// </summary>
        private async void OnSlotClicked(int team, int slot)
        {
            if (roomService == null || roomState == null || sessionState == null)
                return;

            string key = $"{team}_{slot}";
            if (!slotViews.ContainsKey(key))
                return;

            var slotView = slotViews[key];
            var currentRoom = roomState.CurrentRoom;
            var currentPlayer = currentRoom.players?.FirstOrDefault(p => p.userId == sessionState.UserId);

            if (currentPlayer == null)
                return;

            // Case 1: Slot is empty - move to this slot directly
            if (slotView.IsEmpty)
            {
                await MoveToSlot(team, slot);
                return;
            }

            // Case 2: Slot is occupied by current player - do nothing (should be disabled, but safety check)
            if (slotView.Player.userId == sessionState.UserId)
            {
                Debug.Log("Cannot click on own slot");
                return;
            }

            // Case 3: Slot is occupied by someone else - send swap request
            await SendSwapRequest(team, slot, slotView.Player.userId);
        }

        private bool IsAllSlotsFull(List<RoomPlayerResponse> players)
        {
            int totalSlots = maxSlotsPerTeam * 2; // 2 teams
            int occupiedSlots = players?.Count ?? 0;
            return occupiedSlots >= totalSlots;
        }

        private async Task MoveToSlot(int targetTeam, int targetSlot)
        {
            if (roomService == null || roomState == null)
                return;

            var result = await roomService.ChangeTeam(roomState.RoomId, targetTeam, targetSlot);
            if (result.Success)
            {
                RefreshLobby();
            }
            else
            {
                ShowError($"Failed to move to slot: {result.Message}");
            }
        }

        private Task SwapPositions(int team, int targetSlot, int currentSlot)
        {
            // Swap: move to target slot, which will push the other player to current slot
            return MoveToSlot(team, targetSlot);
        }

        private async Task SendSwapRequest(int targetTeam, int targetSlot, long targetPlayerId)
        {
            if (roomService == null || roomState == null)
                return;

            var result = await roomService.RequestSwap(roomState.RoomId, targetPlayerId, targetTeam, targetSlot);
            
            // Always refresh after action to get latest state
            RefreshLobby();
            
            if (result.Success && result.Data != null)
            {
                // Track our pending swap request
                pendingSwapRequestId = result.Data.requestId;
                
                // Get target username from room players
                string targetUsername = "Player";
                if (roomState != null && roomState.IsInRoom)
                {
                    var targetPlayer = roomState.CurrentRoom.players?.FirstOrDefault(p => p.userId == result.Data.targetUserId);
                    if (targetPlayer != null)
                    {
                        targetUsername = targetPlayer.username;
                    }
                }
                
                // Show cancel popup for requester
                ShowSwapRequestCancel(targetUsername);
                
                // Start 5s timeout
                if (swapRequestTimeoutCoroutine != null)
                {
                    StopCoroutine(swapRequestTimeoutCoroutine);
                }
                swapRequestTimeoutCoroutine = StartCoroutine(SwapRequestTimeoutCoroutine(result.Data.requestId));
            }
            else
            {
                ShowError($"Failed to send swap request: {result.Message}");
            }
        }
        
        private async void OnTransferOwnerClicked(long targetUserId)
        {
            if (lobbyController == null || roomState == null)
                return;
            
            try
            {
                // Confirm with user
                // For now, just transfer directly (can add confirmation popup later)
                var result = await lobbyController.TransferOwner(targetUserId);
                
                // Don't refresh immediately - wait for WebSocket event to update
                // WebSocket will broadcast room_updated event when ownership is transferred
                
                if (result.Success)
                {
                    // Success - WebSocket event will update UI
                    // Just show success message briefly
                    Debug.Log("[LobbyView] Ownership transferred successfully - waiting for WebSocket update");
                }
                else
                {
                    // Show error via notice popup
                    ShowErrorViaNotice(result.Message ?? "Không thể chuyển quyền chủ phòng", result.ErrorCode);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyView] Error in OnTransferOwnerClicked: {ex.Message}");
                ShowErrorViaNotice($"Lỗi khi chuyển quyền chủ phòng: {ex.Message}", null);
            }
        }

        // WebSocket event handlers - Update UI based on specific events
        private void HandleRoomUpdated(RoomResponse room)
        {
            // Room updated - might be settings change, owner change, etc.
            // Update room state first, then refresh UI
            if (roomState != null)
            {
                roomState.SetRoom(room);
            }
            
            // Update room info (code, mode)
            if (room != null)
            {
                if (roomCodeText != null)
                    roomCodeText.text = $"Room Code: {room.roomCode}";
                if (modeText != null)
                    modeText.text = $"Mode: {room.mode}";
                
                // Check owner change
                bool ownerChanged = lastOwnerId.HasValue && lastOwnerId.Value != room.ownerId;
                if (ownerChanged)
                {
                    Debug.Log($"[LobbyView] Owner changed from {lastOwnerId.Value} to {room.ownerId}");
                    UpdateButtonStates(room);
                }
                lastOwnerId = room.ownerId;
            }
            
            // Full refresh only if needed (e.g., settings changed)
            RefreshLobby();
        }
        
        private void HandleForceLogout()
        {
            Debug.LogWarning("[LobbyView] Force logout received from WebSocket");
            
            // Show notice popup
            ShowErrorViaNotice("Tài khoản đã đăng nhập ở nơi khác. Vui lòng đăng nhập lại.", "AUTH_FORCE_LOGOUT");
            
            // Navigate to login scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("02_Login");
        }
        
        private void HandleSessionExpired()
        {
            Debug.LogWarning("[LobbyView] Session expired received from WebSocket");
            
            // Show notice popup
            ShowErrorViaNotice("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", "AUTH_SESSION_EXPIRED");
            
            // Navigate to login scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("02_Login");
        }

        private void HandlePlayerJoined(GameWebSocketService.PlayerJoinedEvent evt)
        {
            // Update room state
            if (evt.room != null && roomState != null)
            {
                roomState.SetRoom(evt.room);
            }
            
            // Add player to UI
            if (evt.room?.players != null)
            {
                var newPlayer = evt.room.players.FirstOrDefault(p => p.userId == evt.userId);
                if (newPlayer != null)
                {
                    AddPlayerSlot(newPlayer);
                }
            }
        }

        private void HandlePlayerLeft(GameWebSocketService.PlayerLeftEvent evt)
        {
            // Update room state
            if (evt.room != null && roomState != null)
            {
                roomState.SetRoom(evt.room);
            }
            
            // Remove player from UI
            RemovePlayerSlot(evt.userId);
        }

        private void HandlePlayerReady(GameWebSocketService.PlayerReadyEvent evt)
        {
            // Update room state
            if (evt.room != null && roomState != null)
            {
                roomState.SetRoom(evt.room);
            }
            
            // Update ready status for specific player
            UpdatePlayerReadyStatus(evt.userId, evt.isReady);
        }

        private void HandleTeamChanged(GameWebSocketService.TeamChangedEvent evt)
        {
            // Update room state
            if (evt.room != null && roomState != null)
            {
                roomState.SetRoom(evt.room);
            }
            
            // Move player to new team/slot
            MovePlayerSlot(evt.userId, evt.team, evt.slot);
        }

        private void HandleRoomStatusChanged(GameWebSocketService.RoomStatusChangedEvent evt)
        {
            // Update room state
            if (evt.room != null && roomState != null)
            {
                roomState.SetRoom(evt.room);
            }
            
            // Check if game started
            if (!string.IsNullOrEmpty(lastStatus) && 
                lastStatus == Constants.ROOM_STATUS_WAITING && 
                evt.status == Constants.ROOM_STATUS_IN_GAME)
            {
                if (!gameStartLogged)
                {
                    Debug.Log($"[LobbyView] Game started! Room {evt.room?.roomCode} (ID: {evt.room?.roomId}, MatchID: {evt.room?.matchId})");
                    gameStartLogged = true;
                    // TODO: Trigger game start event
                }
            }
            lastStatus = evt.status;
            
            // Update button states based on new status
            if (evt.room != null)
            {
                UpdateButtonStates(evt.room);
            }
        }

        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt)
        {
            // Check if we are the target
            if (sessionState != null && evt.targetUserId == sessionState.UserId)
            {
                receivedSwapRequestId = evt.requestId;
                ShowSwapRequest(evt.fromUsername ?? "Unknown");
                
                // Start 5s timeout to auto-reject
                if (receivedSwapRequestTimeoutCoroutine != null)
                {
                    StopCoroutine(receivedSwapRequestTimeoutCoroutine);
                }
                receivedSwapRequestTimeoutCoroutine = StartCoroutine(SwapRequestTargetTimeoutCoroutine(evt.requestId));
            }
        }

        private void HandleSwapRequestStatus(GameWebSocketService.SwapRequestStatusEvent evt)
        {
            // If our request was accepted/rejected/cancelled, hide cancel popup
            if (pendingSwapRequestId.HasValue && evt.requestId == pendingSwapRequestId.Value)
            {
                HideSwapRequestCancel();
                pendingSwapRequestId = null;
                
                if (swapRequestTimeoutCoroutine != null)
                {
                    StopCoroutine(swapRequestTimeoutCoroutine);
                    swapRequestTimeoutCoroutine = null;
                }
            }
            
            // If we received a request and it was accepted/rejected/cancelled, hide popup
            if (receivedSwapRequestId.HasValue && evt.requestId == receivedSwapRequestId.Value)
            {
                HideSwapRequest();
                receivedSwapRequestId = null;
                
                if (receivedSwapRequestTimeoutCoroutine != null)
                {
                    StopCoroutine(receivedSwapRequestTimeoutCoroutine);
                    receivedSwapRequestTimeoutCoroutine = null;
                }
            }
            
            RefreshLobby();
        }
        
        private System.Collections.IEnumerator SwapRequestTimeoutCoroutine(long requestId)
        {
            yield return new WaitForSeconds(5f);
            
            // Auto-cancel after 5s
            if (pendingSwapRequestId.HasValue && pendingSwapRequestId.Value == requestId)
            {
                CancelSwapRequestAsync(requestId);
            }
        }
        
        private async void CancelSwapRequestAsync(long requestId)
        {
            await CancelSwapRequest(requestId);
        }
        
        private System.Collections.IEnumerator SwapRequestTargetTimeoutCoroutine(long requestId)
        {
            yield return new WaitForSeconds(5f);
            
            // Auto-reject swap request after 5s if not responded
            if (receivedSwapRequestId.HasValue && receivedSwapRequestId.Value == requestId)
            {
                // Auto-reject the request
                AutoRejectSwapRequestAsync(requestId);
            }
        }
        
        private async void AutoRejectSwapRequestAsync(long requestId)
        {
            if (roomService == null || roomState == null)
                return;
            
            // Reject the swap request
            await roomService.RejectSwapRequest(roomState.RoomId, requestId);
            
            // Hide popup and clear tracking
            HideSwapRequest();
            receivedSwapRequestId = null;
            
            RefreshLobby();
        }
        
        private async System.Threading.Tasks.Task CancelSwapRequest(long requestId)
        {
            if (roomService == null || roomState == null)
                return;
            
            var result = await roomService.CancelSwapRequest(roomState.RoomId, requestId);
            
            if (result.Success)
            {
                HideSwapRequestCancel();
                pendingSwapRequestId = null;
            }
            
            RefreshLobby();
        }

        private async void OnAcceptSwapRequest()
        {
            if (roomService == null || roomState == null)
                return;

            // Use received swap request ID if available, otherwise fetch from server
            long requestIdToAccept;
            if (receivedSwapRequestId.HasValue)
            {
                requestIdToAccept = receivedSwapRequestId.Value;
            }
            else
            {
                // Fallback: Get pending swap request from server
                var requestsResult = await roomService.GetPendingSwapRequests(roomState.RoomId);
                
                if (!requestsResult.Success || requestsResult.Data == null || requestsResult.Data.Count == 0)
                {
                    HideSwapRequest();
                    return;
                }

                requestIdToAccept = requestsResult.Data[0].requestId;
            }
            
            // Stop timeout coroutine
            if (receivedSwapRequestTimeoutCoroutine != null)
            {
                StopCoroutine(receivedSwapRequestTimeoutCoroutine);
                receivedSwapRequestTimeoutCoroutine = null;
            }
            
            var result = await roomService.AcceptSwapRequest(roomState.RoomId, requestIdToAccept);
            
            if (result.Success)
            {
                HideSwapRequest();
                receivedSwapRequestId = null;
                RefreshLobby();
            }
            else
            {
                ShowError($"Failed to accept swap: {result.Message}");
            }
        }

        private async void OnRejectSwapRequest()
        {
            if (roomService == null || roomState == null)
                return;

            // Use received swap request ID if available, otherwise fetch from server
            long requestIdToReject;
            if (receivedSwapRequestId.HasValue)
            {
                requestIdToReject = receivedSwapRequestId.Value;
            }
            else
            {
                // Fallback: Get pending swap request from server
                var requestsResult = await roomService.GetPendingSwapRequests(roomState.RoomId);
                
                if (!requestsResult.Success || requestsResult.Data == null || requestsResult.Data.Count == 0)
                {
                    HideSwapRequest();
                    return;
                }

                requestIdToReject = requestsResult.Data[0].requestId;
            }
            
            // Stop timeout coroutine
            if (receivedSwapRequestTimeoutCoroutine != null)
            {
                StopCoroutine(receivedSwapRequestTimeoutCoroutine);
                receivedSwapRequestTimeoutCoroutine = null;
            }
            
            await roomService.RejectSwapRequest(roomState.RoomId, requestIdToReject);
            HideSwapRequest();
            receivedSwapRequestId = null;
            
            // Always refresh after action to get latest state
            RefreshLobby();
        }

        public void ShowSwapRequest(string requesterUsername)
        {
            if (swapRequestPanel != null)
            {
                swapRequestPanel.SetActive(true);
                if (swapRequestText != null)
                    swapRequestText.text = $"{requesterUsername} wants to swap positions with you";
            }
        }

        private void HideSwapRequest()
        {
            if (swapRequestPanel != null)
                swapRequestPanel.SetActive(false);
        }
        
        private void ShowSwapRequestCancel(string targetUsername)
        {
            if (swapRequestCancelPanel != null)
            {
                swapRequestCancelPanel.SetActive(true);
                if (swapRequestCancelText != null)
                    swapRequestCancelText.text = $"Waiting for {targetUsername} to accept swap request...";
            }
        }
        
        private void HideSwapRequestCancel()
        {
            if (swapRequestCancelPanel != null)
                swapRequestCancelPanel.SetActive(false);
        }
        
        private async void OnCancelSwapRequest()
        {
            if (pendingSwapRequestId.HasValue)
            {
                await CancelSwapRequest(pendingSwapRequestId.Value);
            }
        }

        private async void OnReadyClicked()
        {
            if (lobbyController == null) return;

            bool currentReady = GetCurrentPlayerReady();
            bool result = await lobbyController.SetReady(!currentReady);
            
            // Always refresh after action to get latest state
            RefreshLobby();
            
            if (!result)
            {
                ShowError("Failed to update ready state");
            }
        }

        private async void OnLeaveClicked()
        {
            if (lobbyController == null) return;

            bool result = await lobbyController.LeaveRoom();
            if (result)
            {
                SceneLoader.LoadHome();
            }
            else
            {
                ShowError("Failed to leave room");
            }
        }

        private async void OnStartClicked()
        {
            if (lobbyController == null) return;

            bool result = await lobbyController.StartGame();
            
            // Always refresh after action to get latest state
            RefreshLobby();
            
            if (!result)
            {
                ShowError("Failed to start game. Make sure all players are ready.");
            }
        }

        private bool GetCurrentPlayerReady()
        {
            if (roomState == null || !roomState.IsInRoom || sessionState == null)
                return false;

            var players = roomState.CurrentRoom.players;
            if (players == null) return false;

            var player = players.FirstOrDefault(p => p.userId == sessionState.UserId);
            return player?.isReady ?? false;
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
            Debug.LogError($"[LobbyView] {message}");
        }
        
        private void ShowErrorViaNotice(string message, string errorCode = null)
        {
            // Show error via notice popup
            var noticePopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.NoticePopup : null;
            if (noticePopup != null)
            {
                noticePopup.Show(
                    title: "Lỗi",
                    message: message,
                    onConfirm: () =>
                    {
                        // Just close the popup
                    },
                    autoDismissSeconds: 4f // Auto dismiss after 4 seconds
                );
            }
            else
            {
                // Fallback: use error text if notice popup not available
                ShowError(message);
            }
        }

        // Room Settings Methods (Owner Only)
        private void OnSettingsClicked()
        {
            if (roomSettingsPanel != null && roomState != null && roomState.IsInRoom)
            {
                var room = roomState.CurrentRoom;
                
                // Populate current settings
                if (modeDropdown != null)
                {
                    modeDropdown.ClearOptions();
                    modeDropdown.AddOptions(new System.Collections.Generic.List<string> { "2v2", "3v3", "5v5" });
                    int modeIndex = room.mode switch
                    {
                        "2v2" => 0,
                        "3v3" => 1,
                        "5v5" => 2,
                        _ => 0
                    };
                    modeDropdown.value = modeIndex;
                }

                if (isPublicToggle != null)
                    isPublicToggle.isOn = room.isPublic;

                if (isLockedToggle != null)
                    isLockedToggle.isOn = room.isLocked;

                if (passwordInput != null)
                {
                    passwordInput.text = "";
                    passwordInput.gameObject.SetActive(room.isLocked);
                }

                roomSettingsPanel.SetActive(true);
            }
        }

        private async void OnSaveSettingsClicked()
        {
            if (roomService == null || roomState == null || !roomState.IsInRoom) return;

            string mode = modeDropdown != null ? modeDropdown.options[modeDropdown.value].text : null;
            bool? isPublic = isPublicToggle != null ? (bool?)isPublicToggle.isOn : null;
            bool? isLocked = isLockedToggle != null ? (bool?)isLockedToggle.isOn : null;
            string password = isLocked == true && passwordInput != null ? passwordInput.text : null;

            var request = new UpdateRoomSettingsRequest
            {
                mode = mode,
                isPublic = isPublic,
                isLocked = isLocked,
                password = password
            };

            var result = await roomService.UpdateRoomSettings(roomState.RoomId, request);
            
            // Always refresh after action to get latest state
            RefreshLobby();
            
            if (result.Success)
            {
                roomSettingsPanel?.SetActive(false);
            }
            else
            {
                // Show error via notice popup for better UX
                ShowErrorViaNotice(result.Message ?? "Không thể cập nhật cài đặt phòng", result.ErrorCode);
            }
        }

        private void OnCancelSettingsClicked()
        {
            if (roomSettingsPanel != null)
                roomSettingsPanel.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (GameManager.Instance != null)
            {
                if (roomService == null)
                    roomService = GameManager.Instance.RoomService;
                if (sessionState == null)
                    sessionState = GameManager.Instance.SessionState;
            }

            if (lobbyController == null)
                lobbyController = FindFirstObjectByType<LobbyController>();

            if (roomState == null)
                roomState = RoomState.Instance;
        }
#endif
    }
}
