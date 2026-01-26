using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Tooltip UI that appears when hovering over items
    /// Shows item name, stats, and equipped items icons
    /// </summary>
    public class ItemTooltip : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemStatsText;
        [SerializeField] private Transform equippedItemsContainer;
        [SerializeField] private GameObject equippedItemIconPrefab;

        [Header("Settings")]
        [SerializeField] private float showDelay = 0.3f;
        [SerializeField] private Vector2 offset = new Vector2(10, -10);

        private Canvas canvas;
        private RectTransform rectTransform;
        private InventorySlot currentSlot;
        private float hoverTime = 0f;
        private bool isShowing = false;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            rectTransform = GetComponent<RectTransform>();
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
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
        /// Show tooltip for item
        /// </summary>
        public void ShowTooltip(InventorySlot slot, Vector2 screenPosition)
        {
            if (slot == null || slot.IsEmpty)
            {
                HideTooltip();
                return;
            }

            currentSlot = slot;
            hoverTime = 0f;

            // Start showing after delay
            if (!isShowing)
            {
                StartCoroutine(ShowAfterDelay(screenPosition));
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
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
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
        /// Update tooltip content
        /// </summary>
        private void UpdateTooltipContent()
        {
            if (currentSlot == null || currentSlot.IsEmpty)
                return;

            var item = currentSlot.Item;
            if (item == null)
                return;

            // Item name
            if (itemNameText != null)
            {
                itemNameText.text = item.DisplayName ?? item.ItemId;
            }

            // Item stats
            if (itemStatsText != null)
            {
                string stats = $"Weight: {item.Weight:F1} kg\n";
                stats += $"Category: {item.Category ?? "Unknown"}\n";
                stats += $"Rarity: {item.Rarity ?? "Common"}\n";
                
                if (!string.IsNullOrEmpty(item.EffectType))
                {
                    stats += $"Effect: {item.EffectType}";
                    if (item.EffectValue > 0)
                    {
                        stats += $" ({item.EffectValue})";
                    }
                }

                itemStatsText.text = stats;
            }

            // Equipped items (nested equipment)
            UpdateEquippedItems(item);
        }

        /// <summary>
        /// Update equipped items icons
        /// </summary>
        private void UpdateEquippedItems(ItemConfigData item)
        {
            if (equippedItemsContainer == null)
                return;

            // Clear existing icons
            foreach (Transform child in equippedItemsContainer)
            {
                Destroy(child.gameObject);
            }

            // TODO: Get nested equipment from item
            // For now, this is a placeholder
            // Example:
            // if (item.HasAttachments)
            // {
            //     foreach (var attachment in item.Attachments)
            //     {
            //         CreateEquippedItemIcon(attachment);
            //     }
            // }
        }

        /// <summary>
        /// Create equipped item icon
        /// </summary>
        private void CreateEquippedItemIcon(ItemConfigData item)
        {
            if (equippedItemIconPrefab == null || equippedItemsContainer == null)
                return;

            GameObject iconObj = Instantiate(equippedItemIconPrefab, equippedItemsContainer);
            Image iconImage = iconObj.GetComponent<Image>();
            if (iconImage != null)
            {
                // TODO: Load icon from item
                // iconImage.sprite = item.Icon;
            }
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
