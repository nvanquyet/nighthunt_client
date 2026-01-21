namespace NightHunt.Data
{
    /// <summary>
    /// Item type enumeration
    /// </summary>
    public enum ItemType
    {
        Weapon,
        Armor,
        Attachment,
        Consumable,
        Gadget,
        Deployable,
        QuestItem,
        KeyItem,
        EventItem
    }

    /// <summary>
    /// Use type for items
    /// </summary>
    public enum UseType
    {
        Instant,
        Channel,
        PlaceOnGround,
        Throw
    }

    /// <summary>
    /// Target type for item usage
    /// </summary>
    public enum TargetType
    {
        Self,
        Ground,
        Ally
    }

    /// <summary>
    /// Effect type for consumables and items
    /// </summary>
    public enum EffectType
    {
        HealHP,
        HealStaminaOverTime,
        SpeedBuff,
        NoiseReduce,
        VisionIncrease,
        Cleanse,
        LightArea,
        PlaceVisionNode,
        ExplosiveTrap,
        SlowField,
        SmokeScreen,
        DisableBeacon
    }
}

