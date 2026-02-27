using NightHunt.Gameplay.Spectator;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking;
using UnityEngine;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Điểm vào chính cho HUD + Inventory.
    /// Giữ UIDomainBridge và truyền cho InventoryScreen / PlayerHUDPanel.
    /// Đồng thời re-init UI khi SpectateManager đổi current player.
    /// </summary>
    public class UIRootController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private InventoryScreen _inventoryScreen;
        [SerializeField] private PlayerHUDPanel _playerHudPanel;

        [Header("Inventory Toggle")]
        [SerializeField] private GameObject _inventoryRootObject;

        private UIDomainBridge _domainBridge;
        private bool _inventoryVisible;

        private void Awake()
        {
            InitBridgeAndUI();

            if (_inventoryRootObject != null)
                _inventoryRootObject.SetActive(false);

            _inventoryVisible = false;
        }

        private void OnEnable()
        {
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
            }

            // Inventory hotkey: listen via InputManager → InventoryInputHandler
            var input = InputManager.Instance;
            if (input != null && input.InventoryHandler != null)
            {
                input.InventoryHandler.OpenInventoryPerformed += HandleOpenInventoryPerformed;
            }

            // Keep UI in sync even when contexts are changed elsewhere (Escape PopContext, etc.)
            if (InputLayerManager.Instance != null)
            {
                InputLayerManager.Instance.OnContextChanged += HandleContextChanged;
                // Initial sync (in case we're enabled after context already applied)
                HandleContextChanged(InputLayerManager.Instance.CurrentState, InputLayerManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged -= OnCurrentPlayerChanged;
            }

            var input = InputManager.Instance;
            if (input != null && input.InventoryHandler != null)
            {
                input.InventoryHandler.OpenInventoryPerformed -= HandleOpenInventoryPerformed;
            }

            if (InputLayerManager.Instance != null)
            {
                InputLayerManager.Instance.OnContextChanged -= HandleContextChanged;
            }
        }

        private void OnDestroy()
        {
            _domainBridge?.Dispose();
        }

        private void OnCurrentPlayerChanged(NetworkPlayer player)
        {
            // Đổi player hiển thị UI → reset mọi drag-drop đang diễn ra
            DragDropController.Instance?.ResetAll();

            _domainBridge?.Dispose();
            
            // Tạo bridge mới
            _domainBridge = new UIDomainBridge();
            _domainBridge.InitializeForCurrentPlayer();

            // Chỉ refresh UI, không rebuild layout
            if (_inventoryScreen != null)
            {
                _inventoryScreen.RefreshForNewPlayer(_domainBridge);
            }

            if (_playerHudPanel != null)
            {
                _playerHudPanel.Initialize(_domainBridge);
            }
        }

        private void InitBridgeAndUI()
        {
            _domainBridge = new UIDomainBridge();
            _domainBridge.InitializeForCurrentPlayer();

            if (_inventoryScreen != null)
                _inventoryScreen.Initialize(_domainBridge);

            if (_playerHudPanel != null)
                _playerHudPanel.Initialize(_domainBridge);
        }

        public void ToggleInventory()
        {
            if (_inventoryRootObject == null)
                return;

            bool wasActive = _inventoryRootObject.activeSelf;
            _inventoryRootObject.SetActive(!wasActive);
        }

        private void HandleOpenInventoryPerformed()
        {
            var ilm = InputLayerManager.Instance;
            if (ilm == null) return;

            // Single source of truth: context drives which maps are enabled.
            // UI visibility is synced in HandleContextChanged.
            if (ilm.CurrentState == InputState.InventoryOpen)
                ilm.PopContext();
            else
                ilm.PushContext(InputState.InventoryOpen);
        }

        private void HandleContextChanged(InputState oldState, InputState newState)
        {
            if (_inventoryRootObject == null) return;

            // Only react to transitions to/from InventoryOpen to avoid fighting other systems.
            if (newState == InputState.InventoryOpen)
                SetInventoryVisible(true);
            else if (oldState == InputState.InventoryOpen && newState != InputState.InventoryOpen)
                SetInventoryVisible(false);
        }

        private void SetInventoryVisible(bool visible)
        {
            if (_inventoryVisible == visible) return;
            _inventoryVisible = visible;

            _inventoryRootObject.SetActive(visible);

            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}

