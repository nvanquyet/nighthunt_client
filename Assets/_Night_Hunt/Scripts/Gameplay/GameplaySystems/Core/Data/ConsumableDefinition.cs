using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Definition asset for consumable items (food, medkits, potions, drinks …).
    /// Effects are declared in ConsumableStatConfig and applied by ConsumableHandler
    /// after the UsageDuration (inherited from UsableItemDefinition) elapses.
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable_", menuName = "NightHunt/Items/Consumable Definition")]
    public class ConsumableDefinition : UsableItemDefinition
    {
        public override ItemType Type => ItemType.Consumable;

        [Header("Stat Configuration")]
        [Tooltip("Stat config holding the consumable effects and any base stats.")]
        public ConsumableStatConfig StatConfig;

        /// <summary>All effects applied when this consumable is successfully used.</summary>
        public ConsumableEffect[] GetEffects() => StatConfig?.Effects;

        public float GetStatValue(ItemStatType statType)
            => StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;

        public bool HasStat(ItemStatType statType)
            => StatConfig != null && StatConfig.HasStat(statType);
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

        [Tooltip("If true the effect fires instantly; if false it spreads over UsageDuration.")]
        public bool IsInstant;

        [Tooltip("Human-readable description shown in UI.")]
        public string Description;
    }

    /// <summary>
    /// All effect categories a consumable can trigger.
    /// Never reorder or renumber existing entries — append new values at the end.
    /// </summary>
    public enum ConsumableEffectType
    {
        // Instant stat restore
        RestoreHealth       = 0,
        RestoreStamina      = 1,
        InstantHeal         = 2,

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
        Cure                = 32,
        NoiseReduce         = 33,   // BUFF_SILENT — reduces footstep/noise radius
        VisionIncrease      = 34,   // VISION_EYE  — expands vision radius

        // Special
        Revive              = 40,
        Teleport            = 41,
        UnlockObjective     = 42,   // KEY_CORE — unlocks an objective trigger

        // Deploy / Place on Ground
        DeployBeacon        = 50,   // DEPLOY_BEACON — places RespawnBeacon NetworkObject
        PlaceVisionNode     = 51,   // VISION_NODE + VISION_LIGHTPOINT — places scanner/light
        PlaceExplosiveTrap  = 52,   // TRAP_MINE — proximity explosive
        PlaceSlowField      = 53,   // TRAP_SHOCK — slow / stun field
        DeploySmokeScreen   = 54,   // (ThrowableDefinition uses Smoke type; kept for completeness)
        DisableBeacon       = 55,   // UTIL_EMP  — (ThrowableDefinition uses Impact type; kept for completeness)
    }
}