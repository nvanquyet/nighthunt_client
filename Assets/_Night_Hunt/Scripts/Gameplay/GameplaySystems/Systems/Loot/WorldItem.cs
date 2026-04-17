using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using NightHunt.Core;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Item dropped on the ground — player can pick up.
    /// NETWORK: Server-authoritative. Client calls Interact() → ServerRpc fires.
    ///
    /// �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?
    /// ROOT CAUSE OF BUG "MODEL KHÔNG XUẤT HIỆN":
    ///
    ///   Old order (WRONG):
    ///     ServerManager.Spawn(netObj)   �? FishNet g�?i OnStartClient() NGAY TRONG lệnh này
    ///     worldItem.Initialize(data)    �? too late! OnStartClient already done, SyncVar still empty
    ///
    ///   Host mode: OnStartClient() chạy synchronously BÊN TRONG ServerManager.Spawn()
    ///   → Tại th�?i điểm OnStartClient chạy, _syncItemData.Value = default (empty)
    ///   → Không có defID → SpawnModelLocal bị skip → no model.
    ///
    ///   Dedicated server: SyncVar value embed vào spawn packet khi Spawn() được g�?i.
    ///   Nếu SyncVar chưa set → packet no data → client miss model.
    ///
    /// SOLUTION — InitializeBeforeSpawn():
    ///   WorldSpawnManager g�?i:
    ///     worldItem.InitializeBeforeSpawn(data)   �? set _itemData + _syncItemData TRƯỚC
    ///     ServerManager.Spawn(netObj)             �? FishNet embed SyncVar vào spawn packet
    ///
    ///   Khi OnStartClient() chạy (host) hoặc client nhận packet (dedicated):
    ///   _syncItemData.Value already has data → SpawnModelLocal() success ✓
    /// �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?
    ///
    /// SPAWN FLOW after fix:
    ///
    ///   HOST:
    ///     InitializeBeforeSpawn() → _itemData set, _syncItemData.Value set
    ///     ServerManager.Spawn()
    ///       └─ OnStartNetwork() server + client
    ///       └─ OnStartClient() �? chạy TRONG Spawn(), _syncItemData already has value
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
        /// <summary>Fired server-side khi WorldItem b? despawn (pickup ho?c expired).</summary>
        public event System.Action OnDespawned;

        /// <summary>Fired on the local client when any WorldItem enters hover (raycast target changed).</summary>
        public static event System.Action<WorldItem> OnAnyHoverEnter;

        /// <summary>Fired on the local client when any WorldItem exits hover.</summary>
        public static event System.Action<WorldItem> OnAnyHoverExit;

        [Header("Settings")]
        [Tooltip("Maximum distance to pickup — fallback khi not available LootableConfig.")]
        [FormerlySerializedAs("maxPickupDistance")]
        [SerializeField]
        private float _maxPickupDistance = 3f;
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        // Runtime config — inject từ WorldSpawnManager.
        private NightHunt.GameplaySystems.Core.Configs.LootableConfig _lootableConfig;

        // ── SyncVar ───────────────────────────────────────────────────────────────
        // PHẢI set TRƯỚC ServerManager.Spawn() (dùng InitializeBeforeSpawn)
        // để value embedded in spawn packet → clients nhận data ngay lần đầu.
        private readonly SyncVar<ItemInstanceData> _syncItemData = new SyncVar<ItemInstanceData>();

        // Server-authoritative picked-up flag � SyncVar so all observers immediately see IsPickedUp=true when server confirms pickup.
        private readonly SyncVar<bool> _syncIsPickedUp = new SyncVar<bool>();

        // ── Local state ───────────────────────────────────────────────────────────
        private ItemInstanceData _itemData;
        private GameObject _modelInstance;
        private bool _modelSpawned; // guard: SpawnModelLocal ch? ch?y 1 l?n
        private Coroutine _waitDataCoroutine; // fallback polling coroutine

        // ── Properties ────────────────────────────────────────────────────────────

        private bool IsDataReady => !string.IsNullOrEmpty(_itemData.DefinitionID);

        public ItemInstanceData ItemData => _itemData;
        public bool IsLootable => true;

        // ── IPickupable ───────────────────────────────────────────────────────────

        public string ItemDefinitionID => _itemData.DefinitionID;
        public int Quantity => _itemData.Quantity;
        public bool IsPickedUp { get; private set; }
#pragma warning disable CS0414
        private bool _isPickupPending;
#pragma warning restore CS0414

        // ── IInteractable ─────────────────────────────────────────────────────────

        public string InteractLabel
        {
            get
            {
                var def = ItemDatabase.GetDefinition(_itemData.DefinitionID);
                string n = def != null ? def.DisplayName : _itemData.DefinitionID;
                return $"[F] Pick up {n} \xd7{_itemData.Quantity}";
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? _maxPickupDistance;

        public bool CanInteract(GameObject interactor)
        {
            // IsPickedUp is a SyncVar � reflects the server�s authoritative state on all clients.
            // _isPickupPending is intentionally NOT checked here: if the server ever rejects a
            // pickup RPC (e.g. validation fail) the client would be permanently locked out.
            if (IsPickedUp) return false;
            if (!IsDataReady) return false;
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            if (!IsSpawned || !IsClientStarted) return;

            var playerNob = ComponentResolver.Find<NetworkObject>(interactor)
                .OnSelf()
                .InParent()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            if (playerNob == null)
            {
                Debug.LogError($"[WorldItem] Interact: '{interactor.name}' kh�ng c� NetworkObject!");
                return;
            }

            // _isPickupPending kept only to prevent rapid same-frame spam RPCs.
            // It is intentionally NOT checked in CanInteract() so that a server rejection
            // (which never calls OnStopNetwork to reset it) can't permanently lock the client.
            _isPickupPending = true;
            RequestPickup(playerNob);
        }

        public void OnHoverEnter(GameObject interactor)
        {
            OnAnyHoverEnter?.Invoke(this);
        }

        public void OnHoverExit(GameObject interactor)
        {
            OnAnyHoverExit?.Invoke(this);
        }

        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?
        // NETWORK LIFECYCLE
        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Self-heal layer: Prefab_WorldItem was serialised on an unnamed layer (27).
            // Force "Interactable" so RaycastDetector can find it with a proper mask.
            gameObject.layer = LayerMask.NameToLayer(NightHuntLayers.Interactable);
            _syncItemData.OnChange += OnSyncItemDataChanged;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStartNetwork ── ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}'");
        }

        // OnSpawnServer: fires SERVER-SIDE khi một connection trở thành observer của object này.
        // Nếu log này KHÔNG xuất hiện khi client connect → observer/WriteSpawn not yet g�?i
        // (problem nằm ở FishNet observer layer, không phải WorldItem code)
        // Nếu log này XUẤT HIỆN nhưng client vẫn không thấy model → problem phía client
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnSpawnServer ── conn={connection.ClientId} " +
                      $"ObjId={ObjectId} defID='{_itemData.DefinitionID}'");
        }

        public override void OnStartClient()
        {
            // !! ENTRY: nếu log này không xuất hiện → FishNet không g�?i OnStartClient
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStartClient ENTRY ── ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            base.OnStartClient();

            // Dedicated server không cần render gì
            if (IsServerStarted && !IsClientStarted)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] ── OnStartClient ── SKIP (dedicated server) ObjId={ObjectId}");
                return;
            }

            var syncVal = _syncItemData.Value;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStartClient ── ObjId={ObjectId} " +
                      $"syncVal='{syncVal.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}' " +
                      $"_modelSpawned={_modelSpawned} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            if (_modelSpawned)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] OnStartClient: _modelSpawned=true → skip ObjId={ObjectId}");
                return;
            }

            // Ưu tiên syncVal (đã embed từ spawn packet / set từ InitializeBeforeSpawn)
            string defID = !string.IsNullOrEmpty(syncVal.DefinitionID)
                ? syncVal.DefinitionID
                : _itemData.DefinitionID;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] OnStartClient: resolved defID='{defID}' ObjId={ObjectId}");

            if (!string.IsNullOrEmpty(defID))
            {
                if (string.IsNullOrEmpty(_itemData.DefinitionID))
                {
                    _itemData = syncVal;
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log($"[WorldItem] OnStartClient: synced _itemData từ syncVal ObjId={ObjectId}");
                }

                SpawnModelLocal("OnStartClient");
            }
            else
            {
                // Edge case: data chưa arrive → poll
                Debug.LogWarning(
                    $"[WorldItem] OnStartClient: defID empty → start WaitForDataCoroutine ObjId={ObjectId}. " +
                    "Nếu thấy log này thư�?ng xuyên → InitializeBeforeSpawn() not yet g�?i trước Spawn()!");
                if (_waitDataCoroutine != null) StopCoroutine(_waitDataCoroutine);
                _waitDataCoroutine = StartCoroutine(WaitForDataCoroutine());
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncItemData.OnChange -= OnSyncItemDataChanged;

            _isPickupPending = false;
            _modelSpawned = false;

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

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStopNetwork ── ObjId={ObjectId}");
        }

        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?
        // SERVER API
        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?

        /// <summary>
        /// G�?i TRƯỚC ServerManager.Spawn() để data embedded in spawn packet.
        ///
        /// VÌ SAO PHẢI DÙNG METHOD NÀY (không dùng Initialize sau Spawn):
        ///   FishNet embed SyncVar value vào spawn packet khi Spawn() được g�?i.
        ///   Host mode: OnStartClient() chạy synchronously BÊN TRONG Spawn() —
        ///   nếu SyncVar chưa set thì OnStartClient thấy data empty → no model.
        ///
        /// KHÔNG g�?i method này sau Spawn — dùng UpdateData() nếu cần update sau.
        ///
        /// LƯU �?: KHÔNG �?ƯỢC dùng [Server] attribute VÀ KHÔNG guard NetworkManager ở đây!
        ///   [Server] inject: if (!IsServerInitialized = IsSpawned && IsServerStarted) return;
        ///   NetworkManager property chỉ được FishNet gán TRONG SpawnWithoutChecks,
        ///   trước đó = null → bất kỳ guard nào dùng NetworkManager đ�?u early-return.
        ///   Caller (WorldSpawnManager) đã có [Server] guard → không cần check lại.
        /// </summary>
        public void InitializeBeforeSpawn(
            ItemInstanceData data,
            NightHunt.GameplaySystems.Core.Configs.LootableConfig lootableConfig = null)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── InitializeBeforeSpawn ENTRY ── defID='{data.DefinitionID}'");

            _itemData = data;
            _lootableConfig = lootableConfig;

            // Set SyncVar TR??C Spawn ? FishNet embed value v�o spawn packet
            _syncItemData.Value = data;
            // Reset picked-up flag so that pooled NetworkObjects don't carry stale state.
            _syncIsPickedUp.Value = false;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── InitializeBeforeSpawn ── " +
                      $"defID='{data.DefinitionID}' qty={data.Quantity} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' ObjId={ObjectId}");
        }

        /// <summary>Update data SAU khi đã spawn (ví dụ: thay đổi quantity).</summary>
        [Server]
        public void UpdateData(ItemInstanceData data)
        {
            _itemData = data;
            _syncItemData.Value = data;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── UpdateData ── defID='{data.DefinitionID}' ObjId={ObjectId}");
        }

        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?
        // SYNCVAR CALLBACK
        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?

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
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnSyncItemDataChanged ── asServer={asServer} " +
                      $"old='{oldData.DefinitionID}' → new='{newData.DefinitionID}' " +
                      $"ObjId={ObjectId} _modelSpawned={_modelSpawned}");

            if (string.IsNullOrEmpty(newData.DefinitionID))
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] OnSyncItemDataChanged: newData.DefinitionID empty → skip ObjId={ObjectId}");
                return;
            }

            _itemData = newData;

            SpawnModelLocal($"OnSyncItemDataChanged(asServer={asServer})");
        }

        // � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � 
        // MODEL SPAWNING
        // � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � 

        /// <summary>
        /// Instantiate GroundPrefab làm visual child (non-networked, client-side only).
        ///
        /// GUARDS (theo thứ tự):
        ///   1. Dedicated server → skip (không cần renderer)
        ///   2. _modelSpawned    → skip (đã có rồi)
        ///   3. DefinitionID empty → skip (data not ready)
        ///   4. ItemDefinition null → error + skip
        ///   5. DroppedPrefab null  → error + skip
        /// </summary>
        private void SpawnModelLocal(string caller)
        {
            // !! ENTRY: nếu log này không xuất hiện → SpawnModelLocal not yet g�?i
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal ENTRY [{caller}]: " +
                      $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"_modelSpawned={_modelSpawned} defID='{_itemData.DefinitionID}'");

            // ── Guard 1 ───────────────────────────────────────────────────────────
            if (IsServerStarted && !IsClientStarted)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — dedicated server. ObjId={ObjectId}");
                return;
            }

            // ── Guard 2 ───────────────────────────────────────────────────────────
            if (_modelSpawned)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — already spawned. ObjId={ObjectId}");
                return;
            }

            // ── Guard 3 ───────────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(_itemData.DefinitionID))
            {
                Debug.LogWarning($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — _itemData.DefinitionID empty. " +
                                 $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted}");
                return;
            }

            // ── Guard 4 ───────────────────────────────────────────────────────────
            var def = ItemDatabase.GetDefinition(_itemData.DefinitionID);
            if (def == null)
            {
                Debug.LogError(
                    $"[WorldItem] SpawnModelLocal [{caller}]: ItemDatabase.GetDefinition('{_itemData.DefinitionID}') = NULL! " +
                    $"ObjId={ObjectId} — Kiểm tra: 1) ItemDatabase đã init chưa? " +
                    $"2) DefinitionID '{_itemData.DefinitionID}' có tồn tại không?");
                return;
            }

            // ── Guard 5 ───────────────────────────────────────────────────────────
            if (def.GroundPrefab == null)
            {
                Debug.LogError(
                    $"[WorldItem] SpawnModelLocal [{caller}]: def.GroundPrefab = NULL cho '{_itemData.DefinitionID}'! " +
                    $"ObjId={ObjectId} — Vào Inspector ItemDefinition '{_itemData.DefinitionID}' và gán GroundPrefab.");
                return;
            }

            // ── Instantiate ───────────────────────────────────────────────────────
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: instantiating '{def.GroundPrefab.name}' " +
                      $"for '{_itemData.DefinitionID}' at {transform.position} ObjId={ObjectId}");

            _modelInstance = Instantiate(def.GroundPrefab, transform.position, transform.rotation, transform);

            // GroundPrefab phải là pure visual — not allowed có NetworkObject
            var modelNetObj = ComponentResolver.Find<NetworkObject>(_modelInstance)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            if (modelNetObj != null)
            {
                Debug.LogWarning($"[WorldItem] GroundPrefab '{def.ItemID}' có NetworkObject — removing. " +
                                 "GroundPrefab phải là pure visual.");
                Destroy(modelNetObj);
            }

            // Force-enable tất cả renderer
            var renderers = _modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = true;

            _modelSpawned = true;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
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

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
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
                        _itemData = syncVal;

                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log(
                        $"[WorldItem] WaitForDataCoroutine: found defID='{defID}' after {elapsed:F2}s ObjId={ObjectId}");
                    SpawnModelLocal("WaitForDataCoroutine");
                    yield break;
                }
            }

            Debug.LogError($"[WorldItem] WaitForDataCoroutine: TIMEOUT {kTimeout}s ObjId={ObjectId} — " +
                           "KHÔNG CÓ MODEL! Root cause: InitializeBeforeSpawn() not yet g�?i " +
                           "TRƯỚC ServerManager.Spawn() trong WorldSpawnManager.");
            _waitDataCoroutine = null;
        }

        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?
        // SERVER RPC — PICKUP
        // �?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickup(NetworkObject playerNob, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;

            // Server-side race-condition guard: two clients pressing F on the same tick.
            // Also protects against RPC spam from a single client before the SyncVar
            // update travels back to them.
            if (IsPickedUp)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] RequestPickup: already picked up (ObjId={ObjectId}). Ignoring.");
                return;
            }

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

            var player = ComponentResolver.Find<NetworkPlayer>(playerNob)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkPlayer not found")
                .Resolve();
            if (player == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: not available NetworkPlayer trên '{playerNob.name}'.");
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: quá xa ({dist:F2}m > {maxDist}m).");
                return;
            }

            var inventory = ComponentResolver.Find<IInventorySystem>(player)
                                .OnSelf()
                                .InChildren()
                                .OrLogWarning("[Auto] IInventorySystem not found")
                                .Resolve()
                            ?? ComponentResolver.Find<IInventorySystem>(player)
                                .OnSelf()
                                .InChildren()
                                .InParent()
                                .OrLogWarning("[Auto] IInventorySystem not found")
                                .Resolve();
            if (inventory == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: IInventorySystem not found trên '{player.name}'.");
                return;
            }

            if (inventory is NetworkBehaviour nb && !nb.IsServerInitialized)
            {
                Debug.LogError("[WorldItem] RequestPickup: IInventorySystem.IsServerInitialized=false.");
                return;
            }

            inventory.AddItemFromData(_itemData);
            // Mark as picked up via SyncVar so all clients immediately reflect the state.
            // This fires OnChange on every observer, letting their CanInteract() return false
            // without any client needing to track _isPickupPending.
            _syncIsPickedUp.Value = true;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[WorldItem] ✓ Pickup: '{_itemData.DefinitionID}' ×{_itemData.Quantity} ClientId={conn.ClientId}");
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
            Gizmos.DrawWireSphere(transform.position, _maxPickupDistance);
        }
    }
}
