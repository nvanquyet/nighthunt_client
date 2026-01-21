using System;

namespace NightHunt.Data
{
    /// <summary>
    /// Consumable item configuration
    /// Extends BaseItemConfig with consumable-specific fields
    /// </summary>
    [Serializable]
    public class ConsumableItemConfig : BaseItemConfig
    {
        public EffectType EffectType; // HealHP, HealStaminaOverTime, SpeedBuff...
        public float EffectValue;
        public float EffectDuration;
        public bool IsInstantUse; // true = không cần animation, false = có UseTime + animation

        public ConsumableItemConfig()
        {
            Type = ItemType.Consumable;
            IsStackable = true; // Consumables are typically stackable
        }
    }
}

