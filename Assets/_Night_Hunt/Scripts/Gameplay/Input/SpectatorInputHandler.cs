using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Input handler for spectator mode
    /// </summary>
    public class SpectatorInputHandler : MonoBehaviour
    {
        [Header("Spectator Input Settings")]
        [SerializeField] private Key nextPlayerKey = Key.RightBracket;
        [SerializeField] private Key previousPlayerKey = Key.LeftBracket;
        [SerializeField] private Key exitSpectatorKey = Key.Tab;
        [SerializeField] private float switchCooldown = 0.5f;

        private InputActionMap spectatorActionMap;
        private InputAction nextPlayerAction;
        private InputAction previousPlayerAction;
        private InputAction exitSpectatorAction;

        private float lastSwitchTime = 0f;
        private bool isEnabled = false;

        // Events
        public event System.Action OnNextPlayer;
        public event System.Action OnPreviousPlayer;
        public event System.Action OnExitSpectator;

        private void Awake()
        {
            var inputLayerManager = InputLayerManager.Instance;
            if (inputLayerManager != null)
            {
                var controller = inputLayerManager.GetActionMapController("Spectator");
                if (controller != null)
                {
                    spectatorActionMap = controller.GetAction("Next")?.actionMap;
                    if (spectatorActionMap != null)
                    {
                        nextPlayerAction = spectatorActionMap.FindAction("Next");
                        previousPlayerAction = spectatorActionMap.FindAction("Previous");
                        exitSpectatorAction = spectatorActionMap.FindAction("Exit");
                    }
                }
            }
        }

        private void OnEnable()
        {
            EnableSpectatorInput();
        }

        private void OnDisable()
        {
            DisableSpectatorInput();
        }

        private void Update()
        {
            if (!isEnabled) return;

            HandleKeyboardInput();
        }

        /// <summary>
        /// Handle keyboard input (fallback if action map not available)
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (Keyboard.current == null) return;

            if (Time.time - lastSwitchTime < switchCooldown) return;

            if (Keyboard.current[nextPlayerKey].wasPressedThisFrame)
            {
                OnNextPlayer?.Invoke();
                lastSwitchTime = Time.time;
            }

            if (Keyboard.current[previousPlayerKey].wasPressedThisFrame)
            {
                OnPreviousPlayer?.Invoke();
                lastSwitchTime = Time.time;
            }

            if (Keyboard.current[exitSpectatorKey].wasPressedThisFrame)
            {
                OnExitSpectator?.Invoke();
            }
        }

        /// <summary>
        /// Enable spectator input
        /// </summary>
        public void EnableSpectatorInput()
        {
            if (isEnabled) return;

            isEnabled = true;

            if (nextPlayerAction != null)
                nextPlayerAction.performed += OnNextPlayerPerformed;
            if (previousPlayerAction != null)
                previousPlayerAction.performed += OnPreviousPlayerPerformed;
            if (exitSpectatorAction != null)
                exitSpectatorAction.performed += OnExitSpectatorPerformed;
        }

        /// <summary>
        /// Disable spectator input
        /// </summary>
        public void DisableSpectatorInput()
        {
            if (!isEnabled) return;

            isEnabled = false;

            if (nextPlayerAction != null)
                nextPlayerAction.performed -= OnNextPlayerPerformed;
            if (previousPlayerAction != null)
                previousPlayerAction.performed -= OnPreviousPlayerPerformed;
            if (exitSpectatorAction != null)
                exitSpectatorAction.performed -= OnExitSpectatorPerformed;
        }

        private void OnNextPlayerPerformed(InputAction.CallbackContext context)
        {
            if (Time.time - lastSwitchTime < switchCooldown) return;
            OnNextPlayer?.Invoke();
            lastSwitchTime = Time.time;
        }

        private void OnPreviousPlayerPerformed(InputAction.CallbackContext context)
        {
            if (Time.time - lastSwitchTime < switchCooldown) return;
            OnPreviousPlayer?.Invoke();
            lastSwitchTime = Time.time;
        }

        private void OnExitSpectatorPerformed(InputAction.CallbackContext context)
        {
            OnExitSpectator?.Invoke();
        }
    }
}

