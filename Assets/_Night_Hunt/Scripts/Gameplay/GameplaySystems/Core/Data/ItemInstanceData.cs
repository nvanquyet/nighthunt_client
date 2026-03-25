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
    /// - Equals() so sánh toàn bộ runtime-relevant fields để FishNet SyncList
    ///   dirty-detection hoạt động đúng (ammo, resource, index, attachments).
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
        /// So sánh toàn bộ runtime-relevant fields để FishNet SyncList dirty-detection hoạt động đúng.
        ///
        /// FishNet gọi Equals() trước khi mark SyncList element dirty.
        /// Nếu thiếu field (CurrentMagazine, CurrentResource, InventoryIndex, AttachedItems),
        /// các thay đổi đó sẽ không được broadcast tới clients.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is ItemInstanceData other)) return false;

            return InstanceID      == other.InstanceID
                && DefinitionID    == other.DefinitionID
                && Quantity        == other.Quantity
                && CurrentMagazine == other.CurrentMagazine
                && UnityEngine.Mathf.Approximately(CurrentResource, other.CurrentResource)
                && InventoryIndex  == other.InventoryIndex
                && AttachedItemsEqual(AttachedItems, other.AttachedItems);
        }

        private static bool AttachedItemsEqual(string[] a, string[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = InstanceID  != null ? InstanceID.GetHashCode()  : 0;
                hash = (hash * 397) ^ (DefinitionID != null ? DefinitionID.GetHashCode() : 0);
                hash = (hash * 397) ^ Quantity.GetHashCode();
                hash = (hash * 397) ^ CurrentMagazine.GetHashCode();
                hash = (hash * 397) ^ InventoryIndex.GetHashCode();
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