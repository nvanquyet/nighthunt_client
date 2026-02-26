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
            if (IsPickedUp) return false;
            if (!IsObjectInitialized) return false;
            return Vector3.Distance(transform.position, interactor.transform.position) <= GetInteractDistance();
        }

        public void Interact(GameObject interactor) => RequestPickup();

        public void OnHoverEnter(GameObject interactor) { /* TODO: outline effect */ }
        public void OnHoverExit(GameObject interactor)  { /* TODO: outline effect */ }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncItemData.OnChange += OnItemDataChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncItemData.OnChange -= OnItemDataChanged;
        }

        [Server]
        public void Initialize(ItemInstanceData data, NightHunt.GameplaySystems.Core.Configs.LootableConfig lootableConfig = null)
        {
            itemData = data;
            _lootableConfig = lootableConfig;
            _initialized = true;
            syncItemData.Value = data;
            SpawnModel();
        }

        [Server]
        private void SpawnModel()
        {
            var def = ItemDatabase.GetDefinition(itemData.DefinitionID);
            if (def == null || def.DroppedPrefab == null)
            {
                Debug.LogWarning($"[WorldItem] No DroppedPrefab for {itemData.DefinitionID}");
                return;
            }

            modelInstance = Instantiate(def.DroppedPrefab, transform.position, transform.rotation, transform);
            var modelNetObj = modelInstance.GetComponent<NetworkObject>();
            if (modelNetObj != null)
                ServerManager.Spawn(modelNetObj);
        }

        private void OnItemDataChanged(ItemInstanceData oldData, ItemInstanceData newData, bool asServer)
        {
            if (asServer) return;
            itemData = newData;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickup(NetworkConnection conn = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldItem] RequestPickup: server-only!");
                return;
            }

            var player = conn.FirstObject?.GetComponent<NetworkPlayer>();
            if (player == null)
            {
                var identity = conn.FirstObject;
                if (identity == null) { Debug.LogWarning("[WorldItem] RequestPickup: Player not found"); return; }
                player = identity.GetComponent<NetworkPlayer>();
                if (player == null) { Debug.LogWarning("[WorldItem] RequestPickup: NetworkPlayer not found"); return; }
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            float maxDist = GetInteractDistance();
            if (dist > maxDist)
            {
                Debug.LogWarning($"[WorldItem] RequestPickup: Too far ({dist:F2}m > {maxDist}m)");
                return;
            }

            var inventory = player.GetComponent<IInventorySystem>();
            if (inventory == null)
            {
                Debug.LogWarning("[WorldItem] RequestPickup: IInventorySystem not found on player");
                return;
            }

            inventory.AddItem(itemData.DefinitionID, itemData.Quantity);
            IsPickedUp = true;
            Debug.Log($"[WorldItem] ✓ Picked up: {itemData.DefinitionID} ×{itemData.Quantity} by client {conn?.ClientId}");
            DespawnPickup();
        }

        [Server]
        private void DespawnPickup()
        {
            if (modelInstance != null)
            {
                var modelNetObj = modelInstance.GetComponent<NetworkObject>();
                if (modelNetObj != null && modelNetObj.IsSpawned)
                    ServerManager.Despawn(modelNetObj);
                else
                    Destroy(modelInstance);
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
