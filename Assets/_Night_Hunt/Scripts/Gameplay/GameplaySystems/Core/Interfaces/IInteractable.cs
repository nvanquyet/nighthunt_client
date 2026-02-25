using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Contract for any world object that a player can interact with.
    ///
    /// DESIGN (ISP / SRP):
    ///   - This interface covers only the interaction contract.
    ///   - Specialised behaviour (pickup, loot) is in IPickupable / ILootable.
    ///   - Input and Inventory layers depend on this interface, never on concrete types.
    /// </summary>
    public interface IInteractable
    {
        // ── Display ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Short label shown on HUD when this object is targeted.
        /// Example: "[E] Open Chest", "[F] Pick up AK-47 ×1"
        /// </summary>
        string InteractLabel { get; }

        // ── Gate ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Client-side pre-check: returns true if <paramref name="interactor"/> may
        /// interact right now (distance, lock-state, alive-state, etc.).
        /// Call this before sending any RPC to avoid wasted round-trips.
        /// </summary>
        bool CanInteract(GameObject interactor);

        // ── Action ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Trigger the interaction.  Implementations fire their own ServerRpc here.
        /// Called client-side by InteractionInputHandler.
        /// </summary>
        void Interact(GameObject interactor);

        // ── Visual feedback ─────────────────────────────────────────────────────

        /// <summary>Called when the interactor starts targeting this object (show highlight / prompt).</summary>
        void OnHoverEnter(GameObject interactor);

        /// <summary>Called when the interactor stops targeting this object (hide highlight / prompt).</summary>
        void OnHoverExit(GameObject interactor);
    }
}
