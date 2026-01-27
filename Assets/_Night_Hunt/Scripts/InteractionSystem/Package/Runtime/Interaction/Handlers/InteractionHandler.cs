using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.InteractionSystem.Interaction.Handlers
{
    /// <summary>
    /// Base interaction handler that routes interactions to appropriate handlers.
    /// </summary>
    public class InteractionHandler : MonoBehaviour
    {
        private HoldInteractionHandler holdHandler;

        private void Awake()
        {
            holdHandler = GetComponent<HoldInteractionHandler>();
            if (holdHandler == null)
            {
                holdHandler = gameObject.AddComponent<HoldInteractionHandler>();
            }

            // Note: UI removed - gameplay UI subscribes to InteractionEvents
        }

        /// <summary>
        /// Handle an interaction based on its type.
        /// </summary>
        public void HandleInteraction(IInteractable interactable)
        {
            if (interactable == null)
                return;

            InteractionType type = interactable.GetInteractionType();

            switch (type)
            {
                case InteractionType.Immediate:
                    HandleImmediateInteraction(interactable);
                    break;

                case InteractionType.Hold:
                    HandleHoldInteraction(interactable);
                    break;

                case InteractionType.Toggle:
                    HandleToggleInteraction(interactable);
                    break;

                case InteractionType.Container:
                    HandleContainerInteraction(interactable);
                    break;
            }
        }

        /// <summary>
        /// Handle immediate interaction (instant action).
        /// </summary>
        private void HandleImmediateInteraction(IInteractable interactable)
        {
            interactable.Interact(gameObject);
        }

        /// <summary>
        /// Handle hold interaction (requires holding for duration).
        /// </summary>
        private void HandleHoldInteraction(IInteractable interactable)
        {
            if (holdHandler != null)
            {
                holdHandler.StartHoldInteraction(interactable);
            }
            else
            {
                // Fallback: treat as immediate
                interactable.Interact(gameObject);
            }
        }

        /// <summary>
        /// Handle toggle interaction (on/off state).
        /// </summary>
        private void HandleToggleInteraction(IInteractable interactable)
        {
            interactable.Interact(gameObject);
        }

        /// <summary>
        /// Handle container interaction (open loot UI).
        /// </summary>
        private void HandleContainerInteraction(IInteractable interactable)
        {
            interactable.Interact(gameObject);
        }
    }
}
