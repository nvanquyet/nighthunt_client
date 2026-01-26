using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for items that can be picked up by players.
    /// Separate from IInteractable - pickup is for items going into inventory.
    /// </summary>
    public interface IPickupable
    {
        /// <summary>
        /// Check if this item can be picked up by the specified player.
        /// </summary>
        bool CanPickup(GameObject player);

        /// <summary>
        /// Get the item data for this pickupable item.
        /// </summary>
        ItemDataBase GetItemData();

        /// <summary>
        /// Get the quantity of this item.
        /// </summary>
        int GetQuantity();

        /// <summary>
        /// Called when the item is successfully picked up.
        /// </summary>
        void OnPickedUp(GameObject player);

        /// <summary>
        /// Get the display name for UI prompts.
        /// </summary>
        string GetDisplayName();

        /// <summary>
        /// Get the pickup range (distance required to pickup).
        /// </summary>
        float GetPickupRange();
    }
}
