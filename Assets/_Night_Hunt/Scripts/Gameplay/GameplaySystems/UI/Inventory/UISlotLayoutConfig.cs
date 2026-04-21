using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;

/// <summary>Controls how the item tooltip positions itself after appearing.</summary>
public enum TooltipMode
{
    /// <summary>Tooltip tracks the mouse cursor every frame.</summary>
    FollowMouse,
    /// <summary>Tooltip appears at the slot's position and stays there.</summary>
    SnapToSlot,
    /// <summary>Tooltip always uses the fixed anchored position defined in UISlotLayoutConfig.</summary>
    Fixed
}

/// <summary>Which side of the selected slot the context menu prefers to appear on.</summary>
public enum ContextMenuSide
{
    Right,
    Left
}

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Unified UI layout config for all slot types.
    /// References InventoryConfig for gameplay data; contains only UI-specific settings.
    ///
    /// A single shared asset is assigned to InventoryScreen and loaded automatically.
    /// The static <see cref="Instance"/> accessor is populated when the asset is loaded (OnEnable).
    /// </summary>
    [CreateAssetMenu(
        fileName = "UISlotLayoutConfig",
        menuName = "NightHunt/UI/Slot Layout Config")]
    public class UISlotLayoutConfig : ScriptableObject
    {
        // ── Singleton accessor ────────────────────────────────────────────────
        private static UISlotLayoutConfig _instance;
        /// <summary>Global accessor. Valid once the asset has been loaded by Unity.</summary>
        public static UISlotLayoutConfig Instance => _instance;
        private void OnEnable() => _instance = this;
        [Header("Core Config Reference")]
        [Tooltip("Reference to core InventoryConfig for all gameplay data")]
        public InventoryConfig InventoryConfig;

        [Header("Prefabs")]
        [Tooltip("Prefab chung cho tất cả slot types (Inventory, Equipment, Weapon). " +
                 "Có thể dùng 1 prefab cho tất cả hoặc override riêng bên dưới.")]
        public GameObject DefaultSlotPrefab;

        [Tooltip("Prefab riêng cho Equipment slots (optional, nếu null sẽ dùng DefaultSlotPrefab)")]
        public GameObject EquipmentSlotPrefab;

        [Tooltip("Prefab riêng cho Weapon slots (optional, nếu null sẽ dùng DefaultSlotPrefab)")]
        public GameObject WeaponSlotPrefab;

        [Header("Rarity Backgrounds")]
        [Tooltip("Background icon/sprite cho từng rarity level của item")]
        public RarityBackgroundConfig[] RarityBackgrounds;

        [Tooltip("Background mặc định cho các item not available rarity hoặc rarity not allowed định nghĩa")]
        public Sprite DefaultBackground;

        [Header("Attachment UI")]
        [Tooltip("Prefab cho attachment slot")]
        public GameObject AttachmentSlotPrefab;

        /// <summary>
        /// Get the prefab for a specific slot type.
        /// </summary>
        public GameObject GetSlotPrefab(UISlotType slotType)
        {
            switch (slotType)
            {
                case UISlotType.Equipment:
                    return EquipmentSlotPrefab != null ? EquipmentSlotPrefab : DefaultSlotPrefab;
                case UISlotType.Weapon:
                    return WeaponSlotPrefab != null ? WeaponSlotPrefab : DefaultSlotPrefab;
                case UISlotType.Attachment:
                    return AttachmentSlotPrefab != null ? AttachmentSlotPrefab : DefaultSlotPrefab;
                default:
                    return DefaultSlotPrefab;
            }
        }

        /// <summary>
        /// Get the background sprite for a specific rarity.
        /// </summary>
        public Sprite GetRarityBackground(ItemRarity rarity)
        {
            if (RarityBackgrounds == null) return DefaultBackground;

            foreach (var config in RarityBackgrounds)
            {
                if (config.Rarity == rarity)
                    return config.BackgroundIcon;
            }

            return DefaultBackground;
        }

        // Properties forwarded from InventoryConfig.
        public int InventoryGridWidth => InventoryConfig != null ? InventoryConfig.Inventory.GridWidth : 5;
        public int InventoryGridHeight => InventoryConfig != null ? InventoryConfig.Inventory.GridHeight : 4;
        public int InventoryTotalSlots => InventoryConfig != null ? InventoryConfig.Inventory.TotalSlots : 20;
        public int EquipmentCount => InventoryConfig != null ? InventoryConfig.EquipmentCount : 0;
        public int WeaponCount => InventoryConfig != null ? InventoryConfig.WeaponCount : 0;

        [Header("Empty Slot Padding")]
        [Tooltip("Số empty slots mặc định spawn thêm trong UI.")]
        [Range(10, 50)]
        public int DefaultExtraEmptySlots = 20;

        [Tooltip("Số empty slots tối thiểu luôn display.")]
        [Range(5, 30)]
        public int MinimumEmptySlots = 10;

        [Header("Slot Dimensions")]
        [Tooltip("Default slot size in pixels used by inventory grids.")]
        public Vector2 DefaultSlotSize = new Vector2(100f, 100f);

        // ── Tooltip ──────────────────────────────────────────────────────────

        [Header("Tooltip")]
        [Tooltip("How the tooltip positions itself after Show() is called. " +
                 "FollowMouse = tracks cursor every frame; " +
                 "SnapToSlot  = appears at the slot and stays there; " +
                 "Fixed       = always at TooltipFixedPosition.")]
        public TooltipMode TooltipMode = TooltipMode.FollowMouse;

        [Tooltip("Pixel offset applied to the tooltip from the mouse or slot anchor.")]
        public Vector2 TooltipOffset = new Vector2(16f, -16f);

        [Tooltip("Used only when TooltipMode = Fixed. Anchored position within the parent canvas.")]
        public Vector2 TooltipFixedPosition = new Vector2(200f, -200f);

        [Tooltip("If true: tooltip remains visible while the player is dragging an item. " +
                 "If false: tooltip is hidden as soon as a drag begins.")]
        public bool ShowTooltipDuringDrag = false;

        // ── Context Menu ─────────────────────────────────────────────────────

        [Header("Context Menu")]
        [Tooltip("Preferred side for the context menu relative to the selected slot. " +
                 "Auto-flips to the opposite side if the preferred side would clip the screen edge.")]
        public ContextMenuSide ContextMenuPreferredSide = ContextMenuSide.Right;

        [Tooltip("Pixel gap between the slot edge and the context menu panel.")]
        public float ContextMenuGap = 8f;

        [Tooltip("If true: beginning a drag immediately hides the context menu.")]
        public bool HideContextMenuOnDragStart = true;

        // ── Drag & Drop ───────────────────────────────────────────────────────

        [Header("Drag & Drop")]
        [Tooltip("Duration (seconds) of the snap-back animation when a drag is cancelled " +
                 "or dropped on an invalid slot.")]
        [Range(0f, 0.5f)]
        public float GhostSnapBackDuration = 0.18f;

        [Tooltip("Maximum interval (seconds) between two clicks that counts as a double-click on an inventory slot.")]
        [Range(0.1f, 1f)]
        public float DoubleClickThreshold = 0.3f;

        // ── Attachment Slot Highlight ─────────────────────────────────────────

        [Header("Attachment Slot Highlight")]
        [Tooltip("Color overlay applied to compatible attachment slots when the player is dragging an attachment.")]
        public Color AttachmentSlotHighlightColor = new Color(1f, 0.85f, 0.1f, 0.7f);

        [Tooltip("Speed of the pulse animation on highlighted attachment slots (cycles per second).")]
        [Range(0.5f, 8f)]
        public float AttachmentHighlightPulseSpeed = 2.5f;

        [Tooltip("Per-slot-type icon displayed when the attachment slot is empty. " +
                 "If no entry matches the slot type, falls back to the generic Attachment icon from InventoryConfig.")]
        public AttachmentSlotIconConfig[] AttachmentSlotIconConfigs;

        /// <summary>
        /// Returns the icon configured for <paramref name="slotType"/>, or null if not found.
        /// Callers may fall back to <c>InventoryConfig.GetDefaultEmptyIcon(UISlotType.Attachment)</c>.
        /// </summary>
        public Sprite GetAttachmentSlotIcon(AttachmentSlotType slotType)
        {
            if (AttachmentSlotIconConfigs == null) return null;
            foreach (var cfg in AttachmentSlotIconConfigs)
            {
                if (cfg.SlotType == slotType && cfg.Icon != null)
                    return cfg.Icon;
            }
            return null;
        }
    }

    /// <summary>
    /// Config cho background theo rarity
    /// </summary>
    [System.Serializable]
    public struct RarityBackgroundConfig
    {
        [Tooltip("Rarity level")]
        public ItemRarity Rarity;

        [Tooltip("Background icon/sprite cho rarity này")]
        public Sprite BackgroundIcon;
    }

    /// <summary>
    /// Config cho attachment slot icon
    /// </summary>
    [System.Serializable]
    public struct AttachmentSlotIconConfig
    {
        [Tooltip("Attachment slot type")]
        public AttachmentSlotType SlotType;

        [Tooltip("Icon mặc định cho slot type này khi trống")]
        public Sprite Icon;
    }
}
