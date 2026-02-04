using System;
using UnityEngine;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Camera;
using Unity.Cinemachine;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Handles camera zoom input (mouse wheel) for Cinemachine virtual cameras
    /// </summary>
    public class CameraZoomInput : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private CinemachineCamera playerCamera;

        private CameraInputHandler _inputHandler;
        
        private void OnEnable()
        {
            if (_inputHandler == null) _inputHandler = InputManager.Instance.CameraHandler;
            if (_inputHandler != null) _inputHandler.OnZoom += OnZoomPerformed;
        }

        private void OnDisable()
        {
            if (_inputHandler != null) _inputHandler.OnZoom -= OnZoomPerformed;
        }

        private void OnZoomPerformed(float zoomDelta)
        {
            if (playerCamera == null) return;
            var lens = playerCamera.Lens;
            lens.FieldOfView -= zoomDelta * zoomSpeed;
            lens.FieldOfView = Mathf.Clamp(lens.FieldOfView, 20f, 80f);
            playerCamera.Lens = lens;
        }
    }
}
