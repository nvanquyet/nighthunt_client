using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.Inventory
{
    /// <summary>
    /// Handles ONLY Inventory action-map input (OpenInventory, DropItem, QuickSlots, UseConsumable).
    ///
    /// DESIGN (SRP):
    ///   - Owns ONLY InputSystem wiring (InputActionMap, callbacks).
    ///   - Exposes high-level events for gameplay/UI systems to subscribe to.
    ///   - InputLayerManager remains the single source of truth for enabling/disabling maps.
    /// </summary>
    public class InventoryInputHandler : MonoBehaviour, IInputHandler
    {
        // ── Events ────────────────────────────────────────────────────────────────
        public event Action OpenInventoryPerformed;
        public event Action DropItemPerformed;
        public event Action<int> QuickSlotPerformed; // 0..3
        public event Action UseConsumablePerformed;

        // ── Cached map/actions ───────────────────────────────────────────────────
        private InputActionMap _inventoryMap;
        private InputAction _openInventoryAction;
        private InputAction _dropItemAction;
        private InputAction _quickSlot1Action;
        private InputAction _quickSlot2Action;
        private InputAction _quickSlot3Action;
        private InputAction _quickSlot4Action;
        private InputAction _useConsumableAction;

        // Delegate fields (để unsubscribe đúng, tránh lambda leak)
        private Action<InputAction.CallbackContext> _onQuickSlot1;
        private Action<InputAction.CallbackContext> _onQuickSlot2;
        private Action<InputAction.CallbackContext> _onQuickSlot3;
        private Action<InputAction.CallbackContext> _onQuickSlot4;

        private bool _inputEnabled;

        // ── Lifecycle ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _onQuickSlot1 = _ => QuickSlotPerformed?.Invoke(0);
            _onQuickSlot2 = _ => QuickSlotPerformed?.Invoke(1);
            _onQuickSlot3 = _ => QuickSlotPerformed?.Invoke(2);
            _onQuickSlot4 = _ => QuickSlotPerformed?.Invoke(3);
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

        // ── IInputHandler ─────────────────────────────────────────────────────────
        public bool IsInputEnabled => _inputEnabled;

        public InputActionMap GetActionMap()
        {
            // Ensure InputLayerManager can evaluate map.enabled and decide EnableInput().
            if (_inventoryMap == null && InputLayerManager.Instance != null)
                _inventoryMap = InputLayerManager.Instance.InventoryMap;
            return _inventoryMap;
        }

        public void EnableInput()
        {
            if (_inputEnabled) return;

            if (_inventoryMap == null)
                _inventoryMap = InputLayerManager.Instance != null ? InputLayerManager.Instance.InventoryMap : null;

            if (_inventoryMap == null)
            {
                Debug.LogWarning("[InventoryInputHandler] Inventory action map not found!");
                return;
            }

            // Cache actions (safe if null; some projects disable specific actions)
            _openInventoryAction = _inventoryMap.FindAction("OpenInventory");
            _dropItemAction      = _inventoryMap.FindAction("DropItem");
            _quickSlot1Action    = _inventoryMap.FindAction("QuickSlot1");
            _quickSlot2Action    = _inventoryMap.FindAction("QuickSlot2");
            _quickSlot3Action    = _inventoryMap.FindAction("QuickSlot3");
            _quickSlot4Action    = _inventoryMap.FindAction("QuickSlot4");
            _useConsumableAction = _inventoryMap.FindAction("UseConsumable");

            _inputEnabled = true;

            if (_openInventoryAction != null) _openInventoryAction.performed += OnOpenInventory;
            if (_dropItemAction      != null) _dropItemAction.performed      += OnDropItem;
            if (_quickSlot1Action    != null) _quickSlot1Action.performed    += _onQuickSlot1;
            if (_quickSlot2Action    != null) _quickSlot2Action.performed    += _onQuickSlot2;
            if (_quickSlot3Action    != null) _quickSlot3Action.performed    += _onQuickSlot3;
            if (_quickSlot4Action    != null) _quickSlot4Action.performed    += _onQuickSlot4;
            if (_useConsumableAction != null) _useConsumableAction.performed += OnUseConsumable;
        }

        public void DisableInput()
        {
            if (!_inputEnabled) return;
            _inputEnabled = false;

            if (_openInventoryAction != null) _openInventoryAction.performed -= OnOpenInventory;
            if (_dropItemAction      != null) _dropItemAction.performed      -= OnDropItem;
            if (_quickSlot1Action    != null) _quickSlot1Action.performed    -= _onQuickSlot1;
            if (_quickSlot2Action    != null) _quickSlot2Action.performed    -= _onQuickSlot2;
            if (_quickSlot3Action    != null) _quickSlot3Action.performed    -= _onQuickSlot3;
            if (_quickSlot4Action    != null) _quickSlot4Action.performed    -= _onQuickSlot4;
            if (_useConsumableAction != null) _useConsumableAction.performed -= OnUseConsumable;
        }

        // ── Callbacks ─────────────────────────────────────────────────────────────
        private void OnOpenInventory(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            OpenInventoryPerformed?.Invoke();
        }

        private void OnDropItem(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            DropItemPerformed?.Invoke();
        }

        private void OnUseConsumable(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            UseConsumablePerformed?.Invoke();
        }
    }
}

