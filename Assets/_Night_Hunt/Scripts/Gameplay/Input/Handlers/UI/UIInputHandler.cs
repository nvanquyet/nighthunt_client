using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.UI
{
    /// <summary>
    /// Handles UI input (Menu Navigation, Cancel, Submit)
    /// Used for menus, inventory UI, dialogue, etc.
    /// </summary>
    public class UIInputHandler : MonoBehaviour, IInputHandler
    {
        private InputActionMap uiActionMap;
        private InputAction navigateAction;
        private InputAction submitAction;
        private InputAction cancelAction;
        private InputAction pointAction; // Mouse position

        private bool inputEnabled = false;

        // Events
        public event System.Action<Vector2> OnNavigate;
        public event System.Action OnSubmit;
        public event System.Action OnCancel;
        public event System.Action<Vector2> OnPoint;

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
                Debug.LogError("[UIInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            uiActionMap = InputLayerManager.Instance.UIMap;

            if (uiActionMap != null)
            {
                navigateAction = uiActionMap.FindAction("Navigate");
                submitAction = uiActionMap.FindAction("Submit");
                cancelAction = uiActionMap.FindAction("Cancel");
                pointAction = uiActionMap.FindAction("Point");
            }
            else
            {
                Debug.LogError("[UIInputHandler] 'UI' action map not found!");
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

        public InputActionMap GetActionMap() => uiActionMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            inputEnabled = true;

            if (navigateAction != null)
                navigateAction.performed += OnNavigatePerformed;

            if (submitAction != null)
                submitAction.performed += OnSubmitPerformed;

            if (cancelAction != null)
                cancelAction.performed += OnCancelPerformed;

            if (pointAction != null)
                pointAction.performed += OnPointPerformed;

            Debug.Log("[UIInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            if (navigateAction != null)
                navigateAction.performed -= OnNavigatePerformed;

            if (submitAction != null)
                submitAction.performed -= OnSubmitPerformed;

            if (cancelAction != null)
                cancelAction.performed -= OnCancelPerformed;

            if (pointAction != null)
                pointAction.performed -= OnPointPerformed;

            Debug.Log("[UIInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            Vector2 navigation = context.ReadValue<Vector2>();
            OnNavigate?.Invoke(navigation);
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            OnSubmit?.Invoke();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            OnCancel?.Invoke();
        }

        private void OnPointPerformed(InputAction.CallbackContext context)
        {
            Vector2 point = context.ReadValue<Vector2>();
            OnPoint?.Invoke(point);
        }

        #endregion
    }
}