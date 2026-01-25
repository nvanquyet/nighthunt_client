using System;
using System.Collections.Generic;

namespace NightHunt.InteractionSystem.Core
{
    [Serializable]
    public struct ItemInstance : IEquatable<ItemInstance>
    {
        public string instanceId;
        public string itemDataId;
        public int quantity;
        public float durability;
        public Dictionary<string, object> metadata;

        // For equipped items
        public List<AttachmentInstance> attachments;

        public bool Equals(ItemInstance other) => instanceId == other.instanceId;
        public override int GetHashCode() => instanceId.GetHashCode();
    }
}