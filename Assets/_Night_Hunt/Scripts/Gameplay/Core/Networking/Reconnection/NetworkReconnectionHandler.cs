using UnityEngine;
using FishNet;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Core.Networking.Reconnection
{
    /// <summary>
    /// Handles FishNet reconnection
    /// </summary>
    public class NetworkReconnectionHandler
    {
        /// <summary>
        /// Attempt to reconnect to server
        /// </summary>
        public void AttemptReconnect(string serverAddress, ushort port)
        {
            var networkManager = NetworkGameManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[NetworkReconnectionHandler] NetworkGameManager not found");
                return;
            }

            Debug.Log($"[NetworkReconnectionHandler] Attempting to reconnect to {serverAddress}:{port}");
            networkManager.StartClient(serverAddress);
        }

        /// <summary>
        /// Check if connected
        /// </summary>
        public bool IsConnected()
        {
            return InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started;
        }
    }
}

