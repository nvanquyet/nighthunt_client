using System.Collections.Generic;
using NightHunt.StatSystem.Core.Data;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Context passed to <see cref="IStatContributor"/> when collecting player-stat contributions.
    ///
    /// RULES encoded here:
    /// - Equipment items contribute always (IsHostSelected is always true for equipment).
    /// - Weapon items + their attachments contribute ONLY when IsHostSelected = true.
    /// </summary>
    public readonly struct StatContributionContext
    {
        /// <summary>True when the host item is currently SELECTED/active (weapon drawn, equipment equipped).</summary>
        public readonly bool IsHostSelected;

        /// <summary>Runtime ItemType of the host (Equipment, Weapon…). Attachment follows host.</summary>
        public readonly ItemType HostItemType;

        /// <summary>InstanceId of the contributing item — used as source key for modifier tracking.</summary>
        public readonly string InstanceId;

        public StatContributionContext(bool isHostSelected, ItemType hostItemType, string instanceId)
        {
            IsHostSelected = isHostSelected;
            HostItemType = hostItemType;
            InstanceId = instanceId;
        }

        /// <summary>Shorthand: should this contributor output any player-stat modifiers?</summary>
        public bool ShouldContribute => HostItemType == ItemType.Equipment || IsHostSelected;
    }

    /// <summary>
    /// Any item that can contribute <see cref="PlayerStatModifier"/>s to the player stat system.
    ///
    /// Implemented by logical wrappers around <see cref="ItemInstance"/>:
    /// - <c>EquipmentStatContributor</c>   → always contributes when equipped.
    /// - <c>WeaponStatContributor</c>      → contributes only when weapon is SELECTED.
    /// - <c>AttachmentStatContributor</c>  → delegates to its host's rule.
    ///
    /// StatApplyOrchestrator collects all contributors, calls this, then rebuilds PlayerStatSystem.
    /// </summary>
    public interface IStatContributor
    {
        /// <summary>
        /// Return PlayerStatModifiers this item contributes given the provided context.
        /// Return empty enumerable (not null) when not contributing.
        /// </summary>
        IEnumerable<PlayerStatModifier> GetPlayerStatContributions(StatContributionContext ctx);
    }
}
