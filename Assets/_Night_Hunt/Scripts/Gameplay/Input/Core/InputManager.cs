using UnityEngine;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Gameplay.Input.Handlers.UI;

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
        [SerializeField] private UIInputHandler uiInputHandler;

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
            
            if (uiInputHandler == null)
            {
                uiInputHandler = gameObject.AddComponent<UIInputHandler>();
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
            uiInputHandler?.EnableInput();

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
            uiInputHandler?.DisableInput();

            Debug.Log("[InputManager] All input disabled");
        }

        /// <summary>
        /// Enable only movement (for scout mode)
        /// </summary>
        public void EnableScoutMode()
        {
            movementHandler?.EnableInput();
            combatHandler?.DisableInput(); 
            InputLayerManager.Instance?.TransitionToState(InputState.ScoutMode);

            Debug.Log("[InputManager] Scout mode enabled");
        }

        /// <summary>
        /// Disable movement and combat (for inventory)
        /// </summary>
         
        /// <summary>
        /// Called when inventory is opened.
        /// Disables movement/combat but keeps UI input enabled.
        /// </summary>
        public void OnInventoryOpened()
        {
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            // UI handler stays enabled
            
            InputLayerManager.Instance?.TransitionToState(InputState.InventoryOpen);
            
            Debug.Log("[InputManager] Inventory opened - gameplay input disabled");
        }
        
        /// <summary>
        /// Called when inventory is closed.
        /// Re-enables movement/combat input.
        /// </summary>
        public void OnInventoryClosed()
        {
            movementHandler?.EnableInput();
            combatHandler?.EnableInput();
            
            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);
            
            Debug.Log("[InputManager] Inventory closed - gameplay input enabled");
        }

        #endregion

        #region Public Accessors

        public MovementInputHandler MovementHandler => movementHandler;
        public CombatInputHandler CombatHandler => combatHandler;
        public CameraInputHandler CameraHandler => cameraHandler;
        public UIInputHandler UIHandler => uiInputHandler;
        public bool IsInitialized => isInitialized;

        #endregion
    }
}