namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Orchestrates all player-stat contributions from equipped items and selected weapons.
    ///
    /// USAGE:
    ///   Trigger <see cref="Recalculate"/> on any equip/unequip/select/deselect/attach/detach event.
    ///   The orchestrator does a full clear-and-rebuild — idempotent, safe to call frequently.
    ///
    /// EXTENSION POINT (OCP):
    ///   External systems (Zones, Buff managers, Skill trees…) register as IStatContributor
    ///   via RegisterExternalContributor. Zero changes to StatApplyOrchestrator required.
    /// </summary>
    public interface IStatApplyOrchestrator
    {
        /// <summary>
        /// Full recalculate: clear all item-sourced modifiers from PlayerStatSystem,
        /// then re-apply from all currently active contributors (items + external).
        /// </summary>
        void Recalculate();

        /// <summary>
        /// Schedule a recalculation at end of frame (batched — safe to call frequently).
        /// </summary>
        void ScheduleRecalc();

        /// <summary>
        /// Register an external IStatContributor. It will be polled in every Recalculate().
        /// Triggers an immediate ScheduleRecalc().
        /// </summary>
        void RegisterExternalContributor(IStatContributor contributor);

        /// <summary>
        /// Remove a previously registered external contributor. Triggers ScheduleRecalc().
        /// </summary>
        void UnregisterExternalContributor(IStatContributor contributor);
    }
}
