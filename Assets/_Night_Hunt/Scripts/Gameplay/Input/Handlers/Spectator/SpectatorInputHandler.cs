using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.Spectator
{
    /// <summary>
    /// Handles spectator mode input (Next Player, Previous Player, Exit)
    /// </summary>
    public class SpectatorInputHandler : MonoBehaviour, IInputHandler
    {
        [Header("Settings")]
        [SerializeField] private float switchCooldown = 0.5f;

        private InputActionMap spectatorActionMap;
        private InputAction nextPlayerAction;
        private InputAction previousPlayerAction;
        private InputAction exitSpectatorAction;

        private float lastSwitchTime = 0f;
        private bool inputEnabled = false;

        // Events
        public event System.Action OnNextPlayer;
        public event System.Action OnPreviousPlayer;
        public event System.Action OnExitSpectator;

        #region Lifecycle

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            RegisterWithManager();
        }

        private void OnDisable()
        {
            DisableInput();
            UnregisterFromManager();
        }

        #endregion

        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[SpectatorInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            spectatorActionMap = InputLayerManager.Instance.SpectatorMap;

            if (spectatorActionMap != null)
            {
                nextPlayerAction = spectatorActionMap.FindAction("Next");
                previousPlayerAction = spectatorActionMap.FindAction("Previous");
                exitSpectatorAction = spectatorActionMap.FindAction("Exit");
            }
            else
            {
                Debug.LogError("[SpectatorInputHandler] 'Spectator' action map not found!");
            }
        }

        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        #endregion

        #region IInputHandler Implementation

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap() => spectatorActionMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            inputEnabled = true;

            if (nextPlayerAction != null)
                nextPlayerAction.performed += OnNextPlayerPerformed;

            if (previousPlayerAction != null)
                previousPlayerAction.performed += OnPreviousPlayerPerformed;

            if (exitSpectatorAction != null)
                exitSpectatorAction.performed += OnExitSpectatorPerformed;

            // Transition to spectating state
            InputLayerManager.Instance?.TransitionToState(InputState.Spectating);

            Debug.Log("[SpectatorInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            if (nextPlayerAction != null)
                nextPlayerAction.performed -= OnNextPlayerPerformed;

            if (previousPlayerAction != null)
                previousPlayerAction.performed -= OnPreviousPlayerPerformed;

            if (exitSpectatorAction != null)
                exitSpectatorAction.performed -= OnExitSpectatorPerformed;

            Debug.Log("[SpectatorInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

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

        #endregion
    }
}