using NightHunt.InteractionSystem.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightHunt.InteractionSystem.Loot
{
    // Slot UI component
    public class LootItemSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum SlotType
        {
            Container,
            PlayerInventory
        }

        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI quantityText;

        private ItemInstance? currentItem;
        private SlotType slotType;
        private int slotIndex;

        public void Initialize(SlotType type, int index)
        {
            slotType = type;
            slotIndex = index;
        }

        public void SetItem(ItemInstance item)
        {
            currentItem = item;

            // TODO: Load icon from ItemData
            iconImage.enabled = true;
            quantityText.text = item.quantity > 1 ? item.quantity.ToString() : "";
        }

        public void Clear()
        {
            currentItem = null;
            iconImage.enabled = false;
            quantityText.text = "";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!currentItem.HasValue) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Quick transfer
                OnQuickTransfer();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!currentItem.HasValue) return;

            // TODO: Start drag visual
        }

        public void OnDrag(PointerEventData eventData)
        {
            // TODO: Update drag visual
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // TODO: Handle drop
        }

        private void OnQuickTransfer()
        {
            if (!currentItem.HasValue) return;

            // Transfer from container to inventory or vice versa
            LootTransferHandler.Instance?.TransferItem(currentItem.Value, slotType);
        }
    }
}