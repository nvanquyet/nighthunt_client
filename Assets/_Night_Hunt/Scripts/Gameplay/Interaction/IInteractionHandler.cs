using FishNet.Object;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Interface for handling interactions server-side
    /// Follows Single Responsibility Principle
    /// </summary>
    public interface IInteractionHandler
    {
        /// <summary>
        /// Handle interaction request
        /// </summary>
        bool HandleInteraction(uint targetNetId, string actionType, NetworkObject interactor);
    }
}

