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
    /// Minimap display and full-map overlay controller.
    /// MinimapCameraController owns the follow camera; this component owns UI texture
    /// assignment, full-map visibility, and map input context lifetime.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private RawImage _minimapRawImage;
        [SerializeField] private RenderTexture _renderTexture;

        [Tooltip("Button used to open/toggle the large tactical map from the minimap HUD.")]
        [SerializeField] private Button _openMapButton;

        [Header("Full Map")]
        [Tooltip("Root panel for the large tactical map. Hidden by default.")]
        [SerializeField] private GameObject _fullMapRoot;

        [Tooltip("RawImage used by the large tactical map. If unset, only the root is toggled.")]
        [SerializeField] private RawImage _fullMapRawImage;

        [Tooltip("Optional RenderTexture from a full-area map camera. Falls back to the minimap texture.")]
        [SerializeField] private RenderTexture _fullMapRenderTexture;

        [Tooltip("Optional close button on the full-map overlay panel.")]
        [SerializeField] private Button _closeMapButton;

        [Tooltip("Optional backdrop button on the full-map overlay. Clicking outside the map closes it.")]
        [SerializeField] private Button _backdropCloseButton;

        [Tooltip("Optional full-map camera. If unset, MinimapUI finds a camera named FullMapCamera.")]
        [SerializeField] private Camera _fullMapCamera;

        private NetworkPlayer _localPlayer;
        private bool _fullMapVisible;
        private bool _pushedInputContext;
        private NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler _uiInput;
        private Camera _resolvedFullMapCamera;

        public NetworkPlayer LocalPlayer => _localPlayer;

        private void Awake()
        {
            EnsureButtonReferences();
            ApplyTextures();
            SetFullMapCameraEnabled(false);
            SetFullMapVisible(false);
        }

        private void Start()
        {
            EnsureButtonReferences();
            ApplyTextures();
            BindButtons();

            _uiInput = FindFirstObjectByType<NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler>(FindObjectsInactive.Include);
            if (_uiInput != null)
            {
                _uiInput.OnToggleMapPressed += ToggleFullMap;
                _uiInput.OnCancelPressed += HandleCancelPressed;
            }
        }

        /// <summary>
        /// Close the full-map overlay. Safe to call from a Button.onClick UnityEvent
        /// in the Inspector without needing a direct reference to this component.
        /// </summary>
        public void CloseFullMap() => SetFullMapVisible(false);

        private void EnsureButtonReferences()
        {
            if (_openMapButton == null)
                _openMapButton = GetComponent<Button>();

            if (_fullMapRoot == null)
                return;

            foreach (var btn in _fullMapRoot.GetComponentsInChildren<Button>(includeInactive: true))
            {
                string n = btn.name.ToLowerInvariant();

                if (_closeMapButton == null && (n.Contains("close") || n.Contains("exit")))
                {
                    _closeMapButton = btn;
                    Debug.Log($"[Minimap] Auto-wired close button: '{btn.name}'");
                }

                if (_backdropCloseButton == null && (n.Contains("background") || n.Contains("backdrop")))
                {
                    _backdropCloseButton = btn;
                    Debug.Log($"[Minimap] Auto-wired backdrop close button: '{btn.name}'");
                }
            }

            if (_closeMapButton != null || _backdropCloseButton != null)
                return;

            if (!_fullMapRoot.TryGetComponent<Button>(out var backdrop))
                backdrop = _fullMapRoot.AddComponent<Button>();

            backdrop.transition = Selectable.Transition.None;
            _backdropCloseButton = backdrop;
            Debug.Log("[Minimap] Auto-added fallback backdrop close button to full-map root.");
        }

        private void BindButtons()
        {
            if (_openMapButton != null)
            {
                _openMapButton.onClick.RemoveListener(ToggleFullMap);
                _openMapButton.onClick.AddListener(ToggleFullMap);
            }

            if (_closeMapButton != null)
            {
                _closeMapButton.onClick.RemoveListener(CloseFullMap);
                _closeMapButton.onClick.AddListener(CloseFullMap);
            }

            if (_backdropCloseButton != null && _backdropCloseButton != _closeMapButton)
            {
                _backdropCloseButton.onClick.RemoveListener(CloseFullMap);
                _backdropCloseButton.onClick.AddListener(CloseFullMap);
            }
        }

        private void UnbindButtons()
        {
            if (_openMapButton != null)
                _openMapButton.onClick.RemoveListener(ToggleFullMap);

            if (_closeMapButton != null)
                _closeMapButton.onClick.RemoveListener(CloseFullMap);

            if (_backdropCloseButton != null && _backdropCloseButton != _closeMapButton)
                _backdropCloseButton.onClick.RemoveListener(CloseFullMap);
        }

        private void OnDestroy()
        {
            if (_fullMapVisible || _pushedInputContext)
                SetFullMapVisible(false);

            UnbindButtons();

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
                SetFullMapCameraEnabled(visible);
                return;
            }

            _fullMapVisible = visible;
            var inputLayers = InputLayerManager.Instance;
            InputState beforeState = inputLayers != null ? inputLayers.CurrentState : InputState.None;
            NightHunt.Gameplay.Input.InputLayer beforeLayers = inputLayers != null ? inputLayers.ActiveLayers : NightHunt.Gameplay.Input.InputLayer.None;

            if (visible)
            {
                EnsureFullMapOverlay();
                EnsureButtonReferences();
                BindButtons();
                ApplyTextures();
                SetFullMapCameraEnabled(true);

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
                SetFullMapCameraEnabled(false);
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

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            Debug.Log($"[NH_FLOW][55][Minimap.ReleaseDone] reason={reason} beforeState={beforeState} beforeLayers={beforeLayers} afterState={inputLayers.CurrentState} afterLayers={inputLayers.ActiveLayers} popped={popped}");
        }

        private void ApplyTextures()
        {
            if (_minimapRawImage != null && _renderTexture != null)
            {
                _minimapRawImage.texture = _renderTexture;
                _minimapRawImage.color = Color.white;
            }

            if (_fullMapRawImage != null)
            {
                _fullMapRawImage.texture = ResolveFullMapTexture();
                _fullMapRawImage.color = Color.white;
                _fullMapRawImage.raycastTarget = true;
            }
        }

        private Texture ResolveFullMapTexture()
        {
            if (_fullMapRenderTexture != null)
                return _fullMapRenderTexture;

            var fullMapCamera = ResolveFullMapCamera();
            if (fullMapCamera != null && fullMapCamera.targetTexture != null)
            {
                _fullMapRenderTexture = fullMapCamera.targetTexture;
                return _fullMapRenderTexture;
            }

            return _renderTexture;
        }

        private Camera ResolveFullMapCamera()
        {
            if (_resolvedFullMapCamera != null)
                return _resolvedFullMapCamera;

            if (_fullMapCamera != null)
            {
                _resolvedFullMapCamera = _fullMapCamera;
                return _resolvedFullMapCamera;
            }

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.name != "FullMapCamera")
                    continue;

                _resolvedFullMapCamera = camera;
                return _resolvedFullMapCamera;
            }

            return null;
        }

        private void SetFullMapCameraEnabled(bool enabled)
        {
            var fullMapCamera = ResolveFullMapCamera();
            if (fullMapCamera != null)
                fullMapCamera.enabled = enabled;
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
                _fullMapRawImage.raycastTarget = true;
            }
        }
    }
}
