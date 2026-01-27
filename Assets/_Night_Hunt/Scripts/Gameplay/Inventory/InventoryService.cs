using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Items;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Gameplay-facing adapter over the InteractionSystem inventory components.
    /// This replaces the old InventorySystem and exposes a similar API but uses
    /// InventoryComponentBase (GridInventoryComponent / ListInventoryComponent)
    /// as the underlying data source.
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class InventoryService : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryComponentBase inventoryComponent;
        [SerializeField] private CharacterStats characterStats;

        private void Awake()
        {
            try
            {
                Debug.Log($"[InventoryService] Awake - Go={gameObject.name}, Parent={transform.parent?.name ?? "None"}, Root={GetRootObject().name}");
                
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
                
                Debug.Log($"[InventoryService] Found InventoryComponentBase: {inventoryComponent.gameObject.name}");

                if (characterStats == null)
                {
                    characterStats = GetComponent<CharacterStats>();
                }

                // Subscribe to weight events from package to drive gameplay stats if needed
                InventoryEvents.OnWeightChanged += HandleWeightChanged;
                
                Debug.Log($"[InventoryService] Awake completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InventoryService] EXCEPTION in Awake for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            InventoryEvents.OnWeightChanged -= HandleWeightChanged;
        }

        private void HandleWeightChanged(float currentWeight, float maxWeight)
        {
            if (characterStats == null)
                return;

            characterStats.SetWeight(currentWeight);

            var movement = GetComponent<CharacterPredictedMovement>();
            if (movement != null && maxWeight > 0f)
            {
                float penalty = 1f - Mathf.Clamp01(currentWeight / maxWeight);
                movement.SetWeightPenalty(penalty);
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
        public bool HasItem(string itemId)
        {
            if (inventoryComponent == null)
                return false;

            return inventoryComponent.FindItem(itemId).HasValue;
        }

        /// <summary>
        /// Remove item by id / quantity.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (inventoryComponent == null)
                return false;

            return inventoryComponent.RemoveItem(itemId, quantity);
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
        /// Use item by ID (delegates to ItemUsageSystem).
        /// </summary>
        public bool UseItem(string itemId)
        {
            var usageSystem = GetComponent<ItemUsageSystem>();
            if (usageSystem != null)
            {
                return usageSystem.UseItem(itemId);
            }
            return false;
        }

        /// <summary>
        /// Move item from one grid position to another.
        /// </summary>
        public bool MoveItem(int fromX, int fromY, int toX, int toY)
        {
            var grid = GetGrid();
            if (grid == null)
                return false;

            var item = grid.GetItemAt(fromX, fromY);
            if (!item.HasValue)
                return false;

            // Remove from source
            if (!grid.RemoveItemAt(fromX, fromY))
                return false;

            // Place at destination (will fail if slot is occupied)
            return grid.PlaceItemAt(toX, toY, item.Value);
        }

        /// <summary>
        /// Assign item to quick slot (TODO: implement quick slot system).
        /// </summary>
        public bool AssignQuickSlot(int quickSlotIndex, string itemId)
        {
            // TODO: Implement quick slot system
            Debug.LogWarning($"[InventoryService] AssignQuickSlot not yet implemented. Slot: {quickSlotIndex}, Item: {itemId}");
            return false;
        }

        /// <summary>
        /// Equip weapon to weapon slot (TODO: implement equipment system).
        /// </summary>
        public bool EquipWeapon(int weaponSlotIndex, string weaponId)
        {
            // TODO: Implement weapon equipment system
            Debug.LogWarning($"[InventoryService] EquipWeapon not yet implemented. Slot: {weaponSlotIndex}, Weapon: {weaponId}");
            return false;
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
            return new InventorySlot[4]; // Return empty array for now
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

