using System;

namespace NightHunt.InteractionSystem.Core
{
    [Serializable]
    public struct StatModifier
    {
        public StatType statType;
        public ModifierType modifierType;
        public float value;
        public int priority; // Higher = apply later
        public string sourceId; // Which attachment/buff
    }

    public enum StatType
    {
        // Weapon
        Damage, Accuracy, Recoil, FireRate, ReloadSpeed,
        // Armor
        DamageReduction, MovementSpeed, Weight,
        // Helmet
        VisionRange, HeadshotProtection,
        // Universal
        Durability, RepairCost
    }

    public enum ModifierType
    {
        Additive,        // +10
        Multiplicative,  // *1.5
        Override         // = 100
    }
}