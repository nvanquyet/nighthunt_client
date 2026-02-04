using System;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for world interaction system.
    /// </summary>
    public static class InteractionEvents
    {
        public static event Action<object> OnInteractableDetected; // IInteractable
        public static event Action OnInteractableLost;
        public static event Action<object> OnInstantInteract; // IInteractable
        public static event Action<object> OnHoldStart; // IInteractable
        public static event Action<float> OnHoldProgress; // 0-1
        public static event Action OnHoldCompleted;
        public static event Action<string> OnHoldCancelled; // reason

        public static void InvokeInteractableDetected(object interactable) =>
            OnInteractableDetected?.Invoke(interactable);

        public static void InvokeInteractableLost() => OnInteractableLost?.Invoke();
        public static void InvokeInstantInteract(object interactable) => OnInstantInteract?.Invoke(interactable);
        public static void InvokeHoldStart(object interactable) => OnHoldStart?.Invoke(interactable);
        public static void InvokeHoldProgress(float progress) => OnHoldProgress?.Invoke(progress);
        public static void InvokeHoldCompleted() => OnHoldCompleted?.Invoke();
        public static void InvokeHoldCancelled(string reason) => OnHoldCancelled?.Invoke(reason);
    }
}