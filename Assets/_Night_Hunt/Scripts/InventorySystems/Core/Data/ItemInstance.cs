using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// Runtime instance of an item.
    /// Contains dynamic state that can change during gameplay.
    /// Replicated over network.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        [Header("Identity")]
        [Tooltip("Server-generated unique ID for this instance")]
        public string InstanceId;
        
        [Tooltip("Reference to the static item definition")]
        public ItemDefinition Definition;
        
        [Header("Runtime State")]
        [Tooltip("Current stack count (1 for non-stackable)")]
        public int StackSize = 1;
        
        [Tooltip("Current durability (0-100%)")]
        public float CurrentDurability;
        
        [Tooltip("Current ammo count for weapons")]
        public int CurrentAmmo;
        
        [Header("Attachments")]
        [Tooltip("Items attached to this item")]
        public List<ItemInstance> AttachedItems;
        
        /// <summary>
        /// Creates a new item instance.
        /// </summary>
        public ItemInstance()
        {
            AttachedItems = new List<ItemInstance>();
        }
        
        /// <summary>
        /// Creates a new item instance from a definition.
        /// </summary>
        public ItemInstance(ItemDefinition definition, string instanceId)
        {
            Definition = definition;
            InstanceId = instanceId;
            StackSize = 1;
            CurrentDurability = definition.MaxDurability;
            CurrentAmmo = 0;
            AttachedItems = new List<ItemInstance>();
        }
        
        /// <summary>
        /// Serializes this item instance for network sync and world drop persistence.
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
        /// Deserializes item instance data.
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
                AttachedItems = new List<ItemInstance>() // Populated separately
            };
        }
        
        /// <summary>
        /// Gets the total weight of this item including attachments.
        /// </summary>
        public float GetTotalWeight()
        {
            float weight = Definition.Weight * StackSize;
            
            foreach (var attachment in AttachedItems)
            {
                weight += attachment.Definition.Weight;
            }
            
            return weight;
        }
        
        /// <summary>
        /// Checks if this item can accept the given attachment.
        /// </summary>
        public bool CanAcceptAttachment(ItemInstance attachment)
        {
            // Check if attachment type is in allowed slots
            if (!Definition.AttachmentSlots.Contains(attachment.Definition.AttachmentType))
                return false;
            
            // Check if slot is already occupied
            var existingAttachment = AttachedItems.Find(a => 
                a.Definition.AttachmentType == attachment.Definition.AttachmentType);
            
            return existingAttachment == null;
        }
    }
    
    /// <summary>
    /// Serializable data structure for network transmission and persistence.
    /// </summary>
    [Serializable]
    public struct ItemInstanceData
    {
        public string InstanceId;
        public string ItemId;
        public int StackSize;
        public float CurrentDurability;
        public int CurrentAmmo;
        public string[] AttachedItemIds;
    }
}