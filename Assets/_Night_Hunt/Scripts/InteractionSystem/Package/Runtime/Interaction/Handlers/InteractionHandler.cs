using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Utilities;

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
            // Use ComponentFinder to search in hierarchy (including children)
            holdHandler = ComponentFinder.FindComponentInHierarchy<HoldInteractionHandler>(gameObject, includeInactive: false);
            if (holdHandler == null)
            {
                // If not found, add to current GameObject
                Debug.Log($"[InteractionHandler] HoldInteractionHandler not found in hierarchy, adding to {gameObject.name}");
                holdHandler = gameObject.AddComponent<HoldInteractionHandler>();
            }
            else
            {
                Debug.Log($"[InteractionHandler] Found HoldInteractionHandler on {holdHandler.gameObject.name}");
            }

            // Note: UI removed - gameplay UI subscribes to InteractionEvents
        }

        /// <summary>
        /// Handle an interaction based on its type.
        /// </summary>
        public void HandleInteraction(IInteractable interactable)
        {
            Debug.Log($"[InteractionHandler] HandleInteraction called - interactable={interactable?.GetType().Name ?? "null"}");
            
            if (interactable == null)
            {
                Debug.LogWarning("[InteractionHandler] HandleInteraction: interactable is null!");
                return;
            }

            InteractionType type = interactable.GetInteractionType();
            Debug.Log($"[InteractionHandler] Interaction type: {type}, holdHandler={holdHandler != null}");

            switch (type)
            {
                case InteractionType.Immediate:
                    Debug.Log("[InteractionHandler] Handling Immediate interaction");
                    HandleImmediateInteraction(interactable);
                    break;

                case InteractionType.Hold:
                    Debug.Log("[InteractionHandler] Handling Hold interaction");
                    HandleHoldInteraction(interactable);
                    break;

                case InteractionType.Toggle:
                    Debug.Log("[InteractionHandler] Handling Toggle interaction");
                    HandleToggleInteraction(interactable);
                    break;

                case InteractionType.Container:
                    Debug.Log("[InteractionHandler] Handling Container interaction");
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
            Debug.Log($"[InteractionHandler] HandleHoldInteraction - holdHandler={holdHandler != null}");
            
            if (holdHandler != null)
            {
                Debug.Log($"[InteractionHandler] Calling holdHandler.StartHoldInteraction({interactable.GetType().Name})");
                holdHandler.StartHoldInteraction(interactable);
            }
            else
            {
                Debug.LogWarning("[InteractionHandler] HoldHandler is null! Falling back to immediate interaction.");
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
