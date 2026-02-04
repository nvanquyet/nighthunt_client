using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Represents a single slot change.
    /// </summary>
    [System.Serializable]
    public struct SlotChange
    {
        public int SlotIndex;
        public ItemInstanceData Item; // Null if slot emptied
        public ChangeType Type;
    }

}