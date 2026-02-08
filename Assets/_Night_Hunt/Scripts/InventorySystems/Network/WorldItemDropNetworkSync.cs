using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Database;
using NightHunt.Inventory.Systems;

namespace NightHunt.Inventory.World
{
    /// <summary>
    /// Network synchronization for WorldItemDrop.
    /// Server spawns/despawns drops, clients see synchronized state.
    /// </summary>
    [RequireComponent(typeof(WorldItemDrop))]
    public class WorldItemDropNetworkSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldItemDrop worldItemDrop;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Synced item data
        private FishNet.Object.Synchronizing.SyncVar<ItemInstanceData> syncedItemData = 
            new FishNet.Object.Synchronizing.SyncVar<ItemInstanceData>();
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (worldItemDrop == null)
                worldItemDrop = GetComponent<WorldItemDrop>();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            Log("[SERVER] WorldItemDrop spawned");
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsServer)
            {
                // Initialize from synced data
                InitializeFromSyncedData();
            }
        }
        
        // === Server Initialization ===
        
        /// <summary>
        /// Initialize drop on server (called by PlayerInventoryController).
        /// </summary>
        public void InitializeOnServer(ItemInstance item)
        {
            if (!IsServer)
            {
                LogWarning("InitializeOnServer called on client!");
                return;
            }
            
            // Store synced data
            syncedItemData.Value = item.Serialize();
            
            // Initialize local drop
            worldItemDrop.Initialize(item);
            
            Log($"[SERVER] Initialized drop: {item.Definition.DisplayName}");
        }
        
        // === Client Initialization ===
        
        private void InitializeFromSyncedData()
        {
            var data = syncedItemData.Value;
            
            if (string.IsNullOrEmpty(data.InstanceId))
            {
                LogWarning("[CLIENT] Synced data is empty");
                return;
            }
            
            // Get definition
            var definition = ItemDefinitionDatabase.Instance.GetDefinition(data.ItemId);
            if (definition == null)
            {
                LogWarning($"[CLIENT] Unknown item: {data.ItemId}");
                return;
            }
            
            // Deserialize item
            var item = ItemInstance.Deserialize(data, definition);
            
            // Initialize drop
            worldItemDrop.Initialize(item);
            
            Log($"[CLIENT] Initialized drop from sync: {definition.DisplayName}");
        }
        
        // === Pickup Handling ===
        
        /// <summary>
        /// Request pickup (called by WorldItemDrop trigger on client).
        /// </summary>
        public void RequestPickup(PlayerInventoryController inventory)
        {
            if (!IsOwner && !IsServer)
            {
                // Call ServerRpc to request pickup
                RequestPickup_ServerRpc(inventory.Owner);
            }
            else if (IsServer)
            {
                // Server handles directly
                ProcessPickup(inventory);
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void RequestPickup_ServerRpc(NetworkConnection requester)
        {
            // Find player inventory
            var playerObject = requester.FirstObject;
            if (playerObject == null)
                return;
            
            var inventory = playerObject.GetComponent<PlayerInventoryController>();
            if (inventory == null)
                return;
            
            ProcessPickup(inventory);
        }
        
        private void ProcessPickup(PlayerInventoryController inventory)
        {
            if (!IsServer)
                return;
            
            var item = worldItemDrop.GetItem();
            if (item == null)
                return;
            
            // Try to add to inventory
            var result = inventory.PickupItem(item);
            
            if (result == Core.Enums.OperationResult.Success)
            {
                // Despawn drop
                ServerManager.Despawn(gameObject);
                Log($"[SERVER] Item picked up by {inventory.Owner.ClientId}");
            }
            else
            {
                Log($"[SERVER] Pickup failed: {result}");
            }
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[WorldItemDropNetworkSync] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[WorldItemDropNetworkSync] {message}");
        }
    }
}