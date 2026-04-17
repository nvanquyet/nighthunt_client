using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Thùng chứa / Crate / Barrel — player có thể mở và loot.
    /// Network-synced: All clients see same contents.
    ///
    /// Uses InitializeBeforeSpawn() (gọi TRƯỚC ServerManager.Spawn)
    /// instead of Initialize() (gọi sau Spawn) — tương tự WorldItem.
    /// </summary>
    public class WorldContainer : NetworkBehaviour, ILootable, IHoldInteractable
    {
        /// <summary>Fired server-side khi container bị despawn.</summary>
        public event System.Action OnDespawned;
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        // Runtime-only — inject qua InitializeBeforeSpawn()
        private SpawnTable spawnTable;
        private bool isLocked = false;
        private float holdDuration = 1.5f;
        private LootableConfig _lootableConfig;
        private bool _autoReset = false;
        private float _autoResetDelay = 60f;

        private Coroutine _autoResetCoroutine;

        // Items pending scatter to world on open (DropToWorldOnOpen=true)
        private List<SpawnResult> _pendingDropResults;

        // ── SyncVars ──────────────────────────────────────────────────────────────
        private readonly SyncList<ItemInstanceData> syncStorage = new SyncList<ItemInstanceData>();
        private readonly SyncVar<bool> syncIsLocked = new SyncVar<bool>();
        private readonly SyncVar<bool> syncHasRolled = new SyncVar<bool>();
        private readonly SyncVar<bool> syncIsOpen = new SyncVar<bool>();

        // ── Local cache ───────────────────────────────────────────────────────────
        private List<ItemInstanceData> storage = new List<ItemInstanceData>();
        private bool hasRolled = false;

        // ── Properties ────────────────────────────────────────────────────────────
        public SpawnTable SpawnTable => spawnTable;
        public bool IsLocked => syncIsLocked.Value;
        public bool IsOpen => syncIsOpen.Value;
        public bool HasRolled => syncHasRolled.Value;

        // ── ILootable ─────────────────────────────────────────────────────────────

        public bool IsLooted => hasRolled && storage.Count == 0;

        public string InteractLabel
        {
            get
            {
                if (isLocked) return "[E] Locked";
                if (IsLooted) return "[E] Empty";
                if (!IsOpen) return HoldDuration > 0 ? "[Hold E] Open Container" : "[E] Open Container";
                return "[E] Loot Container";
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? 3f;

        private bool _isOpenPending;

        // ── IHoldInteractable ─────────────────────────────────────────────────────

        // When container is already open, show loot UI instantly (no hold needed).
        public float HoldDuration => IsOpen ? 0f : (_lootableConfig?.HoldDuration ?? holdDuration);

        public bool CanInteract(GameObject interactor)
        {
            if (isLocked) return false;
            if (IsLooted) return false;
            if (!IsOpen && _isOpenPending) return false; // Đang chờ server confirm open — chặn spam
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[WorldContainer] Interact: IsOpen={IsOpen} isLocked={isLocked} _isOpenPending={_isOpenPending} storage={storage.Count}");
            var playerNob = ComponentResolver.Find<NetworkObject>(interactor)
                .OnSelf()
                .InParent()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            if (playerNob == null) return;

            if (IsOpen)
            {
                // Container đã mở — re-show loot UI locally, không cần server call.
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldContainer] Interact: already open, re-showing loot UI. Items={storage.Count}");
                OnContainerOpened?.Invoke(this, playerNob.Owner);
                return;
            }

            if (_isOpenPending) return;
            _isOpenPending = true;
            RequestOpen(playerNob);
        }

        /// <summary>Fired on the local client whenever the player's raycast enters this container.</summary>
        public static event Action<WorldContainer> OnAnyHoverEnter;

        /// <summary>Fired on the local client whenever the player's raycast leaves this container.</summary>
        public static event Action<WorldContainer> OnAnyHoverExit;

        public void OnHoverEnter(GameObject interactor)
        {
            if (IsOpen && !IsLooted)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log(
                    $"[WorldContainer] OnHoverEnter: container open — auto-showing loot UI. Items={storage.Count}");
                OnAnyHoverEnter?.Invoke(this);
            }
        }

        public void OnHoverExit(GameObject interactor)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log("[WorldContainer] OnHoverExit — hiding loot UI if open.");
            OnAnyHoverExit?.Invoke(this);
        }

        public static event Action<WorldContainer, NetworkConnection> OnContainerOpened;

        /// <summary>Fired on clients whenever the synced storage list changes (items added/removed).</summary>
        public event Action OnClientStorageChanged;

        // ── Network Lifecycle ─────────────────────────────────────────────────────

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
            _isOpenPending = false;
            if (IsServerInitialized)
                OnDespawned?.Invoke();
        }

        // ── Server API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Call TRƯỚC ServerManager.Spawn() để SyncVar embedded in spawn packet.
        /// Tương tự WorldItem.InitializeBeforeSpawn() — xem comment ở đó.
        ///
        /// LƯU Ý: KHÔNG ĐƯỢC dùng [Server] attribute VÀ KHÔNG guard NetworkManager ở đây!
        /// Xem WorldItem.InitializeBeforeSpawn để biết lý do.
        /// </summary>
        public void InitializeBeforeSpawn(
            SpawnTable table,
            bool locked,
            LootableConfig lootableConfig = null,
            bool autoReset = false,
            float autoResetDelay = 60f)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldContainer] ── InitializeBeforeSpawn ENTRY ── locked={locked}");

            spawnTable = table;
            isLocked = locked;
            _lootableConfig = lootableConfig;
            _autoReset = autoReset;
            _autoResetDelay = autoResetDelay;
            hasRolled = false;
            _pendingDropResults = null;

            // Set SyncVars TRƯỚC Spawn để embed vào spawn packet
            syncIsLocked.Value = locked;
            syncIsOpen.Value = false;

            // Roll items ngay lúc spawn — không đợi player mở (mặc định initialize luôn)
            RollLootInternal();

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[WorldContainer] InitializeBeforeSpawn: locked={locked} autoReset={autoReset} storage={storage.Count} ObjId={ObjectId}");
        }

        // ── ServerRpc: Open ───────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestOpen(NetworkObject playerNob, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldContainer] RequestOpen: server-only!");
                return;
            }

            if (playerNob == null)
            {
                Debug.LogWarning("[WorldContainer] RequestOpen: playerNob NULL.");
                RpcOnOpenRejected(conn);
                return;
            }

            if (conn == null)
            {
                Debug.LogWarning("[WorldContainer] RequestOpen: conn NULL.");
                return;
            }

            if (playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldContainer] RequestOpen: ownership mismatch ClientId={conn?.ClientId}.");
                RpcOnOpenRejected(conn);
                return;
            }

            var player = ComponentResolver.Find<NetworkPlayer>(playerNob)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkPlayer not found")
                .Resolve();
            if (player == null)
            {
                Debug.LogWarning("[WorldContainer] RequestOpen: NetworkPlayer not found.");
                RpcOnOpenRejected(conn);
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > GetInteractDistance())
            {
                Debug.LogWarning($"[WorldContainer] RequestOpen: quá xa ({dist:F2}m).");
                RpcOnOpenRejected(conn);
                return;
            }

            if (isLocked)
            {
                Debug.LogWarning("[WorldContainer] RequestOpen: container bị khóa.");
                RpcOnOpenRejected(conn);
                return;
            }

            // Items has been roll tại InitializeBeforeSpawn → không cần roll lại.
            // Nếu DropToWorldOnOpen=true: scatter pending items ra world khi lần đầu mở.
            if (_pendingDropResults != null && _pendingDropResults.Count > 0)
            {
                if (WorldSpawnManager.Instance != null)
                    WorldSpawnManager.Instance.SpawnWorldItemsFromResults(_pendingDropResults, transform.position,
                        1.5f);
                else
                    Debug.LogError("[WorldContainer] WorldSpawnManager.Instance is NULL — " +
                                   $"{_pendingDropResults.Count} drop results LOST! Ensure WorldSpawnManager exists in scene.");
                _pendingDropResults = null;
            }

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[WorldContainer] RequestOpen: opening ObjId={ObjectId} storage={storage.Count} items → ClientId={conn?.ClientId}");
            syncIsOpen.Value = true;
            RpcOnContainerOpened(conn);
        }

        /// <summary>
        /// Server → requesting client: server từ chối mở container.
        /// Reset _isOpenPending để client có thể thử lại (vd. vừa hết tầm, nay lại gần).
        /// </summary>
        [TargetRpc]
        private void RpcOnOpenRejected(NetworkConnection conn)
        {
            _isOpenPending = false;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log("[WorldContainer] RpcOnOpenRejected: _isOpenPending reset — có thể thử lại.");
        }

        /// <summary>
        /// Roll loot từ SpawnTable và lưu vào storage.
        /// KHÔNG có [Server] attribute — gọi được từ InitializeBeforeSpawn (trước IsServerInitialized).
        /// </summary>
        private void RollLootInternal()
        {
            if (spawnTable == null)
            {
                Debug.LogWarning("[WorldContainer] RollLootInternal: SpawnTable là NULL — container sẽ empty!");
                hasRolled = true;
                syncHasRolled.Value = true;
                return;
            }

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[WorldContainer] RollLootInternal: START SpawnTable='{spawnTable.name}' Mode={spawnTable.Mode} DropToWorld={spawnTable.DropToWorldOnOpen}");

            if (spawnTable.DropToWorldOnOpen)
            {
                _pendingDropResults = spawnTable.Roll();
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log(
                    $"[WorldContainer] RollLootInternal: DropToWorldOnOpen=true — {_pendingDropResults.Count} item(s) sẽ scatter khi mở.");
            }
            else
            {
                var results = spawnTable.Roll();
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldContainer] RollLootInternal: Rolled {results.Count} item(s):");
                foreach (var result in results)
                {
                    if (result.ItemDef == null)
                    {
                        Debug.LogWarning("[WorldContainer] RollLootInternal:   ✗ ItemDef null — bỏ qua!");
                        continue;
                    }

                    var instance = new ItemInstance(result.ItemDef.ItemID, result.Quantity, -1);
                    var data = instance.ToData();
                    storage.Add(data);
                    syncStorage.Add(data);
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log(
                        $"[WorldContainer] RollLootInternal:   + '{result.ItemDef.ItemID}' ({result.ItemDef.DisplayName}) x{result.Quantity}");
                }

                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldContainer] RollLootInternal: DONE — storage.Count={storage.Count}");
            }

            hasRolled = true;
            syncHasRolled.Value = true;
        }

        [Server]
        private void RollLoot() => RollLootInternal();

        // ── ServerRpc: Take Item ──────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestTakeItem(NetworkObject playerNob, int storageIndex, int quantity,
            NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;
            Debug.Log(
                $"[WorldContainer] RequestTakeItem: arrived — objId={playerNob?.ObjectId} idx={storageIndex} qty={quantity} conn={conn?.ClientId} storage.Count={storage.Count}");
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldContainer] RequestTakeItem: server-only!");
                return;
            }

            if (playerNob == null)
            {
                Debug.LogWarning("[WorldContainer] RequestTakeItem: playerNob NULL.");
                return;
            }

            if (conn == null)
            {
                Debug.LogWarning("[WorldContainer] RequestTakeItem: conn NULL.");
                return;
            }

            if (playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldContainer] RequestTakeItem: ownership mismatch ClientId={conn?.ClientId}.");
                return;
            }

            if (storageIndex < 0 || storageIndex >= storage.Count)
            {
                Debug.LogWarning($"[WorldContainer] RequestTakeItem: invalid index {storageIndex}.");
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
                Debug.LogWarning("[WorldContainer] RequestTakeItem: IInventorySystem not found.");
                return;
            }

            inventory.AddItem(itemData.DefinitionID, takeQty);

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

            if (storage.Count == 0 && _autoReset)
            {
                if (_autoResetCoroutine != null) StopCoroutine(_autoResetCoroutine);
                _autoResetCoroutine = StartCoroutine(AutoResetCoroutine());
            }
        }

        // ── SyncVar / SyncList Callbacks ──────────────────────────────────────────

        /// <summary>
        /// Rebuild client-side storage cache from syncStorage after spawn packet is fully applied.
        /// Called by FishNet AFTER InvokeSyncTypeOnStartCallbacks, so syncStorage is already
        /// populated with whatever the spawn packet delivered.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized) return; // Host: server manages storage directly

            // Authoritative rebuild — overrides anything OnStorageChanged may have written
            // during the initial InvokeSyncTypeOnStartCallbacks pass.
            storage.Clear();
            for (int i = 0; i < syncStorage.Count; i++)
                storage.Add(syncStorage[i]);

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[WorldContainer] OnStartClient: storage rebuilt from syncStorage. Count={storage.Count} ObjId={ObjectId}");
        }

        private void OnStorageChanged(SyncListOperation op, int index,
            ItemInstanceData oldValue, ItemInstanceData newValue, bool asServer)
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

            // Always notify — host storage was already updated by server code directly;
            // pure-client storage was just mirrored above. UI needs a refresh in both cases.
            OnClientStorageChanged?.Invoke();
        }

        private void OnLockedChanged(bool o, bool n, bool s)
        {
            if (!s) isLocked = n;
        }

        private void OnRolledChanged(bool o, bool n, bool s)
        {
            if (!s) hasRolled = n;
        }

        private void OnOpenChanged(bool o, bool n, bool s)
        {
            if (!s) _isOpenPending = false;
        }

        // ── Auto Reset ────────────────────────────────────────────────────────────

        private IEnumerator AutoResetCoroutine()
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldContainer] Auto-reset sau {_autoResetDelay}s... ObjId={ObjectId}");
            yield return new WaitForSeconds(_autoResetDelay);

            syncIsOpen.Value = false;
            storage.Clear();
            syncStorage.Clear();
            hasRolled = false;
            syncHasRolled.Value = false;
            _pendingDropResults = null;

            // Re-roll items cho lần loot tiếp theo
            RollLootInternal();

            _autoResetCoroutine = null;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldContainer] Reset xong — ready loot lại. storage={storage.Count} ObjId={ObjectId}");
        }

        // ── ObserversRpc ──────────────────────────────────────────────────────────

        [ObserversRpc]
        private void RpcOnContainerOpened(NetworkConnection viewer)
            => OnContainerOpened?.Invoke(this, viewer);

        public IReadOnlyList<ItemInstanceData> GetStorage() => storage;
    }
}
