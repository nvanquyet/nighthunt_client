using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class InventoryConfig
    {
        public int BackpackSlots;
        public float BaseWeightCapacity;
        public int QuickSlotCount;
        public float OverWeightSpeedPenalty;
        public float OverWeightStaminaPenalty;
    }
}