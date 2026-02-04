using UnityEngine;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Interfaces
{
    /// <summary>
    /// Interface for objects that can be interacted with in the world.
    /// Implements instant pickup and hold-to-open interactions.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Gets the type of interaction required</summary>
        InteractionType GetInteractionType();
        
        /// <summary>Gets the text to display in interaction prompt</summary>
        string GetInteractText();
        
        /// <summary>Gets the icon to display in interaction prompt</summary>
        Sprite GetInteractIcon();
        
        /// <summary>Gets the world position of this interactable</summary>
        Vector3 GetPosition();
        
        // === Instant Interaction (F key) ===
        
        /// <summary>Called when instant interaction is triggered</summary>
        void OnInstantInteract(object player); // NetworkPlayer
        
        // === Hold Interaction (E key) ===
        
        /// <summary>Gets the duration required to hold</summary>
        float GetHoldDuration();
        
        /// <summary>Called when hold interaction starts</summary>
        void OnHoldStart(object player); // NetworkPlayer
        
        /// <summary>Called during hold progress update</summary>
        void OnHoldProgress(float progress); // 0-1
        
        /// <summary>Called when hold interaction completes successfully</summary>
        void OnHoldComplete(object player); // NetworkPlayer
        
        /// <summary>Called when hold interaction is cancelled</summary>
        void OnHoldCancelled();
    }
}