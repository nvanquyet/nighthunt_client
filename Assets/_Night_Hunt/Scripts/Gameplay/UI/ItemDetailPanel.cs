using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Item detail panel - ALWAYS visible on the left
    /// Displays item stats and nested equipment slots
    /// </summary>
    public class ItemDetailPanel : MonoBehaviour
    {
        [Header("Item Info")]
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private Image itemIcon;

        [Header("Item Stats")]
        [SerializeField] private Transform statsContainer;
        [SerializeField] private GameObject statPrefab;

        [Header("Nested Equipment")]
        [SerializeField] private Transform nestedEquipmentContainer;
        [SerializeField] private GameObject nestedSlotPrefab;
        [SerializeField] private TextMeshProUGUI nestedEquipmentTitle;

        private InventoryPanel inventoryPanel;
        private InventorySlot currentSlot;
        private List<GameObject> statUIs = new List<GameObject>();
        private List<EquipmentSlotUI> nestedSlots = new List<EquipmentSlotUI>();
        private List<InventorySlotUI> nestedSlotUIs = new List<InventorySlotUI>(); // For drag & drop support

        /// <summary>
        /// Initialize item detail panel
        /// </summary>
        public void Initialize(InventoryPanel panel)
        {
            inventoryPanel = panel;
            // Panel is always visible, so we don't hide it
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Display item information
        /// </summary>
        public void DisplayItem(InventorySlot slot, bool isNestedEquipment = false)
        {
            currentSlot = slot;

            if (slot == null || slot.IsEmpty)
            {
                ClearDisplay();
                return;
            }

            // If this is nested equipment (attached to another item), don't show in detail panel
            // Only show tooltip on hover
            if (isNestedEquipment)
            {
                ClearDisplay();
                return;
            }

            var item = slot.Item;
            if (item == null)
            {
                ClearDisplay();
                return;
            }

            // Display basic info
            if (itemNameText != null)
            {
                itemNameText.text = item.DisplayName ?? item.ItemId;
            }

            if (itemDescriptionText != null)
            {
                // Build description from available data
                string description = $"Category: {item.Category ?? "Unknown"}\n";
                description += $"Rarity: {item.Rarity ?? "Common"}\n";
                if (!string.IsNullOrEmpty(item.EffectType))
                {
                    description += $"Effect: {item.EffectType}";
                    if (item.EffectValue > 0)
                    {
                        description += $" ({item.EffectValue})";
                    }
                }
                itemDescriptionText.text = description;
            }

            // TODO: Load item icon
            // if (itemIcon != null && item.Icon != null)
            // {
            //     itemIcon.sprite = item.Icon;
            //     itemIcon.enabled = true;
            // }

            // Display stats
            DisplayStats(item);

            // Display nested equipment if applicable
            DisplayNestedEquipment(item);
        }

        /// <summary>
        /// Display item stats
        /// </summary>
        private void DisplayStats(ItemConfigData item)
        {
            // Clear existing stats
            foreach (var statUI in statUIs)
            {
                if (statUI != null)
                {
                    Destroy(statUI);
                }
            }
            statUIs.Clear();

            if (statsContainer == null || statPrefab == null)
                return;

            // Add weight stat
            AddStat("Weight", $"{item.Weight:F1} kg");

            // Add effect stats if applicable
            if (!string.IsNullOrEmpty(item.EffectType))
            {
                AddStat("Effect", $"{item.EffectType}: {item.EffectValue}");
            }

            // Add other stats as needed
            if (item.MaxStack > 1)
            {
                AddStat("Stack Size", item.MaxStack.ToString());
            }
        }

        /// <summary>
        /// Add a stat display
        /// </summary>
        private void AddStat(string label, string value)
        {
            if (statPrefab == null || statsContainer == null)
                return;

            GameObject statObj = Instantiate(statPrefab, statsContainer);
            TextMeshProUGUI[] texts = statObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = label + ":";
                texts[1].text = value;
            }
            statUIs.Add(statObj);
        }

        /// <summary>
        /// Display nested equipment slots (attachments, mods, etc.)
        /// </summary>
        private void DisplayNestedEquipment(ItemConfigData item)
        {
            // Clear existing nested slots
            foreach (var slot in nestedSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            nestedSlots.Clear();

            foreach (var slotUI in nestedSlotUIs)
            {
                if (slotUI != null)
                {
                    Destroy(slotUI.gameObject);
                }
            }
            nestedSlotUIs.Clear();

            if (nestedEquipmentContainer == null || nestedSlotPrefab == null)
                return;

            // TODO: Check if item has nested equipment slots
            // This would require item config to have attachment/mod slots defined
            // For now, we'll check if it's a weapon type that might have attachments

            bool hasNestedSlots = false; // item.HasAttachments || item.HasMods;

            if (nestedEquipmentTitle != null)
            {
                nestedEquipmentTitle.gameObject.SetActive(hasNestedSlots);
            }

            if (hasNestedSlots)
            {
                // TODO: Create nested equipment slots based on item config
                // Example:
                // foreach (var attachmentSlot in item.AttachmentSlots)
                // {
                //     CreateNestedSlot(attachmentSlot);
                // }
            }
        }

        /// <summary>
        /// Create a nested equipment slot
        /// </summary>
        private void CreateNestedSlot(string slotName, EquipmentSlotType slotType, InventorySlot nestedSlot = null)
        {
            if (nestedSlotPrefab == null || nestedEquipmentContainer == null)
                return;

            GameObject slotObj = Instantiate(nestedSlotPrefab, nestedEquipmentContainer);
            
            // Use InventorySlotUI for nested slots to support drag & drop
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                slotUI = slotObj.AddComponent<InventorySlotUI>();
            }

            // Initialize as nested equipment slot (marked as nested so it won't show in detail panel when selected)
            if (nestedSlot != null)
            {
                slotUI.Initialize(nestedSlot, inventoryPanel, -2, nestedSlotUIs.Count, true); // -2 to distinguish from loot slots (-1)
                nestedSlotUIs.Add(slotUI);
            }
            
            // Also add EquipmentSlotUI for display
            EquipmentSlotUI equipUI = slotObj.GetComponent<EquipmentSlotUI>();
            if (equipUI == null)
            {
                equipUI = slotObj.AddComponent<EquipmentSlotUI>();
            }
            equipUI.Initialize(slotType, null, inventoryPanel);
            if (nestedSlot != null)
            {
                equipUI.UpdateSlot(nestedSlot);
            }
            
            nestedSlots.Add(equipUI);
        }

        /// <summary>
        /// Clear display
        /// </summary>
        private void ClearDisplay()
        {
            if (itemNameText != null)
            {
                itemNameText.text = "";
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = "Select an item to view details.";
            }

            if (itemIcon != null)
            {
                itemIcon.enabled = false;
            }

            // Clear stats
            foreach (var statUI in statUIs)
            {
                if (statUI != null)
                {
                    Destroy(statUI);
                }
            }
            statUIs.Clear();

            // Clear nested equipment
            foreach (var slot in nestedSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            nestedSlots.Clear();

            foreach (var slotUI in nestedSlotUIs)
            {
                if (slotUI != null)
                {
                    Destroy(slotUI.gameObject);
                }
            }
            nestedSlotUIs.Clear();
        }

        /// <summary>
        /// Get current displayed slot
        /// </summary>
        public InventorySlot GetCurrentSlot() => currentSlot;
    }
}
