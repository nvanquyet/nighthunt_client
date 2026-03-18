using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Spectator;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Màn hình Inventory chính: spawn các slot từ config
    /// và bind với UIDomainBridge.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        [Header("Configs")] [SerializeField] private UISlotLayoutConfig _uiConfig;

        [Header("Player Stats")] [SerializeField]
        private PlayerStatUIPanel playerStatUIPanel;

        [Header("Slot Settings")] [Tooltip("Kích thước mặc định cho các slot (Width x Height)")] [SerializeField]
        private Vector2 _slotSize = new Vector2(100, 100);

        [Header("Roots")] [SerializeField] private RectTransform _inventoryGridRoot;
        [SerializeField] private RectTransform _equipmentRoot;
        [SerializeField] private RectTransform _weaponRoot;
        [SerializeField] private RectTransform _quickSlotRoot;
        [SerializeField] private RectTransform _trashSlotRoot;

        [Header("Trash Slot")] [Tooltip("Prefab cho trash slot (setup thủ công trong Inspector)")] [SerializeField]
        private GameObject _trashSlotPrefab;

        private readonly Dictionary<UISlotId, ItemSlotView> _slotViews = new Dictionary<UISlotId, ItemSlotView>();
        private UIDomainBridge _domainBridge;
        private bool _isLayoutBuilt = false; // Flag để track đã build layout chưa

        [Header("Buttons")] [SerializeField] private UnityEngine.UI.Button _sortButton;

        [Header("Attachment Panel")] [SerializeField]
        private AttachmentPanel _attachmentPanel;

        [Header("Tooltip")] [SerializeField] private ItemTooltip _itemTooltip;

        [Header("Drop Quantity Dialog")] [SerializeField]
        private DropQuantityDialog _dropQuantityDialog;

        private ItemSlotView _hoveredSlot;

        private void Awake()
        {
            if (_uiConfig != null && _slotSize == new Vector2(100f, 100f))
                _slotSize = _uiConfig.DefaultSlotSize;
        }

        private PlayerStatUIPanel StatPanel
        {
            get
            {
                if (playerStatUIPanel == null)
                {
                    playerStatUIPanel = ComponentResolver.Find<PlayerStatUIPanel>(this)
                        .OnSelf()
                        .InChildren()
                        .InParent()
                        .OrLogWarning("[Auto] PlayerStatUIPanel not found")
                        .Resolve();
                    if (playerStatUIPanel == null)
                    {
                        Debug.LogWarning("[InventoryScreen] PlayerStatUIPanel not found in children!");
                    }
                }

                return playerStatUIPanel;
            }
        }

        public void Initialize(UIDomainBridge domainBridge)
        {
            _domainBridge = domainBridge;

            // Initialize AttachmentPanel nếu có
            if (_attachmentPanel != null && _uiConfig != null)
            {
                var attachmentSystem = GetAttachmentSystem();
                var gameplayBridge = GetGameplayBridge();

                if (attachmentSystem != null && gameplayBridge != null)
                {
                    _attachmentPanel.Initialize(_uiConfig, attachmentSystem, gameplayBridge);
                }
                else
                {
                    Debug.LogWarning(
                        "[InventoryScreen] AttachmentSystem or GameplayBridge not found - AttachmentPanel not initialized");
                }
            }

            StatPanel?.Initialize(domainBridge);
            // Initialize ItemTooltip nếu có
            if (_itemTooltip != null)
            {
                _itemTooltip.Initialize(_domainBridge);
            }

            // Chỉ build layout lần đầu tiên
            if (!_isLayoutBuilt)
            {
                BuildLayout();
                _isLayoutBuilt = true;
            }
            else
            {
                // Đã có layout rồi → chỉ refresh state từ bridge
                RefreshAllSlots();
            }

            HookBridgeEvents(true);

            if (_sortButton != null)
                _sortButton.onClick.AddListener(OnSortClicked);
        }

        /// <summary>
        /// Refresh tất cả slots với state mới từ bridge (khi đổi player)
        /// </summary>
        public void RefreshForNewPlayer(UIDomainBridge domainBridge)
        {
            // FIX: Unsubscribe from the OLD bridge BEFORE replacing the reference.
            // Previously _domainBridge was replaced first, so HookBridgeEvents(false) silently
            // unsubscribed from the NEW bridge (a no-op) while old subscriptions were leaked.
            HookBridgeEvents(false);

            _domainBridge = domainBridge;

            // Update AttachmentPanel và Tooltip với bridge mới
            if (_attachmentPanel != null && _uiConfig != null)
            {
                var attachmentSystem = GetAttachmentSystem();
                var gameplayBridge = GetGameplayBridge();

                if (attachmentSystem != null && gameplayBridge != null)
                {
                    _attachmentPanel.Initialize(_uiConfig, attachmentSystem, gameplayBridge);
                }
            }

            StatPanel?.RefreshForNewPlayer(domainBridge);
            if (_itemTooltip != null)
            {
                _itemTooltip.Initialize(_domainBridge);
            }

            // Subscribe to new bridge events
            HookBridgeEvents(true);

            // Refresh tất cả slots với state mới
            RefreshAllSlots();
        }

        private void OnDestroy()
        {
            HookBridgeEvents(false);
            HookSlotHoverEvents(false);

            if (_sortButton != null)
                _sortButton.onClick.RemoveListener(OnSortClicked);
        }

        /// <summary>
        /// Build layout - chỉ spawn slots lần đầu tiên
        /// </summary>
        private void BuildLayout()
        {
            // Đảm bảo không spawn lại nếu đã có slots
            if (_slotViews.Count > 0)
            {
                Debug.LogWarning("[InventoryScreen] BuildLayout called but slots already exist. Skipping.");
                return;
            }

            _slotViews.Clear();

            if (_uiConfig == null || _uiConfig.InventoryConfig == null) return;

            // Inventory slots
            if (_inventoryGridRoot != null)
            {
                var prefab = _uiConfig.GetSlotPrefab(UISlotType.Inventory);
                if (prefab != null)
                {
                    for (int i = 0; i < _uiConfig.InventoryTotalSlots; i++)
                    {
                        var go = Instantiate(prefab, _inventoryGridRoot, false);
                        SetupSlotRectTransform(go);
                        var view = ComponentResolver.Find<ItemSlotView>(go)
                            .OnSelf()
                            .InChildren()
                            .OrLogWarning("[Auto] ItemSlotView not found")
                            .Resolve();
                        if (view != null)
                        {
                            var id = UISlotId.Inventory(i);
                            view.Initialize(_uiConfig, id);
                            // Force reset về empty state để đảm bảo icon được set đúng
                            view.SetEmptyState();
                            _slotViews[id] = view;
                            DragDropController.Instance?.RegisterSlotView(view);
                        }
                    }
                }
            }

            // Equipment slots - spawn vào _equipmentRoot (anchor được xử lý ở phần khác)
            if (_equipmentRoot != null && _uiConfig.InventoryConfig.EquipmentConfig != null)
            {
                var prefab = _uiConfig.GetSlotPrefab(UISlotType.Equipment);
                if (prefab != null)
                {
                    foreach (var equipmentSlot in _uiConfig.InventoryConfig.EquipmentConfig)
                    {
                        var go = Instantiate(prefab, _equipmentRoot, false);
                        SetupSlotRectTransform(go);
                        var view = ComponentResolver.Find<ItemSlotView>(go)
                            .OnSelf()
                            .InChildren()
                            .OrLogWarning("[Auto] ItemSlotView not found")
                            .Resolve();
                        if (view != null)
                        {
                            var id = UISlotId.Equipment(equipmentSlot.Type);
                            view.Initialize(_uiConfig, id);
                            // Force reset về empty state để đảm bảo icon được set đúng
                            view.SetEmptyState();
                            _slotViews[id] = view;
                            DragDropController.Instance?.RegisterSlotView(view);
                        }
                    }
                }
            }

            // Weapon slots
            if (_weaponRoot != null && _uiConfig.InventoryConfig.WeaponConfig != null)
            {
                var prefab = _uiConfig.GetSlotPrefab(UISlotType.Weapon);
                if (prefab != null)
                {
                    foreach (var weaponSlot in _uiConfig.InventoryConfig.WeaponConfig)
                    {
                        var go = Instantiate(prefab, _weaponRoot, false);
                        SetupSlotRectTransform(go);
                        var view = ComponentResolver.Find<ItemSlotView>(go)
                            .OnSelf()
                            .InChildren()
                            .OrLogWarning("[Auto] ItemSlotView not found")
                            .Resolve();
                        if (view != null)
                        {
                            var id = UISlotId.Weapon(weaponSlot.Type);
                            view.Initialize(_uiConfig, id);
                            // Force reset về empty state để đảm bảo icon được set đúng
                            view.SetEmptyState();
                            _slotViews[id] = view;
                            DragDropController.Instance?.RegisterSlotView(view);
                        }
                    }
                }
            }

            // QuickSlot
            if (_quickSlotRoot != null)
            {
                var prefab = _uiConfig.GetSlotPrefab(UISlotType.QuickSlot);
                if (prefab != null)
                {
                    for (int i = 0; i < _uiConfig.QuickSlotCount; i++)
                    {
                        var go = Instantiate(prefab, _quickSlotRoot, false);
                        SetupSlotRectTransform(go);
                        var view = ComponentResolver.Find<ItemSlotView>(go)
                            .OnSelf()
                            .InChildren()
                            .OrLogWarning("[Auto] ItemSlotView not found")
                            .Resolve();
                        if (view != null)
                        {
                            var id = UISlotId.QuickSlot(i);
                            view.Initialize(_uiConfig, id);
                            // Force reset về empty state để đảm bảo icon được set đúng
                            view.SetEmptyState();
                            _slotViews[id] = view;
                            DragDropController.Instance?.RegisterSlotView(view);
                        }
                    }
                }
            }

            // Trash Slot
            if (_trashSlotRoot != null && _trashSlotPrefab != null)
            {
                var go = Instantiate(_trashSlotPrefab, _trashSlotRoot, false);
                // Không gọi SetupSlotRectTransform() - giữ nguyên anchor/position từ prefab

                var view = ComponentResolver.Find<ItemSlotView>(go)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] ItemSlotView not found")
                    .Resolve();
                if (view != null)
                {
                    var id = UISlotId.DropArea();
                    view.Initialize(_uiConfig, id);
                    view.SetEmptyState();
                    _slotViews[id] = view;
                    DragDropController.Instance?.RegisterSlotView(view);
                }

                go.gameObject.SetActive(true); // Activate after initialization to avoid showing uninitialized values
            }

            // Force rebuild layout sau khi spawn tất cả slots
            if (_inventoryGridRoot != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryGridRoot);
            if (_equipmentRoot != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_equipmentRoot);
            if (_weaponRoot != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_weaponRoot);
            if (_quickSlotRoot != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_quickSlotRoot);
            if (_trashSlotRoot != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_trashSlotRoot);

            // Hook hover events sau khi spawn tất cả slots
            HookSlotHoverEvents(true);
        }

        /// <summary>
        /// Setup RectTransform cho slot để tránh bị collapse trong Layout Group
        /// </summary>
        private void SetupSlotRectTransform(GameObject slotGO)
        {
            var rectTransform = ComponentResolver.Find<RectTransform>(slotGO)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] RectTransform not found")
                .Resolve();
            if (rectTransform == null) return;

            // Đảm bảo GameObject active để RectTransform có thể được setup
            if (!slotGO.activeSelf)
            {
                slotGO.SetActive(true);
            }

            // Reset về identity transform
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localPosition = Vector3.zero;

            // Set anchor và pivot về center để dễ layout
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // LUÔN set sizeDelta trước (ngay cả khi có Layout Group)
            // Vì một số Layout Group dùng sizeDelta làm base size
            // Force set size ngay lập tức, không check điều kiện
            rectTransform.sizeDelta = _slotSize;

            // Nếu parent có Layout Group, cần thêm LayoutElement
            var parentLayoutGroup = ComponentResolver.Find<UnityEngine.UI.LayoutGroup>(rectTransform.parent)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] UnityEngine.UI.LayoutGroup not found")
                .Resolve();
            if (parentLayoutGroup != null)
            {
                // Thêm LayoutElement với preferred size nếu chưa có
                var layoutElement = ComponentResolver.Find<UnityEngine.UI.LayoutElement>(slotGO)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] UnityEngine.UI.LayoutElement not found")
                    .Resolve();
                if (layoutElement == null)
                {
                    layoutElement = slotGO.AddComponent<UnityEngine.UI.LayoutElement>();
                }

                // LUÔN set preferred size (override để đảm bảo không bị 0)
                // Layout Group sẽ dùng giá trị này để layout
                layoutElement.preferredWidth = _slotSize.x;
                layoutElement.preferredHeight = _slotSize.y;

                // Set min size để đảm bảo không collapse (50% của preferred size)
                layoutElement.minWidth = _slotSize.x * 0.5f;
                layoutElement.minHeight = _slotSize.y * 0.5f;

                // Nếu Layout Group có Child Force Expand, cũng set flexible
                var horizontalLayout = parentLayoutGroup as UnityEngine.UI.HorizontalLayoutGroup;
                var verticalLayout = parentLayoutGroup as UnityEngine.UI.VerticalLayoutGroup;
                var gridLayout = parentLayoutGroup as UnityEngine.UI.GridLayoutGroup;

                if (horizontalLayout != null || verticalLayout != null)
                {
                    // Horizontal/Vertical Layout Group có thể dùng flexible size
                    layoutElement.flexibleWidth = 0f; // Không expand
                    layoutElement.flexibleHeight = 0f; // Không expand
                }
                else if (gridLayout != null)
                {
                    // Grid Layout Group dùng cell size, không cần flexible
                    // Nhưng vẫn cần preferred size để tránh collapse
                }
            }
        }

        /// <summary>
        /// Refresh tất cả slots với state mới từ bridge
        /// Bridge sẽ tự động push snapshot qua events, nhưng ta cũng có thể force refresh ở đây
        /// </summary>
        private void RefreshAllSlots()
        {
            // Clear all slot visuals first
            foreach (var kvp in _slotViews)
            {
                if (kvp.Value != null)
                    kvp.Value.SetEmptyState();
            }

            // ROOT CAUSE D FIX: Re-push current backend state to all slots.
            // Events only fire on changes; a manual clear requires an explicit re-push.
            _domainBridge?.Refresh();
        }

        private void HookBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null) return;

            if (subscribe)
            {
                _domainBridge.OnInventorySlotChanged += HandleInventorySlotChanged;
                _domainBridge.OnEquipmentSlotChanged += HandleEquipmentSlotChanged;
                _domainBridge.OnWeaponSlotChanged += HandleWeaponSlotChanged;
                _domainBridge.OnQuickSlotChanged += HandleQuickSlotChanged;
            }
            else
            {
                _domainBridge.OnInventorySlotChanged -= HandleInventorySlotChanged;
                _domainBridge.OnEquipmentSlotChanged -= HandleEquipmentSlotChanged;
                _domainBridge.OnWeaponSlotChanged -= HandleWeaponSlotChanged;
                _domainBridge.OnQuickSlotChanged -= HandleQuickSlotChanged;
            }
        }

        private void HandleInventorySlotChanged(UISlotId id, UISlotState state)
        {
            UpdateSlot(id, state);
        }

        private void HandleEquipmentSlotChanged(UISlotId id, UISlotState state)
        {
            UpdateSlot(id, state);
        }

        private void HandleWeaponSlotChanged(UISlotId id, UISlotState state)
        {
            UpdateSlot(id, state);
        }

        private void HandleQuickSlotChanged(UISlotId id, UISlotState state)
        {
            UpdateSlot(id, state);
        }

        private void UpdateSlot(UISlotId id, UISlotState state)
        {
            if (_slotViews.TryGetValue(id, out var view))
            {
                view.SetState(state);
            }
        }

        private void OnSortClicked()
        {
            _domainBridge?.RequestSortInventory(InventorySortMode.Default);
        }

        #region Attachment Panel Hover Logic

        private void HookSlotHoverEvents(bool subscribe)
        {
            foreach (var kvp in _slotViews)
            {
                var input = ComponentResolver.Find<ItemSlotInput>(kvp.Value)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] ItemSlotInput not found")
                    .Resolve();
                if (input != null)
                {
                    if (subscribe)
                    {
                        input.OnSlotHoverEnter += OnSlotHoverEnter;
                        input.OnSlotHoverExit += OnSlotHoverExit;
                    }
                    else
                    {
                        input.OnSlotHoverEnter -= OnSlotHoverEnter;
                        input.OnSlotHoverExit -= OnSlotHoverExit;
                    }
                }
            }
        }

        private void OnSlotHoverEnter(ItemSlotView slotView)
        {
            _hoveredSlot = slotView;

            // Show tooltip và attachment panel nếu slot có item
            if (slotView.State?.Item != null)
            {
                var def = ItemDatabase.GetDefinition(slotView.State.Item.DefinitionID);

                // Show tooltip với Item Stats (sử dụng mouse position để tooltip follow mouse)
                if (_itemTooltip != null)
                {
                    Vector3 mousePos = Input.mousePosition;
                    _itemTooltip.Show(slotView.State.Item, mousePos);
                }

                // Show attachment panel nếu item có attachment slots và config cho phép hover
                bool allowHoverPanel =
                    _uiConfig != null &&
                    _uiConfig.InventoryConfig != null &&
                    _uiConfig.InventoryConfig.AttachmentUI.ShowAttachmentPanelOnHover;

                if (allowHoverPanel && def != null && def.AttachmentSlots != null && def.AttachmentSlots.Length > 0)
                {
                    if (_attachmentPanel != null)
                    {
                        _attachmentPanel.Show(slotView.State.Item);
                    }
                }
                else
                {
                    // Item không có attachment slots hoặc hover bị tắt → hide panel
                    if (_attachmentPanel != null)
                        _attachmentPanel.Hide();
                }
            }
            else
            {
                // Slot empty → hide tooltip và panel
                if (_itemTooltip != null)
                    _itemTooltip.Hide();

                if (_attachmentPanel != null)
                    _attachmentPanel.Hide();
            }
        }

        private void OnSlotHoverExit(ItemSlotView slotView)
        {
            if (_hoveredSlot == slotView)
            {
                _hoveredSlot = null;

                // Hide tooltip khi hover ra
                if (_itemTooltip != null)
                    _itemTooltip.Hide();

                // FIX: Chỉ hide attachment panel nếu không pinned
                if (_attachmentPanel != null && !_attachmentPanel.IsPinned)
                    _attachmentPanel.Hide();
            }
        }

        private IAttachmentSystem GetAttachmentSystem()
        {
            var spectate = SpectateManager.Instance;
            if (spectate == null)
            {
                Debug.LogWarning("[InventoryScreen][Attachment] SpectateManager.Instance is NULL");
                return null;
            }

            var currentPlayer = spectate.GetCurrentPlayer();
            if (currentPlayer == null)
            {
                Debug.LogWarning("[InventoryScreen][Attachment] SpectateManager.GetCurrentPlayer() is NULL");
                return null;
            }

            var sys = ComponentResolver.Find<IAttachmentSystem>(currentPlayer)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IAttachmentSystem not found")
                .Resolve();
            Debug.Log(
                $"[InventoryScreen][Attachment] GetAttachmentSystem on '{currentPlayer.name}': {(sys != null ? sys.GetType().Name : "NULL — component missing on prefab!")}");
            return sys;
        }

        private IGameplayBridge GetGameplayBridge()
        {
            var spectate = SpectateManager.Instance;
            if (spectate == null) return null;

            var currentPlayer = spectate.GetCurrentPlayer();
            if (currentPlayer == null) return null;

            var bridge = currentPlayer.GamePlaySystemBridge;
            Debug.Log(
                $"[InventoryScreen][Attachment] GetGameplayBridge: {(bridge != null ? $"IsReady={bridge.IsReady}" : "NULL")}");
            return bridge;
        }

        #endregion
    }
}