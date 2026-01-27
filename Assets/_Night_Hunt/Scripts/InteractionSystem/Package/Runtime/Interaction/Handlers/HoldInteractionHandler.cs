using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Interaction.Handlers
{
    /// <summary>
    /// Handles hold-type interactions with progress tracking.
    /// </summary>
    public class HoldInteractionHandler : MonoBehaviour
    {
        private InteractionProgress currentProgress;
        private IInteractable currentInteractable;
        private bool isHolding = false;

        private void Update()
        {
            if (isHolding && currentInteractable != null)
            {
                // Update progress
                currentProgress = currentProgress.Update(Time.deltaTime);

                // Check if completed
                if (currentProgress.isCompleted)
                {
                    CompleteHoldInteraction();
                }
                else
                {
                    // Fire progress event for gameplay UI
                    InteractionEvents.InvokeHoldProgressChanged(currentProgress.GetProgress());
                }
            }
        }

        /// <summary>
        /// Start a hold interaction.
        /// </summary>
        public void StartHoldInteraction(IInteractable interactable)
        {
            if (interactable == null)
                return;

            if (interactable.GetInteractionType() != InteractionType.Hold)
            {
                Debug.LogWarning("[HoldInteractionHandler] Interactable is not a hold-type interaction!");
                return;
            }

            currentInteractable = interactable;
            float requiredTime = interactable.GetRequiredHoldTime();
            currentProgress = new InteractionProgress(requiredTime);
            currentProgress = currentProgress.StartHolding();
            isHolding = true;

            // Fire event for gameplay UI
            InteractionEvents.InvokeHoldProgressStarted();
        }

        /// <summary>
        /// Stop holding the interaction (called when player releases input).
        /// </summary>
        public void StopHoldInteraction()
        {
            if (!isHolding)
                return;

            isHolding = false;
            currentProgress = currentProgress.StopHolding();

            // Fire event for gameplay UI
            InteractionEvents.InvokeHoldProgressCancelled();

            // Reset if not completed
            if (!currentProgress.isCompleted)
            {
                currentProgress = currentProgress.Reset();
                currentInteractable = null;
            }
        }

        /// <summary>
        /// Complete the hold interaction.
        /// </summary>
        private void CompleteHoldInteraction()
        {
            if (currentInteractable == null)
                return;

            // Perform the interaction
            currentInteractable.Interact(gameObject);

            // Reset
            isHolding = false;
            currentProgress = currentProgress.Reset();
            currentInteractable = null;

            // Fire event for gameplay UI
            InteractionEvents.InvokeHoldProgressCompleted();
        }

        // Note: UI methods removed - gameplay UI subscribes to InteractionEvents

        /// <summary>
        /// Get current progress (0-1).
        /// </summary>
        public float GetProgress()
        {
            return currentProgress.GetProgress();
        }

        /// <summary>
        /// Check if currently holding an interaction.
        /// </summary>
        public bool IsHolding()
        {
            return isHolding;
        }
    }
}
