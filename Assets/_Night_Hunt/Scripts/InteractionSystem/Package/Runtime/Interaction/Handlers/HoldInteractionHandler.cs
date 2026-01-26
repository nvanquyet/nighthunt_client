using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;

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
                    // Update UI progress
                    UpdateProgressUI();
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

            // Show progress UI
            ShowProgressUI();
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

            // Hide progress UI
            HideProgressUI();

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

            // Hide progress UI
            HideProgressUI();
        }

        /// <summary>
        /// Update progress UI.
        /// </summary>
        private void UpdateProgressUI()
        {
            float progress = currentProgress.GetProgress();
            // UI update will be handled by InteractionUIController
            var uiController = GetComponent<InteractionUIController>();
            if (uiController != null)
            {
                uiController.UpdateProgress(progress);
            }
        }

        /// <summary>
        /// Show progress UI.
        /// </summary>
        private void ShowProgressUI()
        {
            var uiController = GetComponent<InteractionUIController>();
            if (uiController != null)
            {
                uiController.ShowProgress();
            }
        }

        /// <summary>
        /// Hide progress UI.
        /// </summary>
        private void HideProgressUI()
        {
            var uiController = GetComponent<InteractionUIController>();
            if (uiController != null)
            {
                uiController.HideProgress();
            }
        }

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
