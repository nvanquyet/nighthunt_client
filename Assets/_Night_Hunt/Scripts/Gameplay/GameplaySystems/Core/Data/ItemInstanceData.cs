using System;
namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Network-syncable snapshot of a runtime item instance (VALUE TYPE / struct).
    ///
    /// RESPONSIBILITIES:
    ///   - Lightweight struct used in SyncList / SyncVar for automatic replication.
    ///   - Converted to/from <see cref="ItemInstance"/> on each sync boundary.
    ///
    /// DESIGN:
    ///   - Must be a VALUE TYPE (struct) — required by FishNet SyncList.
    ///   - <see cref="Equals"/> compares all runtime-relevant fields so that FishNet's
    ///     SyncList dirty-detection fires correctly for every meaningful state change
    ///     (ammo, resource, inventory index, attachments).
    /// </summary>
    [System.Serializable]
    public struct ItemInstanceData
    {
        /// <summary>Unique identifier for this item instance.</summary>
        public string InstanceID;

        /// <summary>Reference to the ItemDefinition asset ID.</summary>
        public string DefinitionID;

        /// <summary>Stack quantity (1 for non-stackable items).</summary>
        public int Quantity;

        /// <summary>Position in the inventory grid (-1 when equipped or attached).</summary>
        public int InventoryIndex;

        /// <summary>Current resource level — ammo reserve, durability, or battery energy.</summary>
        public float CurrentResource;

        /// <summary>Rounds currently in the magazine (weapons only).</summary>
        public int CurrentMagazine;

        /// <summary>Instance IDs of items attached to this item's attachment sockets.</summary>
        public string[] AttachedItems;

        /// <summary>Arbitrary JSON / string payload for quest data, crafting results, etc.</summary>
        public string CustomData;

        /// <summary>Unix timestamp (seconds UTC) of when this instance was first created.</summary>
        public long CreatedTimestamp;

        #region Conversion

        /// <summary>
        /// Convert this network snapshot to a full runtime <see cref="ItemInstance"/>.
        /// Call on the client when sync data is received.
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
        /// Deep equality across all runtime-relevant fields.
        ///
        /// FishNet calls <c>Equals</c> before marking a SyncList element dirty.
        /// Omitting any changed field (CurrentMagazine, CurrentResource, InventoryIndex,
        /// AttachedItems) means that change is silently dropped and never broadcast to clients.
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
            if (a == null || b == null) return a == null && b == null;
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
                ? InstanceID[..8] + "..."
                : InstanceID ?? "null";
            return $"Data[{id}] {DefinitionID} x{Quantity} @{InventoryIndex}";
        }

        #endregion
    }
}