using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Xác chết player — chứa toàn bộ item của player khi chết.
    /// Network-synced: All clients see same contents.
    /// </summary>
    public class WorldCorpse : NetworkBehaviour, ILootable
    {
        /// <summary>Fired server-side khi corpse bị despawn (looted hoặc hết thời gian).</summary>
        public event System.Action OnDespawned;

        [Header("Settings")]
        [FormerlySerializedAs("maxInteractDistance")]
        [SerializeField] private float _maxInteractDistance = 3f; // fallback khi not available LootableConfig

        [FormerlySerializedAs("despawnTime")]
        [SerializeField] private float _despawnTime = 300f; // 5 phút

        // Runtime config — inject on spawn (không gán trên prefab).
        private LootableConfig _lootableConfig;

        // SYNC
        private readonly SyncList<ItemInstanceData> syncStorage = new SyncList<ItemInstanceData>();
        private readonly SyncVar<bool> syncIsOpen = new SyncVar<bool>();

        // Local cache
        private List<ItemInstanceData> storage = new List<ItemInstanceData>();
        private float spawnTime;

        public static event Action<WorldCorpse, NetworkConnection> OnCorpseOpened;

        /// <summary>Fired on the local client when the player's raycast enters this corpse.</summary>
        public static event Action<WorldCorpse> OnAnyHoverEnter;

        /// <summary>Fired on the local client when the player's raycast leaves this corpse.</summary>
        public static event Action<WorldCorpse> OnAnyHoverExit;

        /// <summary>Fired on clients whenever the synced storage list changes (items added/removed).</summary>
        public event Action OnClientStorageChanged;

        // ── ILootable ───────────────────────────────────────────────────────────

        public bool IsLooted => storage.Count == 0;

        public bool IsOpen => syncIsOpen.Value;

        // Pending flag: block spam giữa Interact() và server confirm syncIsOpen=true.
        private bool _isOpenPending;

        public string InteractLabel
        {
            get
            {
                int count = storage.Count;
                return count == 0 ? "[E] Empty Corpse" : $"[E] Loot Corpse ({count} item{(count == 1 ? "" : "s")})";
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (storage.Count == 0) return false;
            if (!IsOpen && _isOpenPending) return false; // Đang chờ server confirm — chặn spam
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor)
        {
            var playerNob = ComponentResolver.Find<FishNet.Object.NetworkObject>(interactor)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] FishNet.Object.NetworkObject not found")
                .Resolve();
            if (playerNob == null) return;

            if (IsOpen)
            {
                // Corpse đã mở — re-show loot UI locally.
                Debug.Log($"[WorldCorpse] Interact: already open, re-showing loot UI. Items={storage.Count}");
                OnCorpseOpened?.Invoke(this, playerNob.Owner);
                return;
            }

            if (_isOpenPending) return;
            _isOpenPending = true;
            RequestOpen(playerNob);
        }

        public void OnHoverEnter(GameObject interactor)
        {
            if (IsOpen && storage.Count > 0)
            {
                Debug.Log($"[WorldCorpse] OnHoverEnter: corpse open — auto-showing loot UI. Items={storage.Count}");
                OnAnyHoverEnter?.Invoke(this);
            }
        }

        public void OnHoverExit(GameObject interactor)
        {
            Debug.Log("[WorldCorpse] OnHoverExit — hiding loot UI if open.");
            OnAnyHoverExit?.Invoke(this);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncStorage.OnChange += OnStorageChanged;
            syncIsOpen.OnChange += (o, n, s) =>
            {
                if (!s) _isOpenPending = false;
            }; // Reset khi server confirm
            spawnTime = Time.time;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncStorage.OnChange -= OnStorageChanged;
            _isOpenPending = false;
        }

        private void Update()
        {
            if (!IsServerInitialized) return;

            if (storage.Count == 0 && Time.time - spawnTime > 10f)
            {
                OnDespawned?.Invoke();
                base.Despawn();
                return;
            }

            if (Time.time - spawnTime > (_lootableConfig != null ? _lootableConfig.DespawnTime : _despawnTime))
            {
                OnDespawned?.Invoke();
                base.Despawn();
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? _maxInteractDistance;

        [Server]
        public void Initialize(List<ItemInstanceData> items, LootableConfig lootableConfig = null)
        {
            storage = new List<ItemInstanceData>(items);
            _lootableConfig = lootableConfig;
            foreach (var item in items)
                syncStorage.Add(item);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestOpen(NetworkObject playerNob, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldCorpse] RequestOpen: server-only!");
                return;
            }

            if (playerNob == null)
            {
                Debug.LogWarning("[WorldCorpse] RequestOpen: playerNob là NULL.");
                return;
            }

            if (conn == null)
            {
                Debug.LogWarning("[WorldCorpse] RequestOpen: conn là NULL.");
                return;
            }

            if (playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldCorpse] RequestOpen: ownership mismatch (ClientId={conn?.ClientId}).");
                return;
            }

            var player = ComponentResolver.Find<NetworkPlayer>(playerNob)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkPlayer not found")
                .Resolve();
            if (player == null)
            {
                Debug.LogWarning("[WorldCorpse] RequestOpen: not found NetworkPlayer.");
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldCorpse] RequestOpen: Quá xa ({dist:F2}m)");
                return;
            }

            syncIsOpen.Value = true;
            RpcOnCorpseOpened(conn);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestTakeItem(NetworkObject playerNob, int storageIndex, int quantity,
            NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;
            Debug.Log(
                $"[WorldCorpse] RequestTakeItem: arrived — objId={playerNob?.ObjectId} idx={storageIndex} qty={quantity} conn={conn?.ClientId} storage.Count={storage.Count}");
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldCorpse] RequestTakeItem: server-only!");
                return;
            }

            if (playerNob == null)
            {
                Debug.LogWarning("[WorldCorpse] RequestTakeItem: playerNob là NULL.");
                return;
            }

            if (conn == null)
            {
                Debug.LogWarning("[WorldCorpse] RequestTakeItem: conn là NULL.");
                return;
            }

            if (playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldCorpse] RequestTakeItem: ownership mismatch (ClientId={conn?.ClientId}).");
                return;
            }

            if (storageIndex < 0 || storageIndex >= storage.Count)
            {
                Debug.LogWarning($"[WorldCorpse] RequestTakeItem: Invalid index {storageIndex}");
                return;
            }

            var itemData = storage[storageIndex];
            int takeQty = Mathf.Min(quantity, itemData.Quantity);

            var inventory = ComponentResolver.Find<IInventorySystem>(playerNob)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IInventorySystem not found")
                .Resolve();
            if (inventory == null)
            {
                Debug.LogWarning("[WorldCorpse] RequestTakeItem: IInventorySystem not found");
                return;
            }

            // Preserve full runtime state (ammo, durability, attachments) when looting.
            // Build a copy of the data with the quantity actually taken, then restore it.
            var takenData = itemData;
            takenData.Quantity = takeQty;
            // Use a fresh InstanceID so the looted item is independent of any remaining stack.
            takenData.InstanceID = System.Guid.NewGuid().ToString();
            inventory.AddItemFromData(takenData);

            if (takeQty >= itemData.Quantity)
            {
                storage.RemoveAt(storageIndex);
                syncStorage.RemoveAt(storageIndex);
            }
            else
            {
                itemData.Quantity -= takeQty;
                storage[storageIndex] = itemData;
                syncStorage[storageIndex] = itemData;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized) return;

            storage.Clear();
            for (int i = 0; i < syncStorage.Count; i++)
                storage.Add(syncStorage[i]);

            Debug.Log(
                $"[WorldCorpse] OnStartClient: storage rebuilt from syncStorage. Count={storage.Count} ObjId={ObjectId}");
        }

        private void OnStorageChanged(SyncListOperation op, int index, ItemInstanceData oldValue,
            ItemInstanceData newValue, bool asServer)
        {
            if (asServer) return; // server-side callback — storage managed directly

            if (!IsServerInitialized)
            {
                // Pure client: mirror the SyncList operation into the local cache.
                switch (op)
                {
                    case SyncListOperation.Add: storage.Add(newValue); break;
                    case SyncListOperation.RemoveAt:
                        if (index >= 0 && index < storage.Count) storage.RemoveAt(index);
                        break;
                    case SyncListOperation.Set:
                        if (index >= 0 && index < storage.Count) storage[index] = newValue;
                        break;
                    case SyncListOperation.Clear: storage.Clear(); break;
                }
            }

            // Always notify — host storage was already updated by server code directly.
            OnClientStorageChanged?.Invoke();
        }

        [ObserversRpc]
        private void RpcOnCorpseOpened(NetworkConnection viewer) => OnCorpseOpened?.Invoke(this, viewer);

        public IReadOnlyList<ItemInstanceData> GetStorage() => storage;
    }
}