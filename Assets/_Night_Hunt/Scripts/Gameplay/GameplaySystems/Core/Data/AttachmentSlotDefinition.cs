using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Definition asset for an attachment item (scope, suppressor, foregrip, armour plate …).
    ///
    /// MODIFIER MODEL:
    ///   StatConfig.ItemModifiers   — change the HOST item's own stats (Damage, MagazineSize, ArmorValue …).
    ///   StatConfig.PlayerModifiers — change PLAYER stats; applied when the HOST is equipped / selected.
    ///
    /// COMPATIBILITY:
    ///   CanAttachTo[] declares which AttachmentSlotTypes this attachment fits into.
    ///   The HOST item's AttachmentSlots[] declares which sockets it exposes.
    ///
    /// NOTE: Attachments do NOT expose their own attachment sockets (no nested attachments).
    /// </summary>
    [CreateAssetMenu(fileName = "Attachment_", menuName = "NightHunt/Items/Attachment Definition")]
    public class AttachmentDefinition : PhysicalItemDefinition
    {
        public override ItemType Type => ItemType.Attachment;

        [Header("Attachment Compatibility")]
        [Tooltip("Socket types this attachment fits into on a host item. " +
                 "Must have at least one entry.")]
        public AttachmentSlotType[] CanAttachTo;

        [Header("Stat Config")]
        [Tooltip("Modifiers applied to the host item and / or the player on equip.")]
        public AttachmentStatConfig StatConfig;

        // ── Stat access ─────────────────────────────────────────────────────────

        /// <summary>Modifiers applied to the host item's own stats.</summary>
        public ItemStatModifier[] GetItemModifiers()
            => StatConfig?.ItemModifiers;

        /// <summary>
        /// Player-stat modifiers. Applied when the host is equipped (Equipment host) or
        /// when the host weapon is drawn (Weapon host).
        /// </summary>
        public PlayerStatModifier[] GetPlayerModifiers()
            => StatConfig?.PlayerModifiers;

        /// <summary>True when this attachment modifies the given item stat.</summary>
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

        /// <summary>
        /// Starting CurrentResource for battery-powered attachments (flashlights etc.).
        /// Returns StatConfig[BatteryCapacity]; 0 for non-powered attachments.
        /// </summary>
        public override float GetDefaultCurrentValue()
            => StatConfig != null ? StatConfig.GetStatValue(ItemStatType.BatteryCapacity) : 0f;

        /// <summary>True when this attachment fits the given slot type.</summary>
        public override bool CanAttachToSlot(AttachmentSlotType slotType)
        {
            if (CanAttachTo == null) return false;
            foreach (var t in CanAttachTo)
                if (t == slotType) return true;
            return false;
        }

        // ── Validation ───────────────────────────────────────────────────────────
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false;

            if (CanAttachTo == null || CanAttachTo.Length == 0)
            { error = "[AttachmentDefinition] CanAttachTo must contain at least one slot type"; return false; }

            if (IsStackable)
            { error = "[AttachmentDefinition] Attachments must not be stackable"; return false; }

            error = null;
            return true;
        }
    }
}