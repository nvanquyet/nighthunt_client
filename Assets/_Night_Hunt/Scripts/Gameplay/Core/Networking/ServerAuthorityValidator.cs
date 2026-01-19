using FishNet.Object;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Networking
{
    /// <summary>
    /// Validates server authority for operations
    /// </summary>
    public static class ServerAuthorityValidator
    {
        /// <summary>
        /// Check if operation should be executed on server
        /// </summary>
        public static bool IsServerAuthoritative(NetworkBehaviour networkBehaviour)
        {
            if (networkBehaviour == null)
            {
                Debug.LogWarning("[ServerAuthorityValidator] NetworkBehaviour is null");
                return false;
            }

            return networkBehaviour.IsServer;
        }

        /// <summary>
        /// Validate and execute server-only operation
        /// </summary>
        public static bool ValidateAndExecute(NetworkBehaviour networkBehaviour, System.Action operation)
        {
            if (!IsServerAuthoritative(networkBehaviour))
            {
                Debug.LogWarning($"[ServerAuthorityValidator] Operation requires server authority. IsServer: {networkBehaviour.IsServer}");
                return false;
            }

            operation?.Invoke();
            return true;
        }

        /// <summary>
        /// Validate server authority and return result
        /// </summary>
        public static T ValidateAndExecute<T>(NetworkBehaviour networkBehaviour, System.Func<T> operation, T defaultValue = default)
        {
            if (!IsServerAuthoritative(networkBehaviour))
            {
                Debug.LogWarning($"[ServerAuthorityValidator] Operation requires server authority. IsServer: {networkBehaviour.IsServer}");
                return defaultValue;
            }

            return operation != null ? operation.Invoke() : defaultValue;
        }
    }
}

