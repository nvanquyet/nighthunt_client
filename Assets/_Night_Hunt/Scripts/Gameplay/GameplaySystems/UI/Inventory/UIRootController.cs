using NightHunt.Gameplay.Spectator;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.Aim;
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
        [SerializeField] private PlayerHUDPanel  _playerHudPanel;
        [SerializeField] private CombatHUDPanel  _combatHudPanel;

        [Header("Inventory Toggle")]
        [SerializeField] private GameObject _inventoryRootObject;

        private UIDomainBridge _domainBridge;
        private bool _inventoryVisible;
        private bool _subscribedToSpectate;
        private bool _hasReceivedPlayerEvent;

        private void Awake()
        {
            // Initialize bridge + inventory screen layout only.
            // CombatHUD data bind is deferred to OnCurrentPlayerChanged.
            _domainBridge = new UIDomainBridge();
            if (_inventoryScreen != null)
                _inventoryScreen.Initialize(_domainBridge);

            // Force slot GO spawn NOW — even if CombatHUDPanel's GameObject is inactive
            // (Awake won't fire on inactive GOs, but we can call methods directly).
            _combatHudPanel?.EnsureSlots();

            if (_inventoryRootObject != null)
                _inventoryRootObject.SetActive(false);

            _inventoryVisible = false;
        }

        private void Start()
        {
            // Guard: if SpectateManager wasn’t instantiated yet during OnEnable (script
            // execution-order race), subscribe now — by Start() all Awake() calls
            // in the scene have already completed.
            if (!_subscribedToSpectate && SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
                _subscribedToSpectate = true;
                Debug.Log("[UIRootController] Start: late-subscribed to SpectateManager (was null in OnEnable).");
            }

            // Edge-case: UIRootController was enabled AFTER the local player already spawned
            // (e.g. additive scene load). Synthesise the callback so CombatHUD gets its
            // first real init without waiting for a future player-change event.
            // Guard: skip if OnCurrentPlayerChanged was already invoked via subscription.
            if (!_hasReceivedPlayerEvent)
            {
                var existing = SpectateManager.Instance?.GetCurrentPlayer();
                if (existing != null)
                    OnCurrentPlayerChanged(existing);
            }
        }

        private void OnEnable()
        {
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
                _subscribedToSpectate = true;
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
            _subscribedToSpectate = false;

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
            _hasReceivedPlayerEvent = true;
            Debug.Log($"[UIRootController] OnCurrentPlayerChanged: player={player?.name ?? "null"}");

            // Đổi player hiển thị UI → reset mọi drag-drop đang diễn ra
            DragDropController.Instance?.ResetAll();

            _domainBridge?.Dispose();
            
            // Tạo bridge mới
            _domainBridge = new UIDomainBridge();
            _domainBridge.InitializeForCurrentPlayer();

            Debug.Log($"[UIRootController] bridge.IsReady={_domainBridge.IsReady}");

            // Chỉ refresh UI, không rebuild layout
            if (_inventoryScreen != null)
            {
                _inventoryScreen.RefreshForNewPlayer(_domainBridge);
            }

            if (_playerHudPanel != null)
                _playerHudPanel.Initialize(_domainBridge);

            if (_combatHudPanel != null)
            {
                
                var playerT = SpectateManager.Instance?.GetCurrentPlayer()?.transform;
                var aimSystemForHUD = UnityEngine.Object.FindFirstObjectByType<AimSystem>();
                _combatHudPanel.Initialize(
                    _domainBridge.IsReady ? _domainBridge.Bridge.Weapon    : null,
                    _domainBridge.IsReady ? _domainBridge.Bridge.ItemSelection : null,
                    _domainBridge.IsReady ? _domainBridge.Bridge.Stat      : null,
                    playerT,
                    aimSystemForHUD,
                    _domainBridge.IsReady ? _domainBridge.Bridge.ItemUse   : null,
                    NightHunt.Gameplay.Input.Core.InputManager.Instance?.CombatHandler,
                    _domainBridge.IsReady ? _domainBridge.Bridge.Inventory : null);
            }
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

            // Cursor is always visible in this top-down game — no lock/unlock needed.
            // Both inventory-open and gameplay states keep cursor free and visible.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }
}

