using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Custom relay uses UDP and may briefly reconnect a peer under a new FishNet client id.
    /// FishNet normally despawns every object owned by a disconnecting connection; for players
    /// that causes visible spawn/destroy loops. This component strips ownership from player
    /// objects just before FishNet performs that cleanup so ServerGameManager can reassign them
    /// when the same backend user reconnects.
    /// </summary>
    public sealed class RelayOwnedObjectPreserver : MonoBehaviour
    {
        private readonly List<NetworkObject> _ownedObjectsCache = new();
        private ServerManager _serverManager;
        private bool _registered;

        public void Initialize(ServerManager serverManager)
        {
            if (_registered && _serverManager == serverManager)
                return;

            Unregister();
            _serverManager = serverManager;
            Register();
        }

        private void Awake()
        {
            if (_serverManager == null)
                _serverManager = GetComponent<ServerManager>();

            Register();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Register()
        {
            if (_registered || _serverManager == null || _serverManager.Objects == null)
                return;

            _serverManager.Objects.OnPreDestroyClientObjects += OnPreDestroyClientObjects;
            _registered = true;
            Debug.Log("[RelayOwnedObjectPreserver] Registered FishNet disconnect preserve hook.");
        }

        private void Unregister()
        {
            if (!_registered || _serverManager == null || _serverManager.Objects == null)
                return;

            _serverManager.Objects.OnPreDestroyClientObjects -= OnPreDestroyClientObjects;
            _registered = false;
        }

        private void OnPreDestroyClientObjects(NetworkConnection conn)
        {
            if (conn == null)
                return;

            _ownedObjectsCache.Clear();
            foreach (NetworkObject networkObject in conn.Objects)
            {
                if (networkObject != null)
                    _ownedObjectsCache.Add(networkObject);
            }

            int preserved = 0;
            foreach (NetworkObject networkObject in _ownedObjectsCache)
            {
                if (networkObject == null)
                    continue;

                if (networkObject.GetComponentInChildren<NetworkPlayer>(true) == null)
                    continue;

                networkObject.RemoveOwnership(includeNested: true);
                preserved++;
            }

            if (preserved > 0)
            {
                Debug.Log(
                    $"[RelayOwnedObjectPreserver] Preserved {preserved} NetworkPlayer object(s) " +
                    $"for disconnecting clientId={conn.ClientId}.");
            }

            _ownedObjectsCache.Clear();
        }
    }
}
