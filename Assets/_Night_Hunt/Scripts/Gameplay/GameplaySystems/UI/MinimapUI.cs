using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Diagnostics;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.GameplaySystems.UI
{
    /// <summary>
    /// Minimap display — assigns the RenderTexture produced by MinimapCameraController
    /// to the HUD RawImage. All world-space marker and camera logic has been moved to:
    ///   • MinimapMarkerController  — per-player dot on "Minimap" layer
    ///   • MinimapCameraController  — top-down follow camera on local player prefab
    /// </summary>
    public class MinimapUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RawImage      _minimapRawImage;
        [SerializeField] private RenderTexture _renderTexture;

        [Header("Full Map")]
        [Tooltip("Root panel for the large tactical map. Hidden by default.")]
        [SerializeField] private GameObject _fullMapRoot;

        [Tooltip("RawImage used by the large tactical map. If unset, only the root is toggled.")]
        [SerializeField] private RawImage _fullMapRawImage;

        [Tooltip("Optional RenderTexture from a full-area map camera. Falls back to the minimap texture.")]
        [SerializeField] private RenderTexture _fullMapRenderTexture;

        [Tooltip("Optional close button on the full-map overlay panel.")]
        [SerializeField] private Button _closeMapButton;

        private NetworkPlayer _localPlayer;
        private bool _fullMapVisible;
        private bool _pushedInputContext;
        private NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler _uiInput;

        public NetworkPlayer LocalPlayer => _localPlayer;

        private void Awake()
        {
            ApplyTextures();
            SetFullMapVisible(false);
        }

        private void Start()
        {
            ApplyTextures();
            // Subscribe to UI input
            _uiInput = FindFirstObjectByType<NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler>(FindObjectsInactive.Include);
            if (_uiInput != null)
            {
                _uiInput.OnToggleMapPressed += ToggleFullMap;
                _uiInput.OnCancelPressed    += HandleCancelPressed;
            }
            // Wire close button if assigned in Inspector, or auto-discover one.
            if (_closeMapButton != null)
                _closeMapButton.onClick.AddListener(() => SetFullMapVisible(false));
            else
                EnsureCloseButton();
        }

        /// <summary>
        /// Close the full-map overlay. Safe to call from a Button.onClick UnityEvent
        /// in the Inspector without needing a direct reference to this component.
        /// </summary>
        public void CloseFullMap() => SetFullMapVisible(false);

        /// <summary>
        /// Attempt to wire a close handler once <see cref="_fullMapRoot"/> is available.
        /// Priority order:
        ///   1. <see cref="_closeMapButton"/> already set — skip.
        ///   2. Find a Button whose name contains "close", "exit", or "back" inside <see cref="_fullMapRoot"/>.
        ///   3. Fall back: add a Button (Transition.None) to <see cref="_fullMapRoot"/> itself so
        ///      tapping anywhere on the background closes the map.
        /// </summary>
        private void EnsureCloseButton()
        {
            if (_closeMapButton != null) return;
            if (_fullMapRoot == null) return;

            // 1. Look for a named close button inside the overlay.
            foreach (var btn in _fullMapRoot.GetComponentsInChildren<Button>(includeInactive: true))
            {
                var n = btn.name.ToLowerInvariant();
                if (n.Contains("close") || n.Contains("exit") || n.Contains("back"))
                {
                    _closeMapButton = btn;
                    _closeMapButton.onClick.AddListener(() => SetFullMapVisible(false));
                    Debug.Log($"[Minimap] Auto-wired close button: '{btn.name}'");
                    return;
                }
            }

            // 2. No named button found — add a transparent background button so a tap
            //    anywhere on the overlay (not covered by a child element) closes the map.
            if (!_fullMapRoot.TryGetComponent<Button>(out var bg))
                bg = _fullMapRoot.AddComponent<Button>();

            bg.transition = Selectable.Transition.None;
            bg.onClick.AddListener(() => SetFullMapVisible(false));
            _closeMapButton = bg;
            Debug.Log("[Minimap] Auto-added background close button to full-map root.");
        }

        private void OnDestroy()
        {
            if (_fullMapVisible || _pushedInputContext)
                SetFullMapVisible(false);

            if (_uiInput != null)
            {
                _uiInput.OnToggleMapPressed -= ToggleFullMap;
                _uiInput.OnCancelPressed -= HandleCancelPressed;
                _uiInput = null;
            }
        }

        private void OnDisable()
        {
            if (_fullMapVisible || _pushedInputContext)
                SetFullMapVisible(false);
        }

        private void Update()
        {
            if (_fullMapVisible && _fullMapRoot != null && !_fullMapRoot.activeInHierarchy)
            {
                Debug.Log($"[NH_FLOW][50][Minimap.WatchdogExternalHide] visible={_fullMapVisible} pushed={_pushedInputContext} state={InputLayerManager.Instance?.CurrentState.ToString() ?? "null"} layers={(InputLayerManager.Instance != null ? InputLayerManager.Instance.ActiveLayers.ToString() : "null")}");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Input,
                    "MinimapExternalHideRecovered",
                    "fullMapRoot was hidden outside MinimapUI; releasing map input context.",
                    this);
                SetFullMapVisible(false);
                return;
            }

            var inputLayers = InputLayerManager.Instance;
            if (!_fullMapVisible && _pushedInputContext && inputLayers != null)
            {
                Debug.Log($"[NH_FLOW][50][Minimap.WatchdogMapOpenLeak] visible={_fullMapVisible} pushed={_pushedInputContext} state={inputLayers.CurrentState} layers={inputLayers.ActiveLayers}");
                ReleaseMapInputContext(inputLayers, inputLayers.CurrentState, inputLayers.ActiveLayers, "watchdog-hidden-map");
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleFullMap();
        }

        /// <summary>
        /// Store the local player for future marker/vision filtering. Target tracking is
        /// still handled by MinimapCameraController via SpectateManager.
        /// </summary>
        public void SetLocalPlayer(NetworkPlayer player)
        {
            _localPlayer = player;
        }

        public void ToggleFullMap()
        {
            SetFullMapVisible(!_fullMapVisible);
        }

        public void SetFullMapVisible(bool visible)
        {
            bool wasVisible = _fullMapVisible;
            Debug.Log($"[NH_FLOW][50][Minimap.SetFullMapVisible] requested={visible} wasVisible={wasVisible} pushed={_pushedInputContext} state={InputLayerManager.Instance?.CurrentState.ToString() ?? "null"} layers={(InputLayerManager.Instance != null ? InputLayerManager.Instance.ActiveLayers.ToString() : "null")}");
            if (wasVisible == visible && !_pushedInputContext)
            {
                if (_fullMapRoot != null)
                    _fullMapRoot.SetActive(visible);
                return;
            }

            _fullMapVisible = visible;
            var inputLayers = InputLayerManager.Instance;
            InputState beforeState = inputLayers != null ? inputLayers.CurrentState : InputState.None;
            NightHunt.Gameplay.Input.InputLayer beforeLayers = inputLayers != null ? inputLayers.ActiveLayers : NightHunt.Gameplay.Input.InputLayer.None;

            if (visible)
            {
                EnsureFullMapOverlay();
                ApplyTextures();
                EnsureCloseButton();
                if (inputLayers != null)
                {
                    if (!_pushedInputContext && inputLayers.CurrentState != InputState.MapOpen)
                    {
                        Debug.Log($"[NH_FLOW][51][Minimap.PushMapOpen] beforeState={inputLayers.CurrentState} beforeLayers={inputLayers.ActiveLayers}");
                        inputLayers.PushContext(InputState.MapOpen);
                        _pushedInputContext = true;
                    }
                    else if (inputLayers.CurrentState == InputState.MapOpen && !_pushedInputContext)
                    {
                        PhaseTestLog.Warning(
                            PhaseTestLogCategory.Input,
                            "MinimapOpenWithoutOwnedContext",
                            $"state={inputLayers.CurrentState} layers={inputLayers.ActiveLayers}",
                            this);
                    }
                }
            }
            else
            {
                ReleaseMapInputContext(inputLayers, beforeState, beforeLayers, "close");
            }

            if (_fullMapRoot != null)
                _fullMapRoot.SetActive(visible);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Input,
                visible ? "MinimapOpen" : "MinimapClose",
                $"prevVisible={wasVisible} beforeState={beforeState} beforeLayers={beforeLayers} afterState={inputLayers?.CurrentState.ToString() ?? "null"} afterLayers={(inputLayers != null ? inputLayers.ActiveLayers.ToString() : "null")} pushed={_pushedInputContext}",
                this);
            Debug.Log($"[NH_FLOW][52][Minimap.VisibleSet] visible={visible} prevVisible={wasVisible} beforeState={beforeState} beforeLayers={beforeLayers} afterState={inputLayers?.CurrentState.ToString() ?? "null"} afterLayers={(inputLayers != null ? inputLayers.ActiveLayers.ToString() : "null")} pushed={_pushedInputContext}");
        }

        private void HandleCancelPressed()
        {
            if (_fullMapVisible)
                SetFullMapVisible(false);
        }

        private void ReleaseMapInputContext(
            InputLayerManager inputLayers,
            InputState beforeState,
            NightHunt.Gameplay.Input.InputLayer beforeLayers,
            string reason)
        {
            if (inputLayers == null)
            {
                _pushedInputContext = false;
                return;
            }

            bool popped = false;
            if (_pushedInputContext && inputLayers.CurrentState == InputState.MapOpen)
            {
                Debug.Log($"[NH_FLOW][53][Minimap.PopMapOpen] reason={reason} state={inputLayers.CurrentState} layers={inputLayers.ActiveLayers}");
                inputLayers.PopContext();
                popped = true;
            }
            else if (_pushedInputContext)
            {
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Input,
                    "MinimapOwnedContextOutOfSync",
                    $"reason={reason} state={inputLayers.CurrentState} layers={inputLayers.ActiveLayers} beforeState={beforeState} beforeLayers={beforeLayers}",
                    this);
            }

            _pushedInputContext = false;

            // Clear EventSystem selection so WASD keyboard input is not consumed by
            // UI Navigation on the previously-focused minimap toggle / close button.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            Debug.Log($"[NH_FLOW][55][Minimap.ReleaseDone] reason={reason} beforeState={beforeState} beforeLayers={beforeLayers} afterState={inputLayers.CurrentState} afterLayers={inputLayers.ActiveLayers} popped={popped}");
        }

        private void ApplyTextures()
        {
            if (_minimapRawImage != null && _renderTexture != null)
                _minimapRawImage.texture = _renderTexture;

            if (_fullMapRawImage != null)
                _fullMapRawImage.texture = ResolveFullMapTexture();
        }

        private Texture ResolveFullMapTexture()
        {
            if (_fullMapRenderTexture != null)
                return _fullMapRenderTexture;

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.name != "FullMapCamera" || camera.targetTexture == null)
                    continue;

                _fullMapRenderTexture = camera.targetTexture;
                return _fullMapRenderTexture;
            }

            return _renderTexture;
        }

        private void EnsureFullMapOverlay()
        {
            if (_fullMapRoot != null && _fullMapRawImage != null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            if (_fullMapRoot == null)
            {
                _fullMapRoot = new GameObject("FullMapOverlay", typeof(RectTransform), typeof(Image));
                _fullMapRoot.transform.SetParent(parent, false);
                _fullMapRoot.transform.SetAsLastSibling();

                var rect = (RectTransform)_fullMapRoot.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var background = _fullMapRoot.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.82f);
            }

            if (_fullMapRawImage == null)
            {
                var rawImageObject = new GameObject("FullMapRawImage", typeof(RectTransform), typeof(RawImage));
                rawImageObject.transform.SetParent(_fullMapRoot.transform, false);

                var rect = (RectTransform)rawImageObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(24f, 24f);
                rect.offsetMax = new Vector2(-24f, -24f);

                _fullMapRawImage = rawImageObject.GetComponent<RawImage>();
                _fullMapRawImage.color = Color.white;
                _fullMapRawImage.raycastTarget = false;
            }
        }
    }
}

