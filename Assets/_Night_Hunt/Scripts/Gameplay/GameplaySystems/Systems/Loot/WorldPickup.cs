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
    /// World pickup item — item dropped on the ground that a player can pick up.
    ///
    /// DESIGN (SRP / ISP):
    ///   Implements IPickupable (which extends IInteractable) so that
    ///   RaycastDetector, ProximityInteractScanner, and InteractionInputHandler
    ///   work through the interface and never need to know this concrete type.
    ///
    /// NETWORK: Server-authoritative.  Client calls Interact() → ServerRpc fires.
    /// </summary>
    public class WorldPickup : NetworkBehaviour, IPickupable
    {
        [Header("Settings")]
        [Tooltip("Maximum distance to pickup (meters)")]
        [SerializeField] private float maxPickupDistance = 3f;

        [Tooltip("Lootable config (optional - for future use)")]
        [SerializeField] private NightHunt.GameplaySystems.Core.Configs.LootableConfig lootConfig;

        // SYNC: Item data (server-authoritative)
        private readonly SyncVar<ItemInstanceData> syncItemData = new SyncVar<ItemInstanceData>();

        // Local cache
        private ItemInstanceData itemData;
        private GameObject modelInstance; // Spawned prefab model
        private bool _initialized;

        /// <summary>True once <see cref="Initialize"/> has been called on the server.</summary>
        private bool IsObjectInitialized => _initialized || !string.IsNullOrEmpty(itemData.DefinitionID);

        public ItemInstanceData ItemData => itemData;
        public bool IsLootable => true; // Future: can add locked/quest items

        // ── IPickupable ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string ItemDefinitionID => itemData.DefinitionID;

        /// <inheritdoc/>
        public int Quantity => itemData.Quantity;

        /// <inheritdoc/>
        public bool IsPickedUp { get; private set; }

        // ── IInteractable ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string InteractLabel
        {
            get
            {
                var def = ItemDatabase.GetDefinition(itemData.DefinitionID);
                string name = def != null ? def.DisplayName : itemData.DefinitionID;
                return $"[F] Pick up {name} \xd7{itemData.Quantity}";
            }
        }

        /// <inheritdoc/>
        public bool CanInteract(GameObject interactor)
        {
            if (IsPickedUp) return false;
            if (!IsObjectInitialized) return false;
            float dist = Vector3.Distance(transform.position, interactor.transform.position);
            return dist <= maxPickupDistance;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Called client-side.  Fires <see cref="RequestPickup"/> as a ServerRpc.
        /// FishNet automatically fills the <c>conn</c> parameter with the caller's connection.
        /// </remarks>
        public void Interact(GameObject interactor)
        {
            RequestPickup();
        }

        /// <inheritdoc/>
        public void OnHoverEnter(GameObject interactor)
        {
            // TODO: enable outline / highlight renderer effect here
        }

        /// <inheritdoc/>
        public void OnHoverExit(GameObject interactor)
        {
            // TODO: disable outline / highlight renderer effect here
        }

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

        /// <summary>
        /// Initialize world pickup with item data
        /// Server-only: Called when spawning
        /// </summary>
        [Server]
        public void Initialize(ItemInstanceData data)
        {
            itemData = data;
            _initialized = true;
            syncItemData.Value = data; // Broadcast to all clients

            // Spawn model (server spawns, clients see via network)
            SpawnModel();
        }

        [Server]
        private void SpawnModel()
        {
            var def = ItemDatabase.GetDefinition(itemData.DefinitionID);
            if (def == null || def.DroppedPrefab == null)
            {
                Debug.LogWarning($"[WorldPickup] No DroppedPrefab for {itemData.DefinitionID}");
                return;
            }

            // Spawn model as child
            modelInstance = Instantiate(def.DroppedPrefab, transform.position, transform.rotation, transform);
            
            // If model has NetworkObject, spawn it too
            var modelNetObj = modelInstance.GetComponent<NetworkObject>();
            if (modelNetObj != null)
            {
                ServerManager.Spawn(modelNetObj);
            }
        }

        /// <summary>
        /// CLIENT: Receive sync data
        /// </summary>
        private void OnItemDataChanged(ItemInstanceData oldData, ItemInstanceData newData, bool asServer)
        {
            if (asServer) return;
            
            itemData = newData;
            // Update visual if needed (e.g., quantity display)
        }

        /// <summary>
        /// SERVER: Pickup request from client.
        /// <para>
        /// <c>conn</c> is optional — when called without arguments (e.g. from
        /// <see cref="Interact"/>) FishNet auto-fills it with the sender's connection.
        /// The explicit overload is kept for backward compatibility with
        /// InteractionInputHandler.
        /// </para>
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestPickup(NetworkConnection conn = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldPickup] RequestPickup: server-only!");
                return;
            }

            // Validate distance - get player from connection
            var player = conn.FirstObject?.GetComponent<NetworkPlayer>();
            if (player == null)
            {
                // Try alternative: get from connection's identity
                var identity = conn.FirstObject;
                if (identity == null)
                {
                    Debug.LogWarning("[WorldPickup] RequestPickup: Player not found");
                    return;
                }
                player = identity.GetComponent<NetworkPlayer>();
                if (player == null)
                {
                    Debug.LogWarning("[WorldPickup] RequestPickup: NetworkPlayer component not found");
                    return;
                }
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > maxPickupDistance)
            {
                Debug.LogWarning($"[WorldPickup] RequestPickup: Too far ({dist:F2}m > {maxPickupDistance}m)");
                return;
            }

            // Get inventory system
            var inventory = player.GetComponent<IInventorySystem>();
            if (inventory == null)
            {
                Debug.LogWarning("[WorldPickup] RequestPickup: IInventorySystem not found on player");
                return;
            }

            // Add to inventory
            inventory.AddItem(itemData.DefinitionID, itemData.Quantity);

            IsPickedUp = true;
            Debug.Log($"[WorldPickup] ✓ Picked up: {itemData.DefinitionID} ×{itemData.Quantity} " +
                      $"by client {conn?.ClientId}");

            // Despawn (broadcast to all clients)
            DespawnPickup();
        }

        [Server]
        private void DespawnPickup()
        {
            // Despawn model if it has NetworkObject
            if (modelInstance != null)
            {
                var modelNetObj = modelInstance.GetComponent<NetworkObject>();
                if (modelNetObj != null && modelNetObj.IsSpawned)
                {
                    ServerManager.Despawn(modelNetObj);
                }
                else
                {
                    Destroy(modelInstance);
                }
            }

            // Despawn this NetworkObject (all clients will see it disappear)
            base.Despawn();
        }

        #region Debug

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, maxPickupDistance);
        }

        #endregion
    }
}
