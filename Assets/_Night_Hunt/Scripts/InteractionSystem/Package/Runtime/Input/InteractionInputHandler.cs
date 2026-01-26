using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Input
{
    /// <summary>
    /// Input handler using Unity's New Input System.
    /// Handles all interaction and pickup inputs.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class InteractionInputHandler : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference pickupAction;
        [SerializeField] private InputActionReference inventoryAction;

        [Header("References")]
        [SerializeField] private Pickup.Detection.PickupDetector pickupDetector;
        [SerializeField] private Interaction.Detection.InteractionDetector interactionDetector;

        private PlayerInput playerInput;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            
            // Find detectors if not assigned
            if (pickupDetector == null)
                pickupDetector = GetComponentInChildren<Pickup.Detection.PickupDetector>();
            
            if (interactionDetector == null)
                interactionDetector = GetComponentInChildren<Interaction.Detection.InteractionDetector>();

            // Setup input actions
            SetupInputActions();
        }

        private void OnEnable()
        {
            if (interactAction != null)
                interactAction.action.Enable();
            if (pickupAction != null)
                pickupAction.action.Enable();
            if (inventoryAction != null)
                inventoryAction.action.Enable();
        }

        private void OnDisable()
        {
            if (interactAction != null)
                interactAction.action.Disable();
            if (pickupAction != null)
                pickupAction.action.Disable();
            if (inventoryAction != null)
                inventoryAction.action.Disable();
        }

        /// <summary>
        /// Setup input action callbacks.
        /// </summary>
        private void SetupInputActions()
        {
            // Interact action
            if (interactAction != null)
            {
                interactAction.action.performed += OnInteractPerformed;
            }
            else
            {
                var a = FindAction("Interact");
                if (a != null)
                    a.performed += OnInteractPerformed;
            }

            // Pickup action
            if (pickupAction != null)
            {
                pickupAction.action.performed += OnPickupPerformed;
            }
            else
            {
                var a = FindAction("Pickup");
                if (a != null)
                    a.performed += OnPickupPerformed;
            }

            // Inventory action
            if (inventoryAction != null)
            {
                inventoryAction.action.performed += OnInventoryPerformed;
            }
            else
            {
                // Your project uses map `Inventory` / action `OpenInventory`
                var a = FindAction("Inventory") ?? FindAction("OpenInventory");
                if (a != null)
                    a.performed += OnInventoryPerformed;
            }
        }

        private InputAction FindAction(string actionName)
        {
            if (playerInput == null || playerInput.actions == null)
                return null;

            // FindAction searches across all maps in the asset.
            return playerInput.actions.FindAction(actionName, throwIfNotFound: false);
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (interactionDetector != null)
            {
                interactionDetector.TryInteract();
            }
        }

        private void OnPickupPerformed(InputAction.CallbackContext context)
        {
            if (pickupDetector != null)
            {
                pickupDetector.TryPickup();
            }
        }

        private void OnInventoryPerformed(InputAction.CallbackContext context)
        {
            // Toggle inventory UI - invoke via method, not direct event access
            InventoryEvents.InvokeInventoryChanged();
        }
    }
}
