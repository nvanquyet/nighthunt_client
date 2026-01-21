using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.Gameplay.Equipment
{
    /// <summary>
    /// Equipment entry for network sync
    /// </summary>
    [Serializable]
    public class EquipmentEntry
    {
        public Guid InstanceId;
        public string SlotId;
        public Dictionary<string, Guid> Sockets; // key = socketId, value = attached item instanceId

        public EquipmentEntry()
        {
            Sockets = new Dictionary<string, Guid>();
        }
    }

    /// <summary>
    /// Equipment manager - server-authoritative
    /// Handles equipping items to player slots and nested attachments
    /// </summary>
    public class EquipmentManager : NetworkBehaviour
    {
        [Header("Equipment Slots")]
        [SerializeField] private Transform[] playerEquipSlots; // Mount points for equipment (PrimaryWeapon, SecondaryWeapon, Head, Body, etc.)

        private Dictionary<string, ItemInstance> equippedItems; // key = slotId
        private InventorySystem inventorySystem;

        private readonly SyncList<EquipmentEntry> networkEquipment = new SyncList<EquipmentEntry>();

        private void Awake()
        {
            inventorySystem = GetComponent<InventorySystem>();
            equippedItems = new Dictionary<string, ItemInstance>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkEquipment.OnChange += OnEquipmentChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkEquipment != null)
                networkEquipment.OnChange -= OnEquipmentChanged;
        }

        /// <summary>
        /// Server: Equip item to player slot
        /// </summary>
        [Server]
        public bool EquipToPlayerSlot(string slotId, Guid itemInstanceId)
        {
            if (inventorySystem == null) return false;

            // Find item in inventory
            ItemInstance itemInstance = FindItemInstanceInInventory(itemInstanceId);
            if (itemInstance == null)
            {
                Debug.LogWarning($"[EquipmentManager] Item instance not found in inventory: {itemInstanceId}");
                return false;
            }

            // Validate slot
            if (!IsValidSlotForItem(slotId, itemInstance))
            {
                Debug.LogWarning($"[EquipmentManager] Invalid slot for item: {slotId}");
                return false;
            }

            // Unequip current item in slot if exists
            if (equippedItems.ContainsKey(slotId))
            {
                UnequipFromPlayerSlot(slotId);
            }

            // Equip new item
            equippedItems[slotId] = itemInstance;

            // Remove from inventory (or keep reference - depends on design)
            // For now, remove from inventory
            inventorySystem.RemoveItem(itemInstance.ItemId, 1);

            // Sync to clients
            SyncEquipmentToClients();

            return true;
        }

        /// <summary>
        /// Server: Unequip item from player slot
        /// </summary>
        [Server]
        public bool UnequipFromPlayerSlot(string slotId)
        {
            if (!equippedItems.ContainsKey(slotId)) return false;

            var itemInstance = equippedItems[slotId];

            // Detach all sockets first
            var socketIds = new List<string>(itemInstance.Sockets.Keys);
            foreach (var socketId in socketIds)
            {
                DetachFromSocket(itemInstance.InstanceId, socketId);
            }

            // Return to inventory
            if (inventorySystem != null)
            {
                inventorySystem.AddItem(itemInstance.ItemId, 1);
            }

            // Remove from equipped
            equippedItems.Remove(slotId);

            // Sync to clients
            SyncEquipmentToClients();

            return true;
        }

        /// <summary>
        /// Server: Attach item to socket on another item
        /// </summary>
        [Server]
        public bool AttachToSocket(Guid parentInstanceId, string socketId, Guid childInstanceId)
        {
            // Find parent item (could be in inventory or equipped)
            ItemInstance parentItem = FindItemInstance(parentInstanceId);
            if (parentItem == null)
            {
                Debug.LogWarning($"[EquipmentManager] Parent item not found: {parentInstanceId}");
                return false;
            }

            // Find child item
            ItemInstance childItem = FindItemInstanceInInventory(childInstanceId);
            if (childItem == null)
            {
                Debug.LogWarning($"[EquipmentManager] Child item not found in inventory: {childInstanceId}");
                return false;
            }

            // Validate socket compatibility
            if (!IsSocketCompatible(parentItem, socketId, childItem))
            {
                Debug.LogWarning($"[EquipmentManager] Socket not compatible: {socketId}");
                return false;
            }

            // Attach
            if (parentItem.AttachToSocket(socketId, childItem))
            {
                // Remove child from inventory
                if (inventorySystem != null)
                {
                    inventorySystem.RemoveItem(childItem.ItemId, 1);
                }

                // Sync to clients
                SyncEquipmentToClients();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Server: Detach item from socket
        /// </summary>
        [Server]
        public bool DetachFromSocket(Guid parentInstanceId, string socketId)
        {
            ItemInstance parentItem = FindItemInstance(parentInstanceId);
            if (parentItem == null) return false;

            var detachedItem = parentItem.DetachFromSocket(socketId);
            if (detachedItem != null)
            {
                // Return to inventory
                if (inventorySystem != null)
                {
                    inventorySystem.AddItem(detachedItem.ItemId, 1);
                }

                // Sync to clients
                SyncEquipmentToClients();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Server: Sync equipment state to clients
        /// </summary>
        [Server]
        private void SyncEquipmentToClients()
        {
            networkEquipment.Clear();

            foreach (var kvp in equippedItems)
            {
                var entry = new EquipmentEntry
                {
                    InstanceId = kvp.Value.InstanceId,
                    SlotId = kvp.Key
                };

                // Add socket attachments
                foreach (var socketKvp in kvp.Value.Sockets)
                {
                    entry.Sockets[socketKvp.Key] = socketKvp.Value.InstanceId;
                }

                networkEquipment.Add(entry);
            }
        }

        /// <summary>
        /// Client: Handle equipment sync changes
        /// </summary>
        private void OnEquipmentChanged(SyncListOperation op, int index, EquipmentEntry oldItem, EquipmentEntry newItem, bool asServer)
        {
            if (asServer) return;

            // Update visual representation (spawn/despawn prefabs)
            // This would spawn equipped prefabs at mount points
            UpdateEquipmentVisuals();
        }

        /// <summary>
        /// Client: Update equipment visuals based on sync data
        /// </summary>
        private void UpdateEquipmentVisuals()
        {
            // TODO: Spawn prefabs from EquippedPrefabId at MountPoints
            // This would iterate through networkEquipment and spawn/update prefabs
        }

        /// <summary>
        /// Find item instance in inventory
        /// </summary>
        private ItemInstance FindItemInstanceInInventory(Guid instanceId)
        {
            if (inventorySystem == null) return null;

            var slots = inventorySystem.GetItems();
            foreach (var slot in slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    // Would need to track ItemInstance in InventorySlot
                    // For now, simplified check
                    // TODO: Store ItemInstance in InventorySlot
                }
            }

            return null;
        }

        /// <summary>
        /// Find item instance (inventory or equipped)
        /// </summary>
        private ItemInstance FindItemInstance(Guid instanceId)
        {
            // Check equipped items
            foreach (var item in equippedItems.Values)
            {
                if (item.InstanceId == instanceId)
                    return item;
            }

            // Check inventory
            return FindItemInstanceInInventory(instanceId);
        }

        /// <summary>
        /// Validate if slot is valid for item
        /// </summary>
        private bool IsValidSlotForItem(string slotId, ItemInstance itemInstance)
        {
            if (itemInstance.Config == null) return false;

            // Check if slotId is in EquipSlots
            if (itemInstance.Config.EquipSlots != null)
            {
                return Array.Exists(itemInstance.Config.EquipSlots, slot => slot == slotId);
            }

            return false;
        }

        /// <summary>
        /// Check if socket is compatible with item
        /// </summary>
        private bool IsSocketCompatible(ItemInstance parentItem, string socketId, ItemInstance childItem)
        {
            if (parentItem.Config?.Sockets == null) return false;
            if (childItem.Config == null) return false;

            // Find socket definition
            foreach (var socket in parentItem.Config.Sockets)
            {
                if (socket.SocketId == socketId)
                {
                    // Check if child item tags match allowed categories
                    if (socket.AllowedCategories != null && childItem.Config.Tags != null)
                    {
                        foreach (var allowedCat in socket.AllowedCategories)
                        {
                            if (Array.Exists(childItem.Config.Tags, tag => tag == allowedCat))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}

