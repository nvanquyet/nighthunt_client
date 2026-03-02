using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
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
    /// </summary>
    public class WorldItem : NetworkBehaviour, IPickupable
    {
        /// <summary>Fired server-side khi WorldItem bị despawn (pickup hoặc expired).</summary>
        public event System.Action OnDespawned;

        [Header("Settings")]
        [Tooltip("Maximum distance to pickup — fallback khi không có LootableConfig.")]
        [SerializeField] private float maxPickupDistance = 3f;

        // Runtime config — inject từ WorldSpawnManager khi spawn (không gán trên prefab).
        private NightHunt.GameplaySystems.Core.Configs.LootableConfig _lootableConfig;

        // Source spawn point (nếu được spawn từ WorldItemSpawnPoint)
        private NightHunt.GameplaySystems.World.WorldItemSpawnPoint _sourcePoint;

        // SYNC: Item data (server-authoritative)
        private readonly SyncVar<ItemInstanceData> syncItemData = new SyncVar<ItemInstanceData>();

        // Local cache
        private ItemInstanceData itemData;
        private GameObject modelInstance;
        private bool _initialized;

        private bool IsObjectInitialized => _initialized || !string.IsNullOrEmpty(itemData.DefinitionID);

        public ItemInstanceData ItemData => itemData;
        public bool IsLootable => true;

        // ── IPickupable ──────────────────────────────────────────────────────────

        public string ItemDefinitionID => itemData.DefinitionID;
        public int Quantity => itemData.Quantity;
        public bool IsPickedUp { get; private set; }
        // Pending flag: true sau khi gửi RPC, reset khi object bị despawn.
        // Ngăn client spam pickup trước khi server xử lý xong.
        private bool _isPickupPending;

        // ── IInteractable ────────────────────────────────────────────────────────

        public string InteractLabel
        {
            get
            {
                var def = ItemDatabase.GetDefinition(itemData.DefinitionID);
                string name = def != null ? def.DisplayName : itemData.DefinitionID;
                return $"[F] Pick up {name} \xd7{itemData.Quantity}";
            }
        }

        private float GetInteractDistance() => _lootableConfig?.MaxInteractDistance ?? maxPickupDistance;

        public bool CanInteract(GameObject interactor)
        {
            if (IsPickedUp || _isPickupPending) return false;
            if (!IsObjectInitialized) return false;
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null || _isPickupPending) return;
            if (!IsSpawned || !IsClientStarted) return;

            var playerNob = interactor.GetComponent<NetworkObject>();
            if (playerNob == null)
            {
                Debug.LogError($"[WorldItem] Interact: interactor '{interactor.name}' không có NetworkObject!");
                return;
            }

            _isPickupPending = true;
            RequestPickup(playerNob);
        }

        public void OnHoverEnter(GameObject interactor) { /* outline effect wired up when highlight system is ready */ }
        public void OnHoverExit(GameObject interactor)  { /* outline effect wired up when highlight system is ready */ }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncItemData.OnChange += OnItemDataChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncItemData.OnChange -= OnItemDataChanged;
            _isPickupPending = false; // Reset khi object bị despawn (pickup thành công hoặc hết hiệu lực)

            // Destroy local visual model on all sides when the NetworkObject is despawned.
            // This covers both the server (after DespawnPickup) and all clients (receive
            // FishNet despawn packet → OnStopNetwork fires → model cleaned up here).
            if (modelInstance != null)
            {
                Destroy(modelInstance);
                modelInstance = null;
            }
        }

        /// <summary>
        /// Server: set item data.  The SyncVar fires OnItemDataChanged on ALL sides
        /// (including server with asServer=true) which then calls SpawnModelLocal().
        /// </summary>
        [Server]
        public void Initialize(ItemInstanceData data, NightHunt.GameplaySystems.Core.Configs.LootableConfig lootableConfig = null)
        {
            itemData = data;
            _lootableConfig = lootableConfig;
            _initialized = true;
            // Setting the SyncVar triggers OnItemDataChanged(empty, data, asServer=true)
            // on the server AND schedules a SyncVar update packet to all current observers.
            // For late-joining clients the current value is embedded in the spawn packet,
            // so OnItemDataChanged fires immediately after OnStartNetwork for them too.
            syncItemData.Value = data;
        }

        /// <summary>
        /// Fires on ALL sides (server + clients) whenever syncItemData changes.
        /// Creates (or recreates) the local client-side visual model.
        /// </summary>
        private void OnItemDataChanged(ItemInstanceData oldData, ItemInstanceData newData, bool asServer)
        {
            itemData = newData;
            _initialized = true;
            // Spawn a local-only visual model.  This is intentionally NOT a networked
            // NetworkObject — each peer (server and every client) creates its own copy.
            // This is the correct pattern for item visuals:
            //   • The WorldItem NetworkObject carries the data (SyncVar).
            //   • Each peer materialises the visual from ItemDatabase using that data.
            SpawnModelLocal();
        }

        /// <summary>
        /// Instantiate the DroppedPrefab as a local (non-networked) visual child.
        /// Runs on server AND every client.
        /// </summary>
        private void SpawnModelLocal()
        {
            if (modelInstance != null) return; // already present – idempotent

            if (string.IsNullOrEmpty(itemData.DefinitionID))
                return;

            var def = ItemDatabase.GetDefinition(itemData.DefinitionID);
            if (def == null || def.DroppedPrefab == null)
            {
                Debug.LogWarning($"[WorldItem] No DroppedPrefab for '{itemData.DefinitionID}'");
                return;
            }

            modelInstance = Instantiate(def.DroppedPrefab,
                transform.position, transform.rotation, transform);

            // Strip any NetworkObject from the model prefab – item-drop models must be
            // pure visuals.  Accidentally leaving a NetworkObject on the DroppedPrefab
            // would cause duplicate-spawn conflicts when this runs client-side.
            var netObj = modelInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                Debug.LogWarning($"[WorldItem] DroppedPrefab '{def.ItemID}' has a NetworkObject " +
                                  "component — removing it. Item-drop models must be pure visuals.");
                Destroy(netObj);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickup(NetworkObject playerNob, NetworkConnection conn = null)
        {
            // ── Guard cơ bản ──────────────────────────────────────────────────
            if (conn == null)
            {
                Debug.LogError("[WorldItem] RequestPickup: conn is NULL — FishNet Codegen lỗi.");
                return;
            }
            if (!IsServerInitialized)
            {
                Debug.LogError("[WorldItem] RequestPickup: IsServerInitialized=false.");
                return;
            }
            if (playerNob == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: playerNob is NULL (ClientId={conn.ClientId}).");
                return;
            }

            // ── Verify ownership: chống cheat — client không được giả mạo playerNob của người khác ──
            if (playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: ClientId={conn.ClientId} cố pickup bằng playerNob '{playerNob.name}' không thuộc họ (owner={playerNob.OwnerId}).");
                return;
            }

            // ── Resolve NetworkPlayer ────────────────────────────────────────
            var player = playerNob.GetComponent<NetworkPlayer>();
            if (player == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: playerNob '{playerNob.name}' không có NetworkPlayer.");
                return;
            }

            // ── Kiểm tra khoảng cách ──────────────────────────────────────────
            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: Quá xa ({dist:F2}m > {maxDist}m)");
                return;
            }

            // ── Tìm inventory ─────────────────────────────────────────────────
            var inventory = player.GetComponent<IInventorySystem>()
                         ?? player.GetComponentInChildren<IInventorySystem>();

            if (inventory == null)
            {
                Debug.LogError($"[WorldItem] RequestPickup: Không tìm thấy IInventorySystem trên player '{player.name}'.");
                return;
            }

            if (inventory is FishNet.Object.NetworkBehaviour netInventory && !netInventory.IsServerInitialized)
            {
                Debug.LogError($"[WorldItem] RequestPickup: InventorySystem.IsServerInitialized=false.");
                return;
            }

            // ── Thêm item ─────────────────────────────────────────────────────
            inventory.AddItem(itemData.DefinitionID, itemData.Quantity);
            IsPickedUp = true;
            Debug.Log($"[WorldItem] ✓ Picked up: {itemData.DefinitionID} ×{itemData.Quantity} by client {conn.ClientId}");
            DespawnPickup();
        }

        [Server]
        private void DespawnPickup()
        {
            // modelInstance is a pure local GO (no NetworkObject).
            // base.Despawn() sends a despawn packet to all clients; each client's
            // OnStopNetwork fires and destroys their local modelInstance copy there.
            // We pre-destroy on the server side now so there is no 1-frame ghost.
            if (modelInstance != null)
            {
                Destroy(modelInstance);
                modelInstance = null;
            }

            OnDespawned?.Invoke();
            base.Despawn();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, maxPickupDistance);
        }
    }
}
