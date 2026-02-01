using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Items;
using NightHunt.Gameplay.UI;
using NightHunt.Gameplay.Core;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.Gameplay.Inventory.Logic.Services;
using NightHunt.Gameplay.UI;
using NightHunt.Networking;
using FishNet;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Gameplay-facing adapter over the InteractionSystem inventory components.
    /// Uses InventoryComponentBase (GridInventoryComponent / ListInventoryComponent)
    /// as the underlying data source.
    /// Implements IInventoryService and fires InventoryLogicEvents for UI layer.
    /// 
    /// EVENT BRIDGE PATTERN:
    /// This service acts as a bridge between two event systems:
    /// 1. InteractionSystem.Events.InventoryEvents (package-level events)
    ///    - Fired by InteractionSystem components (GridInventoryComponent, EquipmentManager, etc.)
    ///    - This service subscribes to these events
    /// 2. Gameplay.Inventory.Events.InventoryLogicEvents (gameplay-level events)
    ///    - Fired by this service for UI layer consumption
    ///    - UI components (InventoryPanel, LootContainerPanel, etc.) subscribe to these
    /// 
    /// Flow: Package Components → InventoryEvents → InventoryService → InventoryLogicEvents → UI Components
    /// 
    /// This separation allows:
    /// - InteractionSystem package to remain independent
    /// - Gameplay layer to have its own event system for UI
    /// - Clear separation of concerns between package and gameplay code
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class InventoryService : MonoBehaviour, IInventoryService
    {
        [Header("References")]
        [SerializeField] private InventoryComponentBase inventoryComponent;
        [SerializeField] private CharacterStats characterStats;
        
        // Cached NetworkPlayer reference (to avoid repeated GetComponent calls)
        private NetworkPlayer _cachedNetworkPlayer;

        private void Awake()
        {
            try
            {
                if (inventoryComponent == null)
                {
                    // Try to find InventoryComponentBase: first in this object, then children, then root and root's children
                    inventoryComponent = GetComponent<InventoryComponentBase>();
                    if (inventoryComponent == null)
                        inventoryComponent = GetComponentInChildren<InventoryComponentBase>();
                    
                    // If still not found, try from root object
                    if (inventoryComponent == null)
                    {
                        GameObject root = GetRootObject();
                        if (root != gameObject)
                        {
                            inventoryComponent = root.GetComponent<InventoryComponentBase>();
                            if (inventoryComponent == null)
                                inventoryComponent = root.GetComponentInChildren<InventoryComponentBase>();
                        }
                    }
                    
                    if (inventoryComponent == null)
                    {
                        Debug.LogError($"[InventoryService] InventoryComponentBase not found! Searched in: {gameObject.name}, children, and root ({GetRootObject().name}) and its children. Please add GridInventoryComponent or ListInventoryComponent.");
                        // Don't destroy - just disable this component
                        enabled = false;
                        return;
                    }
                }

                if (characterStats == null)
                {
                    // Try to get from ComponentRegistry first (event-based, no FindObject)
                    NetworkPlayer networkPlayer = GetNetworkPlayer();
                    if (networkPlayer != null)
                    {
                        characterStats = ComponentRegistry.GetCharacterStats(networkPlayer);
                    }
                    
                    // Fallback to GetComponent if not in registry yet (might be called before CharacterStats.Awake)
                    if (characterStats == null)
                    {
                        characterStats = GetComponent<CharacterStats>();
                    }
                }

                // Initial sync: drive inventory capacity from character stats (single source of truth)
                SyncInventoryCapacityFromStats();

                // Check if running on server - skip UI event subscriptions on server (headless server compatibility)
                bool isServer = InstanceFinder.IsServer;
                if (isServer)
                {
                    // On server, we still need to subscribe to package events for game logic, but NOT fire UI events
                    // Subscribe to weight events from package to drive gameplay stats if needed
                    InventoryEvents.OnWeightChanged += HandleWeightChanged;
                    InventoryEvents.OnEquipmentChanged += HandleEquipmentChanged;
                    // Subscribe to package events but don't fire UI events (HandleItemAdded will check server)
                    InventoryEvents.OnItemAdded += HandleItemAdded;
                    InventoryEvents.OnItemRemoved += HandleItemRemoved;
                    InventoryEvents.OnItemQuantityChanged += HandleItemQuantityChanged;
                }
                else
                {
                    // On client, subscribe to all events and fire UI events
                    InventoryEvents.OnWeightChanged += HandleWeightChanged;
                    InventoryEvents.OnEquipmentChanged += HandleEquipmentChanged;
                    InventoryEvents.OnItemAdded += HandleItemAdded;
                    InventoryEvents.OnItemRemoved += HandleItemRemoved;
                    InventoryEvents.OnItemQuantityChanged += HandleItemQuantityChanged;
                }
                
                // Register this service in ComponentRegistry (event-based, no FindObject)
                RegisterInComponentRegistry();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InventoryService] EXCEPTION in Awake for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                enabled = false;
            }
        }

        /// <summary>
        /// Get cached NetworkPlayer reference (find once, reuse)
        /// </summary>
        private NetworkPlayer GetNetworkPlayer()
        {
            if (_cachedNetworkPlayer != null)
                return _cachedNetworkPlayer;
            
            // Find NetworkPlayer using ComponentFinder (searches in current, parent, children, root, root's children)
            _cachedNetworkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            
            return _cachedNetworkPlayer;
        }

        /// <summary>
        /// Register this service in ComponentRegistry (event-based, no FindObject after Awake)
        /// </summary>
        private void RegisterInComponentRegistry()
        {
            NetworkPlayer networkPlayer = GetNetworkPlayer();
            
            if (networkPlayer != null)
            {
                ComponentRegistry.RegisterInventoryService(networkPlayer, this);
            }
            else
            {
                Debug.LogWarning($"[InventoryService] NetworkPlayer not found! Cannot register in ComponentRegistry. GameObject: {gameObject.name}, Parent: {transform.parent?.name ?? "None"}");
            }
        }

        private void OnDestroy()
        {
            // Unregister from ComponentRegistry
            NetworkPlayer networkPlayer = GetNetworkPlayer();
            if (networkPlayer != null)
            {
                ComponentRegistry.UnregisterInventoryService(networkPlayer, this);
            }
            
            // Unsubscribe from events
            InventoryEvents.OnWeightChanged -= HandleWeightChanged;
            InventoryEvents.OnEquipmentChanged -= HandleEquipmentChanged;
            InventoryEvents.OnItemAdded -= HandleItemAdded;
            InventoryEvents.OnItemRemoved -= HandleItemRemoved;
            InventoryEvents.OnItemQuantityChanged -= HandleItemQuantityChanged;
        }

        // Event handlers for InteractionSystem events - fire our own events for UI layer
        private void HandleItemAdded(ItemInstance item)
        {
            // Only fire UI events on client (not on server for headless server compatibility)
            if (InstanceFinder.IsServer)
            {
                return;
            }
            
            InventoryLogicEvents.FireItemAdded(item);
            InventoryLogicEvents.FireInventoryChanged();
        }

        private void HandleItemRemoved(ItemInstance item, int removedQuantity)
        {
            // Only fire UI events on client (not on server for headless server compatibility)
            if (InstanceFinder.IsServer)
                return;
            
            InventoryLogicEvents.FireItemRemoved(item, removedQuantity);
            InventoryLogicEvents.FireInventoryChanged();
        }

        private void HandleItemQuantityChanged(ItemInstance item, int newQuantity)
        {
            // Only fire UI events on client (not on server for headless server compatibility)
            if (InstanceFinder.IsServer)
                return;
            
            InventoryLogicEvents.FireItemQuantityChanged(item, newQuantity);
            InventoryLogicEvents.FireInventoryChanged();
        }

        /// <summary>
        /// Ensure underlying inventory component capacity matches character stats.
        /// </summary>
        private void SyncInventoryCapacityFromStats()
        {
            if (inventoryComponent == null || characterStats == null)
                return;

            float capacity = characterStats.GetWeightCapacity();
            // InventoryComponentBase exposes the setter; capacity value itself is owned by CharacterStats.
            inventoryComponent.SetMaxWeight(capacity);
        }

        private void HandleEquipmentChanged()
        {
            // Equipment (backpack, armor, etc.) may modify stats including weight capacity.
            // Re-sync inventory capacity whenever equipment changes.
            SyncInventoryCapacityFromStats();
        }

        private void HandleWeightChanged(float currentWeight, float maxWeight)
        {
            if (characterStats == null)
                return;

            characterStats.SetWeight(currentWeight);

            // Use ComponentRegistry instead of GetComponent (event-based, no FindObject)
            NetworkPlayer networkPlayer = GetNetworkPlayer();
            if (networkPlayer != null)
            {
                var movement = ComponentRegistry.GetMovementController(networkPlayer);
                // Use character stats capacity as the authoritative source for penalties.
                float capacity = characterStats.GetWeightCapacity();
                if (movement != null && movement is CharacterPredictedMovement predictedMovement && capacity > 0f)
                {
                    float penalty = 1f - Mathf.Clamp01(currentWeight / capacity);
                    predictedMovement.SetWeightPenalty(penalty);
                }
            }
        }

        #region API compatible methods

        public float GetCurrentWeight()
        {
            return inventoryComponent != null ? inventoryComponent.CurrentWeight : 0f;
        }

        public float GetWeightCapacity()
        {
            return inventoryComponent != null ? inventoryComponent.MaxWeight : 0f;
        }

        public float GetWeightPercentage()
        {
            return inventoryComponent != null ? inventoryComponent.WeightPercentage : 0f;
        }

        /// <summary>
        /// Get all items as a simple list for legacy UI code.
        /// </summary>
        public List<ItemInstance> GetItems()
        {
            if (inventoryComponent == null)
                return new List<ItemInstance>();

            return new List<ItemInstance>(inventoryComponent.Items);
        }

        /// <summary>
        /// Check if inventory has item.
        /// </summary>
        public bool HasItem(string itemId, int quantity = 1)
        {
            if (inventoryComponent == null)
                return false;

            var item = inventoryComponent.FindItem(itemId);
            if (!item.HasValue)
                return false;

            return item.Value.quantity >= quantity;
        }

        /// <summary>
        /// Get item quantity.
        /// </summary>
        public int GetItemQuantity(string itemId)
        {
            if (inventoryComponent == null)
                return 0;

            var item = inventoryComponent.FindItem(itemId);
            if (!item.HasValue)
                return 0;

            return item.Value.quantity;
        }

        /// <summary>
        /// Add item to inventory.
        /// For multiplayer: Use InventoryNetworkSync.AddItemServer() instead
        /// </summary>
        public bool AddItem(string itemId, int quantity)
        {
            if (inventoryComponent == null)
                return false;

            // Check if we have network sync component
            var networkSync = GetComponent<Logic.Sync.InventoryNetworkSync>();
            if (networkSync != null && networkSync.IsSpawned && networkSync.IsServer)
            {
                // Server: Add directly and sync via network
                var itemInstance = new ItemInstance { itemDataId = itemId, quantity = quantity };
                bool success = inventoryComponent.AddItem(itemInstance);
                if (success)
                {
                    // Sync to all clients
                    networkSync.AddItemServer(itemId, quantity);
                }
                return success;
            }
            else
            {
                // Client or single player: Add directly
                var itemInstance = new ItemInstance { itemDataId = itemId, quantity = quantity };
                bool success = inventoryComponent.AddItem(itemInstance);
                // Events will be fired by HandleItemAdded
                return success;
            }
        }

        /// <summary>
        /// Remove item by id / quantity.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (inventoryComponent == null)
                return false;

            bool success = inventoryComponent.RemoveItem(itemId, quantity);
            // Events will be fired by HandleItemRemoved
            return success;
        }

        /// <summary>
        /// Get grid inventory component (if using grid-based inventory).
        /// </summary>
        public GridInventoryComponent GetGrid()
        {
            if (inventoryComponent is GridInventoryComponent gridComponent)
                return gridComponent;
            return null;
        }

        /// <summary>
        /// Get grid size.
        /// </summary>
        public (int width, int height) GetGridSize()
        {
            var grid = GetGrid();
            if (grid == null)
                return (0, 0);

            return grid.GetGridSize();
        }

        /// <summary>
        /// Get item at grid position.
        /// </summary>
        public ItemInstance? GetItemAt(int x, int y)
        {
            var grid = GetGrid();
            if (grid == null)
                return null;

            return grid.GetItemAt(x, y);
        }

        /// <summary>
        /// Use item by ID (delegates to ItemUsageSystem).
        /// </summary>
        public bool UseItem(string itemId)
        {
            // Use ComponentRegistry instead of GetComponent (event-based, no FindObject)
            NetworkPlayer networkPlayer = GetNetworkPlayer();
            if (networkPlayer != null)
            {
                var usageSystem = ComponentRegistry.GetItemUsageSystem(networkPlayer);
                if (usageSystem != null)
                {
                    return usageSystem.UseItem(itemId);
                }
            }
            return false;
        }

        /// <summary>
        /// Move item from one grid position to another.
        /// </summary>
        public bool MoveItem(string itemId, int fromX, int fromY, int toX, int toY)
        {
            var grid = GetGrid();
            if (grid == null)
                return false;

            var item = grid.GetItemAt(fromX, fromY);
            if (!item.HasValue)
                return false;

            // Verify item ID matches
            if (item.Value.itemDataId != itemId)
            {
                Debug.LogWarning($"[InventoryService] MoveItem: Item ID mismatch. Expected: {itemId}, Found: {item.Value.itemDataId}");
                return false;
            }

            // Remove from source
            if (!grid.RemoveItemAt(fromX, fromY))
                return false;

            // Place at destination (will fail if slot is occupied)
            bool success = grid.PlaceItemAt(toX, toY, item.Value);
            
            if (success)
            {
                // Fire event for UI layer
                InventoryLogicEvents.FireItemMoved(itemId, fromX, fromY, toX, toY);
                InventoryLogicEvents.FireInventoryChanged();
            }
            else
            {
                // Restore item to original position if move failed
                grid.PlaceItemAt(fromX, fromY, item.Value);
            }

            return success;
        }

        /// <summary>
        /// Assign item to quick slot.
        /// </summary>
        public bool AssignQuickSlot(int slotIndex, string itemId)
        {
            // TODO: Implement quick slot system with persistence
            bool success = HasItem(itemId);
            if (success)
            {
                InventoryLogicEvents.FireQuickSlotAssigned(slotIndex, itemId);
            }
            return success;
        }

        /// <summary>
        /// Clear quick slot.
        /// </summary>
        public bool ClearQuickSlot(int slotIndex)
        {
            // TODO: Implement quick slot system with persistence
            InventoryLogicEvents.FireQuickSlotCleared(slotIndex);
            return true;
        }

        /// <summary>
        /// Get quick slot item.
        /// </summary>
        public string GetQuickSlotItem(int slotIndex)
        {
            // TODO: Implement quick slot system with persistence
            return null;
        }

        /// <summary>
        /// Equip item to equipment slot.
        /// </summary>
        public bool EquipItem(string itemId, EquipmentSlotType slotType)
        {
            // TODO: Implement equipment system
            InventoryLogicEvents.FireItemEquipped(itemId, slotType);
            return true;
        }

        /// <summary>
        /// Unequip item from equipment slot.
        /// </summary>
        public bool UnequipItem(EquipmentSlotType slotType)
        {
            // TODO: Implement equipment system - get current equipped item
            string equippedItemId = GetEquippedItem(slotType);
            if (string.IsNullOrEmpty(equippedItemId))
                return false;

            InventoryLogicEvents.FireItemUnequipped(equippedItemId, slotType);
            return true;
        }

        /// <summary>
        /// Get equipped item from slot.
        /// </summary>
        public string GetEquippedItem(EquipmentSlotType slotType)
        {
            // TODO: Implement equipment system with persistence
            return null;
        }

        /// <summary>
        /// Equip weapon to weapon slot.
        /// </summary>
        public bool EquipWeapon(int weaponSlotIndex, string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                Debug.LogWarning($"[InventoryService] EquipWeapon: weaponId is null or empty");
                return false;
            }

            // Get WeaponSwitchingSystem from ComponentRegistry (multiplayer-safe, only for this player)
            NetworkPlayer networkPlayer = GetNetworkPlayer();
            if (networkPlayer == null)
            {
                Debug.LogError("[InventoryService] EquipWeapon: NetworkPlayer not found!");
                return false;
            }

            // Get WeaponSwitchingSystem from ComponentRegistry (only for this specific player)
            var weaponSystem = ComponentRegistry.GetWeaponSwitchingSystem(networkPlayer);
            
            // Fallback: Only search within this player's hierarchy (not entire scene)
            if (weaponSystem == null)
            {
                weaponSystem = networkPlayer.GetComponent<Weapons.WeaponSwitchingSystem>();
                if (weaponSystem == null)
                {
                    weaponSystem = networkPlayer.GetComponentInChildren<Weapons.WeaponSwitchingSystem>();
                }
            }

            if (weaponSystem == null)
            {
                Debug.LogWarning($"[InventoryService] EquipWeapon: WeaponSwitchingSystem not found on player!");
                return false;
            }

            // Check if weapon exists in inventory
            if (inventoryComponent == null)
            {
                Debug.LogError("[InventoryService] EquipWeapon: inventoryComponent is null!");
                return false;
            }

            // Check if item is in inventory (for grid inventory)
            if (inventoryComponent is GridInventoryComponent gridInventory)
            {
                var items = gridInventory.Items;
                bool foundInInventory = false;
                foreach (var item in items)
                {
                    if (item.itemDataId == weaponId)
                    {
                        foundInInventory = true;
                        break;
                    }
                }

                if (!foundInInventory)
                {
                    Debug.LogWarning($"[InventoryService] EquipWeapon: Weapon '{weaponId}' not found in inventory!");
                    return false;
                }
            }

            // Equip weapon using WeaponSwitchingSystem
            bool success = weaponSystem.EquipWeapon(weaponId, weaponSlotIndex);
            
            if (!success)
            {
                Debug.LogWarning($"[InventoryService] EquipWeapon: Failed to equip '{weaponId}' to slot {weaponSlotIndex}");
            }

            return success;
        }

        /// <summary>
        /// Drop item from inventory (removes item, should spawn in world).
        /// </summary>
        public bool DropItem(string itemId, int quantity)
        {
            return RemoveItem(itemId, quantity);
        }

        /// <summary>
        /// Get quick slots array (TODO: implement quick slot system).
        /// </summary>
        public InventorySlot[] GetQuickSlots()
        {
            // TODO: Return actual quick slots when system is implemented
            return new InventorySlot[4];
        }

        /// <summary>
        /// Get root GameObject (topmost parent or self if no parent)
        /// </summary>
        private GameObject GetRootObject()
        {
            Transform root = transform.root;
            return root != null ? root.gameObject : gameObject;
        }

        #endregion
    }
}

