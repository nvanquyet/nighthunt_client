using System;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// General UI events.
    /// </summary>
    public static class UIEvents
    {
        public static event Action<string> OnShowMessage;
        public static event Action<string> OnShowError;
        public static event Action<string, Action> OnShowConfirmation; // message, onConfirm
        
        public static void InvokeShowMessage(string message) => OnShowMessage?.Invoke(message);
        public static void InvokeShowError(string error) => OnShowError?.Invoke(error);
        public static void InvokeShowConfirmation(string message, Action onConfirm) => OnShowConfirmation?.Invoke(message, onConfirm);
    }
}