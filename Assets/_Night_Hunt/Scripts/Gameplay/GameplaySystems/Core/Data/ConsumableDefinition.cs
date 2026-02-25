using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Consumable item definition (food, medkits, potions, drinks …)
    /// Effects are now stored in ConsumableStatConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable_", menuName = "GameplaySystems/Items/Consumable Definition")]
    public class ConsumableDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Consumable;

        [Header("Stat Configuration")]
        [Tooltip("Stat config containing effects and base stats")]
        public ConsumableStatConfig StatConfig;

        /// <summary>
        /// Get consumable effects from StatConfig
        /// </summary>
        public ConsumableEffect[] GetEffects()
        {
            return StatConfig?.Effects;
        }
        
        /// <summary>
        /// Get stat value from StatConfig
        /// </summary>
        public float GetStatValue(ItemStatType statType)
        {
            return StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;
        }
        
        /// <summary>
        /// Check if item has specific stat
        /// </summary>
        public bool HasStat(ItemStatType statType)
        {
            return StatConfig != null && StatConfig.HasStat(statType);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public struct ConsumableEffect
    {
        [Tooltip("Type of effect to apply.")]
        public ConsumableEffectType EffectType;

        [Tooltip("Which player stat is targeted (for stat-based effects).")]
        public PlayerStatType StatType;

        [Tooltip("Amount / magnitude.")]
        public float Value;

        [Tooltip("Duration in seconds for time-based effects (HealOverTime, buffs …). 0 = permanent.")]
        public float Duration;

        [Tooltip("Buff / debuff ID passed to the buff system.")]
        public string BuffID;

        [Tooltip("If true, effect fires instantly; if false, spreads over UsageDuration.")]
        public bool IsInstant;

        [Tooltip("Human-readable description shown in UI.")]
        public string Description;
    }

    /// <summary>
    /// All effect categories a consumable can trigger.
    /// ⚠ Never reorder or renumber existing entries – only append new ones.
    /// </summary>
    public enum ConsumableEffectType
    {
        // Instant stat restore
        RestoreHealth       = 0,
        RestoreStamina      = 1,
        InstantHeal         = 2,   // alias for RestoreHealth

        // Stat over time
        HealOverTime        = 10,
        StaminaOverTime     = 11,
        DamageOverTime      = 12,

        // Temporary stat modifiers (Duration = lifetime)
        SpeedBoost          = 20,
        DamageBoost         = 21,
        ArmorBoost          = 22,
        IncreaseMaxHealth   = 23,
        IncreaseMaxStamina  = 24,

        // Buff / debuff system hooks
        ApplyBuff           = 30,
        ApplyDebuff         = 31,
        Cure                = 32,  // remove debuffs

        // Special
        Revive              = 40,
    }
}