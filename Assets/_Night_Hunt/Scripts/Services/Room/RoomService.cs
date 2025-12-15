using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.Services.Game;
using NightHunt.State;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Services.Room
{
    public class RoomService : MonoBehaviour
    {
        [SerializeField] private IBackendClient backendClient;
        [SerializeField] private RoomState roomState;
        
        private void Awake()
        {
            // Always use the shared BackendClient from GameManager to keep auth token
            if (backendClient == null && GameManager.Instance != null)
            {
                backendClient = GameManager.Instance.BackendClient;
            }

            if (backendClient == null)
            {
                backendClient = FindFirstObjectByType<BackendHttpClient>();
            }
            if (roomState == null)
            {
                roomState = RoomState.Instance;
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (GameEventBus.Instance == null)
            {
                return;
            }

            GameEventBus.Instance.OnRoomUpdated += HandleRoomUpdated;
            GameEventBus.Instance.OnPlayerJoined += HandlePlayerJoined;
            GameEventBus.Instance.OnPlayerLeft += HandlePlayerLeft;
            GameEventBus.Instance.OnPlayerReady += HandlePlayerReady;
            GameEventBus.Instance.OnTeamChanged += HandleTeamChanged;
            GameEventBus.Instance.OnRoomStatusChanged += HandleRoomStatusChanged;
            GameEventBus.Instance.OnSwapRequest += HandleSwapRequest;
            GameEventBus.Instance.OnSwapRequestStatus += HandleSwapRequestStatus;
        }

        private void UnsubscribeEvents()
        {
            if (GameEventBus.Instance == null)
            {
                return;
            }

            GameEventBus.Instance.OnRoomUpdated -= HandleRoomUpdated;
            GameEventBus.Instance.OnPlayerJoined -= HandlePlayerJoined;
            GameEventBus.Instance.OnPlayerLeft -= HandlePlayerLeft;
            GameEventBus.Instance.OnPlayerReady -= HandlePlayerReady;
            GameEventBus.Instance.OnTeamChanged -= HandleTeamChanged;
            GameEventBus.Instance.OnRoomStatusChanged -= HandleRoomStatusChanged;
            GameEventBus.Instance.OnSwapRequest -= HandleSwapRequest;
            GameEventBus.Instance.OnSwapRequestStatus -= HandleSwapRequestStatus;
        }

        // Event handlers (from GameEventBus)
        private void HandleRoomUpdated(RoomResponse room)
        {
            roomState?.SetRoom(room);
        }

        private void HandlePlayerJoined(GameWebSocketService.PlayerJoinedEvent evt)
        {
            roomState?.SetRoom(evt.room);
        }

        private void HandlePlayerLeft(GameWebSocketService.PlayerLeftEvent evt)
        {
            roomState?.SetRoom(evt.room);
        }

        private void HandlePlayerReady(GameWebSocketService.PlayerReadyEvent evt)
        {
            roomState?.SetRoom(evt.room);
        }

        private void HandleTeamChanged(GameWebSocketService.TeamChangedEvent evt)
        {
            roomState?.SetRoom(evt.room);
        }

        private void HandleRoomStatusChanged(GameWebSocketService.RoomStatusChangedEvent evt)
        {
            roomState?.SetRoom(evt.room);
        }

        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt)
        {
            // Handle swap request notification
            // UI có thể show notification cho target user
            Debug.Log($"[RoomService] Swap request received: {evt.requestId} from {evt.fromUsername ?? "Unknown"}");
        }
        
        private void HandleSwapRequestStatus(GameWebSocketService.SwapRequestStatusEvent evt)
        {
            // Handle swap request status change (accepted/rejected)
            // Update UI và refresh room state
            Debug.Log($"[RoomService] Swap request {evt.requestId} status: {evt.status}");
            
            // Refresh room state if accepted (team changed)
            if (evt.status == "ACCEPTED" && roomState != null && roomState.IsInRoom)
            {
                // Room state sẽ được update qua team_changed event
                // Hoặc có thể gọi GetRoom() để refresh
            }
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> CreateRoom(CreateRoomRequest request)
        {
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_CREATE, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
                // GameWebSocketService is already connected after login - no need to connect again
            }

            return result;
        }

        // Overload with parameters
        public async Task<ApiResult<RoomResponse>> CreateRoom(string mode, bool isPublic = true, bool isLocked = false, string password = null)
        {
            var request = new CreateRoomRequest
            {
                mode = mode,
                isPublic = isPublic,
                isLocked = isLocked,
                password = password
            };

            return await CreateRoom(request);
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> JoinRoomByCode(JoinRoomRequest request)
        {
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_JOIN_BY_CODE, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        // Overload with parameters
        public async Task<ApiResult<RoomResponse>> JoinByCode(string roomCode, string password = null)
        {
            var request = new JoinRoomRequest
            {
                roomCode = roomCode,
                password = password
            };

            var result = await JoinRoomByCode(request);
            // GameWebSocketService is already connected after login - no need to connect again
            return result;
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> QuickPlay(QuickPlayRequest request)
        {
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_QUICK_PLAY, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
                // GameWebSocketService is already connected after login - no need to connect again
            }

            return result;
        }

        // Overload with mode string
        public async Task<ApiResult<RoomResponse>> QuickPlay(string mode)
        {
            var request = new QuickPlayRequest
            {
                mode = mode
            };

            return await QuickPlay(request);
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> SetReady(long roomId, ReadyRequest request)
        {
            string endpoint = string.Format(Constants.API_ROOMS_READY, roomId);
            var result = await backendClient.PostAsync<RoomResponse>(endpoint, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        // Overload with bool
        public async Task<ApiResult<RoomResponse>> SetReady(long roomId, bool isReady)
        {
            var request = new ReadyRequest
            {
                isReady = isReady
            };

            return await SetReady(roomId, request);
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> ChangeTeam(long roomId, ChangeTeamRequest request)
        {
            string endpoint = string.Format(Constants.API_ROOMS_CHANGE_TEAM, roomId);
            var result = await backendClient.PostAsync<RoomResponse>(endpoint, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        // Overload with parameters
        public async Task<ApiResult<RoomResponse>> ChangeTeam(long roomId, int team, int slot)
        {
            var request = new ChangeTeamRequest
            {
                team = team,
                slot = slot
            };

            return await ChangeTeam(roomId, request);
        }

        public async Task<ApiResult> LeaveRoom(long roomId)
        {
            // Note: GameWebSocketService stays connected (it's session-wide, not room-specific)
            string endpoint = string.Format(Constants.API_ROOMS_LEAVE, roomId);
            var result = await backendClient.PostAsync<object>(endpoint);
            
            if (result.Success)
            {
                roomState.ClearRoom();
            }

            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        public async Task<ApiResult> KickPlayer(long roomId, long playerId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_KICK, roomId, playerId);
            var result = await backendClient.PostAsync<object>(endpoint);
            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        public async Task<ApiResult> DisbandRoom(long roomId)
        {
            // Note: GameWebSocketService stays connected (it's session-wide, not room-specific)
            string endpoint = string.Format(Constants.API_ROOMS_DISBAND, roomId);
            var result = await backendClient.PostAsync<object>(endpoint);
            
            if (result.Success)
            {
                roomState.ClearRoom();
            }

            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        public async Task<ApiResult<RoomResponse>> StartGame(long roomId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_START, roomId);
            var result = await backendClient.PostAsync<RoomResponse>(endpoint);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        public async Task<ApiResult<RoomResponse>> GetRoom(long roomId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_GET, roomId);
            var result = await backendClient.GetAsync<RoomResponse>(endpoint);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        public async Task<ApiResult<RoomResponse>> Reconnect(long? roomId = null)
        {
            if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
            {
                return ApiResult<RoomResponse>.Error("Not authenticated");
            }

            var request = new ReconnectRequest
            {
                accessToken = SessionState.Instance.AccessToken,
                sessionId = SessionState.Instance.SessionId,
                roomId = roomId
            };

            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_RECONNECT, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        // Swap Request methods
        public async Task<ApiResult<SwapRequestDTO>> RequestSwap(long roomId, long targetUserId, int targetTeam, int targetSlot)
        {
            var request = new SwapRequestRequest
            {
                targetUserId = targetUserId,
                targetTeam = targetTeam,
                targetSlot = targetSlot
            };

            string endpoint = string.Format(Constants.API_ROOMS_SWAP_REQUEST, roomId);
            var result = await backendClient.PostAsync<SwapRequestDTO>(endpoint, request);
            return result;
        }

        public async Task<ApiResult<RoomResponse>> AcceptSwapRequest(long roomId, long requestId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_SWAP_ACCEPT, roomId, requestId);
            var result = await backendClient.PostAsync<RoomResponse>(endpoint);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        public async Task<ApiResult> RejectSwapRequest(long roomId, long requestId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_SWAP_REJECT, roomId, requestId);
            var result = await backendClient.PostAsync<object>(endpoint);
            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        public async Task<ApiResult> CancelSwapRequest(long roomId, long requestId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_SWAP_CANCEL, roomId, requestId);
            var result = await backendClient.PostAsync<object>(endpoint);
            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        public async Task<ApiResult<System.Collections.Generic.List<SwapRequestDTO>>> GetPendingSwapRequests(long roomId)
        {
            string endpoint = string.Format(Constants.API_ROOMS_SWAP_REQUESTS, roomId);
            var result = await backendClient.GetAsync<System.Collections.Generic.List<SwapRequestDTO>>(endpoint);
            return result;
        }

        // Update Room Settings (Owner only)
        public async Task<ApiResult<RoomResponse>> UpdateRoomSettings(long roomId, UpdateRoomSettingsRequest request)
        {
            string endpoint = string.Format(Constants.API_ROOMS_UPDATE_SETTINGS, roomId);
            var result = await backendClient.PostAsync<RoomResponse>(endpoint, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        // Convenience methods for updating room settings
        public async Task<ApiResult<RoomResponse>> UpdateRoomMode(long roomId, string mode)
        {
            var request = new UpdateRoomSettingsRequest { mode = mode };
            return await UpdateRoomSettings(roomId, request);
        }

        public async Task<ApiResult<RoomResponse>> UpdateRoomPassword(long roomId, string password)
        {
            var request = new UpdateRoomSettingsRequest { password = password };
            return await UpdateRoomSettings(roomId, request);
        }

        public async Task<ApiResult<RoomResponse>> UpdateRoomVisibility(long roomId, bool isPublic)
        {
            var request = new UpdateRoomSettingsRequest { isPublic = isPublic };
            return await UpdateRoomSettings(roomId, request);
        }

        public async Task<ApiResult<RoomResponse>> UpdateRoomLock(long roomId, bool isLocked, string password = null)
        {
            var request = new UpdateRoomSettingsRequest 
            { 
                isLocked = isLocked,
                password = password
            };
            return await UpdateRoomSettings(roomId, request);
        }

        // Transfer Ownership (Owner only)
        public async Task<ApiResult<RoomResponse>> TransferOwner(long roomId, long targetUserId)
        {
            var request = new TransferOwnerRequest
            {
                targetUserId = targetUserId
            };

            string endpoint = string.Format(Constants.API_ROOMS_TRANSFER_OWNER, roomId);
            var result = await backendClient.PostAsync<RoomResponse>(endpoint, request);
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
            }

            return result;
        }

        // Note: GameWebSocketService is connected after login/auto-login and stays connected throughout the session
        // No need for room-specific WebSocket connections anymore
    }
}

