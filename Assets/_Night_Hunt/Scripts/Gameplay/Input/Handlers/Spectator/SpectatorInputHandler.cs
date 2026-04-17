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
        private InputAction toggleFreeCamAction;

        private float lastSwitchTime = 0f;
        private bool inputEnabled = false;

        // Events
        public event System.Action OnNextPlayer;
        public event System.Action OnPreviousPlayer;
        public event System.Action OnExitSpectator;
        /// <summary>Fired when the player presses Tab to toggle free-fly vs follow mode.</summary>
        public event System.Action OnToggleFreeCam;

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
                // InputSystem_Actions uses "NextPlayer", "PreviousPlayer", "FreeCamera" in Spectator map.
                // "ToggleFreeCam" must be bound to Tab in the InputActionAsset Spectator action map.
                nextPlayerAction     = spectatorActionMap.FindAction("NextPlayer");
                previousPlayerAction = spectatorActionMap.FindAction("PreviousPlayer");
                exitSpectatorAction  = spectatorActionMap.FindAction("FreeCamera");
                toggleFreeCamAction  = spectatorActionMap.FindAction("ToggleFreeCam");
                if (toggleFreeCamAction == null)
                    Debug.LogWarning("[SpectatorInputHandler] 'ToggleFreeCam' action not found in Spectator map. Add it (Tab key) in InputActionAsset.");
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

        public InputActionMap GetActionMap()
        {
            if (spectatorActionMap == null && InputLayerManager.Instance != null)
                spectatorActionMap = InputLayerManager.Instance.SpectatorMap;
            return spectatorActionMap;
        }

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

            if (toggleFreeCamAction != null)
                toggleFreeCamAction.performed += OnToggleFreeCamPerformed;

            // NOTE: Do NOT call TransitionToState here.
            // Context switching is the caller's responsibility (CharacterInputLifecycle, etc.)
            // to avoid circular dependencies.

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

            if (toggleFreeCamAction != null)
                toggleFreeCamAction.performed -= OnToggleFreeCamPerformed;

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

        private void OnToggleFreeCamPerformed(InputAction.CallbackContext context)
        {
            OnToggleFreeCam?.Invoke();
        }

        #endregion
    }
}