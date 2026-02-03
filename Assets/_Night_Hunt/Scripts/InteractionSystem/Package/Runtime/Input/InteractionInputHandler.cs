// This file is in a separate assembly (com.nighthunt.interactionsystem.input)
// that only compiles when ENABLE_INPUT_SYSTEM is defined.
// The assembly definition ensures this file is excluded when InputSystem is not available.

using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Core.Interfaces;
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

                Debug.Log("[InteractionInputHandler] Awake completed successfully");
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

            Debug.Log($"[InteractionInputHandler] Binding actions from PlayerInput: {playerInput.gameObject.name}");
            Debug.Log($"[InteractionInputHandler] Actions asset: {playerInput.actions.name}");

            // Find actions by name (searches across all maps)
            interactAction = playerInput.actions.FindAction("Interact", throwIfNotFound: false);
            pickupAction = playerInput.actions.FindAction("Pickup", throwIfNotFound: false);

            // Inventory action might be in Inventory map, Player map, or UI map
            inventoryAction = playerInput.actions.FindAction("OpenInventory", throwIfNotFound: false)
                              ?? playerInput.actions.FindAction("Inventory", throwIfNotFound: false);

            Debug.Log($"[InteractionInputHandler] Binding actions - Interact: {interactAction != null}, Pickup: {pickupAction != null}, Inventory: {inventoryAction != null}");

            if (interactAction != null)
            {
                string actionMapName = interactAction.actionMap?.name ?? "Unknown";
                bool isEnabled = interactAction.actionMap?.enabled ?? false;
                string actionType = interactAction.type.ToString();
                Debug.Log($"[InteractionInputHandler] Found Interact action: '{interactAction.name}' in action map: '{actionMapName}' (enabled: {isEnabled}, type: {actionType})");
                
                // Check bindings
                int bindingCount = interactAction.bindings.Count;
                Debug.Log($"[InteractionInputHandler] Interact action has {bindingCount} binding(s):");
                for (int i = 0; i < bindingCount; i++)
                {
                    var binding = interactAction.bindings[i];
                    Debug.Log($"[InteractionInputHandler]   Binding {i}: {binding.path} (effective: {binding.effectivePath})");
                }
            }
            else
            {
                Debug.LogError("[InteractionInputHandler] Could not find 'Interact' action in PlayerInput.actions!");
                Debug.LogError("[InteractionInputHandler] Please ensure 'Interact' action exists in Player action map!");
            }
            
            if (pickupAction == null)
                Debug.LogWarning("[InteractionInputHandler] Could not find 'Pickup' action in PlayerInput.actions");
            if (inventoryAction == null)
            {
                Debug.LogWarning("[InteractionInputHandler] Could not find 'OpenInventory' or 'Inventory' action in PlayerInput.actions");
                Debug.LogWarning("[InteractionInputHandler] Please ensure 'OpenInventory' action exists in Player, UI, or Inventory action map in InputActionAsset");
            }
            else
            {
                string actionMapName = inventoryAction.actionMap?.name ?? "Unknown";
                bool isEnabled = inventoryAction.actionMap?.enabled ?? false;
                Debug.Log($"[InteractionInputHandler] Found inventory action: '{inventoryAction.name}' in action map: '{actionMapName}' (enabled: {isEnabled})");
                Debug.LogWarning($"[InteractionInputHandler] IMPORTANT: Action 'OpenInventory' is in '{actionMapName}' map. " +
                    $"This map must be enabled for the action to work. " +
                    $"If it's in Player map, it won't work when inventory is open (Player map is disabled). " +
                    $"Consider moving it to UI map for better compatibility.");
            }
        }

        /// <summary>
        /// Subscribe to action callbacks.
        /// </summary>
        private void SubscribeActions()
        {
            if (interactAction != null)
            {
                // Subscribe to all phases for hold support
                interactAction.started += OnInteractStarted;
                interactAction.performed += OnInteractPerformed;
                interactAction.canceled += OnInteractCanceled;
                Debug.Log($"[InteractionInputHandler] Subscribed to Interact action (started/performed/canceled)");
            }

            if (pickupAction != null)
            {
                pickupAction.performed += OnPickupPerformed;
                Debug.Log($"[InteractionInputHandler] Subscribed to Pickup action");
            }

            if (inventoryAction != null)
            {
                inventoryAction.performed += OnInventoryPerformed;
                Debug.Log($"[InteractionInputHandler] Subscribed to Inventory action: '{inventoryAction.name}'");
            }
            else
            {
                Debug.LogWarning("[InteractionInputHandler] Cannot subscribe to inventory action - action is null!");
            }
        }

        /// <summary>
        /// Unsubscribe from action callbacks.
        /// </summary>
        private void UnsubscribeActions()
        {
            if (interactAction != null)
            {
                interactAction.started -= OnInteractStarted;
                interactAction.performed -= OnInteractPerformed;
                interactAction.canceled -= OnInteractCanceled;
            }

            if (pickupAction != null)
                pickupAction.performed -= OnPickupPerformed;

            if (inventoryAction != null)
                inventoryAction.performed -= OnInventoryPerformed;
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            Debug.Log($"[InteractionInputHandler] OnInteractStarted - Key pressed! interactionDetector={interactionDetector != null}");
            
            // For hold interactions, start holding when key is pressed
            if (interactionDetector != null)
            {
                var currentTarget = interactionDetector.GetCurrentTarget();
                Debug.Log($"[InteractionInputHandler] Current target: {currentTarget?.GetType().Name}, Type: {currentTarget?.GetInteractionType()}");
                
                if (currentTarget != null && currentTarget.GetInteractionType() == InteractionType.Hold)
                {
                    Debug.Log($"[InteractionInputHandler] Starting Hold interaction with {currentTarget.GetType().Name}");
                    // Get InteractionHandler using ComponentFinder (searches in hierarchy including children)
                    var interactionHandler = ComponentFinder.FindComponentInHierarchy<Interaction.Handlers.InteractionHandler>(gameObject, includeInactive: false);
                    Debug.Log($"[InteractionInputHandler] InteractionHandler found: {interactionHandler != null} (searched in hierarchy)");
                    
                    if (interactionHandler != null)
                    {
                        Debug.Log($"[InteractionInputHandler] Calling interactionHandler.HandleInteraction()");
                        interactionHandler.HandleInteraction(currentTarget);
                    }
                    else
                    {
                        Debug.LogError("[InteractionInputHandler] InteractionHandler not found in hierarchy! Cannot start hold interaction.");
                        Debug.LogError($"[InteractionInputHandler] GameObject: {gameObject.name}, Root: {transform.root.name}");
                        Debug.LogError($"[InteractionInputHandler] Searched in: current object, parent, children, root, and root's children");
                        Debug.LogError($"[InteractionInputHandler] Current object components: {string.Join(", ", GetComponents<MonoBehaviour>().Select(c => c.GetType().Name))}");
                    }
                }
                else if (currentTarget == null)
                {
                    Debug.LogWarning("[InteractionInputHandler] No current target to interact with!");
                }
            }
            else
            {
                Debug.LogWarning("[InteractionInputHandler] InteractionDetector is null!");
            }
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            Debug.Log($"[InteractionInputHandler] OnInteractPerformed - Key action performed! interactionDetector={interactionDetector != null}");
            
            if (interactionDetector != null)
            {
                var currentTarget = interactionDetector.GetCurrentTarget();
                Debug.Log($"[InteractionInputHandler] Current target: {currentTarget?.GetType().Name}, Type: {currentTarget?.GetInteractionType()}");
                
                // For non-hold interactions, perform immediately
                if (currentTarget == null || currentTarget.GetInteractionType() != InteractionType.Hold)
                {
                    Debug.Log($"[InteractionInputHandler] Performing immediate interaction with {currentTarget?.GetType().Name ?? "null"}");
                    // Try to interact - this will handle containers/shops
                    // If interaction is with container/shop, it will auto-open inventory via events
                    interactionDetector.TryInteract();
                }
                else
                {
                    Debug.Log("[InteractionInputHandler] Hold interaction - skipping immediate perform (will complete when hold finishes)");
                }
                // For hold interactions, the hold handler will complete it automatically
            }
            else
            {
                Debug.LogWarning("[InteractionInputHandler] InteractionDetector is null!");
            }
        }

        private void OnInteractCanceled(InputAction.CallbackContext context)
        {
            Debug.Log($"[InteractionInputHandler] OnInteractCanceled - Key released! interactionDetector={interactionDetector != null}");
            
            // For hold interactions, stop holding when key is released
            if (interactionDetector != null)
            {
                var currentTarget = interactionDetector.GetCurrentTarget();
                if (currentTarget != null && currentTarget.GetInteractionType() == InteractionType.Hold)
                {
                    Debug.Log($"[InteractionInputHandler] Stopping Hold interaction with {currentTarget.GetType().Name}");
                    // Get HoldInteractionHandler using ComponentFinder (searches in hierarchy including children)
                    var holdHandler = ComponentFinder.FindComponentInHierarchy<Interaction.Handlers.HoldInteractionHandler>(gameObject, includeInactive: false);
                    Debug.Log($"[InteractionInputHandler] HoldInteractionHandler found: {holdHandler != null} (searched in hierarchy)");
                    
                    if (holdHandler != null)
                    {
                        if (holdHandler.IsHolding())
                        {
                            holdHandler.StopHoldInteraction();
                        }
                        else
                        {
                            Debug.LogWarning("[InteractionInputHandler] HoldHandler exists but not currently holding!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[InteractionInputHandler] HoldInteractionHandler not found in hierarchy!");
                    }
                }
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
            Debug.Log("[InteractionInputHandler] OnInventoryPerformed - F key pressed, toggling inventory");
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string stack = System.Environment.StackTrace;
                stack = stack.Replace("\r", " ").Replace("\n", " ");
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_input\",\"timestamp\":{ts},\"location\":\"InteractionInputHandler.cs:OnInventoryPerformed\",\"message\":\"F pressed -> InvokeInventoryChanged\",\"data\":{{\"stack\":\"{stack}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run4\",\"hypothesisId\":\"UI_CLOSE\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
            // Toggle inventory UI - invoke via method, not direct event access
            InventoryEvents.InvokeInventoryChanged();
        }
    }
}