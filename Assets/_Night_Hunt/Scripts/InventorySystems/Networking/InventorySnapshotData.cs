using System;
using System.Collections.Generic;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Serializable inventory snapshot for network sync.
    /// </summary>
    [System.Serializable]
    public struct InventorySnapshotData : IEquatable<InventorySnapshotData>
    {
        public float Timestamp;
        public List<ItemInstanceData> Items;

        public bool Equals(InventorySnapshotData other)
        {
            return Timestamp.Equals(other.Timestamp) && Equals(Items, other.Items);
        }

        public override bool Equals(object obj)
        {
            return obj is InventorySnapshotData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Timestamp, Items);
        }
    }
}