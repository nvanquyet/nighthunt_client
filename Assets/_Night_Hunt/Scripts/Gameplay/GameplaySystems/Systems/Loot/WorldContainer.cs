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
    /// Thùng chứa / Crate / Barrel — player có thể mở và loot.
    /// Network-synced: All clients see same contents.
    /// </summary>
    public class WorldContainer : NetworkBehaviour, ILootable, IHoldInteractable
    {
        /// <summary>Fired server-side khi container bị despawn.</summary>
        public event System.Action OnDespawned;

        // Runtime-only fields — inject qua Initialize(), không gán trên prefab.
        private SpawnTable spawnTable;
        private bool isLocked = false;
        private float holdDuration = 1.5f; // fallback khi không có LootableConfig

        // Runtime config — inject từ WorldSpawnManager khi spawn (không gán trên prefab).
        private LootableConfig _lootableConfig;

        // Auto-reset config
        private bool  _autoReset      = false;
        private float _autoResetDelay = 60f;
        private Coroutine _autoResetCoroutine;
        // SYNC
        private readonly SyncList<ItemInstanceData> syncStorage = new SyncList<ItemInstanceData>();
        private readonly SyncVar<bool> syncIsLocked  = new SyncVar<bool>();
        private readonly SyncVar<bool> syncHasRolled = new SyncVar<bool>();
        private readonly SyncVar<bool> syncIsOpen    = new SyncVar<bool>();

        // Local cache
        private List<ItemInstanceData> storage = new List<ItemInstanceData>();
        private bool hasRolled = false;

        public SpawnTable SpawnTable => spawnTable;
        public bool IsLocked   => syncIsLocked.Value;
        public bool IsOpen     => syncIsOpen.Value;
        public bool HasRolled  => syncHasRolled.Value;

        // ── ILootable ───────────────────────────────────────────────────────────

        public bool IsLooted => hasRolled && storage.Count == 0;

        public string InteractLabel
        {
            get
            {
                if (isLocked) return "[E] Locked";
                if (IsLooted) return "[E] Empty";
                if (!IsOpen)  return "[E] Open Container";
                return "[E] Loot Container";
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? 3f;

        // Pending flag: block spam giữa Interact() và server confirm syncIsOpen=true.
        private bool _isOpenPending;

        // ── IHoldInteractable ────────────────────────────────────────────────────

        /// <summary>Giây giữ nút để mở — từ LootableConfig nếu có, fallback về holdDuration field.</summary>
        public float HoldDuration => _lootableConfig?.HoldDuration ?? holdDuration;

        public bool CanInteract(GameObject interactor)
        {
            if (isLocked) return false;
            if (IsOpen || _isOpenPending) return false; // Đã mở hoặc đang chờ server confirm
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

        public void OnHoverEnter(GameObject interactor) { /* outline effect wired up when highlight system is ready */ }
        public void OnHoverExit(GameObject interactor)  { /* outline effect wired up when highlight system is ready */ }

        public static event Action<WorldContainer, NetworkConnection> OnContainerOpened;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncStorage.OnChange  += OnStorageChanged;
            syncIsLocked.OnChange += OnLockedChanged;
            syncHasRolled.OnChange += OnRolledChanged;
            syncIsOpen.OnChange   += OnOpenChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncStorage.OnChange  -= OnStorageChanged;
            syncIsLocked.OnChange -= OnLockedChanged;
            syncHasRolled.OnChange -= OnRolledChanged;
            syncIsOpen.OnChange   -= OnOpenChanged;
            _isOpenPending = false;
        }

        [Server]
        public void Initialize(SpawnTable table, bool locked, LootableConfig lootableConfig = null,
                               bool autoReset = false, float autoResetDelay = 60f)
        {
            spawnTable      = table;
            isLocked        = locked;
            _lootableConfig = lootableConfig;
            _autoReset      = autoReset;
            _autoResetDelay = autoResetDelay;
            syncIsLocked.Value  = locked;
            syncHasRolled.Value = false;
            hasRolled           = false;
            syncIsOpen.Value    = false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestOpen(NetworkObject playerNob, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;
            if (!IsServerInitialized) { Debug.LogWarning("[WorldContainer] RequestOpen: server-only!"); return; }
            if (playerNob == null) { Debug.LogWarning("[WorldContainer] RequestOpen: playerNob là NULL."); return; }
            if (conn == null) { Debug.LogWarning("[WorldContainer] RequestOpen: conn là NULL."); return; }
            if (playerNob.Owner != conn) { Debug.LogWarning($"[WorldContainer] RequestOpen: ownership mismatch (ClientId={conn?.ClientId})."); return; }

            var player = playerNob.GetComponent<NetworkPlayer>();
            if (player == null) { Debug.LogWarning("[WorldContainer] RequestOpen: không tìm thấy NetworkPlayer."); return; }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist) { Debug.LogWarning($"[WorldContainer] RequestOpen: Quá xa ({dist:F2}m)"); return; }

            if (isLocked) { Debug.LogWarning("[WorldContainer] RequestOpen: Container is locked"); return; }

            if (!hasRolled && spawnTable != null && spawnTable.RollOnOpen)
                RollLoot();

            syncIsOpen.Value = true;
            RpcOnContainerOpened(conn);
        }

        [Server]
        private void RollLoot()
        {
            if (spawnTable == null) { Debug.LogWarning("[WorldContainer] RollLoot: SpawnTable is null"); return; }

            var results = spawnTable.Roll();

            if (spawnTable.DropToWorldOnOpen)
            {
                if (WorldSpawnManager.Instance != null)
                    WorldSpawnManager.Instance.SpawnWorldItemsFromResults(results, transform.position, 1.5f);
            }
            else
            {
                foreach (var result in results)
                {
                    if (result.ItemDef == null) continue;
                    var instance = new ItemInstance(result.ItemDef.ItemID, result.Quantity, -1);
                    storage.Add(instance.ToData());
                    syncStorage.Add(instance.ToData());
                }
            }

            hasRolled = true;
            syncHasRolled.Value = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestTakeItem(NetworkObject playerNob, int storageIndex, int quantity, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;
            if (!IsServerInitialized) { Debug.LogWarning("[WorldContainer] RequestTakeItem: server-only!"); return; }
            if (playerNob == null) { Debug.LogWarning("[WorldContainer] RequestTakeItem: playerNob là NULL."); return; }
            if (conn == null) { Debug.LogWarning("[WorldContainer] RequestTakeItem: conn là NULL."); return; }
            if (playerNob.Owner != conn) { Debug.LogWarning($"[WorldContainer] RequestTakeItem: ownership mismatch (ClientId={conn?.ClientId})."); return; }
            if (storageIndex < 0 || storageIndex >= storage.Count) { Debug.LogWarning($"[WorldContainer] RequestTakeItem: Invalid index {storageIndex}"); return; }

            var itemData = storage[storageIndex];
            int takeQty  = Mathf.Min(quantity, itemData.Quantity);

            var inventory = playerNob.GetComponent<IInventorySystem>();
            if (inventory == null) { Debug.LogWarning("[WorldContainer] RequestTakeItem: IInventorySystem not found"); return; }

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

            // Nếu hết đồ và có auto-reset → bắt đầu đếm ngược reset
            if (storage.Count == 0 && _autoReset)
            {
                if (_autoResetCoroutine != null) StopCoroutine(_autoResetCoroutine);
                _autoResetCoroutine = StartCoroutine(AutoResetCoroutine());
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

        private void OnLockedChanged(bool o, bool n, bool s)  { if (!s) isLocked   = n; }
        private void OnRolledChanged(bool o, bool n, bool s)  { if (!s) hasRolled  = n; }
        private void OnOpenChanged(bool o, bool n, bool s)    { if (!s) _isOpenPending = false; } // Server confirm → reset pending

        // ── Auto Reset ────────────────────────────────────────────────────

        /// <summary>
        /// Sau _autoResetDelay giây: reset trạng thái rương → player có thể mở lại,
        /// roll loot mới (SpawnTable sẽ được roll lại lần sau khi RequestOpen).
        /// </summary>
        private System.Collections.IEnumerator AutoResetCoroutine()
        {
            Debug.Log($"[WorldContainer] Auto-reset sau {_autoResetDelay}s...");
            yield return new UnityEngine.WaitForSeconds(_autoResetDelay);

            // Reset state → player có thể mở lại và loot mới
            syncIsOpen.Value    = false;
            syncHasRolled.Value = false;
            hasRolled           = false;
            storage.Clear();
            syncStorage.Clear();

            _autoResetCoroutine = null;
            Debug.Log($"[WorldContainer] Đã reset — sẵn sàng loot lại.");
        }

        [ObserversRpc]
        private void RpcOnContainerOpened(NetworkConnection viewer) => OnContainerOpened?.Invoke(this, viewer);

        public IReadOnlyList<ItemInstanceData> GetStorage() => storage;


    }
}
