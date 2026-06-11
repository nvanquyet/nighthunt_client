using System;
using System.Collections.Generic;
using NightHunt.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;
using NightHunt.Diagnostics;

namespace NightHunt.Gameplay.Input.Core
{
    // ──────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Single source of truth for all input layer management.
    ///
    /// Rules:
    ///   • Only this class may Enable/Disable ActionMaps.
    ///   • Handlers must NOT call map.Enable() / map.Disable() directly.
    ///   • Use <see cref="PushContext"/> / <see cref="PopContext"/> to switch state
    ///     instead of calling <see cref="TransitionToState"/> from multiple places.
    ///
    /// Context → Layer preset table:
    /// <code>
    ///  Context        | Player | Combat | Camera | Inventory | Team | UI | Spectator | Objectives | Devices
    ///  ───────────────┼────────┼────────┼────────┼───────────┼──────┼────┼───────────┼────────────┼────────
    ///  PlayerAlive    |   ✅   |   ✅   |   ✅   |     ✅    |  ✅  | ✅ |     ❌    |     ✅     |   ✅
    ///  InventoryOpen  |   ❌   |   ❌   |   ✅   |     ✅    |  ❌  | ✅ |     ❌    |     ❌     |   ❌
    ///  MapOpen        |   ❌   |   ❌   |   ✅   |     ❌    |  ❌  | ✅ |     ❌    |     ❌     |   ❌
    ///  Paused         |   ❌   |   ❌   |   ❌   |     ❌    |  ❌  | ✅ |     ❌    |     ❌     |   ❌
    ///  DroneControl   |   ❌   |   ❌   |   ✅   |     ❌    |  ❌  | ✅ |     ❌    |     ❌     |   ✅
    ///  Spectating     |   ❌   |   ❌   |   ❌   |     ❌    |  ✅  | ✅ |     ✅    |     ❌     |   ❌
    ///  PlayerDead     |   ❌   |   ❌   |   ❌   |     ❌    |  ✅  | ✅ |     ❌    |     ❌     |   ❌
    ///  ScoutMode      |   ✅   |   ❌   |   ✅   |     ❌    |  ✅  | ❌ |     ❌    |     ❌     |   ❌
    ///  Cinematic      |   ❌   |   ❌   |   ❌   |     ❌    |  ❌  | ❌ |     ❌    |     ❌     |   ❌
    ///  InDialogue     |   ❌   |   ❌   |   ❌   |     ❌    |  ❌  | ✅ |     ❌    |     ❌     |   ❌
    /// </code>
    /// </summary>
    // ──────────────────────────────────────────────────────────────────────────────
    // Execution order: must Awake() BEFORE any IInputHandler so Instance is ready
    // when handlers call InitializeActions() in their own Awake().
    [UnityEngine.DefaultExecutionOrder(-100)]
    public class InputLayerManager : Singleton<InputLayerManager>
    {

        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Configuration")]
        [SerializeField] private InputConfig inputConfig;

        // ── Context → Layer presets ───────────────────────────────────────────────
        private static readonly Dictionary<InputState, InputLayer> ContextPresets
            = new Dictionary<InputState, InputLayer>
        {
            {
                InputState.PlayerAlive,
                InputLayer.Player | InputLayer.Combat | InputLayer.Camera |
                InputLayer.Inventory | InputLayer.Team | InputLayer.UI |
                InputLayer.Objectives | InputLayer.Devices
            },
            {
                InputState.InventoryOpen,
                // Player ON  → can still move / interact while browsing gear (MOBA style)
                // Camera ON  → can pan / orbit camera
                // Inventory ON → hotkeys still work
                // Combat OFF  → left-click doesn't fire
                InputLayer.Player | InputLayer.UI | InputLayer.Inventory | InputLayer.Camera
            },
            {
                InputState.MapOpen,
                InputLayer.UI | InputLayer.Camera
            },
            {
                InputState.Paused,
                InputLayer.UI
            },
            {
                InputState.Spectating,
                InputLayer.Spectator | InputLayer.UI | InputLayer.Team
            },
            {
                // Free-fly mode: Camera + Spectator nav actions + UI. Player/Combat disabled.
                InputState.SpectatorFreeCamera,
                InputLayer.Camera | InputLayer.Spectator | InputLayer.UI
            },
            {
                InputState.PlayerDead,
                InputLayer.UI | InputLayer.Team
            },
            {
                InputState.ScoutMode,
                // Move + Camera, NO combat
                InputLayer.Player | InputLayer.Camera | InputLayer.Team
            },
            {
                InputState.Cinematic,
                InputLayer.None
            },
            {
                InputState.InDialogue,
                InputLayer.UI
            },
            {
                InputState.None,
                InputLayer.None
            },
            // Camera alias
            {
                InputState.Camera,
                InputLayer.Camera
            },
            // InputState.DroneControl is defined in the enum but intentionally omitted here:
            // no DroneInputHandler exists yet. Add an entry when drone mechanics are implemented.
        };

        // ── ActionMap name → Layer mapping ────────────────────────────────────────
        private static readonly Dictionary<string, InputLayer> MapNameToLayer
            = new Dictionary<string, InputLayer>(StringComparer.OrdinalIgnoreCase)
        {
            { "Player",     InputLayer.Player },
            { "Combat",     InputLayer.Combat },
            { "Camera",     InputLayer.Camera },
            { "Inventory",  InputLayer.Inventory },
            { "Team",       InputLayer.Team },
            { "UI",         InputLayer.UI },
            { "Spectator",  InputLayer.Spectator },
            { "Objectives", InputLayer.Objectives },
            { "Devices",    InputLayer.Devices },
            { "Debug",      InputLayer.Debug },
        };

        // ── Runtime state ─────────────────────────────────────────────────────────
        /// <summary>Currently active layers (bitwise OR).</summary>
        public InputLayer ActiveLayers { get; private set; } = InputLayer.None;

        /// <summary>Current input context.</summary>
        public InputState CurrentState { get; private set; } = InputState.None;

        /// <summary>Stack for Push/Pop support (e.g., opening the map while in inventory → pop back to inventory).</summary>
        private readonly Stack<InputState> _contextStack = new Stack<InputState>();

        // Events
        /// <summary>Fired when the context changes. (oldState, newState)</summary>
        public event Action<InputState, InputState> OnContextChanged;
        /// <summary>Fired when the active layers change.</summary>
        public event Action<InputLayer>             OnLayersChanged;

        // Cache: Layer → ActionMap
        private readonly Dictionary<InputLayer, InputActionMap> _layerToMap
            = new Dictionary<InputLayer, InputActionMap>();

        // ── Legacy cached refs (public accessors for older code) ───────────────
        public InputActionMap PlayerMap     => GetMap(InputLayer.Player);
        public InputActionMap CombatMap     => GetMap(InputLayer.Combat);
        public InputActionMap InventoryMap  => GetMap(InputLayer.Inventory);
        public InputActionMap CameraMap     => GetMap(InputLayer.Camera);
        public InputActionMap UIMap         => GetMap(InputLayer.UI);
        public InputActionMap SpectatorMap  => GetMap(InputLayer.Spectator);
        public InputActionMap TeamMap       => GetMap(InputLayer.Team);
        public InputActionMap ObjectivesMap => GetMap(InputLayer.Objectives);
        public InputActionMap DevicesMap    => GetMap(InputLayer.Devices);
        public InputActionMap DebugMap      => GetMap(InputLayer.Debug);

        public InputConfig Config => inputConfig;

        // ── Registered handlers (backward compat) ────────────────────────────────
        private readonly List<IInputHandler> _registeredHandlers = new List<IInputHandler>();

        // ─────────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            BuildLayerCache();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Do not clear GameActionBus here. HUD panels own their subscriptions in
            // OnEnable/OnDisable; clearing the static bus from an input-manager
            // lifecycle can silently disconnect mobile UI buttons while the HUD is
            // still alive.
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Initialization
        // ─────────────────────────────────────────────────────────────────────────

        private void BuildLayerCache()
        {
            if (inputConfig == null)
            {
                Debug.LogError("[InputLayerManager] InputConfig not yet assign!");
                return;
            }

            var asset = inputConfig.InputActionAsset;
            if (asset == null)
            {
                Debug.LogError("[InputLayerManager] InputActionAsset is null in InputConfig!");
                return;
            }

            _layerToMap.Clear();

            foreach (var map in asset.actionMaps)
            {
                if (MapNameToLayer.TryGetValue(map.name, out var layer))
                    _layerToMap[layer] = map;
                else
                    Debug.LogWarning($"[InputLayerManager] ActionMap '{map.name}' has no layer mapping — skipping.");
            }

            // Validate critical maps
            foreach (var pair in MapNameToLayer)
                if (!_layerToMap.ContainsKey(pair.Value))
                    Debug.LogWarning($"[InputLayerManager] ActionMap '{pair.Key}' not found in asset.");

            // Load persisted key-rebind overrides BEFORE any handler calls EnableInput().
            // This ensures player's custom bindings are active from the first frame.
            InputBindingSaveSystem.LoadBindings(asset);

            Debug.Log("[InputLayerManager] Initialized success.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Context API – PushContext / PopContext / TransitionToState
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Transition to a new context, clearing the entire stack history.
        /// </summary>
        public void TransitionToState(InputState newState)
        {
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InputTransition", $"from={CurrentState} to={newState} clearStack=True", this);
            _contextStack.Clear();
            ApplyContext(newState);
        }

        /// <summary>
        /// Push a new context, saving the current one on the stack for <see cref="PopContext"/> to restore.
        /// <para>Example: Gameplay → PushContext(InventoryOpen) → PopContext() → Gameplay.</para>
        /// </summary>
        public void PushContext(InputState newState)
        {
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InputPushContext", $"from={CurrentState} to={newState} stackBefore={_contextStack.Count}", this);
            _contextStack.Push(CurrentState);
            ApplyContext(newState);
        }

        /// <summary>
        /// Pop to the previous context. If the stack is empty, falls back to <see cref="InputState.PlayerAlive"/>.
        /// </summary>
        public void PopContext()
        {
            if (_contextStack.Count == 0)
            {
                Debug.LogWarning("[InputLayerManager] PopContext: stack empty → fallback PlayerAlive");
                PhaseTestLog.Warning(PhaseTestLogCategory.Input, "InputPopContext", $"from={CurrentState} fallback=PlayerAlive reason=empty-stack", this);
                ApplyContext(InputState.PlayerAlive);
                return;
            }

            InputState previous = _contextStack.Pop();
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InputPopContext", $"from={CurrentState} to={previous} stackAfter={_contextStack.Count}", this);
            ApplyContext(previous);
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────────────
        #region Layer API – manual fine-tuning
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enable or disable a layer outside the preset context.
        /// Use when a tweak is needed (e.g., temporarily disable Camera while keeping the Gameplay context).
        /// </summary>
        public void SetLayerEnabled(InputLayer layer, bool enabled)
        {
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InputSetLayer", $"layer={layer} enabled={enabled} before={ActiveLayers}", this);
            ApplyLayers(enabled ? (ActiveLayers | layer) : (ActiveLayers & ~layer));
        }

        /// <summary>Check whether a layer is currently active.</summary>
        public bool IsLayerActive(InputLayer layer) => (ActiveLayers & layer) != 0;

        /// <summary>Disable all input (use for Cinematic / Loading screens).</summary>
        public void DisableAll()
        {
            _contextStack.Clear();
            ApplyContext(InputState.Cinematic);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Handler Registration (backward compat)
        // ─────────────────────────────────────────────────────────────────────────

        public void RegisterHandler(IInputHandler handler)
        {
            if (handler != null && !_registeredHandlers.Contains(handler))
                _registeredHandlers.Add(handler);
        }

        public void UnregisterHandler(IInputHandler handler)
        {
            if (handler != null)
                _registeredHandlers.Remove(handler);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Legacy API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Legacy: returns the current state.</summary>
        public InputState GetCurrentState() => CurrentState;

        /// <summary>Legacy: get ActionMap by name.</summary>
        public InputActionMap GetActionMap(string mapName)
            => inputConfig?.InputActionAsset?.FindActionMap(mapName);

        /// <summary>Legacy: get Action by map name and action name.</summary>
        public InputAction GetAction(string mapName, string actionName)
            => GetActionMap(mapName)?.FindAction(actionName);

        /// <summary>Legacy: check whether a map is enabled.</summary>
        public bool IsActionMapEnabled(string mapName)
        {
            var map = GetActionMap(mapName);
            return map != null && map.enabled;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Internal
        // ─────────────────────────────────────────────────────────────────────────

        private void ApplyContext(InputState state)
        {
            var oldState = CurrentState;
            CurrentState = state;

            if (!ContextPresets.TryGetValue(state, out var layers))
            {
                Debug.LogWarning($"[InputLayerManager] No preset for state '{state}' — disabling all input");
                layers = InputLayer.None;
            }

#if UNITY_EDITOR
            // Always keep the Debug layer enabled in Editor.
            layers |= InputLayer.Debug;
#endif

            ApplyLayers(layers);
            OnContextChanged?.Invoke(oldState, state);
        }

        private void ApplyLayers(InputLayer layers)
        {
            if (ActiveLayers == layers) return;

            ActiveLayers = layers;
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "InputLayersApplied", $"state={CurrentState} layers={ActiveLayers}", this);

            // 1️⃣ Enable/disable ActionMaps (Single Source of Truth)
            foreach (var kvp in _layerToMap)
            {
                bool shouldEnable = (layers & kvp.Key) != 0;
                if (shouldEnable) kvp.Value.Enable();
                else              kvp.Value.Disable();
            }

            // 2️⃣ Sync registered handlers — call EnableInput/DisableInput to match ActionMap state.
            foreach (var handler in _registeredHandlers)
            {
                if (handler == null) continue;
                var map = handler.GetActionMap();
                if (map == null) continue;

                if (map.enabled && !handler.IsInputEnabled)
                    handler.EnableInput();
                else if (!map.enabled && handler.IsInputEnabled)
                    handler.DisableInput();
            }

            OnLayersChanged?.Invoke(ActiveLayers);

#if UNITY_EDITOR
            LogActiveState();
#endif
        }

        private InputActionMap GetMap(InputLayer layer)
            => _layerToMap.TryGetValue(layer, out var m) ? m : null;

#if UNITY_EDITOR
        private void LogActiveState()
        {
            var sb = new System.Text.StringBuilder("[InputLayerManager] Active: ");
            foreach (InputLayer layer in System.Enum.GetValues(typeof(InputLayer)))
            {
                if (layer == InputLayer.None) continue;
                sb.Append((ActiveLayers & layer) != 0 ? $"✅{layer} " : $"❌{layer} ");
            }
            Debug.Log(sb.ToString());
        }
#endif

        #endregion
    }
}
