using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data.DTOs;
using NightHunt.Networking;
using NightHunt.Services.Room;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private RoomService roomService;
        // Note: NetworkBootstrap đã bị xóa, dùng NetworkGameManager thay thế nếu cần disconnect

        private RoomState roomState;

        private void Awake()
        {
            if (roomService == null)
            {
#if UNITY_2023_1_OR_NEWER
                roomService = FindFirstObjectByType<RoomService>();
#else
                roomService = FindObjectOfType<RoomService>();
#endif
            }
            roomState = RoomState.Instance;
        }

        public async Task<bool> CreateRoom(string mode, bool isPublic = true, bool isLocked = false)
        {
            var result = await roomService.CreateRoom(mode, isPublic, isLocked);
            
            if (result.Success && result.Data != null)
            {
                // Headless disabled: no network connect, UI proceeds to waiting
                return true;
            }
            
            return false;
        }

        public async Task<bool> JoinRoom(string roomCode)
        {
            var result = await roomService.JoinByCode(roomCode);
            
            if (result.Success && result.Data != null)
            {
                // Headless disabled: no network connect, UI proceeds to waiting
                return true;
            }
            
            return false;
        }

        public async Task<bool> QuickPlay(string mode)
        {
            var result = await roomService.QuickPlay(mode);
            
            if (result.Success && result.Data != null)
            {
                // Headless disabled: no network connect, UI proceeds to waiting
                return true;
            }
            
            return false;
        }

        public async Task<bool> SetReady(bool isReady)
        {
            if (roomState == null || !roomState.IsInRoom)
            {
                return false;
            }

            var result = await roomService.SetReady(roomState.CurrentRoom.roomId, isReady);
            return result.Success;
        }

        public async Task<bool> ChangeTeam(int team, int slot)
        {
            if (roomState == null || !roomState.IsInRoom)
            {
                return false;
            }

            var result = await roomService.ChangeTeam(roomState.CurrentRoom.roomId, team, slot);
            return result.Success;
        }

        public async Task<bool> LeaveRoom()
        {
            if (roomState == null || !roomState.IsInRoom)
            {
                return false;
            }

            // Disconnect from network if connected
            var networkGameManager = FindFirstObjectByType<NetworkGameManager>();
            if (networkGameManager != null)
            {
                networkGameManager.Disconnect();
            }

            var result = await roomService.LeaveRoom(roomState.CurrentRoom.roomId);
            return result.Success;
        }

        public async Task<bool> StartGame()
        {
            if (roomState == null || !roomState.IsInRoom)
            {
                return false;
            }

            // Only host can start; safety check
            if (!IsOwner())
            {
                Debug.LogWarning("[LobbyController] StartGame called by non-owner");
                return false;
            }

            var result = await roomService.StartGame(roomState.CurrentRoom.roomId);
            return result.Success;
        }

        public async Task<bool> KickPlayer(long playerId)
        {
            if (roomState == null || !roomState.IsInRoom)
            {
                return false;
            }

            var result = await roomService.KickPlayer(roomState.CurrentRoom.roomId, playerId);
            return result.Success;
        }

        public async Task<bool> DisbandRoom()
        {
            if (roomState == null || !roomState.IsInRoom)
            {
                return false;
            }

            // Disconnect from network if connected
            var networkGameManager = FindFirstObjectByType<NetworkGameManager>();
            if (networkGameManager != null)
            {
                networkGameManager.Disconnect();
            }

            var result = await roomService.DisbandRoom(roomState.CurrentRoom.roomId);
            return result.Success;
        }

        public RoomResponse GetCurrentRoom()
        {
            return roomState?.CurrentRoom;
        }

        public async Task<ApiResult<RoomResponse>> TransferOwner(long targetUserId)
        {
            if (roomState == null || !roomState.IsInRoom || roomService == null)
            {
                return ApiResult<RoomResponse>.Error("Not in room or room service not available");
            }

            try
            {
                var result = await roomService.TransferOwner(roomState.CurrentRoom.roomId, targetUserId);
                
                if (result.Success && result.Data != null)
                {
                    // Update room state with new owner
                    roomState.SetRoom(result.Data);
                }
                
                return result;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[LobbyController] Error transferring ownership: {ex.Message}");
                return ApiResult<RoomResponse>.Error($"Error transferring ownership: {ex.Message}");
            }
        }

        public bool IsOwner()
        {
            if (roomState == null || !roomState.IsInRoom || SessionState.Instance == null)
            {
                return false;
            }

            return roomState.CurrentRoom.ownerId == SessionState.Instance.UserId;
        }
    }
}

