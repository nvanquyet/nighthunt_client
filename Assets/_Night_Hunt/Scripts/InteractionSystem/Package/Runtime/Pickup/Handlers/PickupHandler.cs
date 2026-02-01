using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.InteractionSystem.Inventory;

namespace NightHunt.InteractionSystem.Pickup.Handlers
{
    /// <summary>
    /// Handles server-side pickup logic with Fish-Networking.
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class PickupHandler : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private PickupSettings settings;

        private InventoryComponentBase inventory;
        private IInventoryNetworkSync inventoryNetworkSync;

        private void Awake()
        { 
            try
            {
                Debug.Log($"[PickupHandler] Awake - Go={gameObject.name}, Parent={transform.parent?.name ?? "None"}, Root={transform.root?.name ?? "None"}");
                
                // Use centralized component finder to search in hierarchy
                inventory = ComponentFinder.FindComponentInHierarchy<InventoryComponentBase>(gameObject, includeInactive: false);
            
                if (inventory == null)
                {
                    Debug.LogError($"[PickupHandler] InventoryComponentBase not found! Searched in: {gameObject.name}, parent, children, and root ({transform.root?.name ?? "None"}) and its children. Please ensure an inventory component is attached.");
                    // Don't destroy - just disable this component
                    enabled = false;
                    return;
                }
                
                Debug.Log($"[PickupHandler] Found InventoryComponentBase: {inventory.gameObject.name}");

                // Find IInventoryNetworkSync for network synchronization (interface allows package to work with any implementation)
                inventoryNetworkSync = ComponentFinder.FindInterfaceInHierarchy<IInventoryNetworkSync>(gameObject, includeInactive: false);
                if (inventoryNetworkSync == null)
                {
                    Debug.LogError($"[PickupHandler] ===== IInventoryNetworkSync NOT FOUND! =====");
                    Debug.LogError($"[PickupHandler] Items will NOT sync to clients!");
                    Debug.LogError($"[PickupHandler] Searched in: {gameObject.name}, parent, children, and root.");
                    Debug.LogError($"[PickupHandler] Please ensure InventoryNetworkSync component is attached to player GameObject!");
                }
                else
                {
                    Debug.Log($"[PickupHandler] ===== Found IInventoryNetworkSync =====");
                    Debug.Log($"[PickupHandler] Type: {inventoryNetworkSync.GetType().Name}");
                    Debug.Log($"[PickupHandler] IsSpawned: {inventoryNetworkSync.IsSpawned}");
                }

            // Validate settings - must be assigned in Inspector (no FindObjectOfType for headless server compatibility)
            if (settings == null)
            {
                Debug.LogWarning("[PickupHandler] PickupSettings not assigned in Inspector! Using default values. For headless server compatibility, settings must be assigned in prefab/Inspector, not found at runtime.");
                // Settings will be null, but we'll use default values in methods that need it
            }
                
                Debug.Log($"[PickupHandler] Awake completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PickupHandler] EXCEPTION in Awake for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                enabled = false;
            }
        }

        /// <summary>
        /// Request to pickup an item (called from client).
        /// </summary>
        public void RequestPickup(IPickupable pickupable)
        {
            Debug.Log($"[PickupHandler] RequestPickup called - pickupable={pickupable != null}, IsOwner={IsOwner}, IsSpawned={IsSpawned}");
            
            if (!IsOwner)
            {
                Debug.LogWarning($"[PickupHandler] Not owner! Cannot pickup. IsOwner={IsOwner}, Owner={Owner?.ClientId}");
                return;
            }

            if (pickupable == null)
            {
                Debug.LogWarning("[PickupHandler] Pickupable is null!");
                return;
            }

            // Validate on client first
            if (!pickupable.CanPickup(gameObject))
            {
                Debug.LogWarning($"[PickupHandler] Cannot pickup {pickupable.GetDisplayName()} - CanPickup returned false");
                return;
            }

            // Get NetworkObject from pickupable
            NetworkObject pickupableObject = (pickupable as MonoBehaviour)?.GetComponent<NetworkObject>();
            if (pickupableObject == null)
            {
                Debug.LogWarning($"[PickupHandler] Pickupable {pickupable.GetDisplayName()} does not have NetworkObject component!");
                return;
            }

            Debug.Log($"[PickupHandler] Sending ServerRpc for pickupable {pickupable.GetDisplayName()}, NetworkObject={pickupableObject.ObjectId}");
            // Send to server
            ServerRequestPickup(pickupableObject);
        }

        /// <summary>
        /// Auto pickup an item (called from AutoPickupTrigger).
        /// </summary>
        public void AutoPickup(IPickupable pickupable)
        {
            if (!IsOwner)
                return;

            if (pickupable == null)
                return;

            // Check if auto pickup is enabled
            if (settings != null && !settings.AutoPickupEnabled)
                return;

            // Validate
            if (!pickupable.CanPickup(gameObject))
                return;

            // Get NetworkObject from pickupable
            NetworkObject pickupableObject = (pickupable as MonoBehaviour)?.GetComponent<NetworkObject>();
            if (pickupableObject == null)
                return;

            // Send to server
            ServerRequestPickup(pickupableObject);
        }

        /// <summary>
        /// Server-side pickup request.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void ServerRequestPickup(NetworkObject pickupableObject)
        {
            try
            {
                Debug.Log($"[PickupHandler] ServerRequestPickup called - pickupableObject={pickupableObject != null}, IsServer={IsServer}");
                
                if (pickupableObject == null)
                {
                    Debug.LogWarning("[PickupHandler] ServerRpc: pickupableObject is null!");
                    return;
                }

                Debug.Log($"[PickupHandler] Getting IPickupable component from {pickupableObject.gameObject.name}");
                IPickupable pickupable = pickupableObject.GetComponent<IPickupable>();
                if (pickupable == null)
                {
                    Debug.LogWarning($"[PickupHandler] No IPickupable component found on {pickupableObject.gameObject.name}!");
                    return;
                }

                Debug.Log($"[PickupHandler] Validating distance for {pickupable.GetDisplayName()}");
                // Validate distance
                float maxDistance = settings != null ? settings.MaxPickupDistance : 5f;
                float distance = Vector3.Distance(transform.position, pickupableObject.transform.position);
                Debug.Log($"[PickupHandler] Distance: {distance}m (max: {maxDistance}m)");
                if (distance > maxDistance)
                {
                    Debug.LogWarning($"[PickupHandler] Pickup distance too far: {distance}m (max: {maxDistance}m)");
                    return;
                }

                // Validate line of sight
                LayerMask losLayers = settings != null ? settings.PickupLineOfSightLayers : -1;
                Vector3 direction = (pickupableObject.transform.position - transform.position).normalized;
                RaycastHit hit;
                if (Physics.Raycast(transform.position, direction, out hit, distance, losLayers))
                {
                    if (hit.collider.gameObject != pickupableObject.gameObject)
                    {
                        Debug.LogWarning($"[PickupHandler] Line of sight blocked by {hit.collider.gameObject.name}");
                        return;
                    }
                }

                Debug.Log($"[PickupHandler] Getting item data from {pickupable.GetDisplayName()}");
                // Get item data
                ItemDataBase itemData = pickupable.GetItemData();
                if (itemData == null)
                {
                    Debug.LogWarning($"[PickupHandler] GetItemData() returned null for {pickupable.GetDisplayName()}!");
                    return;
                }

                Debug.Log($"[PickupHandler] Creating item instance - itemData={itemData.name}, quantity={pickupable.GetQuantity()}");
                // Create item instance - preserve state if NetworkLootItem
                ItemInstance itemInstance;
                var networkLootItem = pickupable as Items.Runtime.NetworkLootItem;
                if (networkLootItem != null)
                {
                    Debug.Log($"[PickupHandler] Preserving state from NetworkLootItem");
                    // Preserve state from dropped item (durability, customData, attachments, etc.)
                    itemInstance = networkLootItem.GetItemInstance();
                }
                else
                {
                    Debug.Log($"[PickupHandler] Creating fresh item instance");
                    // Fresh item from spawner
                    itemInstance = itemData.CreateInstance(pickupable.GetQuantity());
                }

                if (!itemInstance.IsValid())
                {
                    Debug.LogError($"[PickupHandler] Failed to create valid item instance! instanceId={itemInstance.instanceId}, itemDataId={itemInstance.itemDataId}, quantity={itemInstance.quantity}");
                    return;
                }
                
                Debug.Log($"[PickupHandler] Item instance created successfully - instanceId={itemInstance.instanceId}, itemDataId={itemInstance.itemDataId}, quantity={itemInstance.quantity}");

                Debug.Log($"[PickupHandler] Checking inventory - inventory={inventory != null}");
                // Try to add to inventory
                if (inventory == null)
                {
                    Debug.LogError($"[PickupHandler] Inventory is null! Cannot add item.");
                    return;
                }

                Debug.Log($"[PickupHandler] Calling CanAddItem with itemDataId: '{itemInstance.itemDataId}'");
                bool canAdd = inventory.CanAddItem(itemInstance);
                Debug.Log($"[PickupHandler] CanAddItem={canAdd} for itemDataId: '{itemInstance.itemDataId}'");
                if (canAdd)
                {
                    Debug.Log($"[PickupHandler] Attempting to add item to inventory via network sync");
                    
                    // Use InventoryNetworkSync to add item (ensures sync to all clients)
                    if (inventoryNetworkSync != null && inventoryNetworkSync.IsSpawned)
                    {
                        Debug.Log($"[PickupHandler] Using InventoryNetworkSync.AddItemServer() to sync item to clients");
                        // AddItemServer validates on server and syncs to all clients via ObserversRpc
                        inventoryNetworkSync.AddItemServer(itemInstance.itemDataId, itemInstance.quantity);
                        
                        Debug.Log($"[PickupHandler] Successfully added {pickupable.GetDisplayName()} to inventory via network sync!");
                        
                        // Success - notify pickupable
                        pickupable.OnPickedUp(gameObject);

                        // Invoke pickup event
                        InventoryEvents.InvokeItemPickedUp(itemInstance, pickupable.GetDisplayName());
                        // Note: ItemAdded event will be fired by inventory component after sync

                        // Notify client - get connection from this NetworkBehaviour's owner (the player)
                        NetworkConnection ownerConn = Owner;
                        if (ownerConn != null)
                        {
                            Debug.Log($"[PickupHandler] Sending success RPC to client {ownerConn.ClientId}");
                            ClientOnPickupSuccess(ownerConn, pickupable.GetDisplayName());
                        }
                        else
                        {
                            Debug.LogWarning($"[PickupHandler] Owner connection is null!");
                        }
                    }
                    else
                    {
                        // Fallback: Direct add if InventoryNetworkSync not available (single player or testing)
                        Debug.LogWarning($"[PickupHandler] InventoryNetworkSync not available, using direct AddItem (items will NOT sync to clients!)");
                        if (inventory.AddItem(itemInstance))
                        {
                            Debug.Log($"[PickupHandler] Successfully added {pickupable.GetDisplayName()} to inventory (direct, no sync)!");
                            
                            // Success - notify pickupable
                            pickupable.OnPickedUp(gameObject);

                            // Invoke pickup event
                            InventoryEvents.InvokeItemPickedUp(itemInstance, pickupable.GetDisplayName());
                            // Note: ItemAdded event is already invoked by inventory component

                            // Notify client
                            NetworkConnection ownerConn = Owner;
                            if (ownerConn != null)
                            {
                                Debug.Log($"[PickupHandler] Sending success RPC to client {ownerConn.ClientId}");
                                ClientOnPickupSuccess(ownerConn, pickupable.GetDisplayName());
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[PickupHandler] AddItem returned false - inventory full?");
                            string reason = "Inventory full";
                            InventoryEvents.InvokePickupFailed(reason);
                            
                            NetworkConnection ownerConn = Owner;
                            if (ownerConn != null)
                            {
                                ClientOnPickupFailed(ownerConn, reason);
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[PickupHandler] Cannot add item - not enough space");
                    string reason = "Not enough space";
                    InventoryEvents.InvokePickupFailed(reason);
                    
                    NetworkConnection ownerConn = Owner;
                    if (ownerConn != null)
                    {
                        ClientOnPickupFailed(ownerConn, reason);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PickupHandler] EXCEPTION in ServerRequestPickup: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Client notification for successful pickup.
        /// </summary>
        [TargetRpc]
        private void ClientOnPickupSuccess(NetworkConnection conn, string itemName)
        {
            Debug.Log($"[PickupHandler] Picked up {itemName}");
            // Trigger pickup animation, sound, etc.
        }

        /// <summary>
        /// Client notification for failed pickup.
        /// </summary>
        [TargetRpc]
        private void ClientOnPickupFailed(NetworkConnection conn, string reason)
        {
            Debug.LogWarning($"[PickupHandler] Pickup failed: {reason}");
        }
    }
}
