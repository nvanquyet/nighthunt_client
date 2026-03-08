using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Item rơi trên đất — player có thể pickup.
    /// NETWORK: Server-authoritative. Client calls Interact() → ServerRpc fires.
    ///
    /// ═══════════════════════════════════════════════════════════════════════════
    /// ROOT CAUSE CỦA BUG "MODEL KHÔNG XUẤT HIỆN":
    ///
    ///   Thứ tự cũ (SAI):
    ///     ServerManager.Spawn(netObj)   ← FishNet gọi OnStartClient() NGAY TRONG lệnh này
    ///     worldItem.Initialize(data)    ← quá trễ! OnStartClient đã xong, SyncVar vẫn rỗng
    ///
    ///   Host mode: OnStartClient() chạy synchronously BÊN TRONG ServerManager.Spawn()
    ///   → Tại thời điểm OnStartClient chạy, _syncItemData.Value = default (rỗng)
    ///   → Không có defID → SpawnModelLocal bị skip → không có model.
    ///
    ///   Dedicated server: SyncVar value embed vào spawn packet khi Spawn() được gọi.
    ///   Nếu SyncVar chưa set → packet không có data → client miss model.
    ///
    /// SOLUTION — InitializeBeforeSpawn():
    ///   WorldSpawnManager gọi:
    ///     worldItem.InitializeBeforeSpawn(data)   ← set _itemData + _syncItemData TRƯỚC
    ///     ServerManager.Spawn(netObj)             ← FishNet embed SyncVar vào spawn packet
    ///
    ///   Khi OnStartClient() chạy (host) hoặc client nhận packet (dedicated):
    ///   _syncItemData.Value đã có data → SpawnModelLocal() thành công ✓
    /// ═══════════════════════════════════════════════════════════════════════════
    ///
    /// SPAWN FLOW sau fix:
    ///
    ///   HOST:
    ///     InitializeBeforeSpawn() → _itemData set, _syncItemData.Value set
    ///     ServerManager.Spawn()
    ///       └─ OnStartNetwork() server + client
    ///       └─ OnStartClient() ← chạy TRONG Spawn(), _syncItemData đã có value
    ///             → SpawnModelLocal("OnStartClient") ✓
    ///       └─ OnSyncItemDataChanged(asServer=true)  → SpawnModelLocal SKIP (ded.srv guard)
    ///       └─ OnSyncItemDataChanged(asServer=false) → SpawnModelLocal SKIP (_modelSpawned=true)
    ///
    ///   DEDICATED SERVER → CLIENT:
    ///     Client nhận spawn packet (SyncVar value embedded)
    ///       └─ OnStartNetwork() → subscribe
    ///       └─ OnSyncItemDataChanged(asServer=false) → SpawnModelLocal("OnSyncItemDataChanged") ✓
    ///       └─ OnStartClient() → _modelSpawned=true → SKIP
    /// </summary>
    public class WorldItem : NetworkBehaviour, IPickupable
    {
        /// <summary>Fired server-side khi WorldItem bị despawn (pickup hoặc expired).</summary>
        public event System.Action OnDespawned;

        [Header("Settings")]
        [Tooltip("Maximum distance to pickup — fallback khi không có LootableConfig.")]
        [SerializeField] private float maxPickupDistance = 3f;

        // Runtime config — inject từ WorldSpawnManager.
        private NightHunt.GameplaySystems.Core.Configs.LootableConfig _lootableConfig;

        // ── SyncVar ───────────────────────────────────────────────────────────────
        // PHẢI được set TRƯỚC ServerManager.Spawn() (dùng InitializeBeforeSpawn)
        // để value được embed vào spawn packet → clients nhận data ngay lần đầu.
        private readonly SyncVar<ItemInstanceData> _syncItemData = new SyncVar<ItemInstanceData>();

        // ── Local state ───────────────────────────────────────────────────────────
        private ItemInstanceData _itemData;
        private GameObject       _modelInstance;
        private bool             _initialized;
        private bool             _modelSpawned;       // guard: SpawnModelLocal chỉ chạy 1 lần
        private Coroutine        _waitDataCoroutine;  // fallback polling coroutine

        // ── Properties ────────────────────────────────────────────────────────────

        private bool IsDataReady => !string.IsNullOrEmpty(_itemData.DefinitionID);

        public ItemInstanceData ItemData   => _itemData;
        public bool             IsLootable => true;

        // ── IPickupable ───────────────────────────────────────────────────────────

        public string ItemDefinitionID => _itemData.DefinitionID;
        public int    Quantity         => _itemData.Quantity;
        public bool   IsPickedUp       { get; private set; }
        private bool  _isPickupPending;

        // ── IInteractable ─────────────────────────────────────────────────────────

        public string InteractLabel
        {
            get
            {
                var def  = ItemDatabase.GetDefinition(_itemData.DefinitionID);
                string n = def != null ? def.DisplayName : _itemData.DefinitionID;
                return $"[F] Pick up {n} \xd7{_itemData.Quantity}";
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? maxPickupDistance;

        public bool CanInteract(GameObject interactor)
        {
            if (IsPickedUp || _isPickupPending) return false;
            if (!IsDataReady)                   return false;
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null || _isPickupPending) return;
            if (!IsSpawned || !IsClientStarted)         return;

            var playerNob = interactor.GetComponent<NetworkObject>();
            if (playerNob == null)
            {
                Debug.LogError($"[WorldItem] Interact: '{interactor.name}' không có NetworkObject!");
                return;
            }

            _isPickupPending = true;
            RequestPickup(playerNob);
        }

        public void OnHoverEnter(GameObject interactor) { }
        public void OnHoverExit(GameObject interactor)  { }

        // ═════════════════════════════════════════════════════════════════════════
        // NETWORK LIFECYCLE
        // ═════════════════════════════════════════════════════════════════════════

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _syncItemData.OnChange += OnSyncItemDataChanged;

            Debug.Log($"[WorldItem] ── OnStartNetwork ── ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}'");
        }

        // OnSpawnServer: fires SERVER-SIDE khi một connection trở thành observer của object này.
        // Nếu log này KHÔNG xuất hiện khi client connect → observer/WriteSpawn chưa được gọi
        // (problem nằm ở FishNet observer layer, không phải WorldItem code)
        // Nếu log này XUẤT HIỆN nhưng client vẫn không thấy model → problem phía client
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            Debug.Log($"[WorldItem] ── OnSpawnServer ── conn={connection.ClientId} " +
                      $"ObjId={ObjectId} defID='{_itemData.DefinitionID}'");
        }

        public override void OnStartClient()
        {
            // !! ENTRY: nếu log này không xuất hiện → FishNet không gọi OnStartClient
            Debug.Log($"[WorldItem] ── OnStartClient ENTRY ── ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            base.OnStartClient();

            // Dedicated server không cần render gì
            if (IsServerStarted && !IsClientStarted)
            {
                Debug.Log($"[WorldItem] ── OnStartClient ── SKIP (dedicated server) ObjId={ObjectId}");
                return;
            }

            var syncVal = _syncItemData.Value;

            Debug.Log($"[WorldItem] ── OnStartClient ── ObjId={ObjectId} " +
                      $"syncVal='{syncVal.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}' " +
                      $"_modelSpawned={_modelSpawned} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            if (_modelSpawned)
            {
                Debug.Log($"[WorldItem] OnStartClient: _modelSpawned=true → skip ObjId={ObjectId}");
                return;
            }

            // Ưu tiên syncVal (đã embed từ spawn packet / set từ InitializeBeforeSpawn)
            string defID = !string.IsNullOrEmpty(syncVal.DefinitionID)
                ? syncVal.DefinitionID
                : _itemData.DefinitionID;

            Debug.Log($"[WorldItem] OnStartClient: resolved defID='{defID}' ObjId={ObjectId}");

            if (!string.IsNullOrEmpty(defID))
            {
                if (string.IsNullOrEmpty(_itemData.DefinitionID))
                {
                    _itemData    = syncVal;
                    _initialized = true;
                    Debug.Log($"[WorldItem] OnStartClient: synced _itemData từ syncVal ObjId={ObjectId}");
                }
                SpawnModelLocal("OnStartClient");
            }
            else
            {
                // Edge case: data chưa arrive → poll
                Debug.LogWarning($"[WorldItem] OnStartClient: defID rỗng → start WaitForDataCoroutine ObjId={ObjectId}. " +
                                 "Nếu thấy log này thường xuyên → InitializeBeforeSpawn() chưa được gọi trước Spawn()!");
                if (_waitDataCoroutine != null) StopCoroutine(_waitDataCoroutine);
                _waitDataCoroutine = StartCoroutine(WaitForDataCoroutine());
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncItemData.OnChange -= OnSyncItemDataChanged;

            _isPickupPending = false;
            _modelSpawned    = false;

            if (_waitDataCoroutine != null)
            {
                StopCoroutine(_waitDataCoroutine);
                _waitDataCoroutine = null;
            }

            if (_modelInstance != null)
            {
                Destroy(_modelInstance);
                _modelInstance = null;
            }

            Debug.Log($"[WorldItem] ── OnStopNetwork ── ObjId={ObjectId}");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // SERVER API
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gọi TRƯỚC ServerManager.Spawn() để data được embed vào spawn packet.
        ///
        /// VÌ SAO PHẢI DÙNG METHOD NÀY (không dùng Initialize sau Spawn):
        ///   FishNet embed SyncVar value vào spawn packet khi Spawn() được gọi.
        ///   Host mode: OnStartClient() chạy synchronously BÊN TRONG Spawn() —
        ///   nếu SyncVar chưa set thì OnStartClient thấy data rỗng → không có model.
        ///
        /// KHÔNG gọi method này sau Spawn — dùng UpdateData() nếu cần update sau.
        ///
        /// LƯU Ý: KHÔNG ĐƯỢC dùng [Server] attribute VÀ KHÔNG guard NetworkManager ở đây!
        ///   [Server] inject: if (!IsServerInitialized = IsSpawned && IsServerStarted) return;
        ///   NetworkManager property chỉ được FishNet gán TRONG SpawnWithoutChecks,
        ///   trước đó = null → bất kỳ guard nào dùng NetworkManager đều early-return.
        ///   Caller (WorldSpawnManager) đã có [Server] guard → không cần check lại.
        /// </summary>
        public void InitializeBeforeSpawn(
            ItemInstanceData data,
            NightHunt.GameplaySystems.Core.Configs.LootableConfig lootableConfig = null)
        {
            Debug.Log($"[WorldItem] ── InitializeBeforeSpawn ENTRY ── defID='{data.DefinitionID}'");

            _itemData       = data;
            _lootableConfig = lootableConfig;
            _initialized    = true;

            // Set SyncVar TRƯỚC Spawn → FishNet embed value vào spawn packet
            _syncItemData.Value = data;

            Debug.Log($"[WorldItem] ── InitializeBeforeSpawn ── " +
                      $"defID='{data.DefinitionID}' qty={data.Quantity} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' ObjId={ObjectId}");
        }

        /// <summary>Update data SAU khi đã spawn (ví dụ: thay đổi quantity).</summary>
        [Server]
        public void UpdateData(ItemInstanceData data)
        {
            _itemData           = data;
            _syncItemData.Value = data;
            Debug.Log($"[WorldItem] ── UpdateData ── defID='{data.DefinitionID}' ObjId={ObjectId}");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // SYNCVAR CALLBACK
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fires trên MỌI side khi _syncItemData thay đổi.
        ///   asServer=true  → server side (dedicated server hoặc host-server)
        ///   asServer=false → client side (remote client hoặc host-client)
        ///
        /// HOST: fires 2 lần (asServer=true, asServer=false).
        /// DEDICATED CLIENT: fires 1 lần (asServer=false).
        /// </summary>
        private void OnSyncItemDataChanged(ItemInstanceData oldData, ItemInstanceData newData, bool asServer)
        {
            Debug.Log($"[WorldItem] ── OnSyncItemDataChanged ── asServer={asServer} " +
                      $"old='{oldData.DefinitionID}' → new='{newData.DefinitionID}' " +
                      $"ObjId={ObjectId} _modelSpawned={_modelSpawned}");

            if (string.IsNullOrEmpty(newData.DefinitionID))
            {
                Debug.Log($"[WorldItem] OnSyncItemDataChanged: newData.DefinitionID rỗng → skip ObjId={ObjectId}");
                return;
            }

            _itemData    = newData;
            _initialized = true;

            SpawnModelLocal($"OnSyncItemDataChanged(asServer={asServer})");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // MODEL SPAWNING
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Instantiate DroppedPrefab làm visual child (non-networked, client-side only).
        ///
        /// GUARDS (theo thứ tự):
        ///   1. Dedicated server → skip (không cần renderer)
        ///   2. _modelSpawned    → skip (đã có rồi)
        ///   3. DefinitionID rỗng → skip (data chưa sẵn sàng)
        ///   4. ItemDefinition null → error + skip
        ///   5. DroppedPrefab null  → error + skip
        /// </summary>
        private void SpawnModelLocal(string caller)
        {
            // !! ENTRY: nếu log này không xuất hiện → SpawnModelLocal chưa được gọi
            Debug.Log($"[WorldItem] SpawnModelLocal ENTRY [{caller}]: " +
                      $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"_modelSpawned={_modelSpawned} defID='{_itemData.DefinitionID}'");

            // ── Guard 1 ───────────────────────────────────────────────────────────
            if (IsServerStarted && !IsClientStarted)
            {
                Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — dedicated server. ObjId={ObjectId}");
                return;
            }

            // ── Guard 2 ───────────────────────────────────────────────────────────
            if (_modelSpawned)
            {
                Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — already spawned. ObjId={ObjectId}");
                return;
            }

            // ── Guard 3 ───────────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(_itemData.DefinitionID))
            {
                Debug.LogWarning($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — _itemData.DefinitionID rỗng. " +
                                 $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted}");
                return;
            }

            // ── Guard 4 ───────────────────────────────────────────────────────────
            var def = ItemDatabase.GetDefinition(_itemData.DefinitionID);
            if (def == null)
            {
                Debug.LogError($"[WorldItem] SpawnModelLocal [{caller}]: ItemDatabase.GetDefinition('{_itemData.DefinitionID}') = NULL! " +
                               $"ObjId={ObjectId} — Kiểm tra: 1) ItemDatabase đã init chưa? " +
                               $"2) DefinitionID '{_itemData.DefinitionID}' có tồn tại không?");
                return;
            }

            // ── Guard 5 ───────────────────────────────────────────────────────────
            if (def.DroppedPrefab == null)
            {
                Debug.LogError($"[WorldItem] SpawnModelLocal [{caller}]: def.DroppedPrefab = NULL cho '{_itemData.DefinitionID}'! " +
                               $"ObjId={ObjectId} — Vào Inspector ItemDefinition '{_itemData.DefinitionID}' và gán DroppedPrefab.");
                return;
            }

            // ── Instantiate ───────────────────────────────────────────────────────
            Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: instantiating '{def.DroppedPrefab.name}' " +
                      $"for '{_itemData.DefinitionID}' at {transform.position} ObjId={ObjectId}");

            _modelInstance = Instantiate(def.DroppedPrefab, transform.position, transform.rotation, transform);

            // DroppedPrefab phải là pure visual — không được có NetworkObject
            var modelNetObj = _modelInstance.GetComponent<NetworkObject>();
            if (modelNetObj != null)
            {
                Debug.LogWarning($"[WorldItem] DroppedPrefab '{def.ItemID}' có NetworkObject — removing. " +
                                 "DroppedPrefab phải là pure visual.");
                Destroy(modelNetObj);
            }

            // Force-enable tất cả renderer
            var renderers = _modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = true;

            _modelSpawned = true;

            Debug.Log($"[WorldItem] SpawnModelLocal SUCCESS [{caller}]: " +
                      $"defID='{_itemData.DefinitionID}' model='{_modelInstance.name}' " +
                      $"renderers={renderers.Length} ObjId={ObjectId}");

            // Delay 1 frame → UpdateRenderers → force-enable lại
            if (IsSpawned)
                StartCoroutine(DelayedUpdateRenderers());
        }

        private IEnumerator DelayedUpdateRenderers()
        {
            yield return null;

            if (_modelInstance == null || !IsSpawned) yield break;

            NetworkObject.UpdateRenderers();

            int count = 0;
            foreach (var r in _modelInstance.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                count++;
            }

            Debug.Log($"[WorldItem] DelayedUpdateRenderers: {count} renderer(s) force-enabled. ObjId={ObjectId}");
        }

        /// <summary>
        /// Fallback: poll data tối đa 3 giây.
        /// Lý tưởng là KHÔNG BAO GIỜ vào đây nếu InitializeBeforeSpawn được dùng đúng.
        /// Log warning để dễ phát hiện nếu flow sai.
        /// </summary>
        private IEnumerator WaitForDataCoroutine()
        {
            const float kTimeout = 3f;
            float elapsed = 0f;

            while (elapsed < kTimeout)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (_modelSpawned) yield break;

                var syncVal = _syncItemData.Value;
                string defID = !string.IsNullOrEmpty(syncVal.DefinitionID)
                    ? syncVal.DefinitionID
                    : _itemData.DefinitionID;

                if (!string.IsNullOrEmpty(defID))
                {
                    if (string.IsNullOrEmpty(_itemData.DefinitionID))
                    {
                        _itemData    = syncVal;
                        _initialized = true;
                    }
                    Debug.Log($"[WorldItem] WaitForDataCoroutine: found defID='{defID}' after {elapsed:F2}s ObjId={ObjectId}");
                    SpawnModelLocal("WaitForDataCoroutine");
                    yield break;
                }
            }

            Debug.LogError($"[WorldItem] WaitForDataCoroutine: TIMEOUT {kTimeout}s ObjId={ObjectId} — " +
                           "KHÔNG CÓ MODEL! Root cause: InitializeBeforeSpawn() chưa được gọi " +
                           "TRƯỚC ServerManager.Spawn() trong WorldSpawnManager.");
            _waitDataCoroutine = null;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // SERVER RPC — PICKUP
        // ═════════════════════════════════════════════════════════════════════════

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickup(NetworkObject playerNob, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;

            if (conn == null)
            {
                Debug.LogError("[WorldItem] RequestPickup: conn NULL.");
                return;
            }
            if (!IsServerInitialized)
            {
                Debug.LogError("[WorldItem] RequestPickup: IsServerInitialized=false.");
                return;
            }
            if (playerNob == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: playerNob NULL (ClientId={conn.ClientId}).");
                return;
            }
            if (playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: ownership mismatch " +
                                 $"ClientId={conn.ClientId} owner={playerNob.OwnerId}.");
                return;
            }

            var player = playerNob.GetComponent<NetworkPlayer>();
            if (player == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: không có NetworkPlayer trên '{playerNob.name}'.");
                return;
            }

            float dist    = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: quá xa ({dist:F2}m > {maxDist}m).");
                return;
            }

            var inventory = player.GetComponent<IInventorySystem>()
                         ?? player.GetComponentInChildren<IInventorySystem>();
            if (inventory == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: IInventorySystem không tìm thấy trên '{player.name}'.");
                return;
            }
            if (inventory is NetworkBehaviour nb && !nb.IsServerInitialized)
            {
                Debug.LogError("[WorldItem] RequestPickup: IInventorySystem.IsServerInitialized=false.");
                return;
            }

            inventory.AddItem(_itemData.DefinitionID, _itemData.Quantity);
            IsPickedUp = true;
            Debug.Log($"[WorldItem] ✓ Pickup: '{_itemData.DefinitionID}' ×{_itemData.Quantity} ClientId={conn.ClientId}");
            DespawnPickup();
        }

        [Server]
        private void DespawnPickup()
        {
            if (_modelInstance != null)
            {
                Destroy(_modelInstance);
                _modelInstance = null;
            }
            OnDespawned?.Invoke();
            base.Despawn();
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, maxPickupDistance);
        }
    }
} 