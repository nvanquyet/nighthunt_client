using System;
using System.Collections.Generic;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Delta update for efficient network sync.
    /// </summary>
    [System.Serializable]
    public struct InventoryDeltaData : IEquatable<InventoryDeltaData>
    {
        public float Timestamp;
        public List<SlotChange> ChangedSlots;

        public bool Equals(InventoryDeltaData other)
        {
            return Timestamp.Equals(other.Timestamp) && Equals(ChangedSlots, other.ChangedSlots);
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryDeltaData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Timestamp, ChangedSlots);
        }
    }
}