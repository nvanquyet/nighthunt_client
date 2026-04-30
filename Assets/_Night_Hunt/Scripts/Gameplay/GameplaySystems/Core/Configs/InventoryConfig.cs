using NightHunt.GameplaySystems.Core.Data;
using UnityEngine;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.UI.Inventory;

namespace NightHunt.GameplaySystems.Core.Configs
{
    public enum TooltipMode { FollowMouse, SnapToSlot, Fixed }
    public enum ContextMenuSide { Right, Left }

    [System.Serializable]
    public struct RarityBackgroundConfig
    {
        public ItemRarity Rarity;
        public Sprite BackgroundIcon;
    }

    [System.Serializable]
    public struct AttachmentSlotIconConfig
    {
        public AttachmentSlotType SlotType;
        public Sprite Icon;
    }

    /// <summary>
    /// Unified ScriptableObject configuration for the inventory, equipment, weapon,
    /// attachment, and item-drop systems.
    ///
    /// A single shared asset is assigned to InventorySystem, EquipmentSystem, WeaponSystem,
    /// and AttachmentSystem on each player prefab. The static <see cref="Instance"/> accessor
    /// is populated automatically when the asset is loaded by Unity (<c>OnEnable</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfig",
        menuName = "NightHunt/Gameplay/Inventory Config")]
    public class InventoryConfig : ScriptableObject
    {
        // ── Singleton accessor — populated automatically when the asset is loaded ─────
        private static InventoryConfig _instance;
        /// <summary>Global accessor. Valid once the asset has been loaded by Unity.</summary>
        public static InventoryConfig Instance => _instance;
        private void OnEnable() => _instance = this;

        #region ========== INVENTORY CONFIG ==========

        [Header("Inventory")]
        [Tooltip("Default icon shown in an empty inventory slot.")]
        public Sprite InventoryEmptyIcon;

        #endregion

        #region ========== EQUIPMENT CONFIG ==========

        [Header("Equipment")]
        [Tooltip("Equipment slot definitions — one entry per body slot with its default empty icon.")]
        public EquipmentSlotConfigStruct[] EquipmentConfig;

        /// <summary>Returns the number of defined equipment slots.</summary>
        public int EquipmentCount => EquipmentConfig != null ? EquipmentConfig.Length : 0;

        #endregion

        #region ========== UI SETTINGS ==========

        [Header("UI — Slot")]
        public Vector2 DefaultSlotSize = new Vector2(100f, 100f);
        public RarityBackgroundConfig[] RarityBackgrounds;
        public Sprite DefaultSlotBackground;

        [Header("UI — Tooltip")]
        public TooltipMode TooltipMode = TooltipMode.FollowMouse;
        public Vector2 TooltipOffset = new Vector2(16f, -16f);
        public Vector2 TooltipFixedPosition = new Vector2(200f, -200f);
        public bool ShowTooltipDuringDrag = false;

        [Header("UI — Context Menu")]
        public ContextMenuSide ContextMenuPreferredSide = ContextMenuSide.Right;
        public float ContextMenuGap = 8f;
        public bool HideContextMenuOnDragStart = true;

        [Header("UI — Drag & Drop")]
        [Range(0f, 0.5f)] public float GhostSnapBackDuration = 0.18f;
        [Range(0.1f, 1f)] public float DoubleClickThreshold = 0.3f;

        [Header("UI — Attachment Highlight")]
        public Color AttachmentSlotHighlightColor = new Color(1f, 0.85f, 0.1f, 0.7f);
        [Range(0.5f, 8f)] public float AttachmentHighlightPulseSpeed = 2.5f;
        public AttachmentSlotIconConfig[] AttachmentSlotIconConfigs;

        #endregion

        #region ========== BEHAVIOR SETTINGS ==========

        [Header("Behavior Settings")]
        [Tooltip("Automatically merge incoming items into existing stacks of the same type.")]
        public bool AutoStackOnAdd = true;

        [Tooltip("Automatically merge stacks when moving or dragging items within the inventory.")]
        public bool AutoMergeOnMove = true;

        #endregion

        #region ========== DROP SETTINGS ==========

        [Header("Drop Settings")]
        [Tooltip("Distance (in metres) in front of the player where dropped items spawn.")]
        [Min(0.5f)]
        public float DropDistance = 2f;

        [Tooltip("Initial physics impulse force applied to dropped items.")]
        [Min(0f)]
        public float DropForce = 5f;

        #endregion

        #region ========== WEIGHT PENALTY ==========

        [Header("Weight — Carry Limit")]
        [Tooltip("When true, players CANNOT pick up items that would exceed MaxWeight (hard cap). " +
                 "When false, weight can exceed the cap — penalties from WeightPenalties still apply.")]
        public bool AllowExceedMaxWeight = true;

        [Tooltip("Penalty tiers applied based on currentWeight / maxWeight ratio. " +
                 "Assign a WeightPenaltyConfig asset to enable weight-based stat penalties. " +
                 "Leave null to disable penalties entirely.")]
        public WeightPenaltyConfig WeightPenalties;

        #endregion

        #region ========== PERFORMANCE SETTINGS ==========

        [Header("Memory Management")]
        [Tooltip("Maximum number of item instances to keep in the runtime cache (0 = unlimited). Recommended: 100 for mobile.")]
        [Range(0, 500)]
        public int MaxCachedInstances = 100;

        [Tooltip("Maximum number of cached item definitions per type (0 = unlimited).")]
        [Range(0, 100)]
        public int MaxCachedDefinitionsByType = 50;

        [Header("Performance")]
        [Tooltip("Batch carry-weight recalculations to reduce per-frame stat updates.")]
        public bool BatchWeightUpdates = true;

        [Tooltip("Pre-warm the type-lookup cache on system initialisation.")]
        public bool PrewarmTypeLookup = true;

        [Tooltip("Automatically remove invalid / orphaned items when syncing state.")]
        public bool AutoCleanupInvalidItems = true;

        [Header("Network Optimization")]
        [Tooltip("Minimum interval (seconds) between carry-weight pushes to PlayerStatSystem.")]
        [Range(0f, 1f)]
        public float WeightUpdateInterval = 0.1f;

        [Tooltip("Enable delta-compression of item data sent over the network.")]
        public bool CompressNetworkData = true;

        #endregion

        #region ========== ATTACHMENT SETTINGS ==========

        [Header("Attachment Behavior")]
        [Tooltip("When an equipped item is unequipped: detach its attachments and return them to inventory.")]
        public bool DetachAttachmentsOnUnequip = true;

        [Tooltip("When an item is dropped: return attachments to inventory (true) or drop them with the item (false).")]
        public bool ReturnAttachmentsToInventoryOnDrop = true;

        #endregion

        #region ========== DEBUG ==========

        [Header("Debug")]
        [Tooltip("Enable per-frame performance logging for the inventory system.")]
        public bool EnablePerformanceLogging = false;

        [Tooltip("Log cache hit / miss statistics for profiling.")]
        public bool LogCacheStatistics = false;

        #endregion

        #region ========== HELPER METHODS ==========

        /// <summary>Returns the default empty icon for the given slot type and optional sub-type.</summary>
        public Sprite GetDefaultEmptyIcon(UISlotType slotType,
            EquipmentSlotType? equipmentSlot = null,
            WeaponSlotType?    weaponSlot    = null)
        {
            switch (slotType)
            {
                case UISlotType.Inventory:
                    return InventoryEmptyIcon;

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
                    return InventoryEmptyIcon;

                case UISlotType.Attachment:
                    return null;
            }

            return null;
        }

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

        public Sprite GetRarityBackground(ItemRarity rarity)
        {
            if (RarityBackgrounds == null) return DefaultSlotBackground;
            foreach (var config in RarityBackgrounds)
            {
                if (config.Rarity == rarity)
                    return config.BackgroundIcon;
            }
            return DefaultSlotBackground;
        }

        /// <summary>Returns the config struct for the given equipment slot type, or null if not configured.</summary>
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
            Debug.Log("[InventoryConfig] Default equipment slots created — assign icons in the Inspector.");
        }



        [ContextMenu("Validate Config")]
        private void ValidateConfig()
        {
            bool hasErrors = false;

            if (EquipmentConfig == null || EquipmentConfig.Length == 0)
                Debug.LogWarning("[InventoryConfig] EquipmentConfig is empty — use 'Setup Default Equipment Slots'.");



            if (!hasErrors)
                Debug.Log("[InventoryConfig] Validation passed.");
        }
#endif

        #endregion
    }

    #region ========== EQUIPMENT SLOT CONFIG ==========

    [System.Serializable]
    public struct EquipmentSlotConfigStruct
    {
        [Tooltip("The body slot type this entry configures.")]
        public EquipmentSlotType Type;

        [Tooltip("Default icon shown when this equipment slot is empty.")]
        public Sprite DefaultIcon;
    }

    #endregion

}
