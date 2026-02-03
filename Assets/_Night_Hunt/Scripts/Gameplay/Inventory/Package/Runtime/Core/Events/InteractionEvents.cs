using System;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for interaction system.
    /// </summary>
    public static class InteractionEvents
    {
        public static event Action<float> OnHoldProgress;
        public static event Action OnHoldCompleted;
        public static event Action<string> OnHoldCancelled;
        
        public static void FireHoldProgress(float progress) => OnHoldProgress?.Invoke(progress);
        public static void FireHoldCompleted() => OnHoldCompleted?.Invoke();
        public static void FireHoldCancelled(string reason) => OnHoldCancelled?.Invoke(reason);
    }
}
