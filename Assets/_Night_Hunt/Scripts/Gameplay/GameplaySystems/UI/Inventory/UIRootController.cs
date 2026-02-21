using NightHunt.Gameplay.Spectator;
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

        private void Awake()
        {
            InitBridgeAndUI();

            if (_inventoryRootObject != null)
                _inventoryRootObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
            }
        }

        private void OnDisable()
        {
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged -= OnCurrentPlayerChanged;
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
    }
}

