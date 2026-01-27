using System;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Events
{
    /// <summary>
    /// Events for interaction system (prompts, progress, targets).
    /// Gameplay UI should subscribe to these events to display UI.
    /// </summary>
    public static class InteractionEvents
    {
        // Interaction target events
        public static event Action<IInteractable, string> OnInteractTargetChanged; // interactable, promptText
        public static event Action OnInteractTargetLost;

        // Pickup target events
        public static event Action<IPickupable, string> OnPickupTargetChanged; // pickupable, promptText
        public static event Action OnPickupTargetLost;

        // Hold interaction progress events
        public static event Action<float> OnHoldProgressChanged; // progress 0-1
        public static event Action OnHoldProgressStarted;
        public static event Action OnHoldProgressCompleted;
        public static event Action OnHoldProgressCancelled;

        /// <summary>
        /// Invoke when interaction target changes.
        /// </summary>
        public static void InvokeInteractTargetChanged(IInteractable interactable, string promptText)
        {
            OnInteractTargetChanged?.Invoke(interactable, promptText);
        }

        /// <summary>
        /// Invoke when interaction target is lost.
        /// </summary>
        public static void InvokeInteractTargetLost()
        {
            OnInteractTargetLost?.Invoke();
        }

        /// <summary>
        /// Invoke when pickup target changes.
        /// </summary>
        public static void InvokePickupTargetChanged(IPickupable pickupable, string promptText)
        {
            OnPickupTargetChanged?.Invoke(pickupable, promptText);
        }

        /// <summary>
        /// Invoke when pickup target is lost.
        /// </summary>
        public static void InvokePickupTargetLost()
        {
            OnPickupTargetLost?.Invoke();
        }

        /// <summary>
        /// Invoke when hold interaction progress changes.
        /// </summary>
        public static void InvokeHoldProgressChanged(float progress)
        {
            OnHoldProgressChanged?.Invoke(progress);
        }

        /// <summary>
        /// Invoke when hold interaction starts.
        /// </summary>
        public static void InvokeHoldProgressStarted()
        {
            OnHoldProgressStarted?.Invoke();
        }

        /// <summary>
        /// Invoke when hold interaction completes.
        /// </summary>
        public static void InvokeHoldProgressCompleted()
        {
            OnHoldProgressCompleted?.Invoke();
        }

        /// <summary>
        /// Invoke when hold interaction is cancelled.
        /// </summary>
        public static void InvokeHoldProgressCancelled()
        {
            OnHoldProgressCancelled?.Invoke();
        }
    }
}
