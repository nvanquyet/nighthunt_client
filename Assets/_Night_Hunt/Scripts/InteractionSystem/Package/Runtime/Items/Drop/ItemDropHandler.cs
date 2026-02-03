using UnityEngine;
using FishNet.Object;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Items.Runtime;
using NightHunt.InteractionSystem.Loot.Definitions;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.InteractionSystem.Loot.Spawn;

namespace NightHunt.InteractionSystem.Items.Drop
{
    /// <summary>
    /// Handles dropping items from inventory to world (preserves state: durability, attachments, etc.).
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class ItemDropHandler : NetworkBehaviour
    {
        [Header("Drop Settings")]
        [Tooltip("Generic NetworkLootItem prefab to spawn when dropping items")]
        [SerializeField] private NetworkLootItem lootItemPrefab;

        [Tooltip("Offset from player position when dropping")]
        [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 1f);

        [Tooltip("Random spread radius when dropping")]
        [SerializeField] private float dropSpreadRadius = 0.5f;

        private InventoryComponentBase inventory;
        private LootItemDefinitionDatabase definitionDatabase;

        private void Awake()
        {
            // Use ComponentFinder to search in hierarchy (component might be in child)
            inventory = ComponentFinder.FindComponentInHierarchy<InventoryComponentBase>(gameObject, includeInactive: false);
            if (inventory == null)
            {
                Debug.LogError("[ItemDropHandler] InventoryComponentBase not found in hierarchy!");
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            definitionDatabase = LootItemDefinitionDatabase.Load();
        }

        /// <summary>
        /// Drop an item from inventory (client calls this).
        /// </summary>
        public void DropItem(ItemInstance itemInstance, Vector3? dropPosition = null)
        {
            if (!IsOwner)
                return;

            if (itemInstance.itemDataId == null || itemInstance.quantity <= 0)
            {
                Debug.LogWarning("[ItemDropHandler] Invalid item instance to drop.");
                return;
            }

            // Remove from inventory first (client-side validation)
            if (!inventory.RemoveItem(itemInstance.itemDataId, itemInstance.quantity))
            {
                Debug.LogWarning("[ItemDropHandler] Failed to remove item from inventory.");
                return;
            }

            // Calculate drop position
            Vector3 spawnPosition = dropPosition ?? (transform.position + transform.TransformDirection(dropOffset));
            Vector2 randomCircle = Random.insideUnitCircle * dropSpreadRadius;
            spawnPosition += new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Send to server
            ServerDropItem(itemInstance, spawnPosition);
        }

        /// <summary>
        /// Drop item by instance ID (for UI drag-drop).
        /// </summary>
        public void DropItemByInstanceId(string instanceId, Vector3? dropPosition = null)
        {
            if (!IsOwner)
                return;

            // Find item in inventory
            ItemInstance? item = null;
            foreach (var invItem in inventory.Items)
            {
                if (invItem.instanceId == instanceId)
                {
                    item = invItem;
                    break;
                }
            }

            if (!item.HasValue)
            {
                Debug.LogWarning($"[ItemDropHandler] Item with instanceId '{instanceId}' not found in inventory.");
                return;
            }

            DropItem(item.Value, dropPosition);
        }

        /// <summary>
        /// Server-side drop item.
        /// Uses LootSpawnManager for centralized spawning.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void ServerDropItem(ItemInstance itemInstance, Vector3 spawnPosition)
{
    // 1. Find LootItemDefinition
    LootItemDefinition lootDef = FindLootDefinitionForItem(itemInstance.itemDataId);
    if (lootDef == null)
    {
        Debug.LogError($"[Drop] No LootItemDefinition for {itemInstance.itemDataId}");
        return;
    }
    
    // 2. Get LootSpawnManager
    LootSpawnManager spawnManager = FindFirstObjectByType<LootSpawnManager>();
    if (spawnManager == null)
    {
        Debug.LogError("[Drop] LootSpawnManager not found!");
        return;
    }
    
    // 3. Spawn NetworkLootItem
    NetworkLootItem spawnedItem = spawnManager.SpawnItemAtPosition(
        itemInstance, 
        lootDef, 
        spawnPosition, 
        lootItemPrefab // Fallback prefab
    );
    
    if (spawnedItem != null)
    {
        Debug.Log($"[Drop] Spawned {itemInstance.itemDataId} at {spawnPosition}");
    }
}
        /// <summary>
        /// Find LootItemDefinition for an item ID.
        /// </summary>
        private LootItemDefinition FindLootDefinitionForItem(string itemId)
        {
            if (definitionDatabase == null)
                definitionDatabase = LootItemDefinitionDatabase.Load();

            if (definitionDatabase == null)
                return null;

            // Search through all definitions
            var allDefinitions = definitionDatabase.GetAllDefinitions();
            foreach (var def in allDefinitions)
            {
                if (def.ItemData != null && def.ItemData.ItemId == itemId)
                {
                    return def;
                }
            }

            return null;
        }

        /// <summary>
        /// Create a default LootItemDefinition if none exists (fallback).
        /// Note: This creates a runtime-only definition. Proper LootItemDefinitions should be created in editor.
        /// </summary>
        private LootItemDefinition CreateDefaultDefinition(string itemId)
        {
            // This is a fallback - ideally all items should have LootItemDefinitions
            Debug.LogWarning($"[ItemDropHandler] No LootItemDefinition found for {itemId}. Item will drop but may not have proper world model. Consider creating a LootItemDefinition asset.");
            
            // Return null - NetworkLootItem will handle missing definition gracefully
            // Or you can create a default definition asset in Resources folder
            return null;
        }
    }
}
