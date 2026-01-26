using UnityEngine;
using UnityEngine.UI;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.UI
{
    /// <summary>
    /// Example UI listener that listens to inventory events.
    /// This demonstrates how to use the event system without direct references.
    /// </summary>
    public class InventoryUIListener : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text weightText;
        [SerializeField] private Slider weightSlider;
        [SerializeField] private Text slotCountText;
        [SerializeField] private GameObject weightWarningPanel;
        [SerializeField] private GameObject weightExceededPanel;
        [SerializeField] private GameObject inventoryFullPanel;

        [Header("Settings")]
        [SerializeField] private float weightWarningThreshold = 0.8f; // 80%

        private void OnEnable()
        {
            // Subscribe to events
            InventoryEvents.OnWeightChanged += HandleWeightChanged;
            InventoryEvents.OnWeightWarning += HandleWeightWarning;
            InventoryEvents.OnWeightLimitReached += HandleWeightLimitReached;
            InventoryEvents.OnWeightLimitExceeded += HandleWeightLimitExceeded;
            InventoryEvents.OnSlotCountChanged += HandleSlotCountChanged;
            InventoryEvents.OnInventoryFull += HandleInventoryFull;
            InventoryEvents.OnInventorySpaceAvailable += HandleInventorySpaceAvailable;
            InventoryEvents.OnItemAdded += HandleItemAdded;
            InventoryEvents.OnItemRemoved += HandleItemRemoved;
            InventoryEvents.OnItemEquipped += HandleItemEquipped;
            InventoryEvents.OnItemUnequipped += HandleItemUnequipped;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            InventoryEvents.OnWeightChanged -= HandleWeightChanged;
            InventoryEvents.OnWeightWarning -= HandleWeightWarning;
            InventoryEvents.OnWeightLimitReached -= HandleWeightLimitReached;
            InventoryEvents.OnWeightLimitExceeded -= HandleWeightLimitExceeded;
            InventoryEvents.OnSlotCountChanged -= HandleSlotCountChanged;
            InventoryEvents.OnInventoryFull -= HandleInventoryFull;
            InventoryEvents.OnInventorySpaceAvailable -= HandleInventorySpaceAvailable;
            InventoryEvents.OnItemAdded -= HandleItemAdded;
            InventoryEvents.OnItemRemoved -= HandleItemRemoved;
            InventoryEvents.OnItemEquipped -= HandleItemEquipped;
            InventoryEvents.OnItemUnequipped -= HandleItemUnequipped;
        }

        private void HandleWeightChanged(float currentWeight, float maxWeight)
        {
            if (weightText != null)
            {
                weightText.text = $"{currentWeight:F1} / {maxWeight:F1} kg";
            }

            if (weightSlider != null)
            {
                weightSlider.value = maxWeight > 0 ? currentWeight / maxWeight : 0f;
            }
        }

        private void HandleWeightWarning(float currentWeight)
        {
            if (weightWarningPanel != null)
            {
                weightWarningPanel.SetActive(true);
            }
        }

        private void HandleWeightLimitReached(float currentWeight)
        {
            Debug.LogWarning($"[InventoryUI] Weight limit reached: {currentWeight} kg");
            // Show warning UI
        }

        private void HandleWeightLimitExceeded(float currentWeight)
        {
            if (weightExceededPanel != null)
            {
                weightExceededPanel.SetActive(true);
            }
            Debug.LogError($"[InventoryUI] Weight limit EXCEEDED: {currentWeight} kg");
        }

        private void HandleSlotCountChanged(int currentSlots, int maxSlots)
        {
            if (slotCountText != null)
            {
                slotCountText.text = $"{currentSlots} / {maxSlots}";
            }
        }

        private void HandleInventoryFull()
        {
            if (inventoryFullPanel != null)
            {
                inventoryFullPanel.SetActive(true);
            }
        }

        private void HandleInventorySpaceAvailable()
        {
            if (inventoryFullPanel != null)
            {
                inventoryFullPanel.SetActive(false);
            }
        }

        private void HandleItemAdded(ItemInstance item)
        {
            Debug.Log($"[InventoryUI] Item added: {item.itemDataId} x{item.quantity}");
            // Update UI, play sound, show notification, etc.
        }

        private void HandleItemRemoved(ItemInstance item, int quantityRemoved)
        {
            Debug.Log($"[InventoryUI] Item removed: {item.itemDataId} x{quantityRemoved}");
            // Update UI, play sound, etc.
        }

        private void HandleItemEquipped(EquipmentSlot slot, ItemInstance item)
        {
            Debug.Log($"[InventoryUI] Item equipped: {item.itemDataId} in slot {slot}");
            // Update equipment UI
        }

        private void HandleItemUnequipped(EquipmentSlot slot, ItemInstance item)
        {
            Debug.Log($"[InventoryUI] Item unequipped: {item.itemDataId} from slot {slot}");
            // Update equipment UI
        }
    }
}
