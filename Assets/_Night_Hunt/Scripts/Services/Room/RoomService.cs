using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data;
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
            SetRoomIfRelevant(room, "room_updated");
        }

        private void HandlePlayerJoined(GameWebSocketService.PlayerJoinedEvent evt)
        {
            SetRoomIfRelevant(evt.room, "player_joined");
        }

        private void HandlePlayerLeft(GameWebSocketService.PlayerLeftEvent evt)
        {
            SetRoomIfRelevant(evt.room, "player_left");
        }

        private void HandlePlayerReady(GameWebSocketService.PlayerReadyEvent evt)
        {
            SetRoomIfRelevant(evt.room, "player_ready");
        }

        private void HandleTeamChanged(GameWebSocketService.TeamChangedEvent evt)
        {
            SetRoomIfRelevant(evt.room, "team_changed");
        }

        private void HandleRoomStatusChanged(GameWebSocketService.RoomStatusChangedEvent evt)
        {
            SetRoomIfRelevant(evt.room, "room_status_changed");
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

        private void SetRoomIfRelevant(RoomResponse room, string source)
        {
            if (room == null)
            {
                Debug.LogWarning($"[RoomService] Ignoring {source}: payload room is null.");
                return;
            }

            if (room.roomId <= 0)
            {
                Debug.LogWarning($"[RoomService] Ignoring {source}: payload roomId is invalid ({room.roomId}).");
                return;
            }

            if (roomState == null)
                roomState = RoomState.Instance;

            if (roomState == null || !roomState.IsInRoom || roomState.RoomId <= 0)
            {
                long localUserId = GameManager.Instance?.SessionState?.UserId ?? 0L;
                bool payloadContainsLocalPlayer = false;
                if (localUserId > 0L && room.players != null)
                {
                    foreach (var player in room.players)
                    {
                        if (player.userId != localUserId) continue;
                        payloadContainsLocalPlayer = true;
                        break;
                    }
                }

                if (payloadContainsLocalPlayer)
                {
                    // Never adopt a terminal room — it belongs to a past session.
                    if (string.Equals(room.status, Constants.ROOM_STATUS_CLOSED, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(room.status, "FINISHED", StringComparison.OrdinalIgnoreCase))
                    {
                        RLog($"Ignoring {source}: terminal room payload (roomId={room.roomId} status={room.status}). Not adopting.");
                        return;
                    }

                    RLog($"Adopting {source} room payload as active room: roomId={room.roomId}");
                    roomState?.SetRoom(room);
                    return;
                }

                Debug.LogWarning($"[RoomService] Ignoring {source}: no active current room.");
                return;
            }

            if (room.roomId != roomState.RoomId)
            {
                Debug.LogWarning($"[RoomService] Ignoring {source}: roomId={room.roomId} does not match current roomId={roomState.RoomId}.");
                return;
            }

            roomState.SetRoom(room);
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> CreateRoom(CreateRoomRequest request)
        {
            if (backendClient == null)
            {
                RLog($"CreateRoom blocked: backendClient is null. local={DescribeLocalRoom()}");
                return ApiResult<RoomResponse>.Error("Backend client is not ready");
            }

            RLog(
                $"CreateRoom request mode={request?.mode ?? "null"} mapId={request?.mapId ?? "null"} allowFill={request?.allowFill} " +
                $"public={request?.isPublic} locked={request?.isLocked} local={DescribeLocalRoom()}");
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_CREATE, request);
            RLog(
                $"CreateRoom response success={result?.Success} errorCode={result?.ErrorCode ?? "null"} message='{result?.Message ?? "null"}' " +
                $"room={DescribeRoom(result?.Data)} localBeforeSet={DescribeLocalRoom()}");

            if (result == null)
                return ApiResult<RoomResponse>.Error("Create room returned no response");
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
                RLog($"CreateRoom local state set. local={DescribeLocalRoom()}");
                // GameWebSocketService is already connected after login - no need to connect again
            }

            return result;
        }

        // Overload with parameters
        public async Task<ApiResult<RoomResponse>> CreateRoom(string mode, bool allowFill = true, bool isPublic = true, bool isLocked = false, string password = null, string mapId = null)
        {
            var request = new CreateRoomRequest
            {
                mode      = mode,
                allowFill = allowFill,
                mapId     = mapId,
                isPublic  = isPublic,
                isLocked  = isLocked,
                password  = password
            };

            return await CreateRoom(request);
        }

        // Overload with DTO
        public async Task<ApiResult<RoomResponse>> JoinRoomByCode(JoinRoomRequest request)
        {
            if (backendClient == null)
            {
                RLog($"JoinRoomByCode blocked: backendClient is null. code={request?.roomCode ?? "null"} local={DescribeLocalRoom()}");
                return ApiResult<RoomResponse>.Error("Backend client is not ready");
            }

            RLog($"JoinRoomByCode request code={request?.roomCode ?? "null"} passwordSet={!string.IsNullOrEmpty(request?.password)} local={DescribeLocalRoom()}");
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_JOIN_BY_CODE, request);
            RLog(
                $"JoinRoomByCode response success={result?.Success} errorCode={result?.ErrorCode ?? "null"} message='{result?.Message ?? "null"}' " +
                $"room={DescribeRoom(result?.Data)} localBeforeSet={DescribeLocalRoom()}");

            if (result == null)
                return ApiResult<RoomResponse>.Error("Join room returned no response");
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
                RLog($"JoinRoomByCode local state set. local={DescribeLocalRoom()}");
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
            if (backendClient == null)
            {
                RLog($"QuickPlay blocked: backendClient is null. local={DescribeLocalRoom()}");
                return ApiResult<RoomResponse>.Error("Backend client is not ready");
            }

            RLog($"QuickPlay request mode={request?.mode ?? "null"} mapId={request?.mapId ?? "null"} allowFill={request?.allowFill} local={DescribeLocalRoom()}");
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_QUICK_PLAY, request);
            RLog(
                $"QuickPlay response success={result?.Success} errorCode={result?.ErrorCode ?? "null"} message='{result?.Message ?? "null"}' " +
                $"room={DescribeRoom(result?.Data)} localBeforeSet={DescribeLocalRoom()}");

            if (result == null)
                return ApiResult<RoomResponse>.Error("Quick play returned no response");
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
                RLog($"QuickPlay local state set. local={DescribeLocalRoom()}");
                // GameWebSocketService is already connected after login - no need to connect again
            }

            return result;
        }

        // Overload with mode string
        public async Task<ApiResult<RoomResponse>> QuickPlay(string mode, bool allowFill = true, string mapId = null)
        {
            var request = new QuickPlayRequest
            {
                mode      = mode,
                allowFill = allowFill,
                mapId     = mapId
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
            RLog($"LeaveRoom request roomId={roomId} local={DescribeLocalRoom()}");
            var result = await backendClient.PostAsync<object>(endpoint);
            RLog($"LeaveRoom response success={result?.Success} errorCode={result?.ErrorCode ?? "null"} message='{result?.Message ?? "null"}' localBeforeClear={DescribeLocalRoom()}");

            if (result == null)
                return ApiResult.Error("Leave room returned no response");
            
            if (result.Success)
            {
                roomState.ClearRoom();
                RLog($"LeaveRoom local state cleared. local={DescribeLocalRoom()}");
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
            RLog($"DisbandRoom request roomId={roomId} local={DescribeLocalRoom()}");
            var result = await backendClient.PostAsync<object>(endpoint);
            RLog($"DisbandRoom response success={result?.Success} errorCode={result?.ErrorCode ?? "null"} message='{result?.Message ?? "null"}' localBeforeClear={DescribeLocalRoom()}");

            if (result == null)
                return ApiResult.Error("Disband room returned no response");
            
            if (result.Success)
            {
                roomState.ClearRoom();
                RLog($"DisbandRoom local state cleared. local={DescribeLocalRoom()}");
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
                RLog($"Reconnect blocked: not authenticated. roomId={roomId?.ToString() ?? "null"} local={DescribeLocalRoom()}");
                return ApiResult<RoomResponse>.Error("Not authenticated");
            }

            var request = new ReconnectRequest
            {
                accessToken = SessionState.Instance.AccessToken,
                sessionId = SessionState.Instance.SessionId,
                roomId = roomId.HasValue && roomId.Value > 0 ? roomId.Value : 0L
            };

            RLog($"Reconnect request roomId={roomId?.ToString() ?? "null"} payloadRoomId={request.roomId} local={DescribeLocalRoom()}");
            var result = await backendClient.PostAsync<RoomResponse>(Constants.API_ROOMS_RECONNECT, request);
            RLog(
                $"Reconnect response success={result?.Success} errorCode={result?.ErrorCode ?? "null"} message='{result?.Message ?? "null"}' " +
                $"room={DescribeRoom(result?.Data)} localBeforeSet={DescribeLocalRoom()}");

            if (result == null)
                return ApiResult<RoomResponse>.Error("Reconnect returned no response");
            
            if (result.Success && result.Data != null)
            {
                roomState.SetRoom(result.Data);
                RLog($"Reconnect local state set. local={DescribeLocalRoom()}");
            }
            else if (!result.Success && roomState != null && roomState.IsInRoom)
            {
                // Only clear local room state when the server definitively says the room is gone
                // (ROOM_NOT_FOUND / 404).  Transient server errors (INTERNAL_ERROR / 500) and
                // network failures do NOT confirm the room is absent — evicting state on those
                // would cause race-condition bugs where a valid in-progress room is wiped.
                bool isDefinitiveNotFound =
                    string.Equals(result.ErrorCode, ErrorCodes.ROOM_NOT_FOUND, StringComparison.OrdinalIgnoreCase);

                if (isDefinitiveNotFound)
                {
                    RLog($"Reconnect: room not found ({result.ErrorCode}) — clearing stale local room state. local={DescribeLocalRoom()}");
                    roomState.ClearRoom();
                }
                else
                {
                    RLog($"Reconnect: transient failure ({result.ErrorCode ?? "null"} / '{result.Message ?? "null"}') — keeping local room state. local={DescribeLocalRoom()}");
                }
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
        private void RLog(string message)
        {
            Debug.Log($"[FLOW][ROOM_API] {message}");
        }

        private string DescribeLocalRoom()
        {
            if (roomState == null)
                roomState = RoomState.Instance;

            if (roomState == null)
                return "roomState=null";

            return
                $"isInRoom={roomState.IsInRoom},roomId={roomState.RoomId},code={roomState.RoomCode},status={roomState.Status},players={roomState.PlayerCount}";
        }

        private static string DescribeRoom(RoomResponse room)
        {
            if (room == null)
                return "null";

            int players = room.players != null ? room.players.Count : 0;
            return
                $"id={room.roomId},code={room.roomCode},mode={room.mode},map={room.mapId},status={room.status},owner={room.ownerId},players={players}";
        }
    }
}

