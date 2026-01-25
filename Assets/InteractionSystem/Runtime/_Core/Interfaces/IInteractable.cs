using FishNet.Connection;

namespace NightHunt.InteractionSystem.Core
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        InteractionType InteractionType { get; }
        float InteractionDistance { get; }
        bool CanInteract(NetworkConnection player);
        void OnInteract(NetworkConnection player);
        void OnInteractStart(NetworkConnection player); // For hold
        void OnInteractCancel(NetworkConnection player); // For hold
    }

    public enum InteractionType
    {
        Immediate,  // Instant action
        Hold,       // Require hold duration
        Toggle,     // On/Off state
        Container   // Open loot UI
    }
}