using System;
using UnityEngine;
using FishNet;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Core.Networking.Reconnection
{
    /// <summary>
    /// Main reconnection manager for handling network reconnection
    /// </summary>
    public class ReconnectionManager : MonoBehaviour
    {
        private static ReconnectionManager _instance;
        public static ReconnectionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("ReconnectionManager");
                    _instance = go.AddComponent<ReconnectionManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private ReconnectionState savedState;
        private NetworkReconnectionHandler reconnectionHandler;
        private bool isReconnecting = false;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            reconnectionHandler = new NetworkReconnectionHandler();
        }

        private void OnEnable()
        {
            // Subscribe to network events
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDisable()
        {
            // Unsubscribe from network events
            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
        }

        /// <summary>
        /// Handle client connection state changes
        /// </summary>
        private void OnClientConnectionState(FishNet.Transporting.ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
            {
                // Connection lost, save state
                SaveCurrentState();
            }
            else if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started && isReconnecting)
            {
                // Reconnected, restore state
                RestoreState();
                isReconnecting = false;
            }
        }

        /// <summary>
        /// Save current game state for reconnection
        /// </summary>
        public void SaveCurrentState()
        {
            savedState = StateSnapshot.CreateSnapshot();
            Debug.Log("[ReconnectionManager] State saved for reconnection");
        }

        /// <summary>
        /// Restore state after reconnection
        /// </summary>
        public void RestoreState()
        {
            if (savedState == null)
            {
                Debug.LogWarning("[ReconnectionManager] No saved state to restore");
                return;
            }

            StateSnapshot.RestoreSnapshot(savedState);
            Debug.Log("[ReconnectionManager] State restored after reconnection");
        }

        /// <summary>
        /// Attempt to reconnect
        /// </summary>
        public void AttemptReconnect(string serverAddress, ushort port)
        {
            if (isReconnecting)
            {
                Debug.LogWarning("[ReconnectionManager] Already attempting to reconnect");
                return;
            }

            isReconnecting = true;
            reconnectionHandler.AttemptReconnect(serverAddress, port);
        }

        /// <summary>
        /// Check if currently reconnecting
        /// </summary>
        public bool IsReconnecting => isReconnecting;

        /// <summary>
        /// Get saved state
        /// </summary>
        public ReconnectionState GetSavedState() => savedState;
    }
}

