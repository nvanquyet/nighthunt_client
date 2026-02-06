using System;
using System.Collections.Generic;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Serializable quick slot snapshot for network sync.
    /// Contains all quick slot items at a point in time.
    /// </summary>
    [Serializable]
    public struct QuickSlotSnapshotData : IEquatable<QuickSlotSnapshotData>
    {
        public float Timestamp;
        public ItemInstanceData[] Slots; // Array of 4 slots (indexed 0-3)

        public bool Equals(QuickSlotSnapshotData other)
        {
            if (!Timestamp.Equals(other.Timestamp)) return false;
            if (Slots == null && other.Slots == null) return true;
            if (Slots == null || other.Slots == null) return false;
            if (Slots.Length != other.Slots.Length) return false;
            
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].InstanceId != other.Slots[i].InstanceId ||
                    Slots[i].ItemId != other.Slots[i].ItemId ||
                    Slots[i].StackSize != other.Slots[i].StackSize)
                {
                    return false;
                }
            }
            
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is QuickSlotSnapshotData other && Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = Timestamp.GetHashCode();
            if (Slots != null)
            {
                foreach (var slot in Slots)
                {
                    hash = HashCode.Combine(hash, slot);
                }
            }
            return hash;
        }
    }
}
