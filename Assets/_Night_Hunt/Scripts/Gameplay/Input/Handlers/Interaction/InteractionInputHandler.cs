using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Diagnostics;

namespace NightHunt.Gameplay.Input.Handlers.Interaction
{
    /// <summary>
    /// Handles interaction / pickup input (E = Interact, F = Pickup / Pickup-All) at the input layer.
    ///
    /// DESIGN (SRP):
    ///   - Owns ONLY InputSystem wiring (InputActionMap, callbacks).
    ///   - Exposes high-level events (Interact, Pickup, LogNearby) for gameplay systems
    ///     such as PlayerInteractionSystem to subscribe to.
    ///   - Lives in the Input layer (InputLayerManager), not on the player prefab.
    /// </summary>
    public class InteractionInputHandler : MonoBehaviour, IInputHandler
    {

        // ── Events (consumed by gameplay layer) ───────────────────────────────────

        /// <summary>Raised when the Interact action is performed (pressed).</summary>
        public event Action InteractPerformed;

        /// <summary>Raised when the Interact action is canceled (released).</summary>
        public event Action InteractCanceled;

        /// <summary>Raised when the Pickup action is performed.</summary>
        public event Action PickupPerformed;

        /// <summary>Raised when the LogNearby action is performed.</summary>
        public event Action LogNearbyPerformed;

        // ── Private state ────────────────────────────────────────────────────────

        private InputActionMap playerMap;
        private InputAction interactAction;  // E
        private InputAction pickupAction;    // F
        private InputAction logNearbyAction; // Tab (optional)

        private bool inputEnabled = false;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void OnEnable()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void OnDisable()
        {
            DisableInput();
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        // ── IInputHandler ────────────────────────────────────────────────────────

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap()
        {
            // IMPORTANT:
            // InputLayerManager uses GetActionMap().enabled to decide whether to call EnableInput().
            // If we return null here, the handler will never be enabled → E/F events won't fire.
            if (playerMap == null && InputLayerManager.Instance != null)
                playerMap = InputLayerManager.Instance.PlayerMap;

            return playerMap;
        }

        public void EnableInput()
        {
            if (inputEnabled) return;

            // Ensure we have the action map (InputLayerManager is the source of truth)
            if (playerMap == null)
            {
                if (InputLayerManager.Instance == null)
                {
                    Debug.LogError("[InteractionInputHandler] InputLayerManager.Instance is null!");
                    return;
                }

                playerMap = InputLayerManager.Instance.PlayerMap;
                if (playerMap == null)
                {
                    Debug.LogWarning("[InteractionInputHandler] Player action map not found!");
                    return;
                }
            }

            // IMPORTANT:
            // playerMap may be pre-populated via GetActionMap() before EnableInput() runs.
            // Always resolve actions if they're missing so callbacks are wired.
            if (interactAction == null)  interactAction  = playerMap.FindAction("Interact");
            if (pickupAction == null)    pickupAction    = playerMap.FindAction("Pickup");
            if (logNearbyAction == null) logNearbyAction = playerMap.FindAction("LogNearby");

            // Chỉ set true after đảm bảo có map
            inputEnabled = true;

            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
                interactAction.canceled  += OnInteractCanceled;
            }
            else
            {
                Debug.LogWarning("[InteractionInputHandler] Action 'Interact' not found in Player map.");
            }

            if (pickupAction != null)
                pickupAction.performed += OnPickupPerformed;
            else
                Debug.LogWarning("[InteractionInputHandler] Action 'Pickup' not found in Player map.");

            if (logNearbyAction != null)
                logNearbyAction.performed += OnLogNearbyPerformed;
            else
                Debug.LogWarning("[InteractionInputHandler] Action 'LogNearby' not found in Player map.");

            Debug.Log("[InteractionInputHandler] Input enabled (Player/Interact, Player/Pickup, Player/LogNearby)");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;
            inputEnabled = false;

            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
                interactAction.canceled  -= OnInteractCanceled;
            }

            if (pickupAction != null)
                pickupAction.performed -= OnPickupPerformed;

            if (logNearbyAction != null)
                logNearbyAction.performed -= OnLogNearbyPerformed;
        }

        // ── Input event handlers ─────────────────────────────────────────────────

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            Debug.Log("[Input][Player] Interact (E) performed");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InteractPressed", $"control={ctx.control?.path ?? "unknown"}", this);
            InteractPerformed?.Invoke();
        }

        private void OnInteractCanceled(InputAction.CallbackContext ctx)
        {
            Debug.Log("[Input][Player] Interact (E) canceled");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InteractReleased", $"control={ctx.control?.path ?? "unknown"}", this);
            InteractCanceled?.Invoke();
        }

        private void OnPickupPerformed(InputAction.CallbackContext ctx)
        {
            Debug.Log("[Input][Player] Pickup (F) performed");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "PickupPressed", $"control={ctx.control?.path ?? "unknown"}", this);
            PickupPerformed?.Invoke();
        }

        private void OnLogNearbyPerformed(InputAction.CallbackContext ctx)
        {
            Debug.Log("[Input][Player] LogNearby (F6) performed");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "LogNearbyPressed", $"control={ctx.control?.path ?? "unknown"}", this);
            LogNearbyPerformed?.Invoke();
        }

        // Mobile HUD API: mirrors the Player/Interact and Player/Pickup actions.
        public void SimulateInteractPressed()
        {
            if (!inputEnabled) return;
            Debug.Log("[Input][Mobile] Interact performed");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "MobileInteractPressed", "source=mobile-hud", this);
            InteractPerformed?.Invoke();
        }

        public void SimulateInteractReleased()
        {
            if (!inputEnabled) return;
            Debug.Log("[Input][Mobile] Interact canceled");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "MobileInteractReleased", "source=mobile-hud", this);
            InteractCanceled?.Invoke();
        }

        public void SimulatePickup()
        {
            if (!inputEnabled) return;
            Debug.Log("[Input][Mobile] Pickup performed");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "MobilePickupPressed", "source=mobile-hud", this);
            PickupPerformed?.Invoke();
        }

    }
}
