using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Tooltip UI that appears when hovering over items or selecting items
    /// Shows item name, stats, and equipped items icons
    /// Supports nested tooltips for equipment icons
    /// </summary>
    public class ItemTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")] [SerializeField]
        private GameObject tooltipPanel;

        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemStatsText;
        [SerializeField] private Transform equippedItemsContainer;
        [SerializeField] private GameObject equippedItemIconPrefab;

        [Header("Settings")] [SerializeField] private float showDelay = 0.3f;
        [SerializeField] private Vector2 offset = new Vector2(10, -10);
        [SerializeField] private bool persistOnHover = true; // Tooltip doesn't disappear when mouse enters tooltip

        private Canvas canvas;
        private RectTransform rectTransform;
        private InventorySlot currentSlot;
        private float hoverTime = 0f;
        private bool isShowing = false;
        private bool isMouseOverTooltip = false; // Track if mouse is over tooltip panel
        private List<GameObject> equippedItemIcons = new List<GameObject>(); // Track created icons for cleanup
        private ItemTooltip nestedTooltip; // For nested tooltips (tooltip of tooltip)

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            rectTransform = GetComponent<RectTransform>();
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);

                // Add EventTrigger to tooltip panel to detect mouse enter/exit
                if (persistOnHover)
                {
                    EventTrigger trigger = tooltipPanel.GetComponent<EventTrigger>();
                    if (trigger == null)
                    {
                        trigger = tooltipPanel.AddComponent<EventTrigger>();
                    }

                    // Pointer Enter
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => { OnPointerEnter((PointerEventData)data); });
                    trigger.triggers.Add(enterEntry);

                    // Pointer Exit
                    EventTrigger.Entry exitEntry = new EventTrigger.Entry();
                    exitEntry.eventID = EventTriggerType.PointerExit;
                    exitEntry.callback.AddListener((data) => { OnPointerExit((PointerEventData)data); });
                    trigger.triggers.Add(exitEntry);
                }
            }
        }

        private void Update()
        {
            if (isShowing && tooltipPanel != null)
            {
                // Update position to follow mouse
                UpdatePosition();
            }
        }


        /// <summary>
        /// Show tooltip for empty attachment slot
        /// ADD this method to ItemTooltip.cs
        /// </summary>
        public void ShowAttachmentSlotTooltip(AttachmentSlotType slotType, Vector2 screenPosition)
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            string slotName = GetAttachmentSlotName(slotType);
            string slotDescription = GetAttachmentSlotDescription(slotType);

            // Update tooltip text
            if (itemNameText != null)
            {
                itemNameText.text = slotName;
            }

            if (itemStatsText != null)
            {
                itemStatsText.text = slotDescription;
            }

            // Show tooltip panel
            isShowing = true;
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);
            }

            UpdatePosition();
        }

        /// <summary>
        /// Get attachment slot name
        /// </summary>
        private string GetAttachmentSlotName(AttachmentSlotType slotType)
        {
            return slotType switch
            {
                AttachmentSlotType.Scope => "Optic Sight Slot",
                AttachmentSlotType.Barrel => "Barrel Slot",
                AttachmentSlotType.Grip => "Foregrip Slot",
                AttachmentSlotType.Magazine => "Magazine Slot",
                AttachmentSlotType.Stock => "Stock Slot",
                _ => "Attachment Slot"
            };
        }

        /// <summary>
        /// Get attachment slot description
        /// </summary>
        private string GetAttachmentSlotDescription(AttachmentSlotType slotType)
        {
            return slotType switch
            {
                AttachmentSlotType.Scope =>
                    "Attach optical sights for better accuracy.\nAccepts: Red Dot, Holographic, ACOG, Sniper Scopes",
                AttachmentSlotType.Barrel =>
                    "Modify barrel for different performance.\nAccepts: Suppressors, Extended Barrels, Compensators",
                AttachmentSlotType.Grip =>
                    "Attach foregrips to reduce recoil.\nAccepts: Vertical Grip, Angled Grip, Bipod",
                AttachmentSlotType.Magazine =>
                    "Change magazine for different capacity.\nAccepts: Standard, Extended, Drum Magazines",
                AttachmentSlotType.Stock =>
                    "Modify stock for better handling.\nAccepts: Tactical Stock, Lightweight Stock",
                _ => "Drag compatible attachments here to equip them."
            };
        }


        /// <summary>
        /// Show tooltip for empty slot (equipment, weapon, quick slot, attachment)
        /// </summary>
        public void ShowSlotTooltip(ItemCell slotCell, Vector2 screenPosition)
        {
            if (slotCell == null)
            {
                HideTooltip();
                return;
            }

            // Ensure gameObject is active in hierarchy before starting coroutine
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"[ItemTooltip] ShowSlotTooltip called but gameObject '{gameObject.name}' is inactive in hierarchy! Activating...");
                gameObject.SetActive(true);

                if (!gameObject.activeInHierarchy)
                {
                    Debug.LogError(
                        $"[ItemTooltip] Cannot activate GameObject '{gameObject.name}' - parent hierarchy might be inactive!");
                    return;
                }
            }

            currentSlot = null; // No item slot for empty slots
            hoverTime = 0f;

            // Start showing after delay
            if (!isShowing)
            {
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(ShowSlotTooltipAfterDelay(slotCell, screenPosition));
                }
            }
            else
            {
                UpdateSlotTooltipContent(slotCell);
                UpdatePosition();
            }
        }

        /// <summary>
        /// Show slot tooltip after delay
        /// </summary>
        private System.Collections.IEnumerator ShowSlotTooltipAfterDelay(ItemCell slotCell, Vector2 screenPosition)
        {
            while (hoverTime < showDelay)
            {
                hoverTime += Time.deltaTime;
                yield return null;
            }

            if (slotCell != null)
            {
                isShowing = true;
                if (tooltipPanel != null)
                {
                    tooltipPanel.SetActive(true);
                }

                UpdateSlotTooltipContent(slotCell);
                UpdatePosition();
            }
        }

        /// <summary>
        /// Update slot tooltip content for empty slots
        /// </summary>
        private void UpdateSlotTooltipContent(ItemCell slotCell)
        {
            if (slotCell == null)
                return;

            var location = slotCell.GetLocation();
            string slotName = "";
            string slotDescription = "";

            switch (location)
            {
                case ItemCellLocation.Weapon:
                    int weaponIndex = slotCell.GetCellIndex();
                    slotName = weaponIndex == 0 ? "Primary Weapon Slot" : "Secondary Weapon Slot";
                    slotDescription = "Drag a weapon here to equip it.\nOnly weapons can be equipped to this slot.";
                    break;

                case ItemCellLocation.Equipment:
                    var slotType = slotCell.GetEquipmentSlotType();
                    slotName = GetEquipmentSlotName(slotType);
                    slotDescription = GetEquipmentSlotDescription(slotType);
                    break;

                case ItemCellLocation.QuickSlot:
                    int quickIndex = slotCell.GetCellIndex();
                    slotName = $"Quick Slot {quickIndex + 1}";
                    slotDescription =
                        "Drag consumable items here for quick access.\nOnly consumable items can be assigned to quick slots.";
                    break;

                case ItemCellLocation.Attachment:
                    slotName = "Attachment Slot";
                    slotDescription =
                        "Drag attachments here to attach them to this item.\nOnly compatible attachments can be attached.";
                    break;

                default:
                    return; // Don't show tooltip for other locations
            }

            // Update tooltip text
            if (itemNameText != null)
            {
                itemNameText.text = slotName;
            }

            if (itemStatsText != null)
            {
                itemStatsText.text = slotDescription;
            }
        }

        /// <summary>
        /// Get equipment slot name
        /// </summary>
        private string GetEquipmentSlotName(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.Backpack => "Backpack Slot",
                EquipmentSlotType.Armor => "Armor Slot",
                EquipmentSlotType.Helmet => "Helmet Slot",
                EquipmentSlotType.Vest => "Vest Slot",
                _ => "Equipment Slot"
            };
        }

        /// <summary>
        /// Get equipment slot description
        /// </summary>
        private string GetEquipmentSlotDescription(EquipmentSlotType slotType)
        {
            string itemType = slotType switch
            {
                EquipmentSlotType.Backpack => "backpack",
                EquipmentSlotType.Armor => "armor",
                EquipmentSlotType.Helmet => "helmet",
                EquipmentSlotType.Vest => "vest",
                _ => "equipment"
            };

            return $"Drag {itemType} items here to equip them.\nOnly {itemType} items can be equipped to this slot.";
        }

        /// <summary>
        /// Show tooltip for item (legacy InventorySlot wrapper).
        /// Under the hood this uses ItemConfigData from GameConfigLoader,
        /// so data still comes from the central JSON config.
        /// </summary>
        public void ShowTooltip(InventorySlot slot, Vector2 screenPosition)
        {
            if (slot == null || slot.IsEmpty)
            {
                HideTooltip();
                return;
            }

            // Ensure gameObject is active in hierarchy before starting coroutine
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"[ItemTooltip] ShowTooltip called but gameObject '{gameObject.name}' is inactive in hierarchy! Activating...");
                // Try to activate the GameObject
                gameObject.SetActive(true);

                // Check again after activation
                if (!gameObject.activeInHierarchy)
                {
                    Debug.LogError(
                        $"[ItemTooltip] Cannot activate GameObject '{gameObject.name}' - parent hierarchy might be inactive!");
                    return;
                }
            }

            currentSlot = slot;
            hoverTime = 0f;

            // Start showing after delay
            if (!isShowing)
            {
                // Double-check before starting coroutine
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(ShowAfterDelay(screenPosition));
                }
                else
                {
                    Debug.LogError(
                        $"[ItemTooltip] Cannot start coroutine - GameObject '{gameObject.name}' is not active in hierarchy!");
                }
            }
            else
            {
                UpdateTooltipContent();
                UpdatePosition();
            }
        }

        /// <summary>
        /// Hide tooltip
        /// </summary>
        public void HideTooltip()
        {
            hoverTime = 0f;
            isShowing = false;
            isMouseOverTooltip = false;
            currentSlot = null;

            // Hide nested tooltip if exists
            if (nestedTooltip != null)
            {
                nestedTooltip.HideTooltip();
                nestedTooltip = null;
            }

            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        /// <summary>
        /// IPointerEnterHandler - Mouse entered tooltip panel
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (persistOnHover)
            {
                isMouseOverTooltip = true;
                Debug.Log("[ItemTooltip] Mouse entered tooltip panel - keeping tooltip visible");
            }
        }

        /// <summary>
        /// IPointerExitHandler - Mouse exited tooltip panel
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (persistOnHover)
            {
                isMouseOverTooltip = false;
                Debug.Log("[ItemTooltip] Mouse exited tooltip panel - hiding tooltip");
                // Hide tooltip after a small delay to allow moving to nested tooltip
                StartCoroutine(HideAfterDelay(0.1f));
            }
        }

        /// <summary>
        /// Hide tooltip after delay (allows moving to nested tooltip)
        /// </summary>
        private System.Collections.IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Only hide if mouse is still not over tooltip
            if (!isMouseOverTooltip && !isShowing)
            {
                HideTooltip();
            }
        }

        /// <summary>
        /// Show tooltip after delay
        /// </summary>
        private System.Collections.IEnumerator ShowAfterDelay(Vector2 screenPosition)
        {
            while (hoverTime < showDelay)
            {
                hoverTime += Time.deltaTime;
                yield return null;
            }

            if (currentSlot != null && !currentSlot.IsEmpty)
            {
                isShowing = true;
                if (tooltipPanel != null)
                {
                    tooltipPanel.SetActive(true);
                }

                UpdateTooltipContent();
                UpdatePosition();
            }
        }

        /// <summary>
        /// Check if tooltip should stay visible (mouse is over tooltip or slot)
        /// </summary>
        public bool ShouldStayVisible()
        {
            return isMouseOverTooltip || isShowing;
        }

        /// <summary>
        /// Update tooltip content.
        /// Uses ItemConfigData looked up from GameConfigLoader based on the slot's item id.
        /// </summary>
        private void UpdateTooltipContent()
        {
            if (currentSlot == null || currentSlot.IsEmpty)
                return;

            var inventoryItem = currentSlot.Item;
            if (inventoryItem == null)
                return;

            // TODO: Use ItemDataBase directly from inventoryItem.ItemData instead of ItemConfigData
            // For now, tooltip is disabled until ItemDataBase is used
            if (inventoryItem.ItemData == null)
                return;

            var itemData = inventoryItem.ItemData;
            // Use itemData directly instead of config

            // Item name
            if (itemNameText != null)
            {
                itemNameText.text = string.IsNullOrEmpty(itemData.DisplayName) ? itemData.ItemId : itemData.DisplayName;
            }

            // Item stats
            if (itemStatsText != null)
            {
                string stats = $"Weight: {itemData.Weight:F1} kg\n";
                stats += $"Category: {itemData.Category}\n";
                // TODO: Add rarity and effect info when ItemDataBase is extended
                // stats += $"Rarity: {itemData.Rarity ?? "Common"}\n";

                // if (!string.IsNullOrEmpty(itemData.EffectType))
                // {
                //     stats += $"Effect: {itemData.EffectType}";
                //     if (itemData.EffectValue > 0)
                //     {
                //         stats += $" ({itemData.EffectValue})";
                //     }
                // }

                itemStatsText.text = stats;
            }

            // Equipped items (nested equipment)
            // TODO: Update when ItemDataBase is extended with nested equipment support
            // UpdateEquippedItems(itemData);
        }

        /// <summary>
        /// Update equipped items icons
        /// TODO: Update when ItemDataBase is extended with nested equipment support
        /// </summary>
        private void UpdateEquippedItems(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase item)
        {
            if (equippedItemsContainer == null)
                return;

            // Clear existing icons
            foreach (var icon in equippedItemIcons)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }

            equippedItemIcons.Clear();

            // TODO: Get nested equipment from item
            // This could come from:
            // 1. ItemConfigData.ExtraParamsJson (parse JSON for attachments)
            // 2. ItemInstance.attachedItems (if exists in InteractionSystem)
            // 3. EquipmentManager.GetAttachedItems(item.ItemId)

            // Check if item has nested equipment slots defined
            // Example structure (would need to be defined in ItemConfigData or ItemDataBase):
            // if (item.HasAttachments || item.AttachmentSlots != null)
            // {
            //     foreach (var attachment in item.Attachments)
            //     {
            //         CreateEquippedItemIcon(attachment);
            //     }
            // }

            // TODO: Check for weapon category when ItemDataBase is extended with nested equipment support
            // if (item.Category == ItemCategory.Weapon)
            // {
            //     // Check for attachments if needed
            // }
        }

        /// <summary>
        /// Create equipped item icon with click handler for nested tooltip
        /// TODO: Update when ItemDataBase is extended with nested equipment support
        /// </summary>
        private void CreateEquippedItemIcon(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase item)
        {
            if (equippedItemIconPrefab == null || equippedItemsContainer == null || item == null)
                return;

            GameObject iconObj = Instantiate(equippedItemIconPrefab, equippedItemsContainer);
            iconObj.SetActive(true); // Ensure prefab is active even if it was disabled
            equippedItemIcons.Add(iconObj);

            Image iconImage = iconObj.GetComponent<Image>();
            if (iconImage != null)
            {
                // Load icon from ItemDataBase
                if (item.Icon != null)
                {
                    iconImage.sprite = item.Icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            // Add click handler for nested tooltip
            Button button = iconObj.GetComponent<Button>();
            if (button == null)
            {
                button = iconObj.AddComponent<Button>();
            }

            button.onClick.AddListener(() =>
            {
                // TODO: Show nested tooltip when ItemDataBase is extended with nested equipment support
                // Create nested tooltip for this equipment item
                // ShowNestedTooltip(item, iconObj.transform.position);
            });

            // Also add hover handler (optional - can show tooltip on hover too)
            EventTrigger trigger = iconObj.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = iconObj.AddComponent<EventTrigger>();
            }

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
            hoverEntry.eventID = EventTriggerType.PointerEnter;
            hoverEntry.callback.AddListener((data) =>
            {
                // Show nested tooltip on hover (optional)
                // ShowNestedTooltip(item, iconObj.transform.position);
            });
            trigger.triggers.Add(hoverEntry);
        }

        /// <summary>
        /// Show nested tooltip for equipment icon
        /// TODO: Update when ItemDataBase is extended with nested equipment support
        /// </summary>
        private void ShowNestedTooltip(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase item,
            Vector3 position)
        {
            // Hide previous nested tooltip
            if (nestedTooltip != null)
            {
                nestedTooltip.HideTooltip();
                nestedTooltip = null;
            }

            // Create nested tooltip (could be a child tooltip or separate instance)
            // Create a new InventorySlot wrapper for the nested item
            InventorySlot nestedSlot = new InventorySlot();
            nestedSlot.SetItem(item, 1);

            // Get screen position
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, position);

            // Create nested tooltip instance (could be pooled or instantiated)
            // For simplicity, we'll use the same tooltip system but offset position
            // TODO: Implement proper nested tooltip system with separate prefab/instance

            Debug.Log($"[ItemTooltip] Show nested tooltip for item: {item.ItemId}");
        }

        /// <summary>
        /// Update tooltip position to follow mouse
        /// </summary>
        private void UpdatePosition()
        {
            if (canvas == null || rectTransform == null)
                return;

            // Use New Input System to get mouse position
            Vector2 mousePos = Vector2.zero;
            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
            }
            else
            {
                // Fallback if mouse is not available
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                mousePos,
                canvas.worldCamera,
                out Vector2 localPoint);

            rectTransform.anchoredPosition = localPoint + offset;
        }
    }
}