using UnityEngine;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Interface for interaction targets
    /// Follows Interface Segregation Principle
    /// </summary>
    public interface IInteractionTarget
    {
        /// <summary>
        /// Check if can interact with this target
        /// </summary>
        bool CanInteract(GameObject interactor);

        /// <summary>
        /// Get interaction text for UI
        /// </summary>
        string GetInteractionText();

        /// <summary>
        /// Get network object ID for RPC
        /// </summary>
        uint GetNetworkObjectId();

        /// <summary>
        /// Get interaction type
        /// </summary>
        string GetInteractionType();
    }
}

