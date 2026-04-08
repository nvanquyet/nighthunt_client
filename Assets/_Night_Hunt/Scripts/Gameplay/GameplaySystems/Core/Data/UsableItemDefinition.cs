using UnityEngine;
using UnityEngine.Serialization;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Abstract base for items that the player activates with a sustained press
    /// (shows a progress bar / usage animation).
    ///
    /// Adds: UsageDuration and CanCancelUsage — the two fields that distinguish
    /// "use with a cast time" items from instant-use items.
    ///
    /// Concrete subclasses: ConsumableDefinition, ThrowableDefinition.
    /// ThrowableDefinition maps its PrepareTime concept onto UsageDuration.
    /// </summary>
    public abstract class UsableItemDefinition : PhysicalItemDefinition
    {
        [Header("Usage")]
        [Tooltip("Seconds the player must hold the use button before the effect triggers. " +
                 "0 = instant activation (no progress bar shown).")]
        [Min(0f)] public float UsageDuration = 0f;

        [Tooltip("Whether the player can interrupt the use action before it completes.")]
        [FormerlySerializedAs("CanCancelUsage")]
        [SerializeField] private bool _canCancelUsage = true;

        public override bool CanCancelUsage { get => _canCancelUsage; set => _canCancelUsage = value; }
    }
}
