using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Centralized manager for input states and action map layering
    /// Manages all input handlers and transitions between states
    /// Singleton pattern for global access
    /// </summary>
    public class InputLayerManager : MonoBehaviour
    {
        public static InputLayerManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private InputConfig inputConfig;

        // Action maps (cached from InputActionAsset)
        private InputActionMap playerMap;
        private InputActionMap combatMap;
        private InputActionMap inventoryMap;
        private InputActionMap cameraMap;
        private InputActionMap uiMap;
        private InputActionMap spectatorMap;
        private InputActionMap teamMap;

        // Registered handlers (populated at runtime)
        private readonly List<IInputHandler> registeredHandlers = new List<IInputHandler>();

        // Current state
        private InputState currentState = InputState.None;

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

            InitializeActionMaps();
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

        /// <summary>
        /// Initialize and cache all action maps from InputActionAsset
        /// </summary>
        private void InitializeActionMaps()
        {
            if (inputConfig == null)
            {
                Debug.LogError("[InputLayerManager] InputConfig is not assigned!");
                return;
            }

            var inputActions = inputConfig.InputActionAsset;
            if (inputActions == null)
            {
                Debug.LogError("[InputLayerManager] InputActionAsset is null in InputConfig!");
                return;
            }

            // Cache action maps
            playerMap = inputActions.FindActionMap(inputConfig.PlayerMapName);
            combatMap = inputActions.FindActionMap(inputConfig.CombatMapName);
            inventoryMap = inputActions.FindActionMap(inputConfig.InventoryMapName);
            cameraMap = inputActions.FindActionMap(inputConfig.CameraMapName);
            uiMap = inputActions.FindActionMap(inputConfig.UIMapName);
            spectatorMap = inputActions.FindActionMap(inputConfig.SpectatorMapName);
            teamMap = inputActions.FindActionMap(inputConfig.TeamMapName);

            // Validate
            ValidateActionMaps();

            Debug.Log("[InputLayerManager] Initialized successfully");
        }

        /// <summary>
        /// Validate that all action maps exist
        /// </summary>
        private void ValidateActionMaps()
        {
            if (playerMap == null) Debug.LogWarning("[InputLayerManager] 'Player' action map not found!");
            if (combatMap == null) Debug.LogWarning("[InputLayerManager] 'Combat' action map not found!");
            if (inventoryMap == null) Debug.LogWarning("[InputLayerManager] 'Inventory' action map not found!");
            if (cameraMap == null) Debug.LogWarning("[InputLayerManager] 'Camera' action map not found!");
            if (uiMap == null) Debug.LogWarning("[InputLayerManager] 'UI' action map not found!");
            if (spectatorMap == null) Debug.LogWarning("[InputLayerManager] 'Spectator' action map not found!");
        }

        #endregion

        #region Handler Registration

        /// <summary>
        /// Register an input handler with the manager
        /// Called by handlers in their Awake/Start
        /// </summary>
        public void RegisterHandler(IInputHandler handler)
        {
            if (handler == null)
            {
                Debug.LogWarning("[InputLayerManager] Attempted to register null handler!");
                return;
            }

            if (!registeredHandlers.Contains(handler))
            {
                registeredHandlers.Add(handler);
                Debug.Log($"[InputLayerManager] Registered handler for action map: {handler.GetActionMap()?.name ?? "Unknown"}");
            }
        }

        /// <summary>
        /// Unregister an input handler
        /// Called by handlers in their OnDestroy
        /// </summary>
        public void UnregisterHandler(IInputHandler handler)
        {
            if (handler != null && registeredHandlers.Contains(handler))
            {
                registeredHandlers.Remove(handler);
                Debug.Log($"[InputLayerManager] Unregistered handler for action map: {handler.GetActionMap()?.name ?? "Unknown"}");
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Transition to a new input state
        /// Automatically enables/disables appropriate action maps and calls InputManager mode methods
        /// </summary>
        public void TransitionToState(InputState newState)
        {
            if (currentState == newState) return;

            Debug.Log($"[InputLayerManager] State transition: {currentState} → {newState}");

            // Disable current state
            DisableStateInputs(currentState);

            // Update state
            currentState = newState;

            // Enable new state (action maps)
            EnableStateInputs(newState);

            // Update InputManager handlers based on state
            UpdateInputManagerMode(newState);
        }

        /// <summary>
        /// Update InputManager mode methods based on state
        /// </summary>
        private void UpdateInputManagerMode(InputState state)
        {
            var inputManager = InputManager.Instance;
            if (inputManager == null) return;

            switch (state)
            {
                case InputState.PlayerAlive:
                    inputManager.SetPlayerAliveMode();
                    break;

                case InputState.InventoryOpen:
                    inputManager.SetInventoryMode();
                    break;

                case InputState.Spectating:
                    inputManager.SetSpectatorMode();
                    break;

                case InputState.MenuOpen:
                case InputState.Paused:
                    inputManager.SetMenuMode();
                    break;

                case InputState.PlayerDead:
                    inputManager.SetDeadMode();
                    break;

                case InputState.ScoutMode:
                    inputManager.SetScoutMode();
                    break;

                case InputState.Camera:
                case InputState.InDialogue:
                    // Tuỳ bạn: có thể tạo mode riêng hoặc dùng menu mode
                    inputManager.SetMenuMode();
                    break;

                case InputState.None:
                    inputManager.DisableAllInput();
                    break;
            }
        }

        /// <summary>
        /// Get current input state
        /// </summary>
        public InputState GetCurrentState() => currentState;

        #endregion

        #region Action Map Control

        /// <summary>
        /// Disable action maps for a specific state
        /// </summary>
        private void DisableStateInputs(InputState state)
        {
            switch (state)
            {
                case InputState.None:
                    // Nothing to disable
                    break;

                case InputState.PlayerAlive:
                    playerMap?.Disable();
                    combatMap?.Disable();
                    inventoryMap?.Disable();
                    cameraMap?.Disable();
                    teamMap?.Disable();
                    break;

                case InputState.InventoryOpen:
                    inventoryMap?.Disable();
                    uiMap?.Disable();
                    break;

                case InputState.MenuOpen:
                case InputState.Paused:
                    uiMap?.Disable();
                    break;

                case InputState.PlayerDead:
                    uiMap?.Disable();
                    break;

                case InputState.Spectating:
                    spectatorMap?.Disable();
                    cameraMap?.Disable();
                    break;

                case InputState.ScoutMode:
                    playerMap?.Disable();
                    cameraMap?.Disable();
                    // Combat disabled in scout mode
                    break;

                case InputState.Camera:
                    cameraMap?.Disable();
                    break;

                case InputState.InDialogue:
                    uiMap?.Disable();
                    break;
            }
        }

        /// <summary>
        /// Enable action maps for a specific state
        /// </summary>
        private void EnableStateInputs(InputState state)
        {
            switch (state)
            {
                case InputState.None:
                    DisableAll();
                    break;

                case InputState.PlayerAlive:
                    // Full gameplay control
                    playerMap?.Enable();
                    combatMap?.Enable();
                    inventoryMap?.Enable(); // Quick slots always active
                    cameraMap?.Enable();
                    teamMap?.Enable();
                    break;

                case InputState.InventoryOpen:
                    // Only inventory + UI navigation
                    inventoryMap?.Enable();
                    uiMap?.Enable();
                    // Player movement & combat disabled
                    break;

                case InputState.MenuOpen:
                case InputState.Paused:
                    // Only UI navigation
                    uiMap?.Enable();
                    break;

                case InputState.PlayerDead:
                    // Only UI for respawn menu
                    uiMap?.Enable();
                    break;

                case InputState.Spectating:
                    // Spectator controls + camera
                    spectatorMap?.Enable();
                    cameraMap?.Enable();
                    break;

                case InputState.ScoutMode:
                    // Movement + Camera, NO combat
                    playerMap?.Enable();
                    cameraMap?.Enable();
                    // Combat explicitly disabled
                    combatMap?.Disable();
                    break;

                case InputState.Camera:
                    // Only camera controls
                    cameraMap?.Enable();
                    break;

                case InputState.InDialogue:
                    // Only UI for dialogue choices
                    uiMap?.Enable();
                    break;
            }
        }

        /// <summary>
        /// Disable all input
        /// </summary>
        public void DisableAll()
        {
            playerMap?.Disable();
            combatMap?.Disable();
            inventoryMap?.Disable();
            cameraMap?.Disable();
            uiMap?.Disable();
            spectatorMap?.Disable();
            teamMap?.Disable();

            Debug.Log("[InputLayerManager] All input disabled");
        }

        #endregion

        #region Action Map Access

        /// <summary>
        /// Get specific action map
        /// </summary>
        public InputActionMap GetActionMap(string mapName)
        {
            return inputConfig.InputActionAsset?.FindActionMap(mapName);
        }

        /// <summary>
        /// Get action from a specific map
        /// </summary>
        public InputAction GetAction(string mapName, string actionName)
        {
            var map = GetActionMap(mapName);
            return map?.FindAction(actionName);
        }

        /// <summary>
        /// Check if specific action map is enabled
        /// </summary>
        public bool IsActionMapEnabled(string mapName)
        {
            var map = GetActionMap(mapName);
            return map != null && map.enabled;
        }

        #endregion

        #region Public Accessors

        public InputActionMap PlayerMap => playerMap;
        public InputActionMap CombatMap => combatMap;
        public InputActionMap InventoryMap => inventoryMap;
        public InputActionMap CameraMap => cameraMap;
        public InputActionMap UIMap => uiMap;
        public InputActionMap SpectatorMap => spectatorMap;
        public InputActionMap TeamMap => teamMap;

        public InputConfig Config => inputConfig;

        #endregion
    }
}