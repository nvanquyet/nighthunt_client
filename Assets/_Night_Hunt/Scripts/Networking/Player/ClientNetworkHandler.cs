using FishNet.Object;
using NightHunt.Networking.Player;
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

            // TODO: Validate data
            // bool valid = ValidatePlayerData(data);
            // if (!valid) { Owner.Disconnect(); return; }

            // Notify ServerGameManager
            ServerGameManager.Instance.OnClientDataReceived(Owner, data);
        }

        /// <summary>
        /// TODO: Get player data từ local storage hoặc backend
        /// </summary>
        private PlayerRegistryData GetLocalPlayerData()
        {
            // TODO: Implement
            // string token = PlayerPrefs.GetString("AuthToken");
            // string backendId = await AuthService.ValidateToken(token);
            // PlayerRegistryData data = await BackendAPI.GetPlayerData(backendId);
            // return data;

            // Mock data for testing
            return new PlayerRegistryData
            {
                BackendPlayerId = $"backend_{Random.Range(1000, 9999)}",
                DisplayName = $"Player_{Random.Range(1, 100)}",
                TeamId = Random.Range(0, 2),
                Status = PlayerConnectionStatus.Connected
            };
        }
    }
}