using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightHunt.Networking;
using NightHunt.Networking.Player;

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

        private NetworkPlayer _localPlayer;
        private bool _fullMapVisible;

        public NetworkPlayer LocalPlayer => _localPlayer;

        private void Awake()
        {
            ApplyTextures();
            SetFullMapVisible(false);
        }

        private void Start()
        {
            ApplyTextures();
        }

        private void Update()
        {
            if (_fullMapVisible && Input.GetKeyDown(KeyCode.Escape))
                SetFullMapVisible(false);
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
            _fullMapVisible = visible;

            if (visible)
            {
                EnsureFullMapOverlay();
                ApplyTextures();
            }

            if (_fullMapRoot != null)
                _fullMapRoot.SetActive(visible);
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

