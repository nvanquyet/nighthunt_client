using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using System;

namespace NightHunt.Gameplay.Input.Handlers.UI
{
    /// <summary>
    /// Handles UI input: QuickSlots, Cancel, OpenMenu, ToggleMap.
    /// <para>Action map "UI" được <see cref="InputLayerManager"/> bật/tắt theo context.
    /// Handler này chỉ subscribe/unsubscribe callbacks, KHÔNG tự enable/disable ActionMap.</para>
    ///
    /// Active contexts: <see cref="InputState.InventoryOpen"/>, <see cref="InputState.Paused"/>,
    /// <see cref="InputState.MapOpen"/>, <see cref="InputState.Spectating"/>,
    /// <see cref="InputState.PlayerDead"/>, <see cref="InputState.InDialogue"/>.
    /// </summary>
    public class UIInputHandler : MonoBehaviour, IInputHandler
    {
        // ── Cached actions ────────────────────────────────────────────────────────
        private InputActionMap _uiActionMap;
        private InputAction _quickSlot1Action;
        private InputAction _quickSlot2Action;
        private InputAction _quickSlot3Action;
        private InputAction _quickSlot4Action;
        private InputAction _cancelAction;
        private InputAction _openMenuAction;
        private InputAction _toggleMapAction;

        private bool _inputEnabled = false;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<int> OnQuickSlotPressed; // slot index 0-3
        public event Action      OnCancelPressed;
        public event Action      OnOpenMenuPressed;
        public event Action      OnToggleMapPressed;

        // ── IInputHandler ─────────────────────────────────────────────────────────
        public bool IsInputEnabled  => _inputEnabled;
        public InputActionMap GetActionMap() => _uiActionMap;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Invoke(nameof(InitializeActions), 0.2f); // Delay để đảm bảo InputLayerManager đã Awake và tạo map
        }

        private void OnEnable()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void OnDisable()
        {
            DisableInput();
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        // ── Initialization ────────────────────────────────────────────────────────

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[UIInputHandler] InputLayerManager.Instance là null!");
                return;
            }

            // Lấy UI map trực tiếp từ InputLayerManager (không qua PlayerInput)
            _uiActionMap = InputLayerManager.Instance.UIMap;

            if (_uiActionMap == null)
            {
                Debug.LogError("[UIInputHandler] 'UI' action map không tìm thấy trong InputLayerManager!");
                return;
            }

            _quickSlot1Action = _uiActionMap.FindAction("QuickSlot1");
            _quickSlot2Action = _uiActionMap.FindAction("QuickSlot2");
            _quickSlot3Action = _uiActionMap.FindAction("QuickSlot3");
            _quickSlot4Action = _uiActionMap.FindAction("QuickSlot4");
            _cancelAction     = _uiActionMap.FindAction("Cancel");
            _openMenuAction   = _uiActionMap.FindAction("OpenMenu");
            _toggleMapAction  = _uiActionMap.FindAction("ToggleMap");

            if (_cancelAction    == null) Debug.LogWarning("[UIInputHandler] 'Cancel' action không tìm thấy");
            if (_openMenuAction  == null) Debug.LogWarning("[UIInputHandler] 'OpenMenu' action không tìm thấy");
            if (_toggleMapAction == null) Debug.LogWarning("[UIInputHandler] 'ToggleMap' action không tìm thấy");
        }

        // ── IInputHandler Implementation ──────────────────────────────────────────

        public void EnableInput()
        {
            if (_inputEnabled) return;

            // Nếu Awake chưa tìm được map (vì InputLayerManager chưa sẵn), thử lại
            if (_uiActionMap == null) InitializeActions();
            if (_uiActionMap == null) return;

            _inputEnabled = true;

            if (_quickSlot1Action != null) _quickSlot1Action.performed += OnQuickSlot1;
            if (_quickSlot2Action != null) _quickSlot2Action.performed += OnQuickSlot2;
            if (_quickSlot3Action != null) _quickSlot3Action.performed += OnQuickSlot3;
            if (_quickSlot4Action != null) _quickSlot4Action.performed += OnQuickSlot4;
            if (_cancelAction    != null) _cancelAction.performed    += OnCancel;
            if (_openMenuAction  != null) _openMenuAction.performed  += OnOpenMenu;
            if (_toggleMapAction != null) _toggleMapAction.performed += OnToggleMap;

            Debug.Log("[UIInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!_inputEnabled) return;
            _inputEnabled = false;

            if (_quickSlot1Action != null) _quickSlot1Action.performed -= OnQuickSlot1;
            if (_quickSlot2Action != null) _quickSlot2Action.performed -= OnQuickSlot2;
            if (_quickSlot3Action != null) _quickSlot3Action.performed -= OnQuickSlot3;
            if (_quickSlot4Action != null) _quickSlot4Action.performed -= OnQuickSlot4;
            if (_cancelAction    != null) _cancelAction.performed    -= OnCancel;
            if (_openMenuAction  != null) _openMenuAction.performed  -= OnOpenMenu;
            if (_toggleMapAction != null) _toggleMapAction.performed -= OnToggleMap;

            Debug.Log("[UIInputHandler] Input disabled");
        }

        // ── Callbacks ─────────────────────────────────────────────────────────────

        private void OnQuickSlot1(InputAction.CallbackContext ctx) => OnQuickSlotPressed?.Invoke(0);
        private void OnQuickSlot2(InputAction.CallbackContext ctx) => OnQuickSlotPressed?.Invoke(1);
        private void OnQuickSlot3(InputAction.CallbackContext ctx) => OnQuickSlotPressed?.Invoke(2);
        private void OnQuickSlot4(InputAction.CallbackContext ctx) => OnQuickSlotPressed?.Invoke(3);

        private void OnCancel(InputAction.CallbackContext ctx)
        {
            OnCancelPressed?.Invoke();
            // Cancel tự động PopContext (Escape đóng inventory/map/menu)
            InputLayerManager.Instance?.PopContext();
        }

        private void OnOpenMenu(InputAction.CallbackContext ctx) => OnOpenMenuPressed?.Invoke();

        private void OnToggleMap(InputAction.CallbackContext ctx)
        {
            OnToggleMapPressed?.Invoke();
            // Toggle map: push MapOpen hoặc pop về context trước
            if (InputLayerManager.Instance == null) return;
            if (InputLayerManager.Instance.CurrentState == InputState.MapOpen)
                InputLayerManager.Instance.PopContext();
            else
                InputLayerManager.Instance.PushContext(InputState.MapOpen);
        }
    }
}