using System;
using System.Collections.Generic;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Delta update for quick slot network sync.
    /// Contains only changed slots for efficient transmission.
    /// </summary>
    [Serializable]
    public struct QuickSlotDeltaData : IEquatable<QuickSlotDeltaData>
    {
        public float Timestamp;
        public List<QuickSlotChange> ChangedSlots;

        public bool Equals(QuickSlotDeltaData other)
        {
            return Timestamp.Equals(other.Timestamp) && Equals(ChangedSlots, other.ChangedSlots);
        }

        public override bool Equals(object obj)
        {
            return obj is QuickSlotDeltaData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Timestamp, ChangedSlots);
        }
    }

    /// <summary>
    /// Represents a change to a single quick slot.
    /// </summary>
    [Serializable]
    public struct QuickSlotChange : IEquatable<QuickSlotChange>
    {
        public int SlotIndex; // 0-3
        public ItemInstanceData Item; // Empty InstanceId if slot cleared

        public bool Equals(QuickSlotChange other)
        {
            return SlotIndex == other.SlotIndex && 
                   Item.InstanceId == other.Item.InstanceId &&
                   Item.ItemId == other.Item.ItemId;
        }

        public override bool Equals(object obj)
        {
            return obj is QuickSlotChange other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SlotIndex, Item.InstanceId, Item.ItemId);
        }
    }
}
