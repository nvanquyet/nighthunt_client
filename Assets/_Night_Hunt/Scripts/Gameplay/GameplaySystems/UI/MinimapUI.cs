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
            if (_fullMapRoot != null)
                _fullMapRoot.SetActive(visible);
        }

        private void ApplyTextures()
        {
            if (_minimapRawImage != null && _renderTexture != null)
                _minimapRawImage.texture = _renderTexture;

            if (_fullMapRawImage != null)
                _fullMapRawImage.texture = _fullMapRenderTexture != null ? _fullMapRenderTexture : _renderTexture;
        }
    }
}

