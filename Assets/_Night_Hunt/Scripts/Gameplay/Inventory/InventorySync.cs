using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Loot;
using FishNet;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Network sync for inventory
    /// </summary>
    public class InventorySync : NetworkBehaviour
    {
        private readonly SyncVar<string> networkInventoryData = new SyncVar<string>();

        private InventorySystem inventorySystem;

        private void Awake()
        {
            inventorySystem = GetComponent<InventorySystem>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkInventoryData.OnChange += OnInventoryDataChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkInventoryData != null)
                networkInventoryData.OnChange -= OnInventoryDataChanged;
        }

        /// <summary>
        /// Server: Sync inventory data
        /// </summary>
        public void SyncInventory(List<InventorySlot> slots)
        {
            // Serialize inventory to JSON
            string jsonData = SerializeInventory(slots);
            networkInventoryData.Value = jsonData;
        }

        /// <summary>
        /// Client: Receive inventory data
        /// </summary>
        private void OnInventoryDataChanged(string oldData, string newData, bool asServer)
        {
            if (!asServer && inventorySystem != null)
            {
                // Deserialize and apply inventory
                var slots = DeserializeInventory(newData);
                inventorySystem.ApplyInventoryData(slots);
            }
        }

        /// <summary>
        /// Server: Request drop item
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void ServerRpc_RequestDrop(string itemId, int dropQty, Vector3 dropPosition)
        {
            if (inventorySystem == null) return;

            // Validate quantity
            var slot = inventorySystem.FindSlotWithItem(itemId);
            if (slot == null || slot.IsEmpty || slot.Quantity < dropQty)
            {
                Debug.LogWarning($"[InventorySync] Cannot drop: item not found or insufficient quantity");
                return;
            }

            // Remove from inventory
            if (inventorySystem.RemoveItem(itemId, dropQty))
            {
                // Spawn world loot
                SpawnWorldLoot(itemId, dropQty, dropPosition);
                
                // Sync inventory
                var slots = inventorySystem.GetItems();
                SyncInventory(slots);
            }
        }

        /// <summary>
        /// Server: Spawn world loot item
        /// </summary>
        [Server]
        private void SpawnWorldLoot(string itemId, int quantity, Vector3 position)
        {
            // Get item config to find prefab
            var itemConfig = GameConfigLoader.Instance?.GetItemConfigBase(itemId);
            if (itemConfig == null)
            {
                Debug.LogWarning($"[InventorySync] Item config not found: {itemId}");
                return;
            }

            // Get world prefab ID (from new BaseItemConfig or legacy)
            string prefabId = itemConfig.WorldPrefabId;

            if (string.IsNullOrEmpty(prefabId))
            {
                // Fallback: try to load default loot prefab
                prefabId = "DefaultLootPrefab";
            }

            // Load prefab (simplified - would use Resources/Addressables in production)
            GameObject lootPrefab = Resources.Load<GameObject>($"Prefabs/Loot/{prefabId}");
            if (lootPrefab == null)
            {
                // Create basic prefab if not found
                lootPrefab = new GameObject("LootItem");
                lootPrefab.AddComponent<NetworkLootItem>();
                var collider = lootPrefab.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.5f;
            }

            // Spawn loot
            GameObject lootObj = Instantiate(lootPrefab, position, Quaternion.identity);
            NetworkLootItem lootItem = lootObj.GetComponent<NetworkLootItem>();
            if (lootItem != null)
            {
                lootItem.Initialize(itemId, quantity);
            }

            // Spawn on network
            if (lootObj.GetComponent<NetworkObject>() != null)
            {
                // Already has NetworkObject, just spawn it
                var netObj = lootObj.GetComponent<NetworkObject>();
                if (!netObj.IsSpawned)
                {
                    var nm = InstanceFinder.NetworkManager;
                    if (nm != null && nm.ServerManager != null)
                    {
                        nm.ServerManager.Spawn(netObj);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[InventorySync] Loot prefab does not have NetworkObject component");
            }
        }

        /// <summary>
        /// Serialize inventory to JSON
        /// </summary>
        private string SerializeInventory(List<InventorySlot> slots)
        {
            // Simple serialization - in production, use proper JSON library
            var data = new System.Text.StringBuilder();
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    data.Append($"{slot.Item.ItemId}:{slot.Quantity};");
                }
            }
            return data.ToString();
        }

        /// <summary>
        /// Deserialize inventory from JSON
        /// </summary>
        private List<InventorySlot> DeserializeInventory(string jsonData)
        {
            var slots = new List<InventorySlot>();
            if (string.IsNullOrEmpty(jsonData)) return slots;

            // Simple deserialization - in production, use proper JSON library
            string[] entries = jsonData.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] parts = entry.Split(':');
                if (parts.Length == 2)
                {
                    string itemId = parts[0];
                    int quantity = int.Parse(parts[1]);
                    var itemConfig = NightHunt.Data.GameConfigLoader.Instance?.GetItemConfig(itemId);
                    if (itemConfig != null)
                    {
                        var slot = new InventorySlot();
                        slot.SetItem(itemConfig, quantity);
                        slots.Add(slot);
                    }
                }
            }
            return slots;
        }
    }
}

