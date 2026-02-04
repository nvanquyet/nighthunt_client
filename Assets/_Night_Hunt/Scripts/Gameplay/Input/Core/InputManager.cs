using UnityEngine;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Inventory.Input;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Centralized input manager for local player
    /// Singleton pattern - manages all input handlers
    /// Components read input values from here
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Handler References")]
        [SerializeField] private MovementInputHandler movementHandler;
        [SerializeField] private CombatInputHandler combatHandler;
        [SerializeField] private CameraInputHandler cameraHandler;
        [SerializeField] private InteractionInputHandler interactionHandler;
        [SerializeField] private InventoryInputHandler inventoryHandler;

        private bool isInitialized = false;

        #region Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-create handlers if not assigned
            InitializeHandlers();
        }

        private void Start()
        {
            // Enable input when start
            EnableAllInput();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private void InitializeHandlers()
        {
            // Auto-create handlers if not assigned
            if (movementHandler == null)
            {
                movementHandler = gameObject.AddComponent<MovementInputHandler>();
            }

            if (combatHandler == null)
            {
                combatHandler = gameObject.AddComponent<CombatInputHandler>();
            }

            if (cameraHandler == null)
            {
                cameraHandler = gameObject.AddComponent<CameraInputHandler>();
            }

            if (interactionHandler == null)
            {
                interactionHandler = gameObject.AddComponent<InteractionInputHandler>();
            }

            if (inventoryHandler == null)
            {
                inventoryHandler = gameObject.AddComponent<InventoryInputHandler>();
            }

            isInitialized = true;
            Debug.Log("[InputManager] Initialized");
        }

        #endregion

        #region Input Control

        /// <summary>
        /// Enable all gameplay input
        /// Called when player spawns and is alive
        /// </summary>
        public void EnableAllInput()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[InputManager] Cannot enable: Not initialized!");
                return;
            }

            movementHandler?.EnableInput();
            combatHandler?.EnableInput();
            cameraHandler?.EnableInput();
            interactionHandler?.EnableInput();
            inventoryHandler?.EnableInput();

            // Transition to PlayerAlive state
            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);

            Debug.Log("[InputManager] All input enabled");
        }

        /// <summary>
        /// Disable all input
        /// Called when player dies or game state changes
        /// </summary>
        public void DisableAllInput()
        {
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            cameraHandler?.DisableInput();
            interactionHandler?.DisableInput();
            inventoryHandler?.DisableInput();

            Debug.Log("[InputManager] All input disabled");
        }

        /// <summary>
        /// Enable only movement (for scout mode)
        /// </summary>
        public void EnableScoutMode()
        {
            movementHandler?.EnableInput();
            combatHandler?.DisableInput(); // No combat in scout mode
            interactionHandler?.DisableInput();

            InputLayerManager.Instance?.TransitionToState(InputState.ScoutMode);

            Debug.Log("[InputManager] Scout mode enabled");
        }

        /// <summary>
        /// Disable movement and combat (for inventory)
        /// </summary>
        public void OnInventoryOpened()
        {
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            interactionHandler?.DisableInput();
            // Inventory handler stays enabled

            InputLayerManager.Instance?.TransitionToState(InputState.InventoryOpen);
        }

        /// <summary>
        /// Re-enable gameplay input after inventory closes
        /// </summary>
        public void OnInventoryClosed()
        {
            movementHandler?.EnableInput();
            combatHandler?.EnableInput();
            interactionHandler?.EnableInput();

            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);
        }

        #endregion

        #region Public Accessors

        public MovementInputHandler MovementHandler => movementHandler;
        public CombatInputHandler CombatHandler => combatHandler;
        public CameraInputHandler CameraHandler => cameraHandler;
        public InteractionInputHandler InteractionHandler => interactionHandler;
        public InventoryInputHandler InventoryHandler => inventoryHandler;

        public bool IsInitialized => isInitialized;

        #endregion
    }
}