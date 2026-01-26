using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Core.State;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Centralized input layer manager with enable/disable management
    /// Prevents action map conflicts and manages state-based input
    /// </summary>
    public class InputLayerManager : MonoBehaviour
    {
        private static InputLayerManager _instance;
        public static InputLayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("InputLayerManager");
                    _instance = go.AddComponent<InputLayerManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset inputActionAsset;

        private readonly Dictionary<string, InputActionMapController> actionMaps = new Dictionary<string, InputActionMapController>();
        private StateMachine<InputState> stateMachine;
        private InputState currentState = InputState.PlayerAlive;

        // Action map names
        private const string PLAYER_MAP = "Player";
        private const string UI_MAP = "UI";
        private const string CAMERA_MAP = "Camera";
        private const string SPECTATOR_MAP = "Spectator";
        private const string GAMEPLAY_MAP = "Gameplay";

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeStateMachine();
            InitializeActionMaps();
        }

        /// <summary>
        /// Initialize state machine
        /// </summary>
        private void InitializeStateMachine()
        {
            stateMachine = new StateMachine<InputState>(InputState.PlayerAlive);

            // Define allowed transitions
            stateMachine.AddTransitions(InputState.PlayerAlive,
                InputState.PlayerDead,
                InputState.Spectating,
                InputState.MenuOpen,
                InputState.Paused,
                InputState.ScoutMode,
                InputState.InventoryOpen);

            stateMachine.AddTransitions(InputState.PlayerDead,
                InputState.Spectating,
                InputState.MenuOpen,
                InputState.Paused,
                InputState.PlayerAlive); // When respawned

            stateMachine.AddTransitions(InputState.Spectating,
                InputState.PlayerAlive, // When respawned
                InputState.MenuOpen,
                InputState.Paused);

            stateMachine.AddTransitions(InputState.MenuOpen,
                InputState.PlayerAlive,
                InputState.PlayerDead,
                InputState.Spectating,
                InputState.Paused,
                InputState.InventoryOpen);

            stateMachine.AddTransitions(InputState.Paused,
                InputState.PlayerAlive,
                InputState.PlayerDead,
                InputState.Spectating,
                InputState.MenuOpen,
                InputState.InventoryOpen);

            stateMachine.AddTransitions(InputState.ScoutMode,
                InputState.PlayerAlive,
                InputState.MenuOpen,
                InputState.Paused,
                InputState.InventoryOpen);

            stateMachine.AddTransitions(InputState.InventoryOpen,
                InputState.PlayerAlive,
                InputState.PlayerDead,
                InputState.MenuOpen,
                InputState.Paused);

            stateMachine.OnStateChanged += OnStateChanged;
        }

        /// <summary>
        /// Initialize action maps
        /// </summary>
        private void InitializeActionMaps()
        {
            if (inputActionAsset == null)
            {
                Debug.LogError("[InputLayerManager] InputActionAsset is null! Please assign in inspector.");
                return;
            }

            // Register all action maps
            RegisterActionMap(PLAYER_MAP);
            RegisterActionMap(UI_MAP);
            RegisterActionMap(CAMERA_MAP);
            RegisterActionMap(SPECTATOR_MAP);
            //RegisterActionMap(GAMEPLAY_MAP);

            // Set initial state
            UpdateActionMapsForState(InputState.PlayerAlive);
        }

        /// <summary>
        /// Register an action map
        /// </summary>
        private void RegisterActionMap(string mapName)
        {
            var map = inputActionAsset.FindActionMap(mapName);
            if (map != null)
            {
                actionMaps[mapName] = new InputActionMapController(map);
            }
            else
            {
                Debug.LogWarning($"[InputLayerManager] Action map '{mapName}' not found in InputActionAsset");
            }
        }

        /// <summary>
        /// Handle state changes
        /// </summary>
        private void OnStateChanged(InputState previousState, InputState newState)
        {
            currentState = newState;
            UpdateActionMapsForState(newState);
        }

        /// <summary>
        /// Update action maps based on current state
        /// </summary>
        private void UpdateActionMapsForState(InputState state)
        {
            // Disable all maps first
            foreach (var kvp in actionMaps)
            {
                kvp.Value.Disable();
            }

            // Enable maps based on state
            switch (state)
            {
                case InputState.PlayerAlive:
                    EnableActionMap(PLAYER_MAP);
                    EnableActionMap(CAMERA_MAP);
                    break;

                case InputState.PlayerDead:
                    EnableActionMap(SPECTATOR_MAP);
                    EnableActionMap(UI_MAP); // Limited UI
                    break;

                case InputState.Spectating:
                    EnableActionMap(SPECTATOR_MAP);
                    EnableActionMap(UI_MAP);
                    break;

                case InputState.MenuOpen:
                    EnableActionMap(UI_MAP);
                    break;

                case InputState.Paused:
                    EnableActionMap(UI_MAP); // Pause menu only
                    break;

                case InputState.ScoutMode:
                    EnableActionMap(CAMERA_MAP); // Scout only
                    // Attack disabled in PlayerInputHandler
                    break;

                case InputState.InventoryOpen:
                    EnableActionMap(UI_MAP); // Only UI map for inventory interactions
                    break;
            }
        }

        /// <summary>
        /// Enable action map with conflict resolution
        /// </summary>
        private void EnableActionMap(string mapName)
        {
            if (actionMaps.TryGetValue(mapName, out var controller))
            {
                controller.Enable();
            }
        }

        /// <summary>
        /// Transition to new input state
        /// </summary>
        public bool TransitionToState(InputState newState)
        {
            return stateMachine?.TransitionTo(newState) ?? false;
        }

        /// <summary>
        /// Get current input state
        /// </summary>
        public InputState CurrentState => currentState;

        /// <summary>
        /// Check if action map is enabled
        /// </summary>
        public bool IsActionMapEnabled(string mapName)
        {
            return actionMaps.TryGetValue(mapName, out var controller) && controller.IsEnabled;
        }

        /// <summary>
        /// Get action map controller
        /// </summary>
        public InputActionMapController GetActionMapController(string mapName)
        {
            actionMaps.TryGetValue(mapName, out var controller);
            return controller;
        }

        /// <summary>
        /// Get controller for input state (alias for compatibility)
        /// </summary>
        public InputActionMapController GetController(InputState state)
        {
            string mapName = GetMapNameForState(state);
            return GetActionMapController(mapName);
        }

        /// <summary>
        /// Get map name for input state
        /// </summary>
        private string GetMapNameForState(InputState state)
        {
            switch (state)
            {
                case InputState.PlayerAlive:
                case InputState.PlayerDead:
                    return PLAYER_MAP;
                case InputState.Spectating:
                    return SPECTATOR_MAP;
                case InputState.MenuOpen:
                case InputState.Paused:
                case InputState.InventoryOpen:
                    return UI_MAP;
                case InputState.Camera:
                case InputState.ScoutMode:
                    return CAMERA_MAP;
                default:
                    return PLAYER_MAP;
            }
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.OnStateChanged -= OnStateChanged;
            }
        }
    }
}

