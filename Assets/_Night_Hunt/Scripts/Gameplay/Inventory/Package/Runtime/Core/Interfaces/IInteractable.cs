using UnityEngine;
using NightHunt.Networking;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Interface for world items and containers that can be interacted with.
    /// </summary>
    public interface IInteractable
    {
        InteractionType GetInteractionType(); // InstantPickup, HoldToOpen
        string GetInteractText();             // "Press F to pickup AK-47"
        Sprite GetInteractIcon();             // Hand, Chest, Corpse icon
        Vector3 GetPosition();
        
        // For instant interactions (F key)
        void OnInstantInteract(NetworkPlayer player);
        
        // For hold interactions (E key)
        float GetHoldDuration();              // Config: default 2s
        void OnHoldStart(NetworkPlayer player);
        void OnHoldProgress(float progress);  // 0-1
        void OnHoldComplete(NetworkPlayer player);
        void OnHoldCancelled();
    }
}
