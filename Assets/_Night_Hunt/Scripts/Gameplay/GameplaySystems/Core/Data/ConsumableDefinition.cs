using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Consumable item definition (food, medkits, potions, drinks …)
    /// Replaced original: added ConsumableEffectType enum + Duration/BuffID on ConsumableEffect.
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable_", menuName = "GameplaySystems/Items/Consumable Definition")]
    public class ConsumableDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Consumable;

        [Header("Usage Effects")]
        [Tooltip("All effects applied when this item finishes being consumed.")]
        public ConsumableEffect[] Effects;

        // ── Editor helpers ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Setup Default Medkit")]
        private void SetupDefaultMedkit()
        {
            DisplayName = "Medkit";
            Description = "Restores 50 HP instantly + 20 HP over 10 s.";
            IsStackable = true; MaxStackSize = 5; Weight = 0.3f;
            UsageDuration = 3.5f; CanCancelUsage = true; CanUseWhileMoving = false;
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
            Effects = new ConsumableEffect[]
            {
                new ConsumableEffect { EffectType = ConsumableEffectType.RestoreHealth,  StatType = PlayerStatType.Health, Value = 50f, IsInstant = false, Description = "Restore 50 HP" },
                new ConsumableEffect { EffectType = ConsumableEffectType.HealOverTime,   StatType = PlayerStatType.Health, Value = 20f, Duration = 10f,   Description = "Heal 2 HP/s for 10 s" },
            };
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Setup Default Energy Drink")]
        private void SetupDefaultEnergyDrink()
        {
            DisplayName = "Energy Drink";
            Description = "Restores 100 stamina instantly.";
            IsStackable = true; MaxStackSize = 3; Weight = 0.2f;
            UsageDuration = 2f; CanCancelUsage = true; CanUseWhileMoving = true;
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
            Effects = new ConsumableEffect[]
            {
                new ConsumableEffect { EffectType = ConsumableEffectType.RestoreStamina, StatType = PlayerStatType.Stamina, Value = 100f, IsInstant = true, Description = "Restore 100 Stamina" },
            };
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
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