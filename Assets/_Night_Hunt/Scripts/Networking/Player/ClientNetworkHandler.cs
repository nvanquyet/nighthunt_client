using FishNet.Object;
using NightHunt.Common;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Networking.Player;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Client handler - gửi player data lên server khi connect
    /// </summary>
    public class ClientNetworkHandler : NetworkBehaviour
    {
        // ===== CLIENT SENDS DATA TO SERVER =====
        // Cache the data locally so the server can query it after receiving via RPC
        private PlayerRegistryData _cachedPlayerData;

        public PlayerPublicData GetPublicPlayerData() => PlayerPublicData.FromRegistryData(_cachedPlayerData);

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            Debug.Log($"Setting up ClientNetworkHandler for local player.");
            // Client gửi data lên server qua ServerRpc
            SendPlayerDataToServer();
        }

        /// <summary>
        /// Client: Gửi data lên server bằng ServerRpc
        /// SyncVar với ClientUnsynchronized KHÔNG truyền dữ liệu lên server - phải dùng ServerRpc
        /// </summary>
        [Client]
        private void SendPlayerDataToServer()
        {
            PlayerRegistryData data = GetLocalPlayerData();

            Debug.Log(
                $"[Client] Sending data to server - Backend ID: {data.BackendPlayerId}, Name: {data.DisplayName}");

            // Gửi lên server bằng ServerRpc
            RpcSendPlayerData(data);
        }

        /// <summary>
        /// ServerRpc: Chạy trên server, được gọi bởi client owner
        /// </summary>
        [ServerRpc]
        private void RpcSendPlayerData(PlayerRegistryData data)
        {
            _cachedPlayerData = data;

            Debug.Log($"[Server] Received client data - Backend ID: {data.BackendPlayerId}, Name: {data.DisplayName}");

            // Basic null guard — payload is validated server-side in ServerGameManager

            // Notify ServerGameManager
            ServerGameManager.Instance.OnClientDataReceived(Owner, data);
        }

        /// <summary>
        /// Reads player identity from the active SessionState (set during login / auto-login).
        /// Falls back to a guest entry when no authenticated session is present.
        ///
        /// CHARACTER RESOLUTION FLOW:
        ///   1. Character-select screen saves the backend string ID (e.g. "character_02")
        ///      to PlayerPrefs key "SelectedCharacterId".
        ///   2. GetLocalPlayerData() reads the string and resolves it to an array index
        ///      via CharacterDatabase.GetIndexById().
        ///   3. The resolved int is sent to the server as CharacterModelIndex.
        ///   4. If the string is missing or unknown, falls back to index 0 (default skin).
        /// </summary>
        private PlayerRegistryData GetLocalPlayerData()
        {
            var session = SessionState.Instance;

            // Resolve selected character: string ID from character-select screen → array index
            string savedCharacterId = PlayerPrefs.GetString(Constants.PREFS_SELECTED_CHARACTER_ID, "");
            int characterModelIndex = 0;

            if (!string.IsNullOrEmpty(savedCharacterId) && CharacterDatabase.Instance != null)
            {
                int resolved = CharacterDatabase.Instance.GetIndexById(savedCharacterId);
                if (resolved >= 0)
                    characterModelIndex = resolved;
                else
                    Debug.LogWarning($"[ClientNetworkHandler] Unknown character ID '{savedCharacterId}' — " +
                                     "falling back to index 0 (default skin).");
            }

            if (session != null && session.IsAuthenticated)
            {
                return new PlayerRegistryData
                {
                    BackendPlayerId     = session.UserId.ToString(),
                    DisplayName         = !string.IsNullOrEmpty(session.Username)
                                              ? session.Username
                                              : $"Player_{session.UserId}",
                    // TeamId = -1: yêu cầu server tự gán team qua load-balancing (GetSmallestTeam).
                    // Không được hardcode 0 vì ResolveTeam chấp nhận 0 là valid → load-balancing bị bỏ qua.
                    // Khi có matchmaking / team-select screen, backend sẽ truyền teamId thực qua SessionState.
                    TeamId              = -1,
                    Status              = PlayerConnectionStatus.Connected,
                    CharacterModelIndex = characterModelIndex
                };
            }

            Debug.LogWarning("[ClientNetworkHandler] No authenticated session found — using fallback guest data.");
            return new PlayerRegistryData
            {
                BackendPlayerId     = $"guest_{Random.Range(1000, 9999)}",
                DisplayName         = $"Guest_{Random.Range(1, 100)}",
                TeamId              = -1, // -1 = auto-assign by server load-balancing
                Status              = PlayerConnectionStatus.Connected,
                CharacterModelIndex = characterModelIndex
            };
        }
    }
}