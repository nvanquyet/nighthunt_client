using NightHunt.Inventory.Core.Data;
using UnityEngine;

namespace NightHunt.Inventory.UI.Data
{
    /// <summary>
    /// Configuration for inventory slot visuals and layout.
    /// Used by ItemSlotUI components for consistent styling.
    /// </summary>
    [CreateAssetMenu(fileName = "SlotLayoutConfig", menuName = "NightHunt/Inventory/Slot Layout Config")]
    public class SlotLayoutConfig : ScriptableObject
    {
        [Header("Slot Visuals")]
        [Tooltip("Icon shown when slot is empty")]
        public Sprite EmptySlotIcon;
        
        [Tooltip("Default slot background color")]
        public Color DefaultSlotColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        [Tooltip("Slot size in pixels (width & height)")]
        public Vector2 SlotSize = new Vector2(64, 64);
        
        [Tooltip("Spacing between slots")]
        public float SlotSpacing = 4f;
        
        [Header("Drag & Drop Feedback")]
        [Tooltip("Highlight color when valid drop target (green)")]
        public Color ValidDropHighlightColor = new Color(0f, 1f, 0f, 0.4f);
        
        [Tooltip("Highlight color when invalid drop target (red)")]
        public Color InvalidDropHighlightColor = new Color(1f, 0f, 0f, 0.4f);
        
        [Tooltip("Dragged item opacity while dragging")]
        [Range(0f, 1f)]
        public float DraggedItemOpacity = 0.6f;
        
        [Tooltip("Ghost icon scale multiplier while dragging")]
        public float DragIconScale = 1.1f;
        
        [Header("Rarity Colors")]
        [Tooltip("Common item border color")]
        public Color CommonColor = Color.white;
        
        [Tooltip("Uncommon item border color")]
        public Color UncommonColor = Color.green;
        
        [Tooltip("Rare item border color")]
        public Color RareColor = Color.blue;
        
        [Tooltip("Epic item border color")]
        public Color EpicColor = new Color(0.64f, 0.21f, 0.93f); // Purple
        
        [Tooltip("Legendary item border color")]
        public Color LegendaryColor = new Color(1f, 0.65f, 0f); // Orange
        
        [Header("Resource Bar")]
        [Tooltip("Resource bar fill color (durability/ammo)")]
        public Color ResourceBarColor = new Color(0.2f, 0.8f, 1f);
        
        [Tooltip("Resource bar low threshold (show warning)")]
        [Range(0f, 1f)]
        public float ResourceLowThreshold = 0.25f;
        
        [Tooltip("Resource bar low color (warning)")]
        public Color ResourceBarLowColor = Color.red;
        
        [Tooltip("Resource bar background color")]
        public Color ResourceBarBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        [Header("Stack Size Display")]
        [Tooltip("Font size for stack count text")]
        public int StackCountFontSize = 14;
        
        [Tooltip("Stack count text color")]
        public Color StackCountColor = Color.white;
        
        [Tooltip("Show stack count even if = 1?")]
        public bool ShowStackCountWhenOne = false;
        
        [Header("Hover Tooltip")]
        [Tooltip("Delay before showing tooltip (seconds)")]
        public float TooltipDelay = 0.5f;
        
        [Tooltip("Tooltip background color")]
        public Color TooltipBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        
        [Tooltip("Tooltip text color")]
        public Color TooltipTextColor = Color.white;
        
        [Tooltip("Tooltip max width")]
        public float TooltipMaxWidth = 300f;
        
        [Header("Animation")]
        [Tooltip("Enable slot animations (pulse, shake, etc.)")]
        public bool EnableAnimations = true;
        
        [Tooltip("Duration of add item animation (seconds)")]
        public float AddItemAnimDuration = 0.2f;
        
        [Tooltip("Duration of remove item animation (seconds)")]
        public float RemoveItemAnimDuration = 0.15f;
        
        [Header("QuickSlot Specific")]
        [Tooltip("Show hotkey numbers on quickslots?")]
        public bool ShowQuickSlotHotkeys = true;
        
        [Tooltip("Hotkey text color")]
        public Color HotkeyTextColor = new Color(1f, 1f, 1f, 0.7f);
        
        [Tooltip("Hotkey font size")]
        public int HotkeyFontSize = 12;
        
        [Header("Attachment Slot Visuals")]
        [Tooltip("Attachment sub-slot size multiplier (relative to parent)")]
        public float AttachmentSlotScale = 0.7f;
        
        [Tooltip("Show attachment slot even when empty?")]
        public bool ShowEmptyAttachmentSlots = true;
        
        /// <summary>
        /// Get rarity color by ItemRarity enum.
        /// </summary>
        public Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => CommonColor,
                ItemRarity.Uncommon => UncommonColor,
                ItemRarity.Rare => RareColor,
                ItemRarity.Epic => EpicColor,
                ItemRarity.Legendary => LegendaryColor,
                _ => CommonColor
            };
        }
    }
}