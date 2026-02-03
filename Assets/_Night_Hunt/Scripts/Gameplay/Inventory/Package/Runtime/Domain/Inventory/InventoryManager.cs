using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Core inventory manager - handles all inventory operations.
    /// Server-authoritative, validates all operations.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int maxSlots = 20;
        
        private InventoryData inventoryData;
        private Dictionary<string, ItemDefinition> itemDefinitions; // ItemId -> Definition
        
        private void Awake()
        {
            inventoryData = new InventoryData(maxSlots);
            itemDefinitions = new Dictionary<string, ItemDefinition>();
            
            // Load all ItemDefinitions from Resources
            LoadItemDefinitions();
        }
        
        private void LoadItemDefinitions()
        {
            var definitions = Resources.LoadAll<ItemDefinition>("Items");
            foreach (var def in definitions)
            {
                itemDefinitions[def.ItemId] = def;
            }
        }
        
        public bool TryAddItem(ItemInstance item)
        {
            return inventoryData.TryAddItem(item);
        }
        
        public bool TryRemoveItem(string instanceId)
        {
            return inventoryData.TryRemoveItem(instanceId);
        }
        
        public ItemInstance GetItem(string instanceId)
        {
            return inventoryData.GetItem(instanceId);
        }
        
        public bool HasItem(string instanceId)
        {
            return inventoryData.HasItem(instanceId);
        }
        
        public List<InventorySlot> GetAllSlots()
        {
            return inventoryData.GetAllSlots();
        }
        
        public InventorySnapshot CreateSnapshot()
        {
            var snapshot = new InventorySnapshot();
            var slots = inventoryData.GetAllSlots();
            
            foreach (var slot in slots)
            {
                if (slot.Item != null)
                {
                    snapshot.Items.Add(slot.Item.Serialize());
                    snapshot.ItemMap[slot.Item.InstanceId] = slot.Item.Serialize();
                }
            }
            
            return snapshot;
        }
        
        public void ApplySnapshot(InventorySnapshot snapshot)
        {
            // Clear current inventory
            foreach (var slot in inventoryData.GetAllSlots())
            {
                slot.Item = null;
            }
            
            // Apply snapshot items
            foreach (var itemData in snapshot.Items)
            {
                if (itemDefinitions.TryGetValue(itemData.ItemId, out var definition))
                {
                    var instance = ItemInstance.Deserialize(itemData, definition);
                    inventoryData.TryAddItem(instance);
                }
            }
        }
        
        public void RestoreSnapshot(InventorySnapshot snapshot)
        {
            ApplySnapshot(snapshot);
        }
        
        public void SortInventory()
        {
            var slots = inventoryData.GetAllSlots();
            InventorySorter.SortByItemType(slots);
            FireInventoryChanged();
        }
        
        public void AutoStackInventory()
        {
            var slots = inventoryData.GetAllSlots();
            InventoryStacker.AutoStack(slots);
            FireInventoryChanged();
        }
        
        private void FireInventoryChanged()
        {
            var snapshot = CreateSnapshot();
            InventoryEvents.FireInventoryChanged(snapshot);
        }
    }
}
