namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Orchestrates all player-stat contributions from equipped items and selected weapons.
    ///
    /// USAGE:
    ///   Trigger <see cref="Recalculate"/> on any equip/unequip/select/deselect/attach/detach event.
    ///   The orchestrator does a full clear-and-rebuild — idempotent, safe to call frequently.
    /// </summary>
    public interface IStatApplyOrchestrator
    {
        /// <summary>
        /// Full recalculate: clear all item-sourced modifiers from PlayerStatSystem,
        /// then re-apply from all currently active contributors.
        /// </summary>
        void Recalculate();
    }
}
