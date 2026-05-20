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
using NightHunt.Diagnostics;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Item dropped on the ground — player can pick up.
    /// NETWORK: Server-authoritative. Client calls Interact() → ServerRpc fires.
    ///
    /// INITIALIZATION ORDER (critical):
    ///   Always call InitializeBeforeSpawn(data) BEFORE ServerManager.Spawn().
    ///   FishNet embeds SyncVar values into the spawn packet at the moment Spawn() is called.
    ///   In host mode OnStartClient() runs synchronously INSIDE ServerManager.Spawn() —
    ///   if SyncVar is not set yet, OnStartClient sees empty data → SpawnModelLocal skipped → no model.
    ///
    ///   Wrong order:
    ///     ServerManager.Spawn(netObj)             ← FishNet calls OnStartClient() here, SyncVar still empty
    ///     worldItem.Initialize(data)              ← too late, OnStartClient already ran
    ///
    ///   Correct order:
    ///     worldItem.InitializeBeforeSpawn(data)   ← set _itemData + _syncItemData FIRST
    ///     ServerManager.Spawn(netObj)             ← FishNet embeds SyncVar into spawn packet
    ///
    /// SPAWN FLOW:
    ///
    ///   HOST:
    ///     InitializeBeforeSpawn() → _itemData set, _syncItemData.Value set
    ///     ServerManager.Spawn()
    ///       └─ OnStartNetwork() server + client
    ///       └─ OnStartClient() — runs inside Spawn(), _syncItemData already has value
    ///             → SpawnModelLocal("OnStartClient") ✓
    ///       └─ OnSyncItemDataChanged(asServer=true)  → SpawnModelLocal SKIP (dedicated server guard)
    ///       └─ OnSyncItemDataChanged(asServer=false) → SpawnModelLocal SKIP (_modelSpawned=true)
    ///
    ///   DEDICATED SERVER → CLIENT:
    ///     Client receives spawn packet (SyncVar value embedded)
    ///       └─ OnStartNetwork() → subscribe
    ///       └─ OnSyncItemDataChanged(asServer=false) → SpawnModelLocal("OnSyncItemDataChanged") ✓
    ///       └─ OnStartClient() → _modelSpawned=true → SKIP
    /// </summary>
    public class WorldItem : NetworkBehaviour, IPickupable
    {
        /// <summary>Fired server-side when this WorldItem is despawned (picked up or expired).</summary>
        public event System.Action OnDespawned;

        /// <summary>Fired on the local client when any WorldItem enters hover (raycast target changed).</summary>
        public static event System.Action<WorldItem> OnAnyHoverEnter;

        /// <summary>Fired on the local client when any WorldItem exits hover.</summary>
        public static event System.Action<WorldItem> OnAnyHoverExit;

        private static readonly WeaponSlotType[] AutoEquipWeaponSlots =
        {
            WeaponSlotType.Primary,
            WeaponSlotType.Secondary,
            WeaponSlotType.Melee
        };

        [Header("Settings")]
        [Tooltip("Maximum distance to pickup — fallback when LootableConfig is not set.")]
        [FormerlySerializedAs("maxPickupDistance")]
        [SerializeField]
        private float _maxPickupDistance = 3f;
        // Runtime config — injected by WorldSpawnManager.
        private NightHunt.GameplaySystems.Core.Configs.LootableConfig _lootableConfig;

        // ── SyncVar ───────────────────────────────────────────────────────────────
        // Must be set BEFORE ServerManager.Spawn() via InitializeBeforeSpawn()
        // so the value is embedded in the spawn packet and clients receive it immediately.
        private readonly SyncVar<ItemInstanceData> _syncItemData = new SyncVar<ItemInstanceData>();

        // Server-authoritative picked-up flag � SyncVar so all observers immediately see IsPickedUp=true when server confirms pickup.
        private readonly SyncVar<bool> _syncIsPickedUp = new SyncVar<bool>();

        // ── Local state ───────────────────────────────────────────────────────────
        private ItemInstanceData _itemData;
        private GameObject _modelInstance;
        private bool _modelSpawned; // guard: SpawnModelLocal runs only once
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
        public bool IsPickupPending => _isPickupPending;

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
            // IsPickedUp is a SyncVar — reflects the server's authoritative state on all clients.
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

        // ── NETWORK LIFECYCLE ───────────────────────────────────────────────────

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Self-heal layer: Prefab_WorldItem was serialised on an unnamed layer (27).
            // Force "Interactable" so RaycastDetector can find it with a proper mask.
            gameObject.layer = LayerMask.NameToLayer(NightHuntLayers.Interactable);
            _syncItemData.OnChange += OnSyncItemDataChanged;
            _syncIsPickedUp.OnChange += OnSyncIsPickedUpChanged;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStartNetwork ── ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}'");
        }

        // OnSpawnServer: fires SERVER-SIDE when a connection becomes an observer of this object.
        // If this log does NOT appear when a client connects → observer/WriteSpawn not sent yet
        //   (problem is in the FishNet observer layer, not WorldItem code)
        // If this log DOES appear but client still has no model → problem is on the client side
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnSpawnServer ── conn={connection.ClientId} " +
                      $"ObjId={ObjectId} defID='{_itemData.DefinitionID}'");
        }

        public override void OnStartClient()
        {
            // !! ENTRY: if this log does not appear → FishNet is not calling OnStartClient
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStartClient ENTRY ── ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            base.OnStartClient();

            // Dedicated server — no rendering needed
            if (IsServerStarted && !IsClientStarted)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] ── OnStartClient ── SKIP (dedicated server) ObjId={ObjectId}");
                return;
            }

            var syncVal = _syncItemData.Value;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStartClient ── ObjId={ObjectId} " +
                      $"syncVal='{syncVal.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}' " +
                      $"_modelSpawned={_modelSpawned} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            if (_modelSpawned)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] OnStartClient: _modelSpawned=true → skip ObjId={ObjectId}");
                return;
            }

            // Prefer syncVal (already embedded in spawn packet via InitializeBeforeSpawn)
            string defID = !string.IsNullOrEmpty(syncVal.DefinitionID)
                ? syncVal.DefinitionID
                : _itemData.DefinitionID;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] OnStartClient: resolved defID='{defID}' ObjId={ObjectId}");

            if (!string.IsNullOrEmpty(defID))
            {
                if (string.IsNullOrEmpty(_itemData.DefinitionID))
                {
                    _itemData = syncVal;
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                        Debug.Log($"[WorldItem] OnStartClient: synced _itemData from syncVal ObjId={ObjectId}");
                }

                SpawnModelLocal("OnStartClient");
            }
            else
            {
                // Edge case: data chưa arrive → poll
                Debug.LogWarning(
                    $"[WorldItem] OnStartClient: defID empty → starting WaitForDataCoroutine ObjId={ObjectId}. " +
                    "If this log appears frequently → InitializeBeforeSpawn() was not called before Spawn()!");
                if (_waitDataCoroutine != null) StopCoroutine(_waitDataCoroutine);
                _waitDataCoroutine = StartCoroutine(WaitForDataCoroutine());
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncItemData.OnChange -= OnSyncItemDataChanged;
            _syncIsPickedUp.OnChange -= OnSyncIsPickedUpChanged;

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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnStopNetwork ── ObjId={ObjectId}");
        }

        public void RequestPickupFromUI(NetworkObject playerNob)
        {
            _isPickupPending = true;
            RequestPickup(playerNob);
        }

        // ── SERVER API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Must be called BEFORE ServerManager.Spawn() so data is embedded in the spawn packet.
        ///
        /// WHY THIS METHOD IS REQUIRED (not Initialize after Spawn):
        ///   FishNet embeds SyncVar values into the spawn packet at the moment Spawn() is called.
        ///   Host mode: OnStartClient() runs synchronously INSIDE Spawn() —
        ///   if SyncVar is not set yet, OnStartClient sees empty data → no model.
        ///
        /// Do NOT call this method after Spawn — use UpdateData() for post-spawn data changes.
        ///
        /// NOTE: Must NOT use [Server] attribute and must NOT guard on NetworkManager here!
        ///   [Server] injects: if (!IsServerInitialized == IsSpawned && IsServerStarted) return;
        ///   NetworkManager property is only assigned by FishNet INSIDE SpawnWithoutChecks,
        ///   before that it is null → any guard using NetworkManager will early-return.
        ///   Caller (WorldSpawnManager) already has a [Server] guard — no need to re-check.
        /// </summary>
        public void InitializeBeforeSpawn(
            ItemInstanceData data,
            NightHunt.GameplaySystems.Core.Configs.LootableConfig lootableConfig = null)
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── InitializeBeforeSpawn ENTRY ── defID='{data.DefinitionID}'");

            _itemData = data;
            _lootableConfig = lootableConfig;

            // Set SyncVar BEFORE Spawn so FishNet embeds the value in the spawn packet
            _syncItemData.Value = data;
            // Reset picked-up flag so that pooled NetworkObjects don't carry stale state.
            _syncIsPickedUp.Value = false;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── InitializeBeforeSpawn ── " +
                      $"defID='{data.DefinitionID}' qty={data.Quantity} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' ObjId={ObjectId}");
        }

        /// <summary>Updates item data after the object is already spawned (e.g. quantity change).</summary>
        [Server]
        public void UpdateData(ItemInstanceData data)
        {
            _itemData = data;
            _syncItemData.Value = data;
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── UpdateData ── defID='{data.DefinitionID}' ObjId={ObjectId}");
        }

        // ── SYNCVAR CALLBACKS ────────────────────────────────────────────────────

        /// <summary>
        /// Fires on EVERY side when _syncItemData changes.
        ///   asServer=true  → server side (dedicated server or host-server)
        ///   asServer=false → client side (remote client or host-client)
        ///
        /// HOST: fires twice (asServer=true, then asServer=false).
        /// DEDICATED CLIENT: fires once (asServer=false).
        /// </summary>
        private void OnSyncItemDataChanged(ItemInstanceData oldData, ItemInstanceData newData, bool asServer)
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] ── OnSyncItemDataChanged ── asServer={asServer} " +
                      $"old='{oldData.DefinitionID}' → new='{newData.DefinitionID}' " +
                      $"ObjId={ObjectId} _modelSpawned={_modelSpawned}");

            if (string.IsNullOrEmpty(newData.DefinitionID))
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] OnSyncItemDataChanged: newData.DefinitionID empty — skip ObjId={ObjectId}");
                return;
            }

            _itemData = newData;

            SpawnModelLocal($"OnSyncItemDataChanged(asServer={asServer})");
        }

        // � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � 
        // MODEL SPAWNING
        // � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � � 

        /// <summary>
        /// Instantiates the VisualPrefab as a visual child (non-networked, client-side only).
        ///
        /// GUARDS (in order):
        ///   1. Dedicated server  → skip (no renderer needed)
        ///   2. _modelSpawned     → skip (already instantiated)
        ///   3. DefinitionID empty → skip (data not ready)
        ///   4. ItemDefinition null → log error + skip
        ///   5. DroppedPrefab null  → use runtime fallback
        /// </summary>
        private void SpawnModelLocal(string caller)
        {
            // !! ENTRY: if this log does not appear → SpawnModelLocal was not called
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal ENTRY [{caller}]: " +
                      $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"_modelSpawned={_modelSpawned} defID='{_itemData.DefinitionID}'");

            // ── Guard 1 ───────────────────────────────────────────────────────────
            if (IsServerStarted && !IsClientStarted)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP — dedicated server. ObjId={ObjectId}");
                return;
            }

            // ── Guard 2 ───────────────────────────────────────────────────────────
            if (_modelSpawned)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
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
                    $"ObjId={ObjectId} — Check: 1) Is ItemDatabase initialized? " +
                    $"2) Does DefinitionID '{_itemData.DefinitionID}' exist in the database?");
                return;
            }
            var visualPrefab = ItemVisualResolver.ResolveVisualPrefab(def);

            // ── Instantiate ───────────────────────────────────────────────────────
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: instantiating '{(visualPrefab != null ? visualPrefab.name : "runtime fallback")}' " +
                      $"for '{_itemData.DefinitionID}' at {transform.position} ObjId={ObjectId}");

            _modelInstance = visualPrefab != null
                ? Instantiate(visualPrefab, transform.position, transform.rotation, transform)
                : ItemVisualResolver.CreateRuntimeFallback(def, ItemVisualPurpose.Ground);

            if (_modelInstance.transform.parent != transform)
                _modelInstance.transform.SetParent(transform, worldPositionStays: true);

            _modelInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);

            // Ground visuals must be pure visuals — NetworkObject is not allowed
            var modelNetObj = _modelInstance.GetComponentInChildren<NetworkObject>(includeInactive: true);
            if (modelNetObj != null)
            {
                Debug.LogWarning($"[WorldItem] VisualPrefab '{def.ItemID}' has NetworkObject — removing.");
                Destroy(modelNetObj);
            }

            // Force-enable all renderers
            var renderers = _modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = true;

            _modelSpawned = true;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal SUCCESS [{caller}]: " +
                      $"defID='{_itemData.DefinitionID}' model='{_modelInstance.name}' " +
                      $"renderers={renderers.Length} ObjId={ObjectId}");

            // Delay 1 frame → UpdateRenderers → re-enable renderers
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] DelayedUpdateRenderers: {count} renderer(s) force-enabled. ObjId={ObjectId}");
        }

        // Fallback: poll data for up to 3 seconds when InitializeBeforeSpawn wasn't used.
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

                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                        Debug.Log($"[WorldItem] WaitForDataCoroutine: found defID='{defID}' after {elapsed:F2}s ObjId={ObjectId}");
                    SpawnModelLocal("WaitForDataCoroutine");
                    yield break;
                }
            }

            Debug.LogError($"[WorldItem] WaitForDataCoroutine: TIMEOUT {kTimeout}s ObjId={ObjectId} — NO MODEL! Root cause: InitializeBeforeSpawn() was not called BEFORE ServerManager.Spawn() in WorldSpawnManager.");
            _waitDataCoroutine = null;
        }

        // ── SERVER RPC — PICKUP ──────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickup(NetworkObject playerNob, NetworkConnection conn = null)
        {
            if (conn == null) conn = playerNob?.Owner;

            // Server-side race-condition guard: two clients pressing F on the same tick.
            // Also protects against RPC spam from a single client before the SyncVar
            // update travels back to them.
            // NOTE: Use _syncIsPickedUp.Value (server-authoritative) instead of IsPickedUp,
            // because in host mode the client-side ApplyPickedUpVisualState() sets IsPickedUp=true
            // on the shared MonoBehaviour instance BEFORE this RPC body executes, causing false rejects.
            if (_syncIsPickedUp.Value)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
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
                Debug.LogError($"[WorldItem] RequestPickup: NetworkPlayer not found on '{playerNob.name}'.");
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: too far ({dist:F2}m > {maxDist}m).");
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
                Debug.LogError($"[WorldItem] RequestPickup: IInventorySystem not found on '{player.name}'.");
                return;
            }

            if (inventory is NetworkBehaviour nb && !nb.IsServerInitialized)
            {
                Debug.LogError("[WorldItem] RequestPickup: IInventorySystem.IsServerInitialized=false.");
                return;
            }

            int requestedQty = Mathf.Max(1, _itemData.Quantity);
            int acceptedQty = LootTransferUtility.CalculateCarryableQuantity(playerNob, inventory, _itemData, requestedQty);
            if (acceptedQty <= 0)
            {
                _isPickupPending = false;
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] RequestPickup rejected by weight cap: '{_itemData.DefinitionID}' x{_itemData.Quantity} ClientId={conn.ClientId}");
                return;
            }

            bool takingFullStack = acceptedQty >= _itemData.Quantity;
            var acceptedData = takingFullStack
                ? _itemData
                : ItemInstanceFactory.CopyDataForQuantity(_itemData, acceptedQty, newInstanceId: true);
            inventory.AddItemFromData(acceptedData);
            TryAutoEquipPickedWeapon(player, acceptedData);

            if (takingFullStack)
            {
                // Mark as picked up via SyncVar so all clients immediately reflect the state.
                // This fires OnChange on every observer, letting their CanInteract() return false
                // without any client needing to track _isPickupPending.
                _syncIsPickedUp.Value = true;
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log(
                    $"[WorldItem] ✓ Pickup: '{_itemData.DefinitionID}' ×{_itemData.Quantity} ClientId={conn.ClientId}");
                DespawnPickup();
            }
            else
            {
                _itemData.Quantity -= acceptedQty;
                _syncItemData.Value = _itemData;
                _isPickupPending = false;
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] Partial pickup by weight cap: '{_itemData.DefinitionID}' took={acceptedQty} remain={_itemData.Quantity} ClientId={conn.ClientId}");
            }
        }

        private void TryAutoEquipPickedWeapon(NetworkPlayer player, ItemInstanceData pickedData)
        {
            if (player == null || string.IsNullOrEmpty(pickedData.DefinitionID) || string.IsNullOrEmpty(pickedData.InstanceID))
                return;

            if (ItemDatabase.GetDefinition(pickedData.DefinitionID) is not WeaponDefinition)
                return;

            var weaponSystem = ComponentResolver.Find<IWeaponSystem>(player)
                .OnSelf()
                .InChildren()
                .OrDefault(null)
                .Resolve();

            if (weaponSystem == null)
            {
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Weapon,
                    "PickupWeaponAutoEquip",
                    $"result=no-weapon-system def={pickedData.DefinitionID} inst={pickedData.InstanceID} player={player.name}",
                    this);
                return;
            }

            foreach (WeaponSlotType slot in AutoEquipWeaponSlots)
            {
                if (weaponSystem.IsSlotOccupied(slot) || !weaponSystem.CanEquipInSlot(pickedData.DefinitionID, slot))
                    continue;

                weaponSystem.EquipWeaponToSlot(pickedData.InstanceID, slot);
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Weapon,
                    "PickupWeaponAutoEquip",
                    $"result=equipped slot={slot} def={pickedData.DefinitionID} inst={pickedData.InstanceID} active={weaponSystem.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }

            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "PickupWeaponAutoEquip",
                $"result=inventory-only reason=no-empty-compatible-slot def={pickedData.DefinitionID} inst={pickedData.InstanceID} active={weaponSystem.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                this);
        }

        private void OnSyncIsPickedUpChanged(bool oldValue, bool newValue, bool asServer)
        {
            IsPickedUp = newValue;
            if (newValue)
                ApplyPickedUpVisualState(true);
        }

        private void ApplyPickedUpVisualState(bool pickedUp)
        {
            IsPickedUp = pickedUp;
            if (!pickedUp) return;

            foreach (var col in GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            if (_modelInstance != null)
                _modelInstance.SetActive(false);
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



