using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Controls tooltip display for items and slots.
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private RectTransform tooltipRect;
        
        [Header("Positioning")]
        [SerializeField] private Vector2 offset = new Vector2(10f, 0f);
        
        private bool isHoveringTooltip = false;
        
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
        
        void ShowItemTooltip(ItemInstance item, Vector3 cellPosition)
        {
            // Display item info
            itemNameText.text = item.Definition.ItemId;
            statsText.text = FormatItemStats(item);
            
            // Position next to cell
            PositionTooltip(cellPosition);
            
            tooltipPanel.SetActive(true);
        }
        
        void ShowSlotTooltip(SlotLocationType slotType, int slotIndex, Vector3 cellPosition)
        {
            // Show slot description for empty slots
            itemNameText.text = $"{slotType} Slot";
            statsText.text = GetSlotDescription(slotType);
            
            PositionTooltip(cellPosition);
            tooltipPanel.SetActive(true);
        }
        
        void PositionTooltip(Vector3 anchorPos)
        {
            // Position to the right of cell
            Vector3 pos = anchorPos + (Vector3)offset;
            
            // Check if tooltip goes off-screen (right edge)
            Vector3[] corners = new Vector3[4];
            tooltipRect.GetWorldCorners(corners);
            
            if (corners[2].x > Screen.width)
            {
                // Flip to left side
                pos = anchorPos - new Vector3(tooltipRect.rect.width + offset.x, 0f, 0f);
            }
            
            // Check bottom edge
            if (corners[0].y < 0)
            {
                pos.y += Mathf.Abs(corners[0].y);
            }
            
            tooltipRect.position = pos;
        }
        
        void TryHide()
        {
            // Don't hide if mouse is over tooltip
            if (isHoveringTooltip) return;
            
            tooltipPanel.SetActive(false);
        }
        
        void OnTooltipHovered(bool isHovering)
        {
            isHoveringTooltip = isHovering;
            
            if (!isHovering)
            {
                // Left tooltip - hide after delay
                StartCoroutine(HideAfterDelay(0.1f));
            }
        }
        
        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!isHoveringTooltip)
            {
                tooltipPanel.SetActive(false);
            }
        }
        
        string FormatItemStats(ItemInstance item)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Weight: {item.Definition.Weight} kg");
            sb.AppendLine($"Durability: {item.CurrentDurability:F0}/{item.Definition.MaxDurability:F0}%");
            
            if (item.Definition.ItemType == ItemType.Weapon)
            {
                sb.AppendLine($"Ammo: {item.CurrentAmmo}");
            }
            
            if (item.Definition.IsStackable)
            {
                sb.AppendLine($"Stack: {item.StackSize}/{item.Definition.MaxStackSize}");
            }
            
            // Show stat modifiers
            foreach (var mod in item.Definition.CharacterStatModifiers)
            {
                string sign = mod.Value >= 0 ? "+" : "";
                sb.AppendLine($"{mod.CharacterStat}: {sign}{mod.Value}");
            }
            
            foreach (var mod in item.Definition.WeaponStatModifiers)
            {
                string sign = mod.Value >= 0 ? "+" : "";
                sb.AppendLine($"{mod.WeaponStat}: {sign}{mod.Value}");
            }
            
            return sb.ToString();
        }
        
        string GetSlotDescription(SlotLocationType slotType)
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
    }
}
