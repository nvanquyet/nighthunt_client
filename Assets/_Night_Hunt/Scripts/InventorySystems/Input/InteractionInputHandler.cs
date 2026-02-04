using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Inventory.Input
{
    /// <summary>
    /// Handles ONLY interaction input (Interact, Pickup)
    /// Used for interacting with objects, picking up items, etc.
    /// Components read input values from this handler via InputManager
    /// </summary>
    public class InteractionInputHandler : MonoBehaviour, IInputHandler
    {
        private InputActionMap playerActionMap;
        private InputAction interactAction;
        private InputAction pickupAction;

        // State
        private bool isInteracting;
        private bool isPickingUp;

        private bool inputEnabled = false;

        // Events
        public event System.Action OnInteract;
        public event System.Action OnInteractStop;
        public event System.Action OnPickup;
        public event System.Action OnPickupStop;

        #region Lifecycle

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            RegisterWithManager();
        }

        private void OnDisable()
        {
            DisableInput();
            UnregisterFromManager();
        }

        #endregion

        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[InteractionInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            playerActionMap = InputLayerManager.Instance.PlayerMap;

            if (playerActionMap != null)
            {
                interactAction = playerActionMap.FindAction("Interact");
                pickupAction = playerActionMap.FindAction("Pickup");
            }
            else
            {
                Debug.LogError("[InteractionInputHandler] 'Player' action map not found!");
            }
        }

        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        #endregion

        #region IInputHandler Implementation

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap() => playerActionMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            inputEnabled = true;

            // Subscribe
            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
                interactAction.canceled += OnInteractCanceled;
            }

            if (pickupAction != null)
            {
                pickupAction.performed += OnPickupPerformed;
                pickupAction.canceled += OnPickupCanceled;
            }

            Debug.Log("[InteractionInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            // Unsubscribe
            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
                interactAction.canceled -= OnInteractCanceled;
            }

            if (pickupAction != null)
            {
                pickupAction.performed -= OnPickupPerformed;
                pickupAction.canceled -= OnPickupCanceled;
            }

            // Reset state
            isInteracting = false;
            isPickingUp = false;

            Debug.Log("[InteractionInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            isInteracting = true;
            OnInteract?.Invoke();
        }

        private void OnInteractCanceled(InputAction.CallbackContext ctx)
        {
            isInteracting = false;
            OnInteractStop?.Invoke();
        }

        private void OnPickupPerformed(InputAction.CallbackContext ctx)
        {
            isPickingUp = true;
            OnPickup?.Invoke();
        }

        private void OnPickupCanceled(InputAction.CallbackContext ctx)
        {
            isPickingUp = false;
            OnPickupStop?.Invoke();
        }

        #endregion

        #region Public API

        public bool IsInteracting() => isInteracting;
        public bool IsPickingUp() => isPickingUp;

        #endregion
    }
}