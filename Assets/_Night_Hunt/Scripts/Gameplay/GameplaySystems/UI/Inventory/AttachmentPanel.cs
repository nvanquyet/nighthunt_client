using UnityEngine;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Panel hiển thị attachment slots của một equipment item.
    /// Show khi hover/select equipment item có attachment slots.
    /// </summary>
    public class AttachmentPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform _slotsRoot;
        [SerializeField] private GameObject _panelRoot;
        
        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;
        
        [Header("References")]
        [SerializeField] private MonoBehaviour _attachmentSystemComponent;
        [SerializeField] private MonoBehaviour _gameplayBridgeComponent;
        
        private IAttachmentSystem _attachmentSystem;
        private IGameplayBridge _gameplayBridge;
        private ItemInstance _currentItem;
        private readonly Dictionary<int, ItemSlotView> _attachmentSlots = new Dictionary<int, ItemSlotView>();
        
        // Pin state để giữ panel mở khi hover ra
        private bool _isPinned = false;
        private ItemInstance _pinnedItem;
        
        private void Awake()
        {
            if (_attachmentSystemComponent != null)
                _attachmentSystem = _attachmentSystemComponent as IAttachmentSystem;
            
            if (_gameplayBridgeComponent != null)
                _gameplayBridge = _gameplayBridgeComponent as IGameplayBridge;
        }
        
        public void Initialize(UISlotLayoutConfig uiConfig, IAttachmentSystem attachmentSystem, IGameplayBridge gameplayBridge)
        {
            _uiConfig = uiConfig;
            _attachmentSystem = attachmentSystem;
            _gameplayBridge = gameplayBridge;
        }
        
        /// <summary>
        /// Show attachment panel cho equipment item
        /// </summary>
        public void Show(ItemInstance equipmentItem)
        {
            if (equipmentItem == null)
            {
                Hide();
                return;
            }
            
            var def = ItemDatabase.GetDefinition(equipmentItem.DefinitionID);
            if (def == null || def.AttachmentSlots == null || def.AttachmentSlots.Length == 0)
            {
                Hide();
                return;
            }
            
            _currentItem = equipmentItem;
            
            // Nếu đang pinned với item khác, unpin
            if (_isPinned && _pinnedItem != null && _pinnedItem.InstanceID != equipmentItem.InstanceID)
            {
                _isPinned = false;
                _pinnedItem = null;
            }
            
            BuildAttachmentSlots(def, equipmentItem);
            
            if (_panelRoot != null)
                _panelRoot.SetActive(true);
        }
        
        /// <summary>
        /// Hide attachment panel (chỉ hide nếu không pinned)
        /// </summary>
        public void Hide()
        {
            // Không hide nếu đang pinned
            if (_isPinned) return;
            
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            
            ClearSlots();
            _currentItem = null;
        }
        
        /// <summary>
        /// Toggle pin state - giữ panel mở khi hover ra
        /// </summary>
        public void TogglePin()
        {
            _isPinned = !_isPinned;
            if (_isPinned)
            {
                _pinnedItem = _currentItem;
            }
            else
            {
                _pinnedItem = null;
                // Auto hide nếu không còn hover
                if (_currentItem == null)
                {
                    if (_panelRoot != null)
                        _panelRoot.SetActive(false);
                    ClearSlots();
                }
            }
        }
        
        /// <summary>
        /// Check if panel is pinned
        /// </summary>
        public bool IsPinned => _isPinned;
        
        /// <summary>
        /// Build attachment slots từ item definition
        /// </summary>
        private void BuildAttachmentSlots(ItemDefinition itemDef, ItemInstance item)
        {
            ClearSlots();
            
            if (_slotsRoot == null || _uiConfig == null) return;
            
            var prefab = _uiConfig.GetSlotPrefab(UISlotType.Attachment);
            if (prefab == null) return;
            
            for (int i = 0; i < itemDef.AttachmentSlots.Length; i++)
            {
                var go = Instantiate(prefab, _slotsRoot);
                var view = go.GetComponent<ItemSlotView>();
                
                if (view != null)
                {
                    var id = UISlotId.Attachment(item.InstanceID, i);
                    view.Initialize(_uiConfig, id);
                    
                    // Set default icon khi empty (dùng AttachmentUI trong InventoryConfig)
                    Sprite defaultIcon = null;
                    if (_uiConfig != null && _uiConfig.InventoryConfig != null)
                    {
                        defaultIcon = _uiConfig.InventoryConfig.GetDefaultEmptyIcon(UISlotType.Attachment);
                    }
                    if (defaultIcon != null)
                    {
                        var emptyState = new UISlotState
                        {
                            Icon = defaultIcon,
                            BackgroundColor = Color.white
                        };
                        view.SetState(emptyState);
                    }
                    
                    // Update với attachment hiện tại nếu có
                    if (_attachmentSystem != null)
                    {
                        var attachment = _attachmentSystem.GetAttachment(item.InstanceID, i);
                        if (attachment != null)
                        {
                            UpdateAttachmentSlot(i, attachment);
                        }
                    }
                    
                    _attachmentSlots[i] = view;
                    DragDropController.Instance?.RegisterSlotView(view);
                }
            }
        }
        
        /// <summary>
        /// Update attachment slot với attachment data
        /// </summary>
        public void UpdateAttachmentSlot(int slotIndex, ItemInstance attachment)
        {
            if (_attachmentSlots.TryGetValue(slotIndex, out var view))
            {
                if (attachment == null)
                {
                    // Set empty state với default icon từ InventoryConfig.AttachmentUI
                    Sprite defaultIcon = null;
                    if (_uiConfig != null && _uiConfig.InventoryConfig != null)
                    {
                        defaultIcon = _uiConfig.InventoryConfig.GetDefaultEmptyIcon(UISlotType.Attachment);
                    }
                    var emptyState = new UISlotState
                    {
                        Icon = defaultIcon,
                        BackgroundColor = Color.white
                    };
                    view.SetState(emptyState);
                }
                else
                {
                    // Set state với attachment data
                    var def = ItemDatabase.GetDefinition(attachment.DefinitionID);
                    var state = new UISlotState
                    {
                        Item = attachment,
                        Icon = def != null ? def.Icon : null,
                        BackgroundColor = Color.white,
                        StackCount = attachment.Quantity
                    };
                    view.SetState(state);
                }
            }
        }
        
        /// <summary>
        /// Get slot type tại index
        /// </summary>
        private AttachmentSlotType GetSlotType(int slotIndex)
        {
            if (_currentItem == null) return AttachmentSlotType.None;
            
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            if (def == null || def.AttachmentSlots == null) return AttachmentSlotType.None;
            
            if (slotIndex < 0 || slotIndex >= def.AttachmentSlots.Length) return AttachmentSlotType.None;
            
            return def.AttachmentSlots[slotIndex];
        }
        
        /// <summary>
        /// Clear all slots
        /// </summary>
        private void ClearSlots()
        {
            foreach (var slot in _attachmentSlots.Values)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _attachmentSlots.Clear();
        }
        
        /// <summary>
        /// Refresh all slots với data hiện tại
        /// </summary>
        public void Refresh()
        {
            if (_currentItem == null || _attachmentSystem == null) return;
            
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            if (def == null || def.AttachmentSlots == null) return;
            
            for (int i = 0; i < def.AttachmentSlots.Length; i++)
            {
                var attachment = _attachmentSystem.GetAttachment(_currentItem.InstanceID, i);
                UpdateAttachmentSlot(i, attachment);
            }
        }
    }
}
