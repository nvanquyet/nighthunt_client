using System;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for the item selection system.
    ///
    /// Uses a single-selection model:
    /// the player selects ONE item at a time (consumable or throwable)
    /// via the left/right filter panels next to the weapon HUD.
    ///
    /// RESPONSIBILITIES:
    /// - Track a single selected item (consumable or throwable)
    /// - Delegate actual usage to IItemUseSystem
    /// - Provide continuous-use support (throw again while qty > 0)
    ///
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: selection stored as SyncVar<string> (instanceID)
    /// - Events fire on all clients after sync
    /// </summary>
    public interface IItemSelectionSystem
    {
        // ─── State ────────────────────────────────────────────────────────────

        /// <summary>Currently selected item instance, or null when nothing selected.</summary>
        ItemInstance SelectedItem { get; }

        /// <summary>True when an item is selected.</summary>
        bool HasSelection { get; }

        // ─── Operations (server-authoritative, call from server or via ServerRpc) ──

        /// <summary>Select an item by its instance ID. Triggers UseItem on IItemUseSystem.</summary>
        void SelectItem(string instanceID);

        /// <summary>Deselect the current item without cancelling an in-progress use.</summary>
        void DeselectItem(bool restorePreviousWeapon = true);

        /// <summary>Cancel the active item-use and deselect.</summary>
        void CancelSelection(bool restorePreviousWeapon = true);

        /// <summary>
        /// Begin using the currently selected item without changing the selection.
        /// For throwables: starts the arm/aim animation (player must fire again to release).
        /// For consumables: immediately applies the effect.
        /// No-op when nothing is selected or a use is already in progress.
        /// </summary>
        void UseSelectedItem();

        // ─── ServerRpc wrappers (safe to call from UI on any connection) ─────

        /// <summary>ServerRpc: select the item with this instanceID. Safe to call from client UI.</summary>
        void RequestSelectItem(string instanceID);

        /// <summary>ServerRpc: deselect without cancelling active use. Safe to call from client UI.</summary>
        void RequestDeselectItem();

        /// <summary>ServerRpc: arm the currently selected item. Safe to call from client UI.</summary>
        void RequestUseSelectedItem();

        /// <summary>ServerRpc: cancel active use and deselect. Safe to call from client UI.</summary>
        void RequestCancelSelection(bool restorePreviousWeapon = true);

        // ─── Events ──────────────────────────────────────────────────────────

        /// <summary>Fired when an item is selected. Parameter: the selected ItemInstance.</summary>
        event Action<ItemInstance> OnItemSelected;

        /// <summary>Fired when the selection is cleared.</summary>
        event Action OnItemDeselected;
    }
}
