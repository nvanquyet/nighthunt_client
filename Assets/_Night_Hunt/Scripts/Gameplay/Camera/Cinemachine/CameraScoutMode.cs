using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Camera;

namespace NightHunt.Gameplay.Camera.Cinemachine
{
    /// <summary>
    /// Scout mode với minimap (không bắn khi scout)
    /// </summary>
    public class CameraScoutMode : MonoBehaviour
    {
        // [Header("Scout Mode Settings")]
        // [SerializeField] private Key toggleScoutKey = Key.M;
        // [SerializeField] private bool isScoutModeActive = false;
        // [SerializeField] private CameraInputHandler inputHandler;
        //
        // private CinemachineCameraController cameraController;
        //
        // private void Awake()
        // {
        //     cameraController = GetComponent<CinemachineCameraController>();
        // }
        //
        // private void Update()
        // {
        //     HandleScoutModeToggle();
        // }
        //
        // /// <summary>
        // /// Handle scout mode toggle input
        // /// </summary>
        // private void HandleScoutModeToggle()
        // {
        //     if (Keyboard.current == null) return;
        //
        //     if (Keyboard.current[toggleScoutKey].wasPressedThisFrame)
        //     {
        //         ToggleScoutMode();
        //     }
        // }
        //
        // /// <summary>
        // /// Toggle scout mode
        // /// </summary>
        // public void ToggleScoutMode()
        // {
        //     isScoutModeActive = !isScoutModeActive;
        //     SetScoutMode(isScoutModeActive);
        // }
        //
        // /// <summary>
        // /// Set scout mode active/inactive
        // /// </summary>
        // public void SetScoutMode(bool active)
        // {
        //     isScoutModeActive = active;
        //
        //     // Update input state
        //     if (inputHandler != null)
        //     {
        //         if (active)
        //         {
        //             inputHandler.OnZoom.TransitionToState(InputState.ScoutMode);
        //         }
        //         else
        //         {
        //             inputLayerManager.TransitionToState(InputState.PlayerAlive);
        //         }
        //     }
        //
        //     // Disable attack input when in scout mode
        //     if (inputHandler != null)
        //     {
        //         inputHandler.SetScoutMode(active);
        //     }
        //
        //     Debug.Log($"[CameraScoutMode] Scout mode: {(active ? "ON" : "OFF")}");
        // }
        //
        // /// <summary>
        // /// Check if scout mode is active
        // /// </summary>
        // public bool IsScoutModeActive => isScoutModeActive;
    }
}

