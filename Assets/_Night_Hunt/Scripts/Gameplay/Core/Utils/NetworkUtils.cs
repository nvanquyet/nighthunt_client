using FishNet.Object;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Utils
{
    /// <summary>
    /// Network helper functions
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// Check if object is owned by local client
        /// </summary>
        public static bool IsLocalOwner(NetworkBehaviour networkBehaviour)
        {
            return networkBehaviour != null && networkBehaviour.IsOwner;
        }

        /// <summary>
        /// Check if running on server
        /// </summary>
        public static bool IsServer(NetworkBehaviour networkBehaviour)
        {
            return networkBehaviour != null && networkBehaviour.IsServer;
        }

        /// <summary>
        /// Check if running on client
        /// </summary>
        public static bool IsClient(NetworkBehaviour networkBehaviour)
        {
            return networkBehaviour != null && networkBehaviour.IsClient;
        }

        /// <summary>
        /// Validate network object is spawned
        /// </summary>
        public static bool IsSpawned(NetworkBehaviour networkBehaviour)
        {
            return networkBehaviour != null && networkBehaviour.IsSpawned;
        }

        /// <summary>
        /// Validate operation can be performed
        /// </summary>
        public static bool ValidateOperation(NetworkBehaviour networkBehaviour, bool requireOwner = false, bool requireServer = false)
        {
            if (networkBehaviour == null)
            {
                Debug.LogWarning("[NetworkUtils] NetworkBehaviour is null");
                return false;
            }

            if (!networkBehaviour.IsSpawned)
            {
                Debug.LogWarning("[NetworkUtils] NetworkBehaviour is not spawned");
                return false;
            }

            if (requireOwner && !networkBehaviour.IsOwner)
            {
                Debug.LogWarning("[NetworkUtils] Operation requires ownership");
                return false;
            }

            if (requireServer && !networkBehaviour.IsServer)
            {
                Debug.LogWarning("[NetworkUtils] Operation requires server");
                return false;
            }

            return true;
        }
    }
}

