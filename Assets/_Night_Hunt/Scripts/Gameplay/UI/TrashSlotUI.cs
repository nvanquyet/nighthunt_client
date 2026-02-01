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
        public void DropItem(ItemCell slotUI)
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
        public void DropItemFromQuickSlot(ItemCell quickSlot)
        {
            if (quickSlot == null || quickSlot.IsEmpty() || inventoryPanel == null)
                return;

            var slot = quickSlot.GetSlot();
            if (slot != null && !slot.IsEmpty)
            {
                inventoryPanel.DropItem(slot.Item.ItemId, slot.Quantity);
            }
        }

        /// <summary>
        /// Drop item from weapon slot
        /// </summary>
        public void DropItemFromWeaponSlot(ItemCell weaponSlot)
        {
            if (weaponSlot == null || weaponSlot.IsEmpty() || inventoryPanel == null)
                return;

            var slot = weaponSlot.GetSlot();
            if (slot != null && !slot.IsEmpty) 
            {
                inventoryPanel.DropItem(slot.Item.ItemId, slot.Quantity);
            }
        }

        /// <summary>
        /// Drop item from equipment slot
        /// </summary>
        public void DropItemFromEquipmentSlot(ItemCell equipmentSlot)
        {
            if (equipmentSlot == null || equipmentSlot.IsEmpty() || inventoryPanel == null)
                return;

            var slot = equipmentSlot.GetSlot();
            if (slot != null && !slot.IsEmpty)
            {
                inventoryPanel.DropItem(slot.Item.ItemId, slot.Quantity);
            }
        }
    }
}
