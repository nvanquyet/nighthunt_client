using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Individual weapon slot UI (Primary/Secondary).
    /// Shows weapon, ammo, active state, supports drag & drop.
    /// </summary>
    public class WeaponSlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private TextMeshProUGUI ammoText;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private Button switchButton;
        [SerializeField] private Button unequipButton;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color activeColor = new Color(0.4f, 0.6f, 0.4f, 1f);
        
        private ItemInstance currentWeapon;
        private WeaponSlotType slotType;
        private WeaponPanelUI parentPanel;
        private bool isActive;
        
        #region Initialization
        
        public void Initialize(WeaponSlotType type, WeaponPanelUI panel)
        {
            slotType = type;
            parentPanel = panel;
            
            if (slotNameText != null)
            {
                slotNameText.text = type.ToString();
            }
            
            if (switchButton != null)
            {
                switchButton.onClick.AddListener(OnSwitchClicked);
                switchButton.gameObject.SetActive(false);
            }
            
            if (unequipButton != null)
            {
                unequipButton.onClick.AddListener(OnUnequipClicked);
                unequipButton.gameObject.SetActive(false);
            }
            
            SetWeapon(null);
            SetActiveState(false);
        }
        
        public void SetWeapon(ItemInstance weapon)
        {
            currentWeapon = weapon;
            
            if (weapon != null)
            {
                // Show weapon
                if (weaponIcon != null)
                {
                    weaponIcon.sprite = weapon.Definition.Icon;
                    weaponIcon.enabled = true;
                }
                
                if (slotBackground != null)
                {
                    slotBackground.color = isActive ? activeColor : occupiedColor;
                }
                
                UpdateAmmoDisplay(weapon.CurrentAmmo);
                
                if (switchButton != null)
                {
                    switchButton.gameObject.SetActive(true);
                }
                
                if (unequipButton != null)
                {
                    unequipButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // Empty slot
                if (weaponIcon != null)
                {
                    weaponIcon.enabled = false;
                }
                
                if (slotBackground != null)
                {
                    slotBackground.color = emptyColor;
                }
                
                if (ammoText != null)
                {
                    ammoText.text = "";
                }
                
                if (switchButton != null)
                {
                    switchButton.gameObject.SetActive(false);
                }
                
                if (unequipButton != null)
                {
                    unequipButton.gameObject.SetActive(false);
                }
            }
        }
        
        public void SetActiveState(bool active)
        {
            isActive = active;
            
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(active);
            }
            
            if (slotBackground != null && currentWeapon != null)
            {
                slotBackground.color = active ? activeColor : occupiedColor;
            }
        }
        
        public void UpdateAmmoDisplay(int ammo)
        {
            if (ammoText != null)
            {
                ammoText.text = currentWeapon != null ? $"{ammo}" : "";
            }
        }
        
        public ItemInstance GetWeapon() => currentWeapon;
        
        #endregion
        
        #region Drag & Drop
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (currentWeapon == null) return;
            
            var context = new DragContext
            {
                SourceLocation = SlotLocationType.Weapon,
                SourceIndex = (int)slotType,
                ItemInstance = currentWeapon
            };
            
            DragDropEvents.InvokeBeginDrag(context);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            DragDropEvents.InvokeDragging(eventData.position);
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            DragDropEvents.InvokeEndDrag();
        }
        
        public void OnDrop(PointerEventData eventData)
        {
            var draggedCell = eventData.pointerDrag?.GetComponent<UI.Cells.InventoryCellUI>();
            if (draggedCell == null || draggedCell.GetItemData() == null)
            {
                return;
            }
            
            parentPanel.OnItemDroppedOnSlot(draggedCell.GetItemData(), slotType);
            
            var dropContext = new DragContext
            {
                SourceLocation = draggedCell.GetLocationType(),
                SourceIndex = draggedCell.GetSlotIndex(),
                TargetLocation = SlotLocationType.Weapon,
                TargetIndex = (int)slotType,
                ItemInstance = draggedCell.GetItemData()
            };
            
            DragDropEvents.InvokeDrop(dropContext);
        }
        
        #endregion
        
        #region Tooltip
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentWeapon != null)
            {
                TooltipEvents.InvokeShowTooltip(currentWeapon, transform.position);
            }
            else
            {
                TooltipEvents.InvokeShowSlotInfo(SlotLocationType.Weapon, (int)slotType, transform.position);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipEvents.InvokeHideTooltip();
        }
        
        #endregion
        
        #region Click Handlers
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && currentWeapon != null)
            {
                OnUnequipClicked();
            }
        }
        
        private void OnSwitchClicked()
        {
            if (!isActive)
            {
                parentPanel.OnSwitchRequested(slotType);
            }
        }
        
        private void OnUnequipClicked()
        {
            parentPanel.OnUnequipRequested(slotType);
        }
        
        #endregion
    }
}