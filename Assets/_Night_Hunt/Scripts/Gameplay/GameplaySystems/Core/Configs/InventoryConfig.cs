using NightHunt.GameplaySystems.Core.Data;
using UnityEngine;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.UI.Inventory;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Unified configuration for inventory system
    /// Contains all settings for inventory, equipment, weapons, item selection, UI, and behavior
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfig",
        menuName = "NightHunt/Gameplay/Inventory Config")]
    public class InventoryConfig : ScriptableObject
    {
        #region ========== INVENTORY CONFIG ==========
        
        [Header("Inventory")]
        public InventorySlotConfig Inventory;
        
        #endregion
        
        #region ========== EQUIPMENT CONFIG ==========
        
        [Header("Equipment")]
        [Tooltip("Định nghĩa các equipment slots với type và default icon")]
        public EquipmentSlotConfigStruct[] EquipmentConfig;
        
        /// <summary>
        /// Get count of equipment slots
        /// </summary>
        public int EquipmentCount => EquipmentConfig != null ? EquipmentConfig.Length : 0;
        
        #endregion
        
        #region ========== WEAPON CONFIG ==========
        
        [Header("Weapon")]
        [Tooltip("Định nghĩa các weapon slots với type và default icon")]
        public WeaponSlotConfigStruct[] WeaponConfig;
        
        /// <summary>
        /// Get count of weapon slots
        /// </summary>
        public int WeaponCount => WeaponConfig != null ? WeaponConfig.Length : 0;
        
        #endregion
        
        #region ========== ATTACHMENT UI ==========
        
        [Header("Attachment UI")]
        [Tooltip("UI config cho attachment slots (icon mặc định, behaviour panel).")]
        public AttachmentUIConfigStruct AttachmentUI;
        
        #endregion
        
        #region ========== BEHAVIOR SETTINGS ==========
        
        [Header("Behavior Settings")]
        [Tooltip("Tự động stack items khi add vào inventory")]
        public bool AutoStackOnAdd = true;
        
        [Tooltip("Tự động merge stacks khi move/drag")]
        public bool AutoMergeOnMove = true;
        
        #endregion
        
        #region ========== DROP SETTINGS ==========
        
        [Header("Drop Settings")]
        [Tooltip("Khoảng cách drop từ player (meters)")]
        [Min(0.5f)]
        public float DropDistance = 2f;
        
        [Tooltip("Drop force")]
        [Min(0f)]
        public float DropForce = 5f;
        
        #endregion
        
        #region ========== PERFORMANCE SETTINGS ==========
        
        [Header("Memory Management")]
        [Tooltip("Max cached item instances (0 = unlimited). Recommended: 100 for mobile")]
        [Range(0, 500)]
        public int MaxCachedInstances = 100;

        [Tooltip("Max cached item definitions by type (0 = unlimited)")]
        [Range(0, 100)]
        public int MaxCachedDefinitionsByType = 50;

        [Header("Performance")]
        [Tooltip("Batch weight updates to reduce stat recalculations")]
        public bool BatchWeightUpdates = true;

        [Tooltip("Pre-warm type lookup cache on init")]
        public bool PrewarmTypeLookup = true;

        [Tooltip("Auto-cleanup invalid items on sync")]
        public bool AutoCleanupInvalidItems = true;

        [Header("Network Optimization")]
        [Tooltip("Min interval between weight updates (seconds)")]
        [Range(0f, 1f)]
        public float WeightUpdateInterval = 0.1f;

        [Tooltip("Compress item data for network sync")]
        public bool CompressNetworkData = true;
        
        #endregion
        
        #region ========== ATTACHMENT SETTINGS ==========
        
        [Header("Attachment Behavior")]
        [Tooltip("Khi unequip equipment: gỡ attachments và return vào inventory")]
        public bool DetachAttachmentsOnUnequip = true;
        
        [Tooltip("Khi drop item: gỡ attachments và return vào inventory (true) hoặc drop cùng item (false)")]
        public bool ReturnAttachmentsToInventoryOnDrop = true;
        
        #endregion
        
        #region ========== DEBUG ==========

        [Header("Debug")]
        [Tooltip("Enable performance logging")]
        public bool EnablePerformanceLogging = false;

        [Tooltip("Log cache hits/misses")]
        public bool LogCacheStatistics = false;
        
        #endregion
        
        #region ========== HELPER METHODS ==========
        
        /// <summary>
        /// Get default empty icon for a specific slot type
        /// </summary>
        public Sprite GetDefaultEmptyIcon(UISlotType slotType, EquipmentSlotType? equipmentSlot = null, WeaponSlotType? weaponSlot = null)
        {
            switch (slotType)
            {
                case UISlotType.Inventory:
                    return Inventory.DefaultEmptyIcon;
                    
                case UISlotType.Equipment:
                    if (equipmentSlot.HasValue && EquipmentConfig != null)
                    {
                        foreach (var config in EquipmentConfig)
                        {
                            if (config.Type == equipmentSlot.Value)
                                return config.DefaultIcon;
                        }
                    }
                    break;
                    
                case UISlotType.Weapon:
                    if (weaponSlot.HasValue && WeaponConfig != null)
                    {
                        foreach (var config in WeaponConfig)
                        {
                            if (config.Type == weaponSlot.Value)
                                return config.DefaultIcon;
                        }
                    }
                    break;
                    
                case UISlotType.Attachment:
                    return AttachmentUI.DefaultEmptyIcon;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get equipment slot config by type
        /// </summary>
        public EquipmentSlotConfigStruct? GetEquipmentSlot(EquipmentSlotType type)
        {
            if (EquipmentConfig == null) return null;
            
            foreach (var config in EquipmentConfig)
            {
                if (config.Type == type)
                    return config;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get weapon slot config by type
        /// </summary>
        public WeaponSlotConfigStruct? GetWeaponSlot(WeaponSlotType type)
        {
            if (WeaponConfig == null) return null;
            
            foreach (var config in WeaponConfig)
            {
                if (config.Type == type)
                    return config;
            }
            
            return null;
        }
        
        #endregion
        
        #region ========== EDITOR SETUP ==========
        
#if UNITY_EDITOR
        [ContextMenu("Setup Default Equipment Slots")]
        private void SetupDefaultEquipmentSlots()
        {
            EquipmentConfig = new EquipmentSlotConfigStruct[]
            {
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Head,   DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Face,   DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Chest,  DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Back,   DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Belt,   DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Legs,   DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Feet,   DefaultIcon = null },
                new EquipmentSlotConfigStruct { Type = EquipmentSlotType.Hands,  DefaultIcon = null }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[InventoryConfig] Setup default equipment slots complete! Assign default icons in Inspector.");
        }
        
        [ContextMenu("Setup Default Weapon Slots")]
        private void SetupDefaultWeaponSlots()
        {
            WeaponConfig = new WeaponSlotConfigStruct[]
            {
                new WeaponSlotConfigStruct { Type = WeaponSlotType.Primary,   DefaultIcon = null },
                new WeaponSlotConfigStruct { Type = WeaponSlotType.Secondary, DefaultIcon = null },
                new WeaponSlotConfigStruct { Type = WeaponSlotType.Melee,     DefaultIcon = null }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[InventoryConfig] Setup default weapon slots complete! Assign default icons in Inspector.");
        }
        
        [ContextMenu("Validate Config")]
        private void ValidateConfig()
        {
            bool hasErrors = false;
            
            // Validate inventory
            if (Inventory.GridWidth < 1 || Inventory.GridWidth > 20)
            {
                Debug.LogError($"[InventoryConfig] Inventory.GridWidth must be 1-20, current: {Inventory.GridWidth}");
                hasErrors = true;
            }
            
            if (Inventory.GridHeight < 1 || Inventory.GridHeight > 20)
            {
                Debug.LogError($"[InventoryConfig] Inventory.GridHeight must be 1-20, current: {Inventory.GridHeight}");
                hasErrors = true;
            }
            
            // Validate equipment
            if (EquipmentConfig == null || EquipmentConfig.Length == 0)
            {
                Debug.LogWarning("[InventoryConfig] EquipmentConfig is empty. Use 'Setup Default Equipment Slots' context menu.");
            }
            
            // Validate weapon
            if (WeaponConfig == null || WeaponConfig.Length == 0)
            {
                Debug.LogWarning("[InventoryConfig] WeaponConfig is empty. Use 'Setup Default Weapon Slots' context menu.");
            }
            
            if (!hasErrors)
            {
                Debug.Log("[InventoryConfig] Validation passed!");
            }
        }
#endif
        
        #endregion
    }
    
    #region ========== INVENTORY CONFIG STRUCT ==========
    
    [System.Serializable]
    public struct InventorySlotConfig
    {
        [Header("Grid Size")]
        [Tooltip("Number of columns in the inventory grid")]
        [Range(1, 20)]
        public int GridWidth;
        
        [Tooltip("Number of rows in the inventory grid")]
        [Range(1, 20)]
        public int GridHeight;
        
        [Header("UI")]
        [Tooltip("Icon mặc định cho inventory slot trống (khi chưa có item)")]
        public Sprite DefaultEmptyIcon;
        
        public int TotalSlots => GridWidth * GridHeight;
    }
    
    #endregion
    
    #region ========== EQUIPMENT CONFIG STRUCT ==========
    
    [System.Serializable]
    public struct EquipmentSlotConfigStruct
    {
        [Tooltip("Equipment slot type")]
        public EquipmentSlotType Type;
        
        [Tooltip("Icon mặc định cho slot này khi trống")]
        public Sprite DefaultIcon;
    }
    
    #endregion
    
    #region ========== WEAPON CONFIG STRUCT ==========
    
    [System.Serializable]
    public struct WeaponSlotConfigStruct
    {
        [Tooltip("Weapon slot type")]
        public WeaponSlotType Type;
        
        [Tooltip("Icon mặc định cho slot này khi trống")]
        public Sprite DefaultIcon;
    }
    
    #endregion
    
    #region ========== ATTACHMENT UI CONFIG STRUCT ==========

    [System.Serializable]
    public struct AttachmentUIConfigStruct
    {
        [Header("UI")]
        [Tooltip("Icon mặc định cho attachment slot trống")]
        public Sprite DefaultEmptyIcon;

        [Header("Behaviour")]
        [Tooltip("Show attachment panel khi hover equipment item")]
        public bool ShowAttachmentPanelOnHover;

        [Tooltip("Show attachment panel khi select/click equipment item")]
        public bool ShowAttachmentPanelOnSelect;
    }

    #endregion
}
