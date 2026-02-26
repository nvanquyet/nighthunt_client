using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

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

        public InputActionMap GetActionMap() => playerMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            // Lazy init – lấy map nếu chưa có
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

                interactAction  = playerMap.FindAction("Interact");
                pickupAction    = playerMap.FindAction("Pickup");
                logNearbyAction = playerMap.FindAction("LogNearby");
            }

            // Chỉ set true sau khi đảm bảo có map
            inputEnabled = true;

            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
                interactAction.canceled  += OnInteractCanceled;
            }

            if (pickupAction != null)
                pickupAction.performed += OnPickupPerformed;

            if (logNearbyAction != null)
                logNearbyAction.performed += OnLogNearbyPerformed;
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
            InteractPerformed?.Invoke();
        }

        private void OnInteractCanceled(InputAction.CallbackContext ctx)
        {
            InteractCanceled?.Invoke();
        }

        private void OnPickupPerformed(InputAction.CallbackContext ctx)
        {
            PickupPerformed?.Invoke();
        }

        private void OnLogNearbyPerformed(InputAction.CallbackContext ctx)
        {
            LogNearbyPerformed?.Invoke();
        }

    }
}
