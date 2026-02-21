using UnityEngine;
using NightHunt.Gameplay.Input;
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
        /// Enable scout mode (movement + camera, no combat).
        /// DEPRECATED: Use InputLayerManager.TransitionToState(InputState.ScoutMode) instead.
        /// Kept for backward compatibility.
        /// </summary>
        [System.Obsolete("Use InputLayerManager.TransitionToState(InputState.ScoutMode) instead")]
        public void EnableScoutMode()
        {
            InputLayerManager.Instance?.TransitionToState(InputState.ScoutMode);
            Debug.Log("[InputManager] Scout mode enabled (via deprecated method)");
        }

        #endregion

        #region State Mode Methods (Called by InputLayerManager)

        /// <summary>
        /// Set input mode for PlayerAlive state.
        /// Called by InputLayerManager when transitioning to PlayerAlive.
        /// </summary>
        public void SetPlayerAliveMode()
        {
            movementHandler?.EnableInput();
            combatHandler?.EnableInput();
            cameraHandler?.EnableInput();
            uiInputHandler?.EnableInput();
        }

        /// <summary>
        /// Set input mode for InventoryOpen state.
        /// Called by InputLayerManager when transitioning to InventoryOpen.
        /// </summary>
        public void SetInventoryMode()
        {
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            cameraHandler?.DisableInput(); // Tuỳ gameplay, có thể giữ hoặc tắt
            uiInputHandler?.EnableInput();
        }

        /// <summary>
        /// Set input mode for Spectating state.
        /// Called by InputLayerManager when transitioning to Spectating.
        /// </summary>
        public void SetSpectatorMode()
        {
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            cameraHandler?.EnableInput(); // Spectator camera
            uiInputHandler?.EnableInput();
        }

        /// <summary>
        /// Set input mode for Menu state.
        /// Called by InputLayerManager when transitioning to MenuOpen.
        /// </summary>
        public void SetMenuMode()
        {
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            cameraHandler?.DisableInput();
            uiInputHandler?.EnableInput();
        }

        /// <summary>
        /// Set input mode for Dead state.
        /// Called by InputLayerManager when transitioning to PlayerDead.
        /// </summary>
        public void SetDeadMode()
        {
            SetSpectatorMode(); // Dead = spectator mode
        }

        /// <summary>
        /// Set input mode for ScoutMode state.
        /// Called by InputLayerManager when transitioning to ScoutMode.
        /// </summary>
        public void SetScoutMode()
        {
            movementHandler?.EnableInput();
            combatHandler?.DisableInput(); // No combat in scout mode
            cameraHandler?.EnableInput();
            uiInputHandler?.EnableInput();
        }

        /// <summary>
        /// Set input mode for UsingDevice state.
        /// Called by InputLayerManager when transitioning to UsingDevice.
        /// </summary>
        public void SetUsingDeviceMode()
        {
            // Tuỳ device: ví dụ khoá di chuyển, chỉ device/camera
            movementHandler?.DisableInput();
            combatHandler?.DisableInput();
            cameraHandler?.EnableInput();
            uiInputHandler?.EnableInput();
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