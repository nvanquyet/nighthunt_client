using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;

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
        [SerializeField] private float maxInteractDistance = 3f; // fallback khi không có LootableConfig
        [SerializeField] private float despawnTime = 300f; // 5 phút

        // Runtime config — inject khi spawn (không gán trên prefab).
        private LootableConfig _lootableConfig;

        // SYNC
        private readonly SyncList<ItemInstanceData> syncStorage = new SyncList<ItemInstanceData>();
        private readonly SyncVar<bool> syncIsOpen = new SyncVar<bool>();

        // Local cache
        private List<ItemInstanceData> storage = new List<ItemInstanceData>();
        private float spawnTime;

        public static event Action<WorldCorpse, NetworkConnection> OnCorpseOpened;

        // ── ILootable ───────────────────────────────────────────────────────────

        public bool IsLooted => storage.Count == 0;
        public bool IsOpen   => syncIsOpen.Value;
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
            if (syncIsOpen.Value || _isOpenPending) return false; // Đã mở hoặc đang chờ server confirm
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor)
        {
            if (_isOpenPending) return;
            var playerNob = interactor?.GetComponent<FishNet.Object.NetworkObject>();
            if (playerNob == null) return;
            _isOpenPending = true;
            RequestOpen(playerNob);
        }

        public void OnHoverEnter(GameObject interactor) { /* TODO: outline */ }
        public void OnHoverExit(GameObject interactor)  { /* TODO: outline */ }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncStorage.OnChange += OnStorageChanged;
            syncIsOpen.OnChange  += (o, n, s) => { if (!s) _isOpenPending = false; }; // Reset khi server confirm
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

            if (Time.time - spawnTime > despawnTime)
            {
                OnDespawned?.Invoke();
                base.Despawn();
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? maxInteractDistance;

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
            if (!IsServerInitialized) { Debug.LogWarning("[WorldCorpse] RequestOpen: server-only!"); return; }
            if (playerNob == null) { Debug.LogWarning("[WorldCorpse] RequestOpen: playerNob là NULL."); return; }
            if (playerNob.Owner != conn) { Debug.LogWarning($"[WorldCorpse] RequestOpen: ownership mismatch (ClientId={conn?.ClientId})."); return; }

            var player = playerNob.GetComponent<NetworkPlayer>();
            if (player == null) { Debug.LogWarning("[WorldCorpse] RequestOpen: không tìm thấy NetworkPlayer."); return; }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist) { Debug.LogWarning($"[WorldCorpse] RequestOpen: Quá xa ({dist:F2}m)"); return; }

            syncIsOpen.Value = true;
            RpcOnCorpseOpened(conn);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestTakeItem(NetworkObject playerNob, int storageIndex, int quantity, NetworkConnection conn = null)
        {
            if (!IsServerInitialized) { Debug.LogWarning("[WorldCorpse] RequestTakeItem: server-only!"); return; }
            if (playerNob == null) { Debug.LogWarning("[WorldCorpse] RequestTakeItem: playerNob là NULL."); return; }
            if (playerNob.Owner != conn) { Debug.LogWarning($"[WorldCorpse] RequestTakeItem: ownership mismatch (ClientId={conn?.ClientId})."); return; }
            if (storageIndex < 0 || storageIndex >= storage.Count) { Debug.LogWarning($"[WorldCorpse] RequestTakeItem: Invalid index {storageIndex}"); return; }

            var itemData  = storage[storageIndex];
            int takeQty   = Mathf.Min(quantity, itemData.Quantity);

            var inventory = playerNob.GetComponent<IInventorySystem>();
            if (inventory == null) { Debug.LogWarning("[WorldCorpse] RequestTakeItem: IInventorySystem not found"); return; }

            inventory.AddItem(itemData.DefinitionID, takeQty);

            if (takeQty >= itemData.Quantity)
            {
                storage.RemoveAt(storageIndex);
                syncStorage.RemoveAt(storageIndex);
            }
            else
            {
                itemData.Quantity -= takeQty;
                storage[storageIndex]     = itemData;
                syncStorage[storageIndex] = itemData;
            }
        }

        private void OnStorageChanged(SyncListOperation op, int index, ItemInstanceData oldValue, ItemInstanceData newValue, bool asServer)
        {
            if (asServer) return;
            switch (op)
            {
                case SyncListOperation.Add:      storage.Add(newValue); break;
                case SyncListOperation.RemoveAt: if (index >= 0 && index < storage.Count) storage.RemoveAt(index); break;
                case SyncListOperation.Set:      if (index >= 0 && index < storage.Count) storage[index] = newValue; break;
                case SyncListOperation.Clear:    storage.Clear(); break;
            }
        }

        [ObserversRpc]
        private void RpcOnCorpseOpened(NetworkConnection viewer) => OnCorpseOpened?.Invoke(this, viewer);

        public IReadOnlyList<ItemInstanceData> GetStorage() => storage;


    }
}
