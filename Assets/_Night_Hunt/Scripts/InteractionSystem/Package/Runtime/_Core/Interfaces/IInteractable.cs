using UnityEngine;

namespace NightHunt.InteractionSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for objects that can be interacted with (doors, chests, switches, etc.).
    /// Separate from IPickupable - interaction is for objects that perform actions.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Check if this object can be interacted with by the specified player.
        /// </summary>
        bool CanInteract(GameObject interactor);

        /// <summary>
        /// Perform the interaction.
        /// </summary>
        void Interact(GameObject interactor);

        /// <summary>
        /// Get the interaction text to display in UI.
        /// </summary>
        string GetInteractionText();

        /// <summary>
        /// Called when player starts looking at this interactable (enters range).
        /// </summary>
        void OnInteractionStart(GameObject interactor);

        /// <summary>
        /// Called when player stops looking at this interactable (leaves range).
        /// </summary>
        void OnInteractionEnd(GameObject interactor);

        /// <summary>
        /// Get the interaction type (Immediate, Hold, Toggle, Container).
        /// </summary>
        InteractionType GetInteractionType();

        /// <summary>
        /// Get the required hold duration for hold-type interactions (0 for immediate).
        /// </summary>
        float GetRequiredHoldTime();

        /// <summary>
        /// Get the interaction range (distance required to interact).
        /// </summary>
        float GetInteractionRange();
    }

    /// <summary>
    /// Types of interactions available.
    /// </summary>
    public enum InteractionType
    {
        Immediate,  // Click once → Instant action
        Hold,       // Hold for X seconds → Action
        Toggle,     // On/Off state
        Container   // Open loot UI
    }
}
