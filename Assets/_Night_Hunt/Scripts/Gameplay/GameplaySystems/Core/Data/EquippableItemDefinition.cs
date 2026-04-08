using UnityEngine;
using UnityEngine.Serialization;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Abstract base for items that go into an equipment or weapon slot and can
    /// host attachments in their own sockets.
    ///
    /// Adds: attachment socket declarations (AttachmentSlots) and a unified stat-config
    /// access pattern so all callers can use GetStatValue / HasStat without knowing the
    /// concrete type. Also provides GetDefaultCurrentValue() for the runtime ammo /
    /// durability initialisation performed by InventorySystem.
    ///
    /// Concrete subclasses: WeaponDefinition, EquipmentDefinition.
    /// </summary>
    public abstract class EquippableItemDefinition : PhysicalItemDefinition
    {
        [Header("Attachment Sockets")]
        [Tooltip("Sockets this item exposes for attachments. " +
                 "Length determines how many attachment slots the item has.")]
        [FormerlySerializedAs("AttachmentSlots")]
        [SerializeField] private AttachmentSlotType[] _attachmentSlots;

        public override AttachmentSlotType[] AttachmentSlots { get => _attachmentSlots; set => _attachmentSlots = value; }

        // ── Stat config bridge ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the raw ItemStatConfig asset for this definition.
        /// Subclasses expose a typed StatConfig field and override this property
        /// so callers can access stats through the common interface.
        /// </summary>
        protected abstract ItemStatConfig StatConfigBase { get; }

        /// <summary>Base value for <paramref name="statType"/> before attachment modifiers.</summary>
        public float GetStatValue(ItemStatType statType)
            => StatConfigBase != null ? StatConfigBase.GetStatValue(statType) : 0f;

        /// <summary>True when <paramref name="statType"/> is explicitly defined in the stat config.</summary>
        public bool HasStat(ItemStatType statType)
            => StatConfigBase != null && StatConfigBase.HasStat(statType);

        /// <summary>
        /// Starting CurrentResource value assigned when a new ItemInstance is created.
        /// Weapons return MaxAmmo capacity; equipment returns MaxDurability.
        /// Defaults to 0 when not overridden.
        /// </summary>
        public override float GetDefaultCurrentValue() => 0f;

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Number of attachment sockets this item has.</summary>
        public int GetAttachmentSlotCount()
            => _attachmentSlots != null ? _attachmentSlots.Length : 0;
    }
}
