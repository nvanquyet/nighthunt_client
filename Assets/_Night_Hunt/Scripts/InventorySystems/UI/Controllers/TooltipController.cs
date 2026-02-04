using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using System.Text;

namespace NightHunt.Inventory.UI.Controllers
{
    /// <summary>
    /// Manages tooltip display for items and slots.
    /// Handles positioning and content formatting.
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private RectTransform tooltipRect;
        
        [Header("Positioning")]
        [SerializeField] private Vector2 offset = new Vector2(10f, 0f);
        [SerializeField] private float edgePadding = 10f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private bool isHoveringTooltip = false;
        private Canvas canvas;
        
        #region Lifecycle
        
        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }
        
        void OnEnable()
        {
            TooltipEvents.OnShowTooltip += ShowItemTooltip;
            TooltipEvents.OnShowSlotInfo += ShowSlotTooltip;
            TooltipEvents.OnHideTooltip += TryHide;
            TooltipEvents.OnTooltipHovered += OnTooltipHovered;
        }
        
        void OnDisable()
        {
            TooltipEvents.OnShowTooltip -= ShowItemTooltip;
            TooltipEvents.OnShowSlotInfo -= ShowSlotTooltip;
            TooltipEvents.OnHideTooltip -= TryHide;
            TooltipEvents.OnTooltipHovered -= OnTooltipHovered;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void ShowItemTooltip(ItemInstance item, Vector3 cellPosition)
        {
            if (item == null) return;
            
            // Set title
            titleText.text = item.Definition.ItemId;
            
            // Set description (if exists)
            if (descriptionText != null)
            {
                descriptionText.text = GetItemDescription(item);
            }
            
            // Set stats
            statsText.text = FormatItemStats(item);
            
            // Position tooltip
            PositionTooltip(cellPosition);
            
            // Show
            tooltipPanel.SetActive(true);
            
            if (enableDebugLogs)
                Debug.Log($"[TooltipController] Showing tooltip for {item.Definition.ItemId}");
        }
        
        private void ShowSlotTooltip(SlotLocationType slotType, int slotIndex, Vector3 cellPosition)
        {
            // Set title
            titleText.text = $"{slotType} Slot";
            
            // Set description
            if (descriptionText != null)
            {
                descriptionText.text = GetSlotDescription(slotType);
            }
            
            // Clear stats
            statsText.text = "";
            
            // Position tooltip
            PositionTooltip(cellPosition);
            
            // Show
            tooltipPanel.SetActive(true);
            
            if (enableDebugLogs)
                Debug.Log($"[TooltipController] Showing slot info for {slotType}");
        }
        
        private void TryHide()
        {
            // Don't hide if mouse is over tooltip
            if (isHoveringTooltip) return;
            
            tooltipPanel.SetActive(false);
            
            if (enableDebugLogs)
                Debug.Log("[TooltipController] Tooltip hidden");
        }
        
        private void OnTooltipHovered(bool isHovering)
        {
            isHoveringTooltip = isHovering;
            
            if (!isHovering)
            {
                // Left tooltip - hide after delay
                StartCoroutine(HideAfterDelay(0.1f));
            }
        }
        
        #endregion
        
        #region Content Formatting
        
        private string GetItemDescription(ItemInstance item)
        {
            // Override this to add item descriptions from your data
            return $"Type: {item.Definition.ItemType}";
        }
        
        private string FormatItemStats(ItemInstance item)
        {
            var sb = new StringBuilder();
            
            // Basic properties
            sb.AppendLine($"Weight: {item.Definition.Weight} kg");
            
            if (item.Definition.MaxDurability > 0)
            {
                sb.AppendLine($"Durability: {item.CurrentDurability:F0}/{item.Definition.MaxDurability:F0}%");
            }
            
            // Weapon-specific
            if (item.Definition.ItemType == ItemType.Weapon)
            {
                sb.AppendLine($"Ammo: {item.CurrentAmmo}");
            }
            
            // Stackable
            if (item.Definition.IsStackable)
            {
                sb.AppendLine($"Stack: {item.StackSize}/{item.Definition.MaxStackSize}");
            }
            
            // Character stat modifiers
            if (item.Definition.CharacterStatModifiers != null && item.Definition.CharacterStatModifiers.Length > 0)
            {
                sb.AppendLine("\n<b>Character Stats:</b>");
                foreach (var mod in item.Definition.CharacterStatModifiers)
                {
                    string sign = mod.Value >= 0 ? "+" : "";
                    sb.AppendLine($"  {mod.CharacterStat}: {sign}{mod.Value}");
                }
            }
            
            // Weapon stat modifiers
            if (item.Definition.WeaponStatModifiers != null && item.Definition.WeaponStatModifiers.Length > 0)
            {
                sb.AppendLine("\n<b>Weapon Stats:</b>");
                foreach (var mod in item.Definition.WeaponStatModifiers)
                {
                    string sign = mod.Value >= 0 ? "+" : "";
                    sb.AppendLine($"  {mod.WeaponStat}: {sign}{mod.Value}");
                }
            }
            
            // Attachment info
            if (item.AttachedItems != null && item.AttachedItems.Count > 0)
            {
                sb.AppendLine($"\n<b>Attachments:</b> {item.AttachedItems.Count}");
                foreach (var attachment in item.AttachedItems)
                {
                    sb.AppendLine($"  • {attachment.Definition.ItemId}");
                }
            }
            
            // Attachment slots
            if (item.Definition.AttachmentSlots != null && item.Definition.AttachmentSlots.Length > 0)
            {
                sb.AppendLine($"\n<b>Available Slots:</b>");
                foreach (var slot in item.Definition.AttachmentSlots)
                {
                    sb.AppendLine($"  • {slot}");
                }
            }
            
            return sb.ToString();
        }
        
        private string GetSlotDescription(SlotLocationType slotType)
        {
            switch (slotType)
            {
                case SlotLocationType.Equipment:
                    return "Equip armor, helmets, backpacks";
                case SlotLocationType.Weapon:
                    return "Equip weapons (Primary/Secondary)";
                case SlotLocationType.QuickSlot:
                    return "Quick access (Ctrl+1-4)";
                case SlotLocationType.Attachment:
                    return "Attach scopes, grips, muzzles";
                case SlotLocationType.Container:
                    return "Container storage";
                default:
                    return "";
            }
        }
        
        #endregion
        
        #region Positioning
        
        private void PositionTooltip(Vector3 anchorPos)
        {
            // Convert world position to screen position if needed
            Vector3 screenPos = anchorPos;
            
            // Position to the right of anchor
            Vector3 targetPos = screenPos + (Vector3)offset;
            
            // Get tooltip corners in screen space
            tooltipRect.position = targetPos;
            Vector3[] corners = new Vector3[4];
            tooltipRect.GetWorldCorners(corners);
            
            // Check if tooltip goes off-screen (right edge)
            if (corners[2].x > Screen.width - edgePadding)
            {
                // Flip to left side
                targetPos = screenPos - new Vector3(tooltipRect.rect.width + offset.x, 0f, 0f);
            }
            
            // Check bottom edge
            if (corners[0].y < edgePadding)
            {
                targetPos.y += Mathf.Abs(corners[0].y) + edgePadding;
            }
            
            // Check top edge
            if (corners[2].y > Screen.height - edgePadding)
            {
                targetPos.y -= (corners[2].y - Screen.height + edgePadding);
            }
            
            tooltipRect.position = targetPos;
        }
        
        #endregion
        
        #region Coroutines
        
        private System.Collections.IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!isHoveringTooltip)
            {
                tooltipPanel.SetActive(false);
            }
        }
        
        #endregion
    }
}