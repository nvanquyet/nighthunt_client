using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Pure UI view for an item/equipment/weapon slot.
    /// Renders from <see cref="UISlotState"/> – contains no gameplay logic.
    /// Selection highlight is managed externally via <see cref="SetSelectedVisual"/>.
    /// </summary>
    public class ItemSlotView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image _icon;
        [SerializeField] private Image _background;
        [SerializeField] private Image _highlightFrame;
        [SerializeField] private Image _selectedFrame;
        [SerializeField] private TextMeshProUGUI _stackText;
        [SerializeField] private GameObject _stackObj;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;

        public UISlotId SlotId { get; private set; }
        public UISlotState State { get; private set; }
        public bool IsSelectedVisually { get; private set; }

        /// <summary>
        /// Initialize với UI config và slot id
        /// </summary>
        public void Initialize(UISlotLayoutConfig uiConfig, UISlotId id)
        {
            _uiConfig = uiConfig;
            SlotId = id;
            SetEmptyState();
        }

        /// <summary>
        /// Initialize chỉ với slot id (nếu config đã được set từ bên ngoài)
        /// </summary>
        public void Initialize(UISlotId id)
        {
            SlotId = id;
            SetEmptyState();
        }

        public void SetState(UISlotState state)
        {
            State = state;

            // Chỉ coi là empty nếu không có item và cũng không có icon
            if (state == null || (state.Item == null && state.Icon == null))
            {
                SetEmptyState();
                return;
            }

            if (_icon != null)
            {
                _icon.enabled = state.Icon != null;
                _icon.sprite = state.Icon;
            }

            if (_background != null)
            {
                _background.color = state.BackgroundColor;
            }

        // Show/hide stack count
        if (_stackObj != null)
        {
            bool showStack = state.StackCount > 1;
            _stackObj.SetActive(showStack);
            if (showStack && _stackText != null)
                _stackText.text = state.StackCount.ToString();
        }
        else if (_stackText != null)
        {
            _stackText.text = state.StackCount > 1 ? state.StackCount.ToString() : string.Empty;
        }

            if (_highlightFrame != null)
                _highlightFrame.enabled = state.IsHighlight || state.IsValidDropTarget;

            // Selection frame is managed externally via SetSelectedVisual – do not touch it here.

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void SetEmptyState()
        {
            State = new UISlotState();

            // Set default icon dựa trên slot type
            Sprite defaultIcon = null;
            if (_uiConfig != null && _uiConfig.InventoryConfig != null)
            {
                defaultIcon = _uiConfig.InventoryConfig.GetDefaultEmptyIcon(
                    SlotId.Type, 
                    SlotId.EquipmentSlot, 
                    SlotId.WeaponSlot
                );
            }
            else
            {
                Debug.LogWarning($"[ItemSlotView] SetEmptyState: Config is null! SlotType={SlotId.Type}");
            }

            if (_icon != null)
            {
                // DropArea (trash slot): giữ nguyên icon đã set trong prefab, không clear
                if (SlotId.Type == UISlotType.DropArea)
                {
                    // Chỉ enable icon nếu đã có sprite (set trong prefab)
                    if (_icon.sprite != null)
                    {
                        _icon.enabled = true;
                    }
                    // Không clear icon cho trash slot
                }
                else
                {
                    // Các slot khác (bao gồm Attachment): clear icon và set default
                    _icon.sprite = null;
                    _icon.enabled = false;
                    
                    // Sau đó set default icon nếu có
                    if (defaultIcon != null)
                    {
                        _icon.sprite = defaultIcon;
                        _icon.enabled = true;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[ItemSlotView] SetEmptyState: _icon is null!");
            }

            // Set default background khi empty
            if (_background != null && _uiConfig != null && _uiConfig.DefaultBackground != null)
            {
                _background.sprite = _uiConfig.DefaultBackground;
            }

            // Ẩn stack object mặc định khi empty
            if (_stackObj != null)
            {
                _stackObj.SetActive(false);
            }
            else if (_stackText != null)
            {
                // Fallback: nếu không có stackObj thì chỉ clear text
                _stackText.text = string.Empty;
            }

            if (_highlightFrame != null)
                _highlightFrame.enabled = false;

            // Clear selection visual when slot becomes empty.
            SetSelectedVisual(false);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// Toggles the selection frame overlay (driven by InventoryScreen, not by item data).
        /// </summary>
        public void SetSelectedVisual(bool selected)
        {
            IsSelectedVisually = selected;
            if (_selectedFrame != null)
                _selectedFrame.enabled = selected;
        }

        /// <summary>
        /// Được gọi khi slot tạm thời bị clear visual trong lúc drag.
        /// </summary>
        public void SetHiddenForDrag()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }
}

