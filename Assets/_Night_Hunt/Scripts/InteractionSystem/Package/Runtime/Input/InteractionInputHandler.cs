using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Input
{
    /// <summary>
    /// Input handler using Unity's New Input System.
    /// Auto-binds actions from PlayerInput.actions asset (no inspector fields needed).
    /// Handles all interaction and pickup inputs.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class InteractionInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Pickup.Detection.PickupDetector pickupDetector;
        [SerializeField] private Interaction.Detection.InteractionDetector interactionDetector;
 
        private PlayerInput playerInput;
        private InputAction interactAction;
        private InputAction pickupAction;
        private InputAction inventoryAction;

        private void Awake()
        {
            try
            {
                Debug.Log($"[InteractionInputHandler] Awake - Go={gameObject.name}, Parent={transform.parent?.name ?? "None"}, Root={transform.root?.name ?? "None"}");
                
                // Use centralized component finder to search in hierarchy
                playerInput = ComponentFinder.FindComponentInHierarchy<PlayerInput>(gameObject, includeInactive: false);
                
                if (playerInput == null)
                {
                    Debug.LogError($"[InteractionInputHandler] PlayerInput component not found! Searched in: {gameObject.name}, parent, children, and root ({transform.root?.name ?? "None"}) and its children. This component requires PlayerInput.");
                    enabled = false;
                    return;
                }
                
                Debug.Log($"[InteractionInputHandler] Found PlayerInput: {playerInput.gameObject.name}");
                
                // Find detectors if not assigned - use centralized component finder
                if (pickupDetector == null)
                {
                    pickupDetector = ComponentFinder.FindComponentInHierarchy<Pickup.Detection.PickupDetector>(gameObject, includeInactive: false);
                }
                
                if (interactionDetector == null)
                {
                    interactionDetector = ComponentFinder.FindComponentInHierarchy<Interaction.Detection.InteractionDetector>(gameObject, includeInactive: false);
                }

                // Auto-bind actions from PlayerInput.actions
                BindActions();
                
                Debug.Log($"[InteractionInputHandler] Awake completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InteractionInputHandler] EXCEPTION in Awake for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            // Actions are managed by PlayerInput/InputLayerManager, no need to enable/disable here
            SubscribeActions();
        }

        private void OnDisable()
        {
            UnsubscribeActions();
        }

        /// <summary>
        /// Auto-bind actions from PlayerInput.actions asset.
        /// </summary>
        private void BindActions()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogWarning("[InteractionInputHandler] PlayerInput or actions asset is null!");
                return;
            }

            // Find actions by name (searches across all maps)
            interactAction = playerInput.actions.FindAction("Interact", throwIfNotFound: false);
            pickupAction = playerInput.actions.FindAction("Pickup", throwIfNotFound: false);
            
            // Inventory action might be in Inventory map
            inventoryAction = playerInput.actions.FindAction("OpenInventory", throwIfNotFound: false) 
                           ?? playerInput.actions.FindAction("Inventory", throwIfNotFound: false);

            if (interactAction == null)
                Debug.LogWarning("[InteractionInputHandler] Could not find 'Interact' action in PlayerInput.actions");
            if (pickupAction == null)
                Debug.LogWarning("[InteractionInputHandler] Could not find 'Pickup' action in PlayerInput.actions");
            if (inventoryAction == null)
                Debug.LogWarning("[InteractionInputHandler] Could not find 'OpenInventory' or 'Inventory' action in PlayerInput.actions");
        }

        /// <summary>
        /// Subscribe to action callbacks.
        /// </summary>
        private void SubscribeActions()
        {
            if (interactAction != null)
                interactAction.performed += OnInteractPerformed;
            
            if (pickupAction != null)
                pickupAction.performed += OnPickupPerformed;
            
            if (inventoryAction != null)
                inventoryAction.performed += OnInventoryPerformed;
        }

        /// <summary>
        /// Unsubscribe from action callbacks.
        /// </summary>
        private void UnsubscribeActions()
        {
            if (interactAction != null)
                interactAction.performed -= OnInteractPerformed;
            
            if (pickupAction != null)
                pickupAction.performed -= OnPickupPerformed;
            
            if (inventoryAction != null)
                inventoryAction.performed -= OnInventoryPerformed;
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
            Debug.Log($"[InteractionInputHandler] Pickup action performed! pickupDetector={pickupDetector != null}");
            if (pickupDetector != null)
            {
                pickupDetector.TryPickup();
            }
            else
            {
                Debug.LogWarning("[InteractionInputHandler] PickupDetector is null! Cannot pickup.");
            }
        }

        private void OnInventoryPerformed(InputAction.CallbackContext context)
        {
            // Toggle inventory UI - invoke via method, not direct event access
            InventoryEvents.InvokeInventoryChanged();
        }
    }
}
