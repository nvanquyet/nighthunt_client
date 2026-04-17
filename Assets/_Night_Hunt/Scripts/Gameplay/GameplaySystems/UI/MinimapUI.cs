using UnityEngine;
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
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private RawImage      _minimapRawImage;
        [SerializeField] private RenderTexture _renderTexture;

        private void Start()
        {
            if (_minimapRawImage != null && _renderTexture != null)
                _minimapRawImage.texture = _renderTexture;
        }

        /// <summary>
        /// No-op stub kept so GameHUD.Initialize() compiles without modification.
        /// Target tracking is now handled by MinimapCameraController via
        /// SpectateManager.OnCurrentPlayerChanged subscription.
        /// </summary>
        public void SetLocalPlayer(NetworkPlayer player) { }
    }
}

