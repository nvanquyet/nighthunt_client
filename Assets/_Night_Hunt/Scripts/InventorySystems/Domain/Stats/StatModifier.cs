namespace NightHunt.Inventory.Domain.Stats
{
    /// <summary>
    /// Represents a single stat modifier.
    /// </summary>
    [System.Serializable]
    public class StatModifier
    {
        public float Value;
        public string SourceId; // e.g., "Equip:item_ak47_instance_123"
    }
}