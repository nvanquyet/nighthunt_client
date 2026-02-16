namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Network-syncable item instance data
    /// Used in SyncList for automatic synchronization
    /// Keep as lightweight as possible for network efficiency
    /// 
    /// Design Notes:
    /// - This is a VALUE TYPE (struct) for FishNet SyncList
    /// - Contains only essential data
    /// - Can be converted to/from ItemInstance (reference type)
    /// </summary>
    [System.Serializable]
    public struct ItemInstanceData
    {
        /// <summary>
        /// Unique identifier for this item instance
        /// </summary>
        public string InstanceID;
        
        /// <summary>
        /// Reference to ItemDefinition ID
        /// </summary>
        public string DefinitionID;
        
        /// <summary>
        /// Stack quantity
        /// </summary>
        public int Quantity;
        
        /// <summary>
        /// Position in inventory grid (-1 if equipped)
        /// </summary>
        public int InventoryIndex;
        
        /// <summary>
        /// Current resource (ammo, durability, energy)
        /// </summary>
        public float CurrentResource;
        
        /// <summary>
        /// Current magazine ammo (weapons only)
        /// </summary>
        public int CurrentMagazine;
        
        /// <summary>
        /// Attached item instance IDs
        /// </summary>
        public string[] AttachedItems;
        
        /// <summary>
        /// Custom data string
        /// </summary>
        public string CustomData;
        
        /// <summary>
        /// Creation timestamp
        /// </summary>
        public long CreatedTimestamp;
        
        #region Conversion
        
        /// <summary>
        /// Convert to runtime ItemInstance
        /// Called on clients when receiving sync data
        /// </summary>
        public ItemInstance ToInstance()
        {
            return new ItemInstance(DefinitionID, Quantity, InventoryIndex)
            {
                InstanceID = this.InstanceID,
                CurrentResource = this.CurrentResource,
                CurrentMagazine = this.CurrentMagazine,
                AttachedItems = this.AttachedItems,
                CustomData = this.CustomData,
                CreatedTimestamp = this.CreatedTimestamp
            };
        }
        
        #endregion
        
        #region Equality (for SyncList comparisons)
        
        public override bool Equals(object obj)
        {
            if (!(obj is ItemInstanceData))
                return false;
            
            var other = (ItemInstanceData)obj;
            return InstanceID == other.InstanceID;
        }
        
        public override int GetHashCode()
        {
            return InstanceID != null ? InstanceID.GetHashCode() : 0;
        }
        
        public static bool operator ==(ItemInstanceData a, ItemInstanceData b)
        {
            return a.InstanceID == b.InstanceID;
        }
        
        public static bool operator !=(ItemInstanceData a, ItemInstanceData b)
        {
            return !(a == b);
        }
        
        #endregion
        
        #region Debug
        
        public override string ToString()
        {
            string id = InstanceID != null && InstanceID.Length > 8 
                ? InstanceID.Substring(0, 8) + "..." 
                : InstanceID;
            return $"Data[{id}] {DefinitionID} x{Quantity} @{InventoryIndex}";
        }
        
        #endregion
    }
}