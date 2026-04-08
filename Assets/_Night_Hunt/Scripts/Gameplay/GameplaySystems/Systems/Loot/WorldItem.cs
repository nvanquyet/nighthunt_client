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
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Item rÆ¡i trÃªn Ä‘áº¥t â€” player cÃ³ thá»ƒ pickup.
    /// NETWORK: Server-authoritative. Client calls Interact() â†’ ServerRpc fires.
    ///
    /// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    /// ROOT CAUSE Cá»¦A BUG "MODEL KHÃ”NG XUáº¤T HIá»†N":
    ///
    ///   Thá»© tá»± cÅ© (SAI):
    ///     ServerManager.Spawn(netObj)   â† FishNet gá»i OnStartClient() NGAY TRONG lá»‡nh nÃ y
    ///     worldItem.Initialize(data)    â† quÃ¡ trá»…! OnStartClient Ä‘Ã£ xong, SyncVar váº«n rá»—ng
    ///
    ///   Host mode: OnStartClient() cháº¡y synchronously BÃŠN TRONG ServerManager.Spawn()
    ///   â†’ Táº¡i thá»i Ä‘iá»ƒm OnStartClient cháº¡y, _syncItemData.Value = default (rá»—ng)
    ///   â†’ KhÃ´ng cÃ³ defID â†’ SpawnModelLocal bá»‹ skip â†’ khÃ´ng cÃ³ model.
    ///
    ///   Dedicated server: SyncVar value embed vÃ o spawn packet khi Spawn() Ä‘Æ°á»£c gá»i.
    ///   Náº¿u SyncVar chÆ°a set â†’ packet khÃ´ng cÃ³ data â†’ client miss model.
    ///
    /// SOLUTION â€” InitializeBeforeSpawn():
    ///   WorldSpawnManager gá»i:
    ///     worldItem.InitializeBeforeSpawn(data)   â† set _itemData + _syncItemData TRÆ¯á»šC
    ///     ServerManager.Spawn(netObj)             â† FishNet embed SyncVar vÃ o spawn packet
    ///
    ///   Khi OnStartClient() cháº¡y (host) hoáº·c client nháº­n packet (dedicated):
    ///   _syncItemData.Value Ä‘Ã£ cÃ³ data â†’ SpawnModelLocal() thÃ nh cÃ´ng âœ“
    /// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    ///
    /// SPAWN FLOW sau fix:
    ///
    ///   HOST:
    ///     InitializeBeforeSpawn() â†’ _itemData set, _syncItemData.Value set
    ///     ServerManager.Spawn()
    ///       â””â”€ OnStartNetwork() server + client
    ///       â””â”€ OnStartClient() â† cháº¡y TRONG Spawn(), _syncItemData Ä‘Ã£ cÃ³ value
    ///             â†’ SpawnModelLocal("OnStartClient") âœ“
    ///       â””â”€ OnSyncItemDataChanged(asServer=true)  â†’ SpawnModelLocal SKIP (ded.srv guard)
    ///       â””â”€ OnSyncItemDataChanged(asServer=false) â†’ SpawnModelLocal SKIP (_modelSpawned=true)
    ///
    ///   DEDICATED SERVER â†’ CLIENT:
    ///     Client nháº­n spawn packet (SyncVar value embedded)
    ///       â””â”€ OnStartNetwork() â†’ subscribe
    ///       â””â”€ OnSyncItemDataChanged(asServer=false) â†’ SpawnModelLocal("OnSyncItemDataChanged") âœ“
    ///       â””â”€ OnStartClient() â†’ _modelSpawned=true â†’ SKIP
    /// </summary>
    public class WorldItem : NetworkBehaviour, IPickupable
    {
        /// <summary>Fired server-side khi WorldItem bị despawn (pickup hoặc expired).</summary>
        public event System.Action OnDespawned;

        /// <summary>Fired on the local client when any WorldItem enters hover (raycast target changed).</summary>
        public static event System.Action<WorldItem> OnAnyHoverEnter;

        /// <summary>Fired on the local client when any WorldItem exits hover.</summary>
        public static event System.Action<WorldItem> OnAnyHoverExit;

        [Header("Settings")]
        [Tooltip("Maximum distance to pickup â€” fallback khi khÃ´ng cÃ³ LootableConfig.")]
        [FormerlySerializedAs("maxPickupDistance")]
        [SerializeField]
        private float _maxPickupDistance = 3f;
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        // Runtime config â€” inject tá»« WorldSpawnManager.
        private NightHunt.GameplaySystems.Core.Configs.LootableConfig _lootableConfig;

        // â”€â”€ SyncVar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PHáº¢I Ä‘Æ°á»£c set TRÆ¯á»šC ServerManager.Spawn() (dÃ¹ng InitializeBeforeSpawn)
        // Ä‘á»ƒ value Ä‘Æ°á»£c embed vÃ o spawn packet â†’ clients nháº­n data ngay láº§n Ä‘áº§u.
        private readonly SyncVar<ItemInstanceData> _syncItemData = new SyncVar<ItemInstanceData>();

        // Server-authoritative picked-up flag — SyncVar so all observers immediately see IsPickedUp=true when server confirms pickup.
        private readonly SyncVar<bool> _syncIsPickedUp = new SyncVar<bool>();

        // â”€â”€ Local state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private ItemInstanceData _itemData;
        private GameObject _modelInstance;
        private bool _modelSpawned; // guard: SpawnModelLocal chỉ chạy 1 lần
        private Coroutine _waitDataCoroutine; // fallback polling coroutine

        // â”€â”€ Properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool IsDataReady => !string.IsNullOrEmpty(_itemData.DefinitionID);

        public ItemInstanceData ItemData => _itemData;
        public bool IsLootable => true;

        // â”€â”€ IPickupable â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public string ItemDefinitionID => _itemData.DefinitionID;
        public int Quantity => _itemData.Quantity;
        public bool IsPickedUp { get; private set; }
#pragma warning disable CS0414
        private bool _isPickupPending;
#pragma warning restore CS0414

        // â”€â”€ IInteractable â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            // IsPickedUp is a SyncVar — reflects the server’s authoritative state on all clients.
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
                Debug.LogError($"[WorldItem] Interact: '{interactor.name}' không có NetworkObject!");
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // NETWORK LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Self-heal layer: Prefab_WorldItem was serialised on an unnamed layer (27).
            // Force "Interactable" so RaycastDetector can find it with a proper mask.
            gameObject.layer = LayerMask.NameToLayer(NightHuntLayers.Interactable);
            _syncItemData.OnChange += OnSyncItemDataChanged;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ OnStartNetwork â”€â”€ ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}'");
        }

        // OnSpawnServer: fires SERVER-SIDE khi má»™t connection trá»Ÿ thÃ nh observer cá»§a object nÃ y.
        // Náº¿u log nÃ y KHÃ”NG xuáº¥t hiá»‡n khi client connect â†’ observer/WriteSpawn chÆ°a Ä‘Æ°á»£c gá»i
        // (problem náº±m á»Ÿ FishNet observer layer, khÃ´ng pháº£i WorldItem code)
        // Náº¿u log nÃ y XUáº¤T HIá»†N nhÆ°ng client váº«n khÃ´ng tháº¥y model â†’ problem phÃ­a client
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ OnSpawnServer â”€â”€ conn={connection.ClientId} " +
                      $"ObjId={ObjectId} defID='{_itemData.DefinitionID}'");
        }

        public override void OnStartClient()
        {
            // !! ENTRY: náº¿u log nÃ y khÃ´ng xuáº¥t hiá»‡n â†’ FishNet khÃ´ng gá»i OnStartClient
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ OnStartClient ENTRY â”€â”€ ObjId={ObjectId} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            base.OnStartClient();

            // Dedicated server khÃ´ng cáº§n render gÃ¬
            if (IsServerStarted && !IsClientStarted)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] â”€â”€ OnStartClient â”€â”€ SKIP (dedicated server) ObjId={ObjectId}");
                return;
            }

            var syncVal = _syncItemData.Value;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ OnStartClient â”€â”€ ObjId={ObjectId} " +
                      $"syncVal='{syncVal.DefinitionID}' " +
                      $"_itemData='{_itemData.DefinitionID}' " +
                      $"_modelSpawned={_modelSpawned} " +
                      $"IsServer={IsServerStarted} IsClient={IsClientStarted}");

            if (_modelSpawned)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] OnStartClient: _modelSpawned=true â†’ skip ObjId={ObjectId}");
                return;
            }

            // Æ¯u tiÃªn syncVal (Ä‘Ã£ embed tá»« spawn packet / set tá»« InitializeBeforeSpawn)
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
                        Debug.Log($"[WorldItem] OnStartClient: synced _itemData tá»« syncVal ObjId={ObjectId}");
                }

                SpawnModelLocal("OnStartClient");
            }
            else
            {
                // Edge case: data chÆ°a arrive â†’ poll
                Debug.LogWarning(
                    $"[WorldItem] OnStartClient: defID rá»—ng â†’ start WaitForDataCoroutine ObjId={ObjectId}. " +
                    "Náº¿u tháº¥y log nÃ y thÆ°á»ng xuyÃªn â†’ InitializeBeforeSpawn() chÆ°a Ä‘Æ°á»£c gá»i trÆ°á»›c Spawn()!");
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
                Debug.Log($"[WorldItem] â”€â”€ OnStopNetwork â”€â”€ ObjId={ObjectId}");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // SERVER API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Gá»i TRÆ¯á»šC ServerManager.Spawn() Ä‘á»ƒ data Ä‘Æ°á»£c embed vÃ o spawn packet.
        ///
        /// VÃŒ SAO PHáº¢I DÃ™NG METHOD NÃ€Y (khÃ´ng dÃ¹ng Initialize sau Spawn):
        ///   FishNet embed SyncVar value vÃ o spawn packet khi Spawn() Ä‘Æ°á»£c gá»i.
        ///   Host mode: OnStartClient() cháº¡y synchronously BÃŠN TRONG Spawn() â€”
        ///   náº¿u SyncVar chÆ°a set thÃ¬ OnStartClient tháº¥y data rá»—ng â†’ khÃ´ng cÃ³ model.
        ///
        /// KHÃ”NG gá»i method nÃ y sau Spawn â€” dÃ¹ng UpdateData() náº¿u cáº§n update sau.
        ///
        /// LÆ¯U Ã: KHÃ”NG ÄÆ¯á»¢C dÃ¹ng [Server] attribute VÃ€ KHÃ”NG guard NetworkManager á»Ÿ Ä‘Ã¢y!
        ///   [Server] inject: if (!IsServerInitialized = IsSpawned && IsServerStarted) return;
        ///   NetworkManager property chá»‰ Ä‘Æ°á»£c FishNet gÃ¡n TRONG SpawnWithoutChecks,
        ///   trÆ°á»›c Ä‘Ã³ = null â†’ báº¥t ká»³ guard nÃ o dÃ¹ng NetworkManager Ä‘á»u early-return.
        ///   Caller (WorldSpawnManager) Ä‘Ã£ cÃ³ [Server] guard â†’ khÃ´ng cáº§n check láº¡i.
        /// </summary>
        public void InitializeBeforeSpawn(
            ItemInstanceData data,
            NightHunt.GameplaySystems.Core.Configs.LootableConfig lootableConfig = null)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ InitializeBeforeSpawn ENTRY â”€â”€ defID='{data.DefinitionID}'");

            _itemData = data;
            _lootableConfig = lootableConfig;

            // Set SyncVar TRƯỚC Spawn → FishNet embed value vào spawn packet
            _syncItemData.Value = data;
            // Reset picked-up flag so that pooled NetworkObjects don't carry stale state.
            _syncIsPickedUp.Value = false;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ InitializeBeforeSpawn â”€â”€ " +
                      $"defID='{data.DefinitionID}' qty={data.Quantity} " +
                      $"syncVal='{_syncItemData.Value.DefinitionID}' ObjId={ObjectId}");
        }

        /// <summary>Update data SAU khi Ä‘Ã£ spawn (vÃ­ dá»¥: thay Ä‘á»•i quantity).</summary>
        [Server]
        public void UpdateData(ItemInstanceData data)
        {
            _itemData = data;
            _syncItemData.Value = data;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ UpdateData â”€â”€ defID='{data.DefinitionID}' ObjId={ObjectId}");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // SYNCVAR CALLBACK
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Fires trÃªn Má»ŒI side khi _syncItemData thay Ä‘á»•i.
        ///   asServer=true  â†’ server side (dedicated server hoáº·c host-server)
        ///   asServer=false â†’ client side (remote client hoáº·c host-client)
        ///
        /// HOST: fires 2 láº§n (asServer=true, asServer=false).
        /// DEDICATED CLIENT: fires 1 láº§n (asServer=false).
        /// </summary>
        private void OnSyncItemDataChanged(ItemInstanceData oldData, ItemInstanceData newData, bool asServer)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] â”€â”€ OnSyncItemDataChanged â”€â”€ asServer={asServer} " +
                      $"old='{oldData.DefinitionID}' â†’ new='{newData.DefinitionID}' " +
                      $"ObjId={ObjectId} _modelSpawned={_modelSpawned}");

            if (string.IsNullOrEmpty(newData.DefinitionID))
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] OnSyncItemDataChanged: newData.DefinitionID rá»—ng â†’ skip ObjId={ObjectId}");
                return;
            }

            _itemData = newData;

            SpawnModelLocal($"OnSyncItemDataChanged(asServer={asServer})");
        }

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        // MODEL SPAWNING
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        /// <summary>
        /// Instantiate GroundPrefab lÃ m visual child (non-networked, client-side only).
        ///
        /// GUARDS (theo thá»© tá»±):
        ///   1. Dedicated server â†’ skip (khÃ´ng cáº§n renderer)
        ///   2. _modelSpawned    â†’ skip (Ä‘Ã£ cÃ³ rá»“i)
        ///   3. DefinitionID rá»—ng â†’ skip (data chÆ°a sáºµn sÃ ng)
        ///   4. ItemDefinition null â†’ error + skip
        ///   5. DroppedPrefab null  â†’ error + skip
        /// </summary>
        private void SpawnModelLocal(string caller)
        {
            // !! ENTRY: náº¿u log nÃ y khÃ´ng xuáº¥t hiá»‡n â†’ SpawnModelLocal chÆ°a Ä‘Æ°á»£c gá»i
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal ENTRY [{caller}]: " +
                      $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted} " +
                      $"_modelSpawned={_modelSpawned} defID='{_itemData.DefinitionID}'");

            // â”€â”€ Guard 1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (IsServerStarted && !IsClientStarted)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP â€” dedicated server. ObjId={ObjectId}");
                return;
            }

            // â”€â”€ Guard 2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_modelSpawned)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: SKIP â€” already spawned. ObjId={ObjectId}");
                return;
            }

            // â”€â”€ Guard 3 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (string.IsNullOrEmpty(_itemData.DefinitionID))
            {
                Debug.LogWarning($"[WorldItem] SpawnModelLocal [{caller}]: SKIP â€” _itemData.DefinitionID rá»—ng. " +
                                 $"ObjId={ObjectId} IsServer={IsServerStarted} IsClient={IsClientStarted}");
                return;
            }

            // â”€â”€ Guard 4 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var def = ItemDatabase.GetDefinition(_itemData.DefinitionID);
            if (def == null)
            {
                Debug.LogError(
                    $"[WorldItem] SpawnModelLocal [{caller}]: ItemDatabase.GetDefinition('{_itemData.DefinitionID}') = NULL! " +
                    $"ObjId={ObjectId} â€” Kiá»ƒm tra: 1) ItemDatabase Ä‘Ã£ init chÆ°a? " +
                    $"2) DefinitionID '{_itemData.DefinitionID}' cÃ³ tá»“n táº¡i khÃ´ng?");
                return;
            }

            // â”€â”€ Guard 5 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (def.GroundPrefab == null)
            {
                Debug.LogError(
                    $"[WorldItem] SpawnModelLocal [{caller}]: def.GroundPrefab = NULL cho '{_itemData.DefinitionID}'! " +
                    $"ObjId={ObjectId} â€” VÃ o Inspector ItemDefinition '{_itemData.DefinitionID}' vÃ  gÃ¡n GroundPrefab.");
                return;
            }

            // â”€â”€ Instantiate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal [{caller}]: instantiating '{def.GroundPrefab.name}' " +
                      $"for '{_itemData.DefinitionID}' at {transform.position} ObjId={ObjectId}");

            _modelInstance = Instantiate(def.GroundPrefab, transform.position, transform.rotation, transform);

            // GroundPrefab pháº£i lÃ  pure visual â€” khÃ´ng Ä‘Æ°á»£c cÃ³ NetworkObject
            var modelNetObj = ComponentResolver.Find<NetworkObject>(_modelInstance)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            if (modelNetObj != null)
            {
                Debug.LogWarning($"[WorldItem] GroundPrefab '{def.ItemID}' cÃ³ NetworkObject â€” removing. " +
                                 "GroundPrefab pháº£i lÃ  pure visual.");
                Destroy(modelNetObj);
            }

            // Force-enable táº¥t cáº£ renderer
            var renderers = _modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = true;

            _modelSpawned = true;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[WorldItem] SpawnModelLocal SUCCESS [{caller}]: " +
                      $"defID='{_itemData.DefinitionID}' model='{_modelInstance.name}' " +
                      $"renderers={renderers.Length} ObjId={ObjectId}");

            // Delay 1 frame â†’ UpdateRenderers â†’ force-enable láº¡i
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
        /// Fallback: poll data tá»‘i Ä‘a 3 giÃ¢y.
        /// LÃ½ tÆ°á»Ÿng lÃ  KHÃ”NG BAO GIá»œ vÃ o Ä‘Ã¢y náº¿u InitializeBeforeSpawn Ä‘Æ°á»£c dÃ¹ng Ä‘Ãºng.
        /// Log warning Ä‘á»ƒ dá»… phÃ¡t hiá»‡n náº¿u flow sai.
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

            Debug.LogError($"[WorldItem] WaitForDataCoroutine: TIMEOUT {kTimeout}s ObjId={ObjectId} â€” " +
                           "KHÃ”NG CÃ“ MODEL! Root cause: InitializeBeforeSpawn() chÆ°a Ä‘Æ°á»£c gá»i " +
                           "TRÆ¯á»šC ServerManager.Spawn() trong WorldSpawnManager.");
            _waitDataCoroutine = null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // SERVER RPC â€” PICKUP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
                Debug.LogError($"[WorldItem] RequestPickup: khÃ´ng cÃ³ NetworkPlayer trÃªn '{playerNob.name}'.");
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: quÃ¡ xa ({dist:F2}m > {maxDist}m).");
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
                Debug.LogError($"[WorldItem] RequestPickup: IInventorySystem khÃ´ng tÃ¬m tháº¥y trÃªn '{player.name}'.");
                return;
            }

            if (inventory is NetworkBehaviour nb && !nb.IsServerInitialized)
            {
                Debug.LogError("[WorldItem] RequestPickup: IInventorySystem.IsServerInitialized=false.");
                return;
            }

            inventory.AddItem(_itemData.DefinitionID, _itemData.Quantity);
            // Mark as picked up via SyncVar so all clients immediately reflect the state.
            // This fires OnChange on every observer, letting their CanInteract() return false
            // without any client needing to track _isPickupPending.
            _syncIsPickedUp.Value = true;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[WorldItem] âœ“ Pickup: '{_itemData.DefinitionID}' Ã—{_itemData.Quantity} ClientId={conn.ClientId}");
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

        // â”€â”€ Gizmos â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _maxPickupDistance);
        }
    }
}
