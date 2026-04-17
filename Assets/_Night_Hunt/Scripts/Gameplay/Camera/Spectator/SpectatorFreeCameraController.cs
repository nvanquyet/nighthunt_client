using UnityEngine;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Handlers.Spectator;
using NightHunt.Gameplay.Spectator;

namespace NightHunt.Gameplay.Camera.Spectator
{
    /// <summary>
    /// Free-fly spectator camera controller.
    ///
    /// USAGE:
    ///   1. Attach to the spectator camera GameObject.
    ///   2. Assign a SpectatorInputHandler reference.
    ///   3. Toggle via SpectatorInputHandler.OnToggleFreeCam (Tab key).
    ///   4. Deactivates automatically when SpectateManager.OnSpectateStopped fires.
    ///
    /// CONTROLS (free-fly mode active):
    ///   WASD / Arrow keys  — translate camera position
    ///   Mouse drag         — rotate camera (yaw + pitch)
    ///   Scroll wheel       — adjust movement speed
    ///   Tab                — toggle back to follow-player mode
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class SpectatorFreeCameraController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The SpectatorInputHandler that raises OnToggleFreeCam.")]
        [SerializeField] private SpectatorInputHandler _inputHandler;

        [Header("Movement")]
        [Tooltip("Base movement speed (WASD / arrows) in units per second.")]
        [SerializeField] private float _moveSpeed = 10f;

        [Tooltip("Minimum and maximum speed that the scroll wheel can set.")]
        [SerializeField] private Vector2 _speedRange = new Vector2(1f, 50f);

        [Tooltip("Scroll wheel multiplier for speed adjustment.")]
        [SerializeField] private float _scrollSpeedMultiplier = 5f;

        [Header("Mouse Look")]
        [Tooltip("Horizontal mouse sensitivity.")]
        [SerializeField] private float _sensitivityX = 2f;

        [Tooltip("Vertical mouse sensitivity.")]
        [SerializeField] private float _sensitivityY = 2f;

        [Tooltip("Clamp pitch angle (degrees up/down).")]
        [SerializeField] private Vector2 _pitchClamp = new Vector2(-80f, 80f);

        // ── Private state ──────────────────────────────────────────────────────

        private bool _freeFlyActive = false;
        private float _yaw;
        private float _pitch;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            // Seed yaw/pitch from current transform so the camera doesn't snap on first enable.
            var euler = transform.eulerAngles;
            _yaw   = euler.y;
            _pitch = euler.x;
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
                _inputHandler.OnToggleFreeCam += HandleToggleFreeCam;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnSpectateStopped += HandleSpectateStopped;
        }

        private void OnDisable()
        {
            SetFreeFly(false);

            if (_inputHandler != null)
                _inputHandler.OnToggleFreeCam -= HandleToggleFreeCam;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnSpectateStopped -= HandleSpectateStopped;
        }

        private void Update()
        {
            if (!_freeFlyActive) return;

            HandleSpeedScroll();
            HandleMouseLook();
            HandleMovement();
        }

        // ── Private logic ──────────────────────────────────────────────────────

        private void HandleToggleFreeCam()
        {
            SetFreeFly(!_freeFlyActive);
        }

        private void HandleSpectateStopped()
        {
            SetFreeFly(false);
            gameObject.SetActive(false);
        }

        private void SetFreeFly(bool active)
        {
            _freeFlyActive = active;

            // Switch input context so UI/Spectator layers stay enabled in free-fly mode.
            if (InputLayerManager.Instance != null)
            {
                InputLayerManager.Instance.TransitionToState(
                    active ? InputState.SpectatorFreeCamera : InputState.Spectating);
            }

            // Lock/unlock cursor.
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !active;

            Debug.Log($"[SpectatorFreeCameraController] Free-fly {(active ? "ON" : "OFF")}");
        }

        private void HandleSpeedScroll()
        {
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                _moveSpeed = Mathf.Clamp(_moveSpeed + scroll * _scrollSpeedMultiplier, _speedRange.x, _speedRange.y);
        }

        private void HandleMouseLook()
        {
            float mouseX = UnityEngine.Input.GetAxis("Mouse X") * _sensitivityX;
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * _sensitivityY;

            _yaw   += mouseX;
            _pitch -= mouseY;
            _pitch  = Mathf.Clamp(_pitch, _pitchClamp.x, _pitchClamp.y);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMovement()
        {
            float h = UnityEngine.Input.GetAxisRaw("Horizontal"); // A/D, Left/Right arrows
            float v = UnityEngine.Input.GetAxisRaw("Vertical");   // W/S, Up/Down arrows

            Vector3 direction = transform.right * h + transform.forward * v;
            transform.position += direction.normalized * (_moveSpeed * Time.deltaTime);
        }
    }
}
