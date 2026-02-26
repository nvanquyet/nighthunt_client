namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Extension of <see cref="IInteractable"/> for objects that require a hold-to-interact input.
    ///
    /// Examples:
    ///   - <c>WorldContainer</c> with LootableConfig.InteractionMode = Hold
    ///   - <c>WorldDoor</c> with InteractableConfig.InteractionMode = Hold
    ///
    /// DESIGN: PlayerInteractionSystem checks <c>target is IHoldInteractable</c> at runtime
    ///         and starts a hold timer, reading <see cref="HoldDuration"/> from the target.
    ///         This keeps PlayerInteractionSystem decoupled from concrete types.
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>
        /// Seconds the player must hold the interact button before <see cref="IInteractable.Interact"/> fires.
        /// </summary>
        float HoldDuration { get; }
    }
}
