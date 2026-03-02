using FishNet.Object;
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
        /// </summary>
        private PlayerRegistryData GetLocalPlayerData()
        {
            var session = SessionState.Instance;
            if (session != null && session.IsAuthenticated)
            {
                return new PlayerRegistryData
                {
                    BackendPlayerId = session.UserId.ToString(),
                    DisplayName     = !string.IsNullOrEmpty(session.Username)
                                          ? session.Username
                                          : $"Player_{session.UserId}",
                    TeamId          = 0,
                    Status          = PlayerConnectionStatus.Connected
                };
            }

            Debug.LogWarning("[ClientNetworkHandler] No authenticated session found — using fallback guest data.");
            return new PlayerRegistryData
            {
                BackendPlayerId = $"guest_{Random.Range(1000, 9999)}",
                DisplayName     = $"Guest_{Random.Range(1, 100)}",
                TeamId          = 0,
                Status          = PlayerConnectionStatus.Connected
            };
        }
    }
}