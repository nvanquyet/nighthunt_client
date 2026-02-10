using FishNet.Object;
using FishNet.Object.Synchronizing;
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
        private readonly SyncVar<PlayerRegistryData> _clientPlayerData = new SyncVar<PlayerRegistryData>(
            new SyncTypeSettings(WritePermission.ClientUnsynchronized)
        );
        
        public PlayerPublicData GetPublicPlayerData() => PlayerPublicData.FromRegistryData(_clientPlayerData.Value);
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            Debug.Log($"Setting up ClientNetworkHandler for local player.");
            // Client gửi data lên server
            SendPlayerDataToServer();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Server listen khi client update data
            _clientPlayerData.OnChange += OnClientDataReceived;
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            
            if (_clientPlayerData != null)
            {
                _clientPlayerData.OnChange -= OnClientDataReceived;
            }
        }
        
        /// <summary>
        /// Client: Gửi data lên server
        /// </summary>
        [Client]
        private void SendPlayerDataToServer()
        {
            PlayerRegistryData data = GetLocalPlayerData();
            
            Debug.Log($"[Client] Sending data to server - Backend ID: {data.BackendPlayerId}, Name: {data.DisplayName}");
            
            // Set value → tự động gửi lên server
            _clientPlayerData.Value = data;
        }
        
        /// <summary>
        /// Server: Nhận data từ client
        /// </summary>
        [Server]
        private void OnClientDataReceived(PlayerRegistryData prev, PlayerRegistryData next, bool asServer)
        {
            if (!asServer) return;
            
            Debug.Log($"[Server] Received client data - Backend ID: {next.BackendPlayerId}, Name: {next.DisplayName}");
            
            // TODO: Validate data
            // bool valid = ValidatePlayerData(next);
            // if (!valid) { Owner.Disconnect(); return; }
            
            // Notify ServerGameManager
            ServerGameManager.Instance.OnClientDataReceived(Owner, next);
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
                TeamId = Random.Range(0,2), 
                Status = PlayerConnectionStatus.Connected
            };
        }
    }
}