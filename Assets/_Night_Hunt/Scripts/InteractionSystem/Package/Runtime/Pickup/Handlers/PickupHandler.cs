using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Pickup.Handlers
{
    /// <summary>
    /// Handles server-side pickup logic with Fish-Networking.
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class PickupHandler : NetworkBehaviour
    {
        [Header("Pickup Settings")]
        [SerializeField] private float maxPickupDistance = 5f;
        [SerializeField] private LayerMask lineOfSightLayers = -1;

        private InventoryComponentBase inventory;
        private PickupSettings pickupSettings;

        private void Awake()
        {
            // Try to find InventoryComponentBase in this object, parent, or children
            inventory = GetComponentInParent<InventoryComponentBase>();
            if (inventory == null)
                inventory = GetComponentInChildren<InventoryComponentBase>();
            
            if (inventory == null)
            {
                Debug.LogError("[PickupHandler] InventoryComponentBase not found! Please ensure an inventory component is attached to the player or a child object.");
            }

            pickupSettings = FindObjectOfType<PickupSettings>();
        }

        /// <summary>
        /// Request to pickup an item (called from client).
        /// </summary>
        public void RequestPickup(IPickupable pickupable)
        {
            if (!IsOwner)
                return;

            if (pickupable == null)
                return;

            // Validate on client first
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
        /// Auto pickup an item (called from AutoPickupTrigger).
        /// </summary>
        public void AutoPickup(IPickupable pickupable)
        {
            if (!IsOwner)
                return;

            if (pickupable == null)
                return;

            // Check if auto pickup is enabled
            if (pickupSettings != null && !pickupSettings.AutoPickupEnabled)
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
            if (pickupableObject == null)
                return;

            IPickupable pickupable = pickupableObject.GetComponent<IPickupable>();
            if (pickupable == null)
                return;

            // Validate distance
            float distance = Vector3.Distance(transform.position, pickupableObject.transform.position);
            if (distance > maxPickupDistance)
            {
                Debug.LogWarning($"[PickupHandler] Pickup distance too far: {distance}m");
                return;
            }

            // Validate line of sight
            Vector3 direction = (pickupableObject.transform.position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, distance, lineOfSightLayers))
            {
                if (hit.collider.gameObject != pickupableObject.gameObject)
                {
                    Debug.LogWarning("[PickupHandler] Line of sight blocked");
                    return;
                }
            }

            // Get item data
            ItemDataBase itemData = pickupable.GetItemData();
            if (itemData == null)
                return;

            // Create item instance - preserve state if NetworkLootItem
            ItemInstance itemInstance;
            var networkLootItem = pickupable as Items.Runtime.NetworkLootItem;
            if (networkLootItem != null)
            {
                // Preserve state from dropped item (durability, customData, attachments, etc.)
                itemInstance = networkLootItem.GetItemInstance();
            }
            else
            {
                // Fresh item from spawner
                itemInstance = itemData.CreateInstance(pickupable.GetQuantity());
            }

            // Try to add to inventory
            if (inventory != null && inventory.CanAddItem(itemInstance))
            {
                if (inventory.AddItem(itemInstance))
                {
                    // Success - notify pickupable
                    pickupable.OnPickedUp(gameObject);

                    // Invoke pickup event
                    InventoryEvents.InvokeItemPickedUp(itemInstance, pickupable.GetDisplayName());
                    // Note: ItemAdded event is already invoked by inventory component

                    // Notify client - get connection from this NetworkBehaviour's owner (the player)
                    NetworkConnection ownerConn = Owner;
                    if (ownerConn != null)
                    {
                        ClientOnPickupSuccess(ownerConn, pickupable.GetDisplayName());
                    }
                }
                else
                {
                    string reason = "Inventory full";
                    InventoryEvents.InvokePickupFailed(reason);
                    
                    NetworkConnection ownerConn = Owner;
                    if (ownerConn != null)
                    {
                        ClientOnPickupFailed(ownerConn, reason);
                    }
                }
            }
            else
            {
                string reason = "Not enough space";
                InventoryEvents.InvokePickupFailed(reason);
                
                NetworkConnection ownerConn = Owner;
                if (ownerConn != null)
                {
                    ClientOnPickupFailed(ownerConn, reason);
                }
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
