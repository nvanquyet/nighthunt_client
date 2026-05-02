
namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Specialised IInteractable for items that lie on the ground and can be picked up.
    ///
    /// DESIGN: Extends IInteractable so callers that only care about
    /// "can I interact?" still work without knowing about pickup specifics.
    /// </summary>
    public interface IPickupable : IInteractable
    {
        /// <summary>ItemDefinition ID of the dropped item.</summary>
        string ItemDefinitionID { get; }

        /// <summary>Stack quantity on the ground.</summary>
        int Quantity { get; }

        /// <summary>
        /// True once the pickup has been claimed and is pending network despawn.
        /// CanInteract() must return false when IsPickedUp is true.
        /// </summary>
        bool IsPickedUp { get; }
    }
}
