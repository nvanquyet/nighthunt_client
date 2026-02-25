using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Container loot source - chest, crate, etc.
    /// Network-synced: All clients see same contents.
    ///
    /// DESIGN (SRP / ISP):
    ///   Implements ILootable (extends IInteractable) so RaycastDetector and
    ///   ProximityInteractScanner never need to know this concrete type.
    /// </summary>
    public class ContainerLootSource : NetworkBehaviour, ILootable
    {
        [Header("Config")]
        [SerializeField] private SpawnTable spawnTable;
        [SerializeField] private bool isLocked = false;
        [SerializeField] private float holdDuration = 1.5f;

        // SYNC: Storage contents (server-authoritative)
        private readonly SyncList<ItemInstanceData> syncStorage = new SyncList<ItemInstanceData>();

        // SYNC: State flags
        private readonly SyncVar<bool> syncIsLocked = new SyncVar<bool>();
        private readonly SyncVar<bool> syncHasRolled = new SyncVar<bool>();
        private readonly SyncVar<bool> syncIsOpen = new SyncVar<bool>();

        // Local cache
        private List<ItemInstanceData> storage = new List<ItemInstanceData>();
        private bool hasRolled = false;

        public SpawnTable SpawnTable => spawnTable;
        public bool IsLocked => syncIsLocked.Value;
        public bool IsOpen => syncIsOpen.Value;
        public bool HasRolled => syncHasRolled.Value;

        // ── ILootable / IInteractable ───────────────────────────────────────────

        /// <inheritdoc/>
        public bool IsLooted
        {
            get
            {
                if (!hasRolled) return false;
                return storage.Count == 0;
            }
        }

        /// <inheritdoc/>
        public string InteractLabel
        {
            get
            {
                if (isLocked)  return "[E] Locked";
                if (IsLooted)  return "[E] Empty";
                if (!IsOpen)   return "[E] Open Container";
                return "[E] Loot Container";
            }
        }

        /// <inheritdoc/>
        public bool CanInteract(GameObject interactor)
        {
            if (isLocked) return false;
            float dist = Vector3.Distance(transform.position, interactor.transform.position);
            return dist <= 3f;
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

        public static event Action<ContainerLootSource, NetworkConnection> OnContainerOpened;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncStorage.OnChange += OnStorageChanged;
            syncIsLocked.OnChange += OnLockedChanged;
            syncHasRolled.OnChange += OnRolledChanged;
            syncIsOpen.OnChange += OnOpenChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncStorage.OnChange -= OnStorageChanged;
            syncIsLocked.OnChange -= OnLockedChanged;
            syncHasRolled.OnChange -= OnRolledChanged;
            syncIsOpen.OnChange -= OnOpenChanged;
        }

        /// <summary>
        /// SERVER: Initialize container
        /// </summary>
        [Server]
        public void Initialize(SpawnTable table, bool locked)
        {
            spawnTable = table;
            isLocked = locked;
            syncIsLocked.Value = locked;
            syncHasRolled.Value = false;
            hasRolled = false;
            syncIsOpen.Value = false;
        }

        /// <summary>
        /// SERVER: Open request (hold or instant).
        /// <c>conn</c> is optional — FishNet auto-fills when called without argument.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestOpen(NetworkConnection conn = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[ContainerLootSource] RequestOpen: server-only!");
                return;
            }

            var player = GetPlayerFromConnection(conn);
            if (player == null) return;

            // Validate distance
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > 3f)
            {
                Debug.LogWarning($"[ContainerLootSource] RequestOpen: Too far ({dist:F2}m)");
                return;
            }

            // Check lock
            if (isLocked)
            {
                // TODO: Check key/quest/unlock logic
                Debug.LogWarning("[ContainerLootSource] RequestOpen: Container is locked");
                return;
            }

            // Roll nếu chưa roll
            if (!hasRolled && spawnTable != null && spawnTable.RollOnOpen)
            {
                RollLoot();
            }

            // Open (broadcast to all clients)
            syncIsOpen.Value = true;

            // Notify UI system (ObserversRpc)
            RpcOnContainerOpened(conn);
        }

        [Server]
        private void RollLoot()
        {
            if (spawnTable == null)
            {
                Debug.LogWarning("[ContainerLootSource] RollLoot: SpawnTable is null");
                return;
            }

            var results = spawnTable.Roll();

            if (spawnTable.DropToWorldOnOpen)
            {
                // Spawn ra world thay vì giữ trong storage
                if (WorldDropManager.Instance != null)
                {
                    WorldDropManager.Instance.SpawnWorldPickupsFromResults(results, transform.position, 1.5f);
                }
            }
            else
            {
                // Fill storage
                foreach (var result in results)
                {
                    if (result.ItemDef == null) continue;

                    var instance = new ItemInstance(result.ItemDef.ItemID, result.Quantity, -1);
                    storage.Add(instance.ToData());
                    syncStorage.Add(instance.ToData()); // Broadcast
                }
            }

            hasRolled = true;
            syncHasRolled.Value = true;
        }

        /// <summary>
        /// SERVER: Take item từ container
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestTakeItem(NetworkConnection conn, int storageIndex, int quantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[ContainerLootSource] RequestTakeItem: server-only!");
                return;
            }

            if (storageIndex < 0 || storageIndex >= storage.Count)
            {
                Debug.LogWarning($"[ContainerLootSource] RequestTakeItem: Invalid index {storageIndex}");
                return;
            }

            var itemData = storage[storageIndex];
            int takeQty = Mathf.Min(quantity, itemData.Quantity);

            // Get player inventory
            var player = GetPlayerFromConnection(conn);
            var inventory = player?.GetComponent<IInventorySystem>();
            if (inventory == null)
            {
                Debug.LogWarning("[ContainerLootSource] RequestTakeItem: IInventorySystem not found");
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

        private void OnLockedChanged(bool oldValue, bool newValue, bool asServer)
        {
            if (asServer) return;
            isLocked = newValue;
        }

        private void OnRolledChanged(bool oldValue, bool newValue, bool asServer)
        {
            if (asServer) return;
            hasRolled = newValue;
        }

        private void OnOpenChanged(bool oldValue, bool newValue, bool asServer)
        {
            if (asServer) return;
            // UI sẽ react to this change
        }

        [ObserversRpc]
        private void RpcOnContainerOpened(NetworkConnection viewer)
        {
            // Notify UI system để mở loot panel
            OnContainerOpened?.Invoke(this, viewer);
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
