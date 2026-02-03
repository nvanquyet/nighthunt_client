using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Snapshot of inventory state for network sync and rollback.
    /// </summary>
    [Serializable]
    public class InventorySnapshot
    {
        public List<ItemInstanceData> Items;
        public Dictionary<string, ItemInstanceData> ItemMap; // InstanceId -> Data
        
        public InventorySnapshot()
        {
            Items = new List<ItemInstanceData>();
            ItemMap = new Dictionary<string, ItemInstanceData>();
        }
        
        public byte[] Serialize()
        {
            // TODO: Implement proper serialization (JSON, Binary, etc.)
            return System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        }
        
        public static InventorySnapshot Deserialize(byte[] data)
        {
            // TODO: Implement proper deserialization
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonUtility.FromJson<InventorySnapshot>(json);
        }
        
        public List<ItemInstance> GetAllItemInstances(Dictionary<string, ItemDefinition> itemDefinitions)
        {
            var instances = new List<ItemInstance>();
            
            foreach (var itemData in Items)
            {
                if (itemDefinitions.TryGetValue(itemData.ItemId, out var definition))
                {
                    var instance = ItemInstance.Deserialize(itemData, definition);
                    instances.Add(instance);
                }
            }
            
            return instances;
        }
    }
}
