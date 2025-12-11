using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Lobby;
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

        [Header("Swap Request")]
        [SerializeField] private GameObject swapRequestPanel;
        [SerializeField] private TextMeshProUGUI swapRequestText;
        [SerializeField] private Button acceptSwapButton;
        [SerializeField] private Button rejectSwapButton;

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
        
        // Auto-refresh polling
        [Header("Auto-Refresh")]
        [SerializeField] private float refreshInterval = 1f; // Refresh every 1 second
        private float lastRefreshTime = 0f;
        private bool isPolling = false;
        
        // Track owner ID to detect changes
        private long? lastOwnerId = null;
        
        // Track room status to detect game start
        private string lastStatus = null;
        private bool gameStartLogged = false; // Flag to ensure log only once

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

            if (swapRequestPanel != null)
                swapRequestPanel.SetActive(false);

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
            RefreshLobby();
            StartPolling();
        }

        private void OnDisable()
        {
            StopPolling();
        }

        /// <summary>
        /// Show lobby view (for panel overlay mode)
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            RefreshLobby();
            StartPolling();
        }

        /// <summary>
        /// Hide lobby view (for panel overlay mode)
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            StopPolling();
        }

        private void StartPolling()
        {
            isPolling = true;
            lastRefreshTime = Time.time;
        }

        private void StopPolling()
        {
            isPolling = false;
        }

        private void Update()
        {
            // Auto-refresh lobby periodically
            if (isPolling && Time.time - lastRefreshTime >= refreshInterval)
            {
                RefreshLobby();
                CheckPendingSwapRequests();
                lastRefreshTime = Time.time;
            }
        }

        public void RefreshLobby()
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
            
            if (result.Success)
            {
                ShowError("Swap request sent. Waiting for response...");
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
            
            // Confirm with user
            // For now, just transfer directly (can add confirmation popup later)
            bool result = await lobbyController.TransferOwner(targetUserId);
            
            // Always refresh after action to get latest state
            RefreshLobby();
            
            if (result)
            {
                ShowError("Ownership transferred successfully");
            }
            else
            {
                ShowError("Failed to transfer ownership");
            }
        }

        private async void CheckPendingSwapRequests()
        {
            if (roomService == null || roomState == null || sessionState == null)
                return;

            var result = await roomService.GetPendingSwapRequests(roomState.RoomId);
            
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                // Show swap request popup for the first pending request
                var request = result.Data[0];
                ShowSwapRequest(request.requesterUsername);
            }
        }

        private async void OnAcceptSwapRequest()
        {
            if (roomService == null || roomState == null)
                return;

            // Get pending swap request
            var requestsResult = await roomService.GetPendingSwapRequests(roomState.RoomId);
            
            if (!requestsResult.Success || requestsResult.Data == null || requestsResult.Data.Count == 0)
            {
                HideSwapRequest();
                return;
            }

            var request = requestsResult.Data[0]; // Get first pending request
            
            var result = await roomService.AcceptSwapRequest(roomState.RoomId, request.requestId);
            
            if (result.Success)
            {
                HideSwapRequest();
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

            // Get pending swap request
            var requestsResult = await roomService.GetPendingSwapRequests(roomState.RoomId);
            
            if (!requestsResult.Success || requestsResult.Data == null || requestsResult.Data.Count == 0)
            {
                HideSwapRequest();
                return;
            }

            var request = requestsResult.Data[0]; // Get first pending request
            
            await roomService.RejectSwapRequest(roomState.RoomId, request.requestId);
            HideSwapRequest();
            
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
                ShowError($"Failed to update settings: {result.Message}");
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
