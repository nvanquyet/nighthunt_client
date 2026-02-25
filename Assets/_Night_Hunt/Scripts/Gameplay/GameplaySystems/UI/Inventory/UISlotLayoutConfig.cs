using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Item rarity levels
    /// </summary>
    public enum ItemRarity
    {
        Common,     // Xám/Trắng
        Uncommon,   // Xanh lá
        Rare,       // Xanh dương
        Epic,       // Tím
        Legendary,  // Vàng/Cam
        Mythic      // Đỏ/Hồng
    }

    /// <summary>
    /// Unified UI layout config cho tất cả slot types.
    /// Reference InventoryConfig cho gameplay data, chỉ chứa UI-specific settings (prefabs, backgrounds).
    /// </summary>
    [CreateAssetMenu(
        fileName = "UISlotLayoutConfig",
        menuName = "GameplaySystems/UI/Slot Layout")]
    public class UISlotLayoutConfig : ScriptableObject
    {
        [Header("Core Config Reference")]
        [Tooltip("Reference to core InventoryConfig for all gameplay data")]
        public InventoryConfig InventoryConfig;

        [Header("Prefabs")]
        [Tooltip("Prefab chung cho tất cả slot types (Inventory, Equipment, Weapon, QuickSlot). " +
                 "Có thể dùng 1 prefab cho tất cả hoặc override riêng bên dưới.")]
        public GameObject DefaultSlotPrefab;

        [Tooltip("Prefab riêng cho Equipment slots (optional, nếu null sẽ dùng DefaultSlotPrefab)")]
        public GameObject EquipmentSlotPrefab;

        [Tooltip("Prefab riêng cho Weapon slots (optional, nếu null sẽ dùng DefaultSlotPrefab)")]
        public GameObject WeaponSlotPrefab;

        [Tooltip("Prefab riêng cho QuickSlot (optional, nếu null sẽ dùng DefaultSlotPrefab)")]
        public GameObject QuickSlotPrefab;

        [Header("Rarity Backgrounds")]
        [Tooltip("Background icon/sprite cho từng rarity level của item")]
        public RarityBackgroundConfig[] RarityBackgrounds;

        [Tooltip("Background mặc định cho các item không có rarity hoặc rarity không được định nghĩa")]
        public Sprite DefaultBackground;

        [Header("Attachment UI")]
        [Tooltip("Prefab cho attachment slot")]
        public GameObject AttachmentSlotPrefab;

        /// <summary>
        /// Get prefab cho slot type cụ thể
        /// </summary>
        public GameObject GetSlotPrefab(UISlotType slotType)
        {
            switch (slotType)
            {
                case UISlotType.Equipment:
                    return EquipmentSlotPrefab != null ? EquipmentSlotPrefab : DefaultSlotPrefab;
                case UISlotType.Weapon:
                    return WeaponSlotPrefab != null ? WeaponSlotPrefab : DefaultSlotPrefab;
                case UISlotType.QuickSlot:
                    return QuickSlotPrefab != null ? QuickSlotPrefab : DefaultSlotPrefab;
                case UISlotType.Attachment:
                    return AttachmentSlotPrefab != null ? AttachmentSlotPrefab : DefaultSlotPrefab;
                default:
                    return DefaultSlotPrefab;
            }
        }

        /// <summary>
        /// Get background sprite cho rarity cụ thể
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

        // Properties từ InventoryConfig
        public int InventoryGridWidth => InventoryConfig != null ? InventoryConfig.Inventory.GridWidth : 5;
        public int InventoryGridHeight => InventoryConfig != null ? InventoryConfig.Inventory.GridHeight : 4;
        public int InventoryTotalSlots => InventoryConfig != null ? InventoryConfig.Inventory.TotalSlots : 20;
        public int QuickSlotCount => InventoryConfig != null ? InventoryConfig.QuickSlotConfig.SlotCount : 4;
        public int EquipmentCount => InventoryConfig != null ? InventoryConfig.EquipmentCount : 0;
        public int WeaponCount => InventoryConfig != null ? InventoryConfig.WeaponCount : 0;
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
