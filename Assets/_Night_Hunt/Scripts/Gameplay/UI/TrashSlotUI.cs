using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component for trash slot (drop items)
    /// </summary>
    public class TrashSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image trashIcon;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private GameObject highlightIndicator;

        private InventoryPanel inventoryPanel;
        private bool isHighlighted = false;

        /// <summary>
        /// Initialize trash slot
        /// </summary>
        public void Initialize(InventoryPanel panel)
        {
            inventoryPanel = panel;

            if (labelText != null)
            {
                labelText.text = "Drop";
            }
        }

        /// <summary>
        /// Set highlight state (when item is being dragged over)
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            if (highlightIndicator != null)
            {
                highlightIndicator.SetActive(highlighted);
            }
        }

        /// <summary>
        /// Handle drop item
        /// </summary>
        public void DropItem(InventorySlotUI slotUI)
        {
            if (slotUI == null || slotUI.IsEmpty() || inventoryPanel == null)
                return;

            var slot = slotUI.GetSlot();
            if (slot != null && !slot.IsEmpty)
            {
                // Notify inventory panel to drop item
                inventoryPanel.DropItem(slot.Item.ItemId, slot.Quantity);
            }
        }

        /// <summary>
        /// Drop item from quick slot
        /// </summary>
        public void DropItemFromQuickSlot(QuickSlotUI quickSlot)
        {
            if (quickSlot == null || inventoryPanel == null)
                return;

            var itemInstance = quickSlot.GetSlot();
            if (itemInstance.HasValue && itemInstance.Value.IsValid())
            {
                inventoryPanel.DropItem(itemInstance.Value.itemDataId, itemInstance.Value.quantity);
            }
        }

        /// <summary>
        /// Drop item from weapon slot
        /// </summary>
        public void DropItemFromWeaponSlot(WeaponSlotUI weaponSlot)
        {
            if (weaponSlot == null || inventoryPanel == null)
                return;

            var slot = weaponSlot.GetWeaponSlot();
            if (slot != null && !slot.IsEmpty)
            {
                inventoryPanel.DropItem(slot.Item.ItemId, slot.Quantity);
            }
        }

        /// <summary>
        /// Drop item from equipment slot
        /// </summary>
        public void DropItemFromEquipmentSlot(EquipmentSlotUI equipmentSlot)
        {
            if (equipmentSlot == null || inventoryPanel == null)
                return;

            var slot = equipmentSlot.GetSlot();
            if (slot != null && !slot.IsEmpty)
            {
                inventoryPanel.DropItem(slot.Item.ItemId, slot.Quantity);
            }
        }
    }
}
