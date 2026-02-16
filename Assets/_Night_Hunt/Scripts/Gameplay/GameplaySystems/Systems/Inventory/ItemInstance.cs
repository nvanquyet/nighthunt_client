using System;
using UnityEngine;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Runtime representation of an item instance
    /// Contains all data for a specific item in player's possession
    /// 
    /// Design:
    /// - Each item has unique InstanceID (GUID)
    /// - References ItemDefinition via DefinitionID
    /// - Stores runtime data (quantity, resource, attachments)
    /// - Can be converted to/from ItemInstanceData for network sync
    /// </summary>
    [System.Serializable]
    public class ItemInstance
    {
        #region Core Fields
        
        /// <summary>
        /// Unique identifier for this specific item instance
        /// Generated on creation, never changes
        /// </summary>
        public string InstanceID;
        
        /// <summary>
        /// Reference to ItemDefinition (ItemID from ScriptableObject)
        /// Used to lookup item properties from ItemDatabase
        /// </summary>
        public string DefinitionID;
        
        /// <summary>
        /// Stack quantity (1 for non-stackable items)
        /// Max determined by ItemDefinition.MaxStackSize
        /// </summary>
        public int Quantity;
        
        /// <summary>
        /// Position in inventory grid (-1 if in equipment/weapon slot)
        /// Can have gaps (e.g., items at index 0, 2, 5)
        /// </summary>
        public int InventoryIndex;
        
        #endregion
        
        #region Resource System
        
        /// <summary>
        /// Current resource value (ammo, durability, energy, etc.)
        /// Type determined by ItemDefinition.ResourceType
        /// 
        /// Examples:
        /// - Weapon: Current total ammo (300/300)
        /// - Armor: Current durability (150/150)
        /// - Flashlight: Current battery (3600/3600 seconds)
        /// </summary>
        public float CurrentResource;
        
        /// <summary>
        /// Current magazine ammo (WEAPONS ONLY)
        /// Separate from CurrentResource
        /// 
        /// Example: AK-47
        /// - CurrentResource: 270 (total ammo remaining)
        /// - CurrentMagazine: 30 (ammo in current magazine)
        /// - MagazineSize: 30 (from definition)
        /// </summary>
        public int CurrentMagazine;
        
        #endregion
        
        #region Attachments
        
        /// <summary>
        /// Array of attached item instance IDs
        /// Length = ItemDefinition.AttachmentSlots.Length
        /// null or empty string = no attachment in slot
        /// 
        /// Example: AK-47 with [Optic, Grip, Magazine, Barrel]
        /// AttachedItems = ["scope_instance_01", null, "mag_instance_02", "suppressor_01"]
        ///                  [Red Dot Sight]      [empty] [Ext. Mag]         [Suppressor]
        /// </summary>
        public string[] AttachedItems;
        
        #endregion
        
        #region Metadata
        
        /// <summary>
        /// Custom data string for future extensibility
        /// Can store JSON for quest data, crafting results, etc.
        /// </summary>
        public string CustomData;
        
        /// <summary>
        /// Unix timestamp when item was created
        /// Used for sorting (remove oldest first)
        /// </summary>
        public long CreatedTimestamp;
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Default constructor for FishNet serialization
        /// DO NOT USE directly - use other constructor
        /// </summary>
        public ItemInstance()
        {
            InstanceID = Guid.NewGuid().ToString();
            DefinitionID = string.Empty;
            Quantity = 1;
            InventoryIndex = -1;
            CurrentResource = 0f;
            CurrentMagazine = 0;
            AttachedItems = null;
            CustomData = string.Empty;
            CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        /// <summary>
        /// Create new item instance
        /// </summary>
        /// <param name="definitionID">ItemDefinition ID</param>
        /// <param name="quantity">Stack quantity</param>
        /// <param name="inventoryIndex">Position in inventory (-1 if equipped)</param>
        public ItemInstance(string definitionID, int quantity = 1, int inventoryIndex = -1)
        {
            InstanceID = Guid.NewGuid().ToString();
            DefinitionID = definitionID;
            Quantity = Mathf.Max(1, quantity);
            InventoryIndex = inventoryIndex;
            CurrentResource = 0f;
            CurrentMagazine = 0;
            AttachedItems = null;
            CustomData = string.Empty;
            CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        #endregion
        
        #region Serialization
        
        /// <summary>
        /// Convert to network-syncable struct
        /// Used by SyncList in InventorySystem
        /// </summary>
        public ItemInstanceData ToData()
        {
            return new ItemInstanceData
            {
                InstanceID = this.InstanceID,
                DefinitionID = this.DefinitionID,
                Quantity = this.Quantity,
                InventoryIndex = this.InventoryIndex,
                CurrentResource = this.CurrentResource,
                CurrentMagazine = this.CurrentMagazine,
                AttachedItems = this.AttachedItems,
                CustomData = this.CustomData,
                CreatedTimestamp = this.CreatedTimestamp
            };
        }
        
        #endregion
        
        #region Helpers
        
        /// <summary>
        /// Clone this item (new InstanceID)
        /// Used for splitting stacks
        /// </summary>
        public ItemInstance Clone()
        {
            return new ItemInstance(DefinitionID, Quantity, InventoryIndex)
            {
                InstanceID = Guid.NewGuid().ToString(),
                CurrentResource = this.CurrentResource,
                CurrentMagazine = this.CurrentMagazine,
                AttachedItems = this.AttachedItems != null ? (string[])this.AttachedItems.Clone() : null,
                CustomData = this.CustomData,
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
        
        /// <summary>
        /// Check if has attachment in specific slot
        /// </summary>
        public bool HasAttachment(int slotIndex)
        {
            if (AttachedItems == null || slotIndex < 0 || slotIndex >= AttachedItems.Length)
                return false;
            
            return !string.IsNullOrEmpty(AttachedItems[slotIndex]);
        }
        
        /// <summary>
        /// Get attachment instance ID in slot
        /// </summary>
        public string GetAttachment(int slotIndex)
        {
            if (AttachedItems == null || slotIndex < 0 || slotIndex >= AttachedItems.Length)
                return null;
            
            return AttachedItems[slotIndex];
        }
        
        /// <summary>
        /// Set attachment in slot
        /// </summary>
        public void SetAttachment(int slotIndex, string attachmentInstanceID)
        {
            if (AttachedItems == null)
                return;
            
            if (slotIndex >= 0 && slotIndex < AttachedItems.Length)
            {
                AttachedItems[slotIndex] = attachmentInstanceID;
            }
        }
        
        /// <summary>
        /// Clear attachment from slot
        /// </summary>
        public void ClearAttachment(int slotIndex)
        {
            SetAttachment(slotIndex, null);
        }
        
        /// <summary>
        /// Get number of attached items
        /// </summary>
        public int GetAttachmentCount()
        {
            if (AttachedItems == null)
                return 0;
            
            int count = 0;
            foreach (var item in AttachedItems)
            {
                if (!string.IsNullOrEmpty(item))
                    count++;
            }
            return count;
        }
        
        #endregion
        
        #region Debug
        
        public override string ToString()
        {
            return $"ItemInstance[{InstanceID.Substring(0, 8)}...] {DefinitionID} x{Quantity} @{InventoryIndex}";
        }
        
        #endregion
    }
}