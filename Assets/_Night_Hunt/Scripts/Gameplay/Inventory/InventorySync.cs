using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Data;

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

