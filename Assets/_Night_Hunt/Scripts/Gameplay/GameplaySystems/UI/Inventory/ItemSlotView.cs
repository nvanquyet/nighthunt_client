using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Pure UI view for an item/equipment/weapon slot.
    /// Renders from <see cref="UISlotState"/> — contains no gameplay logic.
    /// Selection highlight is managed externally via <see cref="SetSelectedVisual"/>.
    /// Spectator read-only mode is controlled via <see cref="SetLockedVisual"/>.
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

        [Header("Spectator Lock")]
        [Tooltip("Optional semi-transparent overlay shown in spectator (read-only) mode. " +
                 "If null: slot is only visually dimmed via CanvasGroup alpha.")]
        [SerializeField] private GameObject _lockedOverlay;
        [Tooltip("Alpha applied to this slot when in spectator read-only mode.")]
        [SerializeField] [Range(0.1f, 1f)] private float _lockedAlpha = 0.5f;

        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;

        public UISlotId   SlotId             { get; private set; }
        public UISlotState State             { get; private set; }
        public bool       IsSelectedVisually { get; private set; }
        public bool       IsLocked           { get; private set; }

        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(UISlotLayoutConfig uiConfig, UISlotId id)
        {
            _uiConfig = uiConfig;
            SlotId    = id;
            SetEmptyState();
        }

        public void Initialize(UISlotId id)
        {
            SlotId = id;
            SetEmptyState();
        }

        // ─────────────────────────────────────────────────────────────────────
        #region State

        public void SetState(UISlotState state)
        {
            State = state;

            if (state == null || (state.Item == null && state.Icon == null))
            {
                SetEmptyState();
                return;
            }

            if (_icon != null)
            {
                _icon.enabled = state.Icon != null;
                _icon.sprite  = state.Icon;
            }

            if (_background != null)
                _background.color = state.BackgroundColor;

            // Stack count
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

            // Selection frame managed externally via SetSelectedVisual — do not touch here.

            ApplyLockAlpha();
        }

        public void SetEmptyState()
        {
            State = new UISlotState();

            Sprite defaultIcon = null;
            if (_uiConfig != null && _uiConfig.InventoryConfig != null)
            {
                defaultIcon = _uiConfig.InventoryConfig.GetDefaultEmptyIcon(
                    SlotId.Type,
                    SlotId.EquipmentSlot,
                    SlotId.WeaponSlot);
            }
            else
            {
                Debug.LogWarning($"[ItemSlotView] SetEmptyState: Config is null! SlotType={SlotId.Type}");
            }

            if (_icon != null)
            {
                _icon.sprite  = null;
                _icon.enabled = false;
                if (defaultIcon != null)
                {
                    _icon.sprite  = defaultIcon;
                    _icon.enabled = true;
                }
            }
            else
            {
                Debug.LogWarning($"[ItemSlotView] SetEmptyState: _icon is null!");
            }

            if (_background != null && _uiConfig != null && _uiConfig.DefaultBackground != null)
                _background.sprite = _uiConfig.DefaultBackground;

            if (_stackObj != null)
                _stackObj.SetActive(false);
            else if (_stackText != null)
                _stackText.text = string.Empty;

            if (_highlightFrame != null)
                _highlightFrame.enabled = false;

            SetSelectedVisual(false);
            ApplyLockAlpha();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Visual Controls

        /// <summary>
        /// Toggles the selection frame overlay (driven by InventoryScreen, not by item data).
        /// No-op when the slot is locked (spectator mode).
        /// </summary>
        public void SetSelectedVisual(bool selected)
        {
            IsSelectedVisually = selected && !IsLocked;
            if (_selectedFrame != null)
                _selectedFrame.enabled = IsSelectedVisually;
        }

        /// <summary>
        /// Dims the slot and blocks raycasts in spectator (read-only) mode.
        /// Pass <c>true</c> when viewing another player's inventory.
        /// Pass <c>false</c> to restore normal interactive state.
        /// </summary>
        public void SetLockedVisual(bool locked)
        {
            IsLocked = locked;

            if (_lockedOverlay != null)
                _lockedOverlay.SetActive(locked);

            ApplyLockAlpha();

            // Clear selection when locking — cannot be selected in spectator mode.
            if (locked)
                SetSelectedVisual(false);
        }

        /// <summary>
        /// Temporarily hides the slot icon/item during a drag (ghost represents the item).
        /// Call <see cref="SetState"/> or <see cref="SetEmptyState"/> to restore.
        /// </summary>
        public void SetHiddenForDrag()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void ApplyLockAlpha()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = IsLocked ? _lockedAlpha : 1f;
                _canvasGroup.blocksRaycasts = !IsLocked;
            }
        }

        #endregion
    }
}
