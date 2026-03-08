using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;

namespace NightHunt.Gameplay.Input.Core
{
    // ──────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// SINGLE SOURCE OF TRUTH cho toàn bộ Input.
    ///
    /// Nguyên tắc:
    ///   • Chỉ class này được Enable/Disable ActionMap.
    ///   • Các handler KHÔNG tự gọi map.Enable() / map.Disable().
    ///   • Dùng <see cref="PushContext"/> / <see cref="PopContext"/> để chuyển state,
    ///     thay vì gọi <see cref="TransitionToState"/> trực tiếp từ nhiều nơi.
    ///
    /// Bảng preset Context → Layer:
    /// <code>
    ///  Context        | Player | Combat | Camera | Inventory | Team | UI | Spectator | Objectives | Devices
    ///  ───────────────┼────────┼────────┼────────┼───────────┼──────┼────┼───────────┼────────────┼────────
    ///  PlayerAlive    |   ✅   |   ✅   |   ✅   |     ✅    |  ✅  | ❌ |     ❌    |     ✅     |   ✅
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
    // ──────────────────────────────────────────────────────────────────────────────
    // Execution order: must Awake() BEFORE any IInputHandler so Instance is ready
    // when handlers call InitializeActions() in their own Awake().
    [UnityEngine.DefaultExecutionOrder(-100)]
    public class InputLayerManager : MonoBehaviour
    {
        public static InputLayerManager Instance { get; private set; }

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
                InputLayer.Inventory | InputLayer.Team | InputLayer.Objectives | InputLayer.Devices
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
            // {
            //     InputState.DroneControl,
            //     InputLayer.Devices | InputLayer.Camera | InputLayer.UI
            // },
            {
                InputState.Spectating,
                InputLayer.Spectator | InputLayer.UI | InputLayer.Team
            },
            {
                InputState.PlayerDead,
                InputLayer.UI | InputLayer.Team
            },
            {
                InputState.ScoutMode,
                // Di chuyển + Camera, KHÔNG combat
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
        /// <summary>Layer đang active hiện tại (bitwise OR).</summary>
        public InputLayer ActiveLayers { get; private set; } = InputLayer.None;

        /// <summary>Context hiện tại.</summary>
        public InputState CurrentState { get; private set; } = InputState.None;

        /// <summary>Stack để hỗ trợ Push/Pop (ví dụ: mở map trong inventory → pop về inventory).</summary>
        private readonly Stack<InputState> _contextStack = new Stack<InputState>();

        // Events
        /// <summary>Fired khi context thay đổi. (oldState, newState)</summary>
        public event Action<InputState, InputState> OnContextChanged;
        /// <summary>Fired khi các layer active thay đổi.</summary>
        public event Action<InputLayer>             OnLayersChanged;

        // Cache: Layer → ActionMap
        private readonly Dictionary<InputLayer, InputActionMap> _layerToMap
            = new Dictionary<InputLayer, InputActionMap>();

        // ── Legacy cached refs (public accessors cho code cũ) ────────────────────
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildLayerCache();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Initialization
        // ─────────────────────────────────────────────────────────────────────────

        private void BuildLayerCache()
        {
            if (inputConfig == null)
            {
                Debug.LogError("[InputLayerManager] InputConfig chưa được assign!");
                return;
            }

            var asset = inputConfig.InputActionAsset;
            if (asset == null)
            {
                Debug.LogError("[InputLayerManager] InputActionAsset là null trong InputConfig!");
                return;
            }

            _layerToMap.Clear();

            foreach (var map in asset.actionMaps)
            {
                if (MapNameToLayer.TryGetValue(map.name, out var layer))
                    _layerToMap[layer] = map;
                else
                    Debug.LogWarning($"[InputLayerManager] ActionMap '{map.name}' không có mapping → bỏ qua.");
            }

            // Validate critical maps
            foreach (var pair in MapNameToLayer)
                if (!_layerToMap.ContainsKey(pair.Value))
                    Debug.LogWarning($"[InputLayerManager] Không tìm thấy ActionMap '{pair.Key}' trong asset.");

            Debug.Log("[InputLayerManager] Initialized thành công.");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Context API – PushContext / PopContext / TransitionToState
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Chuyển context, xóa toàn bộ stack history.
        /// </summary>
        public void TransitionToState(InputState newState)
        {
            _contextStack.Clear();
            ApplyContext(newState);
        }

        /// <summary>
        /// Push context mới, lưu context cũ vào stack để <see cref="PopContext"/> khôi phục.
        /// <para>Ví dụ: Gameplay → PushContext(InventoryOpen) → PopContext() → Gameplay.</para>
        /// </summary>
        public void PushContext(InputState newState)
        {
            _contextStack.Push(CurrentState);
            ApplyContext(newState);
        }

        /// <summary>
        /// Pop về context trước đó. Nếu stack rỗng → fallback <see cref="InputState.PlayerAlive"/>.
        /// </summary>
        public void PopContext()
        {
            if (_contextStack.Count == 0)
            {
                Debug.LogWarning("[InputLayerManager] PopContext: stack rỗng → fallback PlayerAlive");
                ApplyContext(InputState.PlayerAlive);
                return;
            }
            ApplyContext(_contextStack.Pop());
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Layer API – thủ công fine-tune
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bật/tắt thủ công một layer ngoài preset context.
        /// Dùng khi cần tweak (VD: tạm tắt Camera nhưng giữ nguyên context Gameplay).
        /// </summary>
        public void SetLayerEnabled(InputLayer layer, bool enabled)
        {
            ApplyLayers(enabled ? (ActiveLayers | layer) : (ActiveLayers & ~layer));
        }

        /// <summary>Kiểm tra layer có đang active không.</summary>
        public bool IsLayerActive(InputLayer layer) => (ActiveLayers & layer) != 0;

        /// <summary>Disable tất cả input (dùng cho Cinematic / Loading screen).</summary>
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

        /// <summary>Legacy: trả về state hiện tại.</summary>
        public InputState GetCurrentState() => CurrentState;

        /// <summary>Legacy: lấy ActionMap theo tên.</summary>
        public InputActionMap GetActionMap(string mapName)
            => inputConfig?.InputActionAsset?.FindActionMap(mapName);

        /// <summary>Legacy: lấy Action theo tên map và tên action.</summary>
        public InputAction GetAction(string mapName, string actionName)
            => GetActionMap(mapName)?.FindAction(actionName);

        /// <summary>Legacy: kiểm tra map có enabled không.</summary>
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
                Debug.LogWarning($"[InputLayerManager] Không có preset cho state '{state}' → disable all");
                layers = InputLayer.None;
            }

#if UNITY_EDITOR
            // Luôn giữ Debug layer bật trong Editor
            layers |= InputLayer.Debug;
#endif

            ApplyLayers(layers);
            OnContextChanged?.Invoke(oldState, state);
        }

        private void ApplyLayers(InputLayer layers)
        {
            if (ActiveLayers == layers) return;

            ActiveLayers = layers;

            // 1️⃣ Enable/disable ActionMaps (Single Source of Truth)
            foreach (var kvp in _layerToMap)
            {
                bool shouldEnable = (layers & kvp.Key) != 0;
                if (shouldEnable) kvp.Value.Enable();
                else              kvp.Value.Disable();
            }

            // 2️⃣ Sync registered handlers – gọi EnableInput/DisableInput khớp với ActionMap
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