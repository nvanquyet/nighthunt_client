using NightHunt.Config;
using NightHunt.Gameplay.Input.Handlers.Camera;
using Unity.Cinemachine;
using UnityEngine;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Applies MouseSensitivity, InvertY, and FOV from GameSettings to the actual Camera/Input handlers.
    /// Attach to the player prefab or virtual camera.
    /// </summary>
    public class CameraSettingsApplier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera _vcam;
        [SerializeField] private CameraInputHandler _manualHandler;

        private void Awake()
        {
            if (_vcam == null) _vcam = GetComponent<CinemachineCamera>();
            if (_manualHandler == null) _manualHandler = GetComponentInParent<CameraInputHandler>();
        }

        private void OnEnable()
        {
            Apply();
            GameSettings.OnSettingsChanged += Apply;
        }

        private void OnDisable()
        {
            GameSettings.OnSettingsChanged -= Apply;
        }

        public void Apply()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            // 1. Apply FOV
            if (_vcam != null)
            {
                var lens = _vcam.Lens;
                lens.FieldOfView = settings.FOV;
                _vcam.Lens = lens;
            }

            // 2. Apply to manual handler (Rotation speed and Invert Y if applicable)
            if (_manualHandler != null)
            {
                _manualHandler.ApplySettings(settings.MouseSensitivity, settings.InvertY);
            }
        }

        private void OnValidate()
        {
            if (_vcam == null) _vcam = GetComponent<CinemachineCamera>();
        }
    }
}