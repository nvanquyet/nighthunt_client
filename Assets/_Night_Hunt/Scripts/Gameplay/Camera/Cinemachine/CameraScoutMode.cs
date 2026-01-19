using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;

namespace NightHunt.Gameplay.Camera.Cinemachine
{
    /// <summary>
    /// Scout mode với minimap (không bắn khi scout)
    /// </summary>
    public class CameraScoutMode : MonoBehaviour
    {
        [Header("Scout Mode Settings")]
        [SerializeField] private Key toggleScoutKey = Key.M;
        [SerializeField] private bool isScoutModeActive = false;

        private CinemachineCameraController cameraController;
        private PlayerInputHandler inputHandler;
        private InputLayerManager inputLayerManager;

        private void Awake()
        {
            cameraController = GetComponent<CinemachineCameraController>();
            inputHandler = GetComponentInParent<PlayerInputHandler>();
            inputLayerManager = InputLayerManager.Instance;
        }

        private void Update()
        {
            HandleScoutModeToggle();
        }

        /// <summary>
        /// Handle scout mode toggle input
        /// </summary>
        private void HandleScoutModeToggle()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[toggleScoutKey].wasPressedThisFrame)
            {
                ToggleScoutMode();
            }
        }

        /// <summary>
        /// Toggle scout mode
        /// </summary>
        public void ToggleScoutMode()
        {
            isScoutModeActive = !isScoutModeActive;
            SetScoutMode(isScoutModeActive);
        }

        /// <summary>
        /// Set scout mode active/inactive
        /// </summary>
        public void SetScoutMode(bool active)
        {
            isScoutModeActive = active;

            // Update input state
            if (inputLayerManager != null)
            {
                if (active)
                {
                    inputLayerManager.TransitionToState(InputState.ScoutMode);
                }
                else
                {
                    inputLayerManager.TransitionToState(InputState.PlayerAlive);
                }
            }

            // Disable attack input when in scout mode
            if (inputHandler != null)
            {
                inputHandler.SetScoutMode(active);
            }

            Debug.Log($"[CameraScoutMode] Scout mode: {(active ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Check if scout mode is active
        /// </summary>
        public bool IsScoutModeActive => isScoutModeActive;
    }
}

