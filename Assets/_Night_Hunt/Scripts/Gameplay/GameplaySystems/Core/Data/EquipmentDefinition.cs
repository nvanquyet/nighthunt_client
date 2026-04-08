using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Definition asset for an equipment item (armour, clothing, backpack).
    ///
    /// DURABILITY MODEL:
    ///   StatConfig[MaxDurability]  — maximum durability (attachment-buffable).
    ///   instance.CurrentResource   — current durability at runtime.
    ///   DurabilityLossRate         — fraction of incoming damage subtracted from durability.
    /// </summary>
    [CreateAssetMenu(fileName = "Armor_", menuName = "NightHunt/Items/Armor Definition")]
    public class EquipmentDefinition : EquippableItemDefinition
    {
        public override ItemType Type => ItemType.Equipment;

        [Header("Stat Config")]
        [Tooltip("Drag an EquipmentStatConfig asset here.")]
        public EquipmentStatConfig StatConfig;

        [Header("Equipment Slot")]
        [Tooltip("Body slot this item occupies when equipped.")]
        public EquipmentSlotType EquipmentSlot = EquipmentSlotType.Chest;

        [Header("Durability")]
        [Tooltip("Fraction of incoming damage removed from durability per hit. " +
                 "1.0 = loses 1 durability per 1 damage taken.")]
        [Min(0f)] public float DurabilityLossRate = 1f;

        // ── EquippableItemDefinition overrides ───────────────────────────────
        protected override ItemStatConfig StatConfigBase => StatConfig;

        /// <summary>Starting durability = StatConfig[MaxDurability].</summary>
        public override float GetDefaultCurrentValue()
            => StatConfig != null ? StatConfig.GetStatValue(ItemStatType.MaxDurability) : 0f;

        /// <summary>Player-stat modifiers applied while this equipment is equipped.</summary>
        public PlayerStatModifier[] GetPlayerModifiers()
            => StatConfig?.PlayerModifiers;
    }
}