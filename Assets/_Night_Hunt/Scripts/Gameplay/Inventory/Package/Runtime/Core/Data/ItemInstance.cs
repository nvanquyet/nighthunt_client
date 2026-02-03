using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Runtime item instance with mutable state.
    /// Replicated across network.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        public string InstanceId;              // Server-generated GUID
        
        [System.NonSerialized]
        public ItemDefinition Definition;      // Reference to static data (NOT serialized - contains Sprite)
        
        [Header("Runtime State")]
        public int StackSize = 1;              // Current stack count (1 for non-stackable)
        public float CurrentDurability;        // 0-100
        public int CurrentAmmo;                // For weapons (no separate ammo item)
        
        [Header("Attachments")]
        public List<ItemInstance> AttachedItems; // Nested items
        
        public ItemInstance()
        {
            AttachedItems = new List<ItemInstance>();
        }
        
        /// <summary>
        /// Serialize for network sync & world drop persistence.
        /// </summary>
        public ItemInstanceData Serialize()
        {
            return new ItemInstanceData
            {
                InstanceId = this.InstanceId,
                ItemId = this.Definition.ItemId,
                StackSize = this.StackSize,
                CurrentDurability = this.CurrentDurability,
                CurrentAmmo = this.CurrentAmmo,
                AttachedItemIds = this.AttachedItems.Select(a => a.InstanceId).ToArray()
            };
        }
        
        /// <summary>
        /// Deserialize from network data.
        /// Note: AttachedItems must be populated separately via network sync.
        /// </summary>
        public static ItemInstance Deserialize(ItemInstanceData data, ItemDefinition definition)
        {
            return new ItemInstance
            {
                InstanceId = data.InstanceId,
                Definition = definition,
                StackSize = data.StackSize,
                CurrentDurability = data.CurrentDurability,
                CurrentAmmo = data.CurrentAmmo,
                // AttachedItems populated separately via network sync
                AttachedItems = new List<ItemInstance>()
            };
        }
    }
}
