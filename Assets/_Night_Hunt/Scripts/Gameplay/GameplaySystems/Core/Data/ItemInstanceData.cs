namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Network-syncable item instance data (VALUE TYPE / struct).
    ///
    /// RESPONSIBILITIES:
    /// - Lightweight struct for network synchronization
    /// - Used in SyncList / SyncVar for automatic synchronization
    /// - Can be converted to/from ItemInstance
    ///
    /// DESIGN:
    /// - VALUE TYPE (struct) — FishNet SyncVar / SyncList yêu cầu.
    /// - Equals() so sánh InstanceID + DefinitionID + Quantity để SyncVar dirty
    ///   detection hoạt động đúng khi Initialize() được gọi với data mới.
    ///   (Chỉ so sánh InstanceID như cũ khiến SyncVar không fire nếu cùng InstanceID
    ///   nhưng DefinitionID / Quantity khác nhau — ví dụ respawn cùng slot.)
    /// </summary>
    [System.Serializable]
    public struct ItemInstanceData
    {
        /// <summary>Unique identifier for this item instance.</summary>
        public string InstanceID;

        /// <summary>Reference to ItemDefinition ID.</summary>
        public string DefinitionID;

        /// <summary>Stack quantity.</summary>
        public int Quantity;

        /// <summary>Position in inventory grid (-1 if equipped).</summary>
        public int InventoryIndex;

        /// <summary>Current resource (ammo reserve, durability, energy).</summary>
        public float CurrentResource;

        /// <summary>Current magazine ammo (weapons only).</summary>
        public int CurrentMagazine;

        /// <summary>Attached item instance IDs.</summary>
        public string[] AttachedItems;

        /// <summary>Custom data string (JSON, quest data, etc.).</summary>
        public string CustomData;

        /// <summary>Unix timestamp khi item được tạo.</summary>
        public long CreatedTimestamp;

        #region Conversion

        /// <summary>
        /// Convert to runtime ItemInstance.
        /// Gọi trên client khi nhận sync data.
        /// </summary>
        public ItemInstance ToInstance()
        {
            return new ItemInstance(DefinitionID, Quantity, InventoryIndex)
            {
                InstanceID       = this.InstanceID,
                CurrentResource  = this.CurrentResource,
                CurrentMagazine  = this.CurrentMagazine,
                AttachedItems    = this.AttachedItems,
                CustomData       = this.CustomData,
                CreatedTimestamp = this.CreatedTimestamp
            };
        }

        #endregion

        #region Equality

        /// <summary>
        /// So sánh đầy đủ để FishNet SyncVar dirty-detection hoạt động đúng.
        ///
        /// QUAN TRỌNG: FishNet gọi Equals() trước khi mark SyncVar dirty.
        /// Nếu chỉ so InstanceID (như cũ), hai ItemInstanceData khác DefinitionID
        /// nhưng cùng InstanceID sẽ bị coi là BẰNG NHAU → SyncVar không fire
        /// → OnItemDataChanged không được gọi → model không spawn.
        ///
        /// Fix: so sánh InstanceID + DefinitionID + Quantity — đủ để detect
        /// mọi thay đổi có ý nghĩa với gameplay mà không quá nặng.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is ItemInstanceData other)) return false;

            return InstanceID    == other.InstanceID
                && DefinitionID  == other.DefinitionID
                && Quantity      == other.Quantity;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = InstanceID   != null ? InstanceID.GetHashCode()   : 0;
                hash = (hash * 397) ^ (DefinitionID != null ? DefinitionID.GetHashCode() : 0);
                hash = (hash * 397) ^ Quantity.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ItemInstanceData a, ItemInstanceData b) =>  a.Equals(b);
        public static bool operator !=(ItemInstanceData a, ItemInstanceData b) => !a.Equals(b);

        #endregion

        #region Debug

        public override string ToString()
        {
            string id = InstanceID != null && InstanceID.Length > 8
                ? InstanceID.Substring(0, 8) + "..."
                : InstanceID ?? "null";
            return $"Data[{id}] {DefinitionID} x{Quantity} @{InventoryIndex}";
        }

        #endregion
    }
}