namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Specialised IInteractable for containers / corpses that hold multiple items.
    ///
    /// DESIGN: Extend per-type if you need richer behaviour
    /// (e.g. ILockableContainer : ILootable).
    /// </summary>
    public interface ILootable : IInteractable
    {
        /// <summary>True once all items inside have been taken.</summary>
        bool IsLooted { get; }

        /// <summary>True once the container has been opened at least once.</summary>
        bool IsOpen { get; }
    }
}
