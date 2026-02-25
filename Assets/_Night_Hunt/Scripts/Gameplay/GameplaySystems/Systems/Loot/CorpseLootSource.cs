using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Corpse loot source - player death loot.
    /// Network-synced: All clients see same contents.
    ///
    /// DESIGN (SRP / ISP):
    ///   Implements IInteractable so RaycastDetector and ProximityInteractScanner
    ///   work through the interface without coupling to this concrete type.
    /// </summary>
    public class CorpseLootSource : NetworkBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private float maxInteractDistance = 3f;
        [SerializeField] private float despawnTime = 300f; // 5 minutes

        // SYNC: Storage contents (server-authoritative)
        private readonly SyncList<ItemInstanceData> syncStorage = new SyncList<ItemInstanceData>();

        // Local cache
        private List<ItemInstanceData> storage = new List<ItemInstanceData>();
        private float spawnTime;

        public static event Action<CorpseLootSource, NetworkConnection> OnCorpseOpened;

        // ── IInteractable ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string InteractLabel
        {
            get
            {
                int count = storage.Count;
                return count == 0 ? "[E] Empty Corpse" : $"[E] Loot Corpse ({count} item{(count == 1 ? "" : "s")})";
            }
        }

        /// <inheritdoc/>
        public bool CanInteract(GameObject interactor)
        {
            if (storage.Count == 0) return false;
            float dist = Vector3.Distance(transform.position, interactor.transform.position);
            return dist <= maxInteractDistance;
        }

        /// <inheritdoc/>
        /// <remarks>Client calls this → fires <see cref="RequestOpen"/> as a ServerRpc.</remarks>
        public void Interact(GameObject interactor)
        {
            RequestOpen();
        }

        /// <inheritdoc/>
        public void OnHoverEnter(GameObject interactor)
        {
            // TODO: enable outline / highlight renderer effect
        }

        /// <inheritdoc/>
        public void OnHoverExit(GameObject interactor)
        {
            // TODO: disable outline / highlight renderer effect
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncStorage.OnChange += OnStorageChanged;
            spawnTime = Time.time;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncStorage.OnChange -= OnStorageChanged;
        }

        private void Update()
        {
            if (!IsServerInitialized) return;

            // Auto-despawn when empty
            if (storage.Count == 0 && Time.time - spawnTime > 10f)
            {
                base.Despawn();
                return;
            }

            // Auto-despawn after time limit
            if (Time.time - spawnTime > despawnTime)
            {
                base.Despawn();
                return;
            }
        }

        /// <summary>
        /// SERVER: Initialize corpse with items
        /// </summary>
        [Server]
        public void Initialize(List<ItemInstanceData> items)
        {
            storage = new List<ItemInstanceData>(items);

            // Broadcast to all clients
            foreach (var item in items)
                syncStorage.Add(item);
        }

        /// <summary>
        /// SERVER: Open request (instant — no hold needed).
        /// <c>conn</c> is optional — FishNet auto-fills when called without argument.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestOpen(NetworkConnection conn = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[CorpseLootSource] RequestOpen: server-only!");
                return;
            }

            var player = GetPlayerFromConnection(conn);
            if (player == null) return;

            // Validate distance
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > maxInteractDistance)
            {
                Debug.LogWarning($"[CorpseLootSource] RequestOpen: Too far ({dist:F2}m)");
                return;
            }

            // Open (broadcast)
            RpcOnCorpseOpened(conn);
        }

        /// <summary>
        /// SERVER: Take item từ corpse
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestTakeItem(NetworkConnection conn, int storageIndex, int quantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[CorpseLootSource] RequestTakeItem: server-only!");
                return;
            }

            if (storageIndex < 0 || storageIndex >= storage.Count)
            {
                Debug.LogWarning($"[CorpseLootSource] RequestTakeItem: Invalid index {storageIndex}");
                return;
            }

            var itemData = storage[storageIndex];
            int takeQty = Mathf.Min(quantity, itemData.Quantity);

            // Get player inventory
            var player = GetPlayerFromConnection(conn);
            var inventory = player?.GetComponent<IInventorySystem>();
            if (inventory == null)
            {
                Debug.LogWarning("[CorpseLootSource] RequestTakeItem: IInventorySystem not found");
                return;
            }

            // Add to inventory
            inventory.AddItem(itemData.DefinitionID, takeQty);

            // Remove from storage
            if (takeQty >= itemData.Quantity)
            {
                storage.RemoveAt(storageIndex);
                syncStorage.RemoveAt(storageIndex); // Broadcast
            }
            else
            {
                itemData.Quantity -= takeQty;
                storage[storageIndex] = itemData;
                syncStorage[storageIndex] = itemData; // Broadcast
            }
        }

        /// <summary>
        /// CLIENT: Nhận sync
        /// </summary>
        private void OnStorageChanged(SyncListOperation op, int index, ItemInstanceData oldValue, ItemInstanceData newValue, bool asServer)
        {
            if (asServer) return;

            switch (op)
            {
                case SyncListOperation.Add:
                    storage.Add(newValue);
                    break;
                case SyncListOperation.RemoveAt:
                    if (index >= 0 && index < storage.Count)
                        storage.RemoveAt(index);
                    break;
                case SyncListOperation.Set:
                    if (index >= 0 && index < storage.Count)
                        storage[index] = newValue;
                    break;
                case SyncListOperation.Clear:
                    storage.Clear();
                    break;
            }
        }

        [ObserversRpc]
        private void RpcOnCorpseOpened(NetworkConnection viewer)
        {
            // Notify UI system để mở loot panel
            OnCorpseOpened?.Invoke(this, viewer);
        }

        /// <summary>
        /// Get storage contents (read-only)
        /// </summary>
        public IReadOnlyList<ItemInstanceData> GetStorage()
        {
            return storage;
        }

        /// <summary>
        /// Helper: Get player from connection
        /// </summary>
        private NetworkPlayer GetPlayerFromConnection(NetworkConnection conn)
        {
            var identity = conn.FirstObject;
            if (identity == null) return null;
            return identity.GetComponent<NetworkPlayer>();
        }
    }
}
