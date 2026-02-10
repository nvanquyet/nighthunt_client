using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;
#if UNITY_TMPRO
using TMPro;
#endif

namespace NightHunt.Inventory.UI.Components
{
    /// <summary>
    /// Displays tooltip on hover with item information.
    /// Shows: name, description, stats, weight, durability, attachments list.
    /// Handles positioning and visibility.
    /// </summary>
    public class ItemTooltipUI : MonoBehaviour
    {
        [Header("Components")] [SerializeField]
        private GameObject tooltipPanel;

        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI itemTypeText;
        [SerializeField] private TextMeshProUGUI weightText;
        [SerializeField] private TextMeshProUGUI durabilityText;
        [SerializeField] private TextMeshProUGUI stackSizeText;
        [SerializeField] private TextMeshProUGUI rarityText;
        
        [SerializeField] private Transform statsContainer;
        [SerializeField] private Transform attachmentsContainer;
        [SerializeField] private GameObject statLinePrefab;
        [SerializeField] private GameObject attachmentLinePrefab;

        [Header("Settings")] [SerializeField] private float offsetX = 10f;
        [SerializeField] private float offsetY = 10f;
        [SerializeField] private bool followMouse = true;
        [SerializeField] private float showDelay = 0.3f;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private ItemInstance currentItem;
        private float showTimer = 0f;
        private bool shouldShow = false;
        private RectTransform rectTransform;
        private Canvas parentCanvas;

        // === Public API ===

        /// <summary>
        /// Show tooltip for item.
        /// </summary>
        public void ShowTooltip(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                HideTooltip();
                return;
            }

            currentItem = item;
            shouldShow = true;
            showTimer = 0f;

            UpdateTooltipContent();
        }

        /// <summary>
        /// Hide tooltip.
        /// </summary>
        public void HideTooltip()
        {
            shouldShow = false;
            showTimer = 0f;

            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        // === Lifecycle ===

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();

            if (tooltipPanel == null)
                tooltipPanel = gameObject;

            HideTooltip();
        }

        void Update()
        {
            if (shouldShow)
            {
                showTimer += Time.deltaTime;

                if (showTimer >= showDelay)
                {
                    if (tooltipPanel != null && !tooltipPanel.activeSelf)
                    {
                        tooltipPanel.SetActive(true);
                    }

                    if (followMouse)
                    {
                        UpdatePosition();
                    }
                }
            }
            else
            {
                if (tooltipPanel != null && tooltipPanel.activeSelf)
                {
                    HideTooltip();
                }
            }
        }

        // === Visual Updates ===

        private void UpdateTooltipContent()
        {
            if (currentItem == null || currentItem.Definition == null)
                return;

            var definition = currentItem.Definition;

            // Item name
            if (itemNameText != null)
            {
                itemNameText.text = definition.DisplayName;
                // Set color based on rarity
                itemNameText.color = GetRarityColor(definition.Rarity);
            }

            // Description
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = definition.Description;
            }

            // Item type
            if (itemTypeText != null)
            {
                itemTypeText.text = definition.ItemType.ToString();
            }

            // Weight
            if (weightText != null)
            {
                float totalWeight = currentItem.GetTotalWeight();
                weightText.text = $"Weight: {totalWeight:F2} kg";
            }

            // Durability
            if (durabilityText != null)
            {
                if (definition.MaxDurability > 0)
                {
                    float durabilityPercent = (currentItem.CurrentDurability / definition.MaxDurability) * 100f;
                    durabilityText.text = $"Durability: {durabilityPercent:F0}%";
                    durabilityText.gameObject.SetActive(true);
                }
                else
                {
                    durabilityText.gameObject.SetActive(false);
                }
            }

            // Stack size
            if (stackSizeText != null)
            {
                if (definition.IsStackable && currentItem.StackSize > 1)
                {
                    stackSizeText.text = $"Stack: {currentItem.StackSize}/{definition.MaxStackSize}";
                    stackSizeText.gameObject.SetActive(true);
                }
                else
                {
                    stackSizeText.gameObject.SetActive(false);
                }
            }

            // Rarity
            if (rarityText != null)
            {
                rarityText.text = definition.Rarity.ToString();
                rarityText.color = GetRarityColor(definition.Rarity);
            }

            // Stats
            UpdateStats();

            // Attachments
            UpdateAttachments();
        }

        private void UpdateStats()
        {
            if (statsContainer == null || statLinePrefab == null)
                return;

            // Clear existing stat lines
            foreach (Transform child in statsContainer)
            {
                Destroy(child.gameObject);
            }

            if (currentItem == null || currentItem.Definition == null)
                return;

            var modifiers = currentItem.GetStatModifiers();
            if (modifiers == null || modifiers.Count == 0)
                return;

            // Create stat lines
            foreach (var modifier in modifiers)
            {
                GameObject statLine = Instantiate(statLinePrefab, statsContainer);
#if UNITY_TMPRO
                TextMeshProUGUI statText = statLine.GetComponent<TextMeshProUGUI>();
#else
                Text statText = statLine.GetComponent<Text>();
#endif

                if (statText != null)
                {
                    string statName = "";
                    string value = "";

                    if (modifier.Target == StatModifierTarget.Character)
                    {
                        statName = modifier.CharacterStat.ToString();
                        value = FormatModifierValue(modifier.CalculationType, modifier.Value);
                    }
                    else if (modifier.Target == StatModifierTarget.Weapon)
                    {
                        statName = modifier.WeaponStat.ToString();
                        value = FormatModifierValue(modifier.CalculationType, modifier.Value);
                    }

                    statText.text = $"{statName}: {value}";
                }
            }
        }

        private void UpdateAttachments()
        {
            if (attachmentsContainer == null || attachmentLinePrefab == null)
                return;

            // Clear existing attachment lines
            foreach (Transform child in attachmentsContainer)
            {
                Destroy(child.gameObject);
            }

            if (currentItem == null || currentItem.AttachedItems == null || currentItem.AttachedItems.Count == 0)
                return;

            // Create attachment lines
            foreach (var attachment in currentItem.AttachedItems)
            {
                if (attachment == null || attachment.Definition == null)
                    continue;

                GameObject attachmentLine = Instantiate(attachmentLinePrefab, attachmentsContainer);
#if UNITY_TMPRO
                TextMeshProUGUI attachmentText = attachmentLine.GetComponent<TextMeshProUGUI>();
#else
                Text attachmentText = attachmentLine.GetComponent<Text>();
#endif

                if (attachmentText != null)
                {
                    attachmentText.text =
                        $"• {attachment.Definition.DisplayName} ({attachment.Definition.AttachmentType})";
                }
            }
        }

        private void UpdatePosition()
        {
            if (rectTransform == null || parentCanvas == null)
                return;

            Vector2 mousePosition = Input.mousePosition;
            Vector2 tooltipPosition;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                mousePosition,
                parentCanvas.worldCamera,
                out tooltipPosition
            );

            // Offset from mouse
            tooltipPosition.x += offsetX;
            tooltipPosition.y += offsetY;

            // Keep tooltip on screen
            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            Vector2 tooltipSize = rectTransform.sizeDelta;

            // Check right edge
            if (tooltipPosition.x + tooltipSize.x > canvasRect.rect.width)
            {
                tooltipPosition.x = canvasRect.rect.width - tooltipSize.x - offsetX;
            }

            // Check top edge
            if (tooltipPosition.y + tooltipSize.y > canvasRect.rect.height)
            {
                tooltipPosition.y = canvasRect.rect.height - tooltipSize.y - offsetY;
            }

            // Check left edge
            if (tooltipPosition.x < 0)
            {
                tooltipPosition.x = offsetX;
            }

            // Check bottom edge
            if (tooltipPosition.y < 0)
            {
                tooltipPosition.y = offsetY;
            }

            rectTransform.localPosition = tooltipPosition;
        }

        // === Helpers ===

        private string FormatModifierValue(ModifierCalculationType calculationType, float value)
        {
            switch (calculationType)
            {
                case ModifierCalculationType.Flat:
                    return $"+{value:F2}";
                case ModifierCalculationType.Percentage:
                    return $"+{value * 100:F1}%";
                default:
                    return value.ToString("F2");
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => Color.white,
                ItemRarity.Uncommon => Color.green,
                ItemRarity.Rare => Color.blue,
                ItemRarity.Epic => Color.magenta,
                ItemRarity.Legendary => Color.yellow,
                _ => Color.white
            };
        }

        // === Debug ===

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[ItemTooltipUI] {message}");
        }
    }
}