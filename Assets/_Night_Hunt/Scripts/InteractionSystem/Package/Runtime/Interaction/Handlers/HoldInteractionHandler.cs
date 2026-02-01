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
                float progress = currentProgress.GetProgress();

                // Check if completed
                if (currentProgress.isCompleted)
                {
                    Debug.Log($"[HoldInteractionHandler] Hold completed! Progress: {progress}");
                    CompleteHoldInteraction();
                }
                else
                {
                    // Fire progress event for gameplay UI (only log every 0.1s to avoid spam)
                    if (Time.frameCount % 6 == 0) // ~10 times per second at 60fps
                    {
                        Debug.Log($"[HoldInteractionHandler] Hold progress: {progress:F2} ({progress * 100:F1}%)");
                    }
                    InteractionEvents.InvokeHoldProgressChanged(progress);
                }
            }
        }

        /// <summary>
        /// Start a hold interaction.
        /// </summary>
        public void StartHoldInteraction(IInteractable interactable)
        {
            Debug.Log($"[HoldInteractionHandler] StartHoldInteraction called - interactable={interactable?.GetType().Name ?? "null"}");
            
            if (interactable == null)
            {
                Debug.LogWarning("[HoldInteractionHandler] StartHoldInteraction: interactable is null!");
                return;
            }

            if (interactable.GetInteractionType() != InteractionType.Hold)
            {
                Debug.LogWarning($"[HoldInteractionHandler] Interactable is not a hold-type interaction! Type: {interactable.GetInteractionType()}");
                return;
            }

            currentInteractable = interactable;
            float requiredTime = interactable.GetRequiredHoldTime();
            Debug.Log($"[HoldInteractionHandler] Starting hold - requiredTime={requiredTime}s");
            
            currentProgress = new InteractionProgress(requiredTime);
            currentProgress = currentProgress.StartHolding();
            isHolding = true;

            Debug.Log($"[HoldInteractionHandler] Hold started - isHolding={isHolding}, progress={currentProgress.GetProgress()}");

            // Fire event for gameplay UI
            Debug.Log("[HoldInteractionHandler] Firing OnHoldProgressStarted event");
            InteractionEvents.InvokeHoldProgressStarted();
        }

        /// <summary>
        /// Stop holding the interaction (called when player releases input).
        /// </summary>
        public void StopHoldInteraction()
        {
            Debug.Log($"[HoldInteractionHandler] StopHoldInteraction called - isHolding={isHolding}");
            
            if (!isHolding)
            {
                Debug.LogWarning("[HoldInteractionHandler] StopHoldInteraction: Not currently holding!");
                return;
            }

            float progress = currentProgress.GetProgress();
            Debug.Log($"[HoldInteractionHandler] Stopping hold - progress was {progress:F2} ({progress * 100:F1}%)");
            
            isHolding = false;
            currentProgress = currentProgress.StopHolding();

            // Fire event for gameplay UI
            Debug.Log("[HoldInteractionHandler] Firing OnHoldProgressCancelled event");
            InteractionEvents.InvokeHoldProgressCancelled();

            // Reset if not completed
            if (!currentProgress.isCompleted)
            {
                Debug.Log("[HoldInteractionHandler] Hold was not completed, resetting progress");
                currentProgress = currentProgress.Reset();
                currentInteractable = null;
            }
        }

        /// <summary>
        /// Complete the hold interaction.
        /// </summary>
        private void CompleteHoldInteraction()
        {
            Debug.Log($"[HoldInteractionHandler] CompleteHoldInteraction called - currentInteractable={currentInteractable?.GetType().Name ?? "null"}");
            
            if (currentInteractable == null)
            {
                Debug.LogWarning("[HoldInteractionHandler] CompleteHoldInteraction: currentInteractable is null!");
                return;
            }

            // Find player GameObject (root or parent with NetworkObject)
            GameObject playerObject = FindPlayerObject();
            Debug.Log($"[HoldInteractionHandler] Player object found: {playerObject?.name ?? "null"}");

            if (playerObject == null)
            {
                Debug.LogError("[HoldInteractionHandler] Cannot find player GameObject! Cannot complete interaction.");
                return;
            }

            // Perform the interaction with player GameObject
            Debug.Log($"[HoldInteractionHandler] Calling Interact({playerObject.name}) on {currentInteractable.GetType().Name}");
            currentInteractable.Interact(playerObject);

            // Reset
            isHolding = false;
            currentProgress = currentProgress.Reset();
            currentInteractable = null;

            // Fire event for gameplay UI
            Debug.Log("[HoldInteractionHandler] Firing OnHoldProgressCompleted event");
            InteractionEvents.InvokeHoldProgressCompleted();
        }

        /// <summary>
        /// Find player GameObject (root or parent with NetworkObject).
        /// </summary>
        private GameObject FindPlayerObject()
        {
            // Strategy 1: Check root GameObject
            Transform root = transform.root;
            if (root != null)
            {
                var rootNO = root.GetComponent<FishNet.Object.NetworkObject>();
                if (rootNO != null && rootNO.IsOwner)
                {
                    return root.gameObject;
                }
            }

            // Strategy 2: Check parent hierarchy
            Transform current = transform.parent;
            while (current != null)
            {
                var no = current.GetComponent<FishNet.Object.NetworkObject>();
                if (no != null && no.IsOwner)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }

            // Strategy 3: Check current GameObject
            var currentNO = GetComponent<FishNet.Object.NetworkObject>();
            if (currentNO != null && currentNO.IsOwner)
            {
                return gameObject;
            }

            // Strategy 4: Use root as fallback
            return root != null ? root.gameObject : gameObject;
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
