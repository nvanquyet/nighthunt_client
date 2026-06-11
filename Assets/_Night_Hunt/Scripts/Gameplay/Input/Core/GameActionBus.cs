using System;
using NightHunt.GameplaySystems.Core.Data;
using UnityEngine;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Centralized, platform-agnostic game-action event bus.
    ///
    /// PURPOSE:
    ///   Eliminates dual code paths where keyboard input and mobile UI buttons
    ///   previously called different methods to achieve the same game action.
    ///
    /// FLOW (before):
    ///   Key 4 → InventoryInputHandler.QuickSlotPerformed → ItemSelectionHUD.ActivateQuickSlot
    ///   UI Button → ItemFilterPanel.ActivateShortcut  ← separate path, no canonical event
    ///
    /// FLOW (after):
    ///   Key 4  → InventoryInputHandler.QuickSlotPerformed
    ///              → GameActionBus.RequestItemSlot(ItemType.Consumable)  ← canonical
    ///   UI Button → GameActionBus.RequestItemSlot(ItemType.Consumable)   ← same canonical
    ///              ↓
    ///         ItemSelectionHUD.OnItemSlotRequested  ← single subscriber
    ///
    /// RULES:
    ///   • All input sources (keyboard, mobile button, gamepad) call RequestXxx() methods.
    ///   • All game systems subscribe to OnXxx events — NOT to device-specific paths.
    ///   • This class has NO Unity lifecycle; it is purely static.
    ///   • Clear() is only for explicit full teardown when every subscriber is gone.
    /// </summary>
    public static class GameActionBus
    {
        // ── Item Slot ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any input source requests activation of an item slot by type.
        /// Subscribers: <see cref="NightHunt.UI.ItemSelectionHUD"/>.
        /// </summary>
        public static event Action<ItemType> OnItemSlotRequested;

        /// <summary>
        /// Route all sources through here to activate a consumable/throwable/deployable slot.
        /// Called by <see cref="NightHunt.Gameplay.Input.Handlers.Inventory.InventoryInputHandler"/>
        /// and by mobile/desktop UI buttons instead of calling panel methods directly.
        /// </summary>
        public static void RequestItemSlot(ItemType type)
        {
            Debug.Log($"[NH_FLOW][00][GameActionBus.ItemSlot] type={type} subscribers={OnItemSlotRequested?.GetInvocationList().Length ?? 0}");
            OnItemSlotRequested?.Invoke(type);
        }

        // ── Weapon Slot ────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any input source selects a weapon slot by 0-based index.
        /// Subscribers: <see cref="NightHunt.UI.WeaponHUDPanel"/> (highlight update).
        /// </summary>
        public static event Action<int> OnWeaponSlotRequested;

        /// <summary>
        /// Route all sources through here to switch the active weapon slot.
        /// Called by <see cref="NightHunt.Gameplay.Input.Handlers.Combat.CombatInputHandler"/>
        /// and by mobile weapon slot buttons.
        /// </summary>
        /// <param name="zeroBasedIndex">0 = Primary, 1 = Secondary, 2 = Melee.</param>
        public static void RequestWeaponSlot(int zeroBasedIndex)
            => OnWeaponSlotRequested?.Invoke(zeroBasedIndex);

        // ── Camera Lock ────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any input source changes the camera lock (strafe vs tank) state.
        /// Subscribers: <see cref="NightHunt.UI.CameraLockIndicator"/>.
        /// </summary>
        public static event Action<bool> OnCameraLockChanged;

        /// <summary>
        /// Route camera lock toggle through here from keyboard (X key) and mobile button.
        /// </summary>
        /// <param name="isLocked">true = Strafe/Locked, false = Tank/Free.</param>
        public static void SetCameraLock(bool isLocked)
            => OnCameraLockChanged?.Invoke(isLocked);

        // ── Open Inventory ─────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any source requests opening/toggling the inventory.
        /// Canonical path replaces SimulateToggle() calls from mobile buttons.
        /// </summary>
        public static event Action OnInventoryToggleRequested;

        /// <summary>
        /// Call from both the keyboard Tab handler and the mobile inventory button.
        /// </summary>
        public static void RequestInventoryToggle()
            => OnInventoryToggleRequested?.Invoke();

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Clear all event subscriptions. Call on scene unload to prevent stale references.
        /// Do not call this from InputManager/InputLayerManager lifecycle while HUD panels may still be alive.
        /// </summary>
        public static void Clear()
        {
            OnItemSlotRequested        = null;
            OnWeaponSlotRequested      = null;
            OnCameraLockChanged        = null;
            OnInventoryToggleRequested = null;
        }
    }
}
