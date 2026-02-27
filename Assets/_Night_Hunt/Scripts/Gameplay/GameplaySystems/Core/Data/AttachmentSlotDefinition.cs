using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Attachment item definition.
    ///
    /// DUAL MODIFIER MODEL:
    /// - ItemStatModifiers  (StatConfig.ItemModifiers):
    ///     Modify the HOST item's own stats (Damage, MagazineSize, Armor…).
    ///     Generic — works for both Weapon and Equipment hosts.
    ///
    /// - PlayerStatModifiers (StatConfig.PlayerModifiers):
    ///     Modify PLAYER stats. Follow HOST apply-rule:
    ///       • Host = Equipment → applied immediately when equipment is equipped.
    ///       • Host = Weapon    → applied only when weapon is SELECTED (drawn).
    ///
    /// COMPATIBILITY:
    ///     CanAttachTo[]  on base ItemDefinition specifies which AttachmentSlotTypes
    ///     this attachment can go into (e.g. Optic, Barrel, Plate…).
    /// </summary>
    [CreateAssetMenu(fileName = "Attachment_", menuName = "GameplaySystems/Items/Attachment Definition")]
    public class AttachmentDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Attachment;

        // ── Stat Config ──────────────────────────────────────────────────────────
        [Header("Stat Config")]
        [Tooltip("AttachmentStatConfig: ItemModifiers (host item stats) + PlayerModifiers (player stats)")]
        public AttachmentStatConfig StatConfig;

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>All modifiers that change the HOST item's own stats (Damage, MagazineSize, ArmorValue…).</summary>
        public ItemStatModifier[] GetItemModifiers()
            => StatConfig?.ItemModifiers;

        /// <summary>
        /// Player stat modifiers — applied according to HOST item's rule
        /// (Equipment = on equip, Weapon = on select).
        /// </summary>
        public PlayerStatModifier[] GetPlayerModifiers()
            => StatConfig?.PlayerModifiers;

        /// <summary>Check if this attachment modifies a specific item stat.</summary>
        public bool ModifiesItemStat(ItemStatType statType)
        {
            if (StatConfig?.ItemModifiers == null) return false;
            foreach (var m in StatConfig.ItemModifiers)
                if (m.StatType == statType) return true;
            return false;
        }

        public float GetStatValue(ItemStatType statType)
            => StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;

        public bool HasStat(ItemStatType statType)
            => StatConfig != null && StatConfig.HasStat(statType);

        // ── Validation ───────────────────────────────────────────────────────────
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false;

            if (CanAttachTo == null || CanAttachTo.Length == 0)
            {
                error = "[AttachmentDefinition] CanAttachTo must define at least one slot type";
                return false;
            }

            if (IsStackable)
            {
                error = "[AttachmentDefinition] Attachments must not be stackable";
                return false;
            }

            error = null;
            return true;
        }
    }
}