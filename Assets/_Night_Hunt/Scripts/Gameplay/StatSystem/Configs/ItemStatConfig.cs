using UnityEngine;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// Configuration for item stat definitions
    /// 
    /// RESPONSIBILITIES:
    /// - Defines display settings for item stats (UI colors, formats, etc.)
    /// - Used by UI for tooltips and stat displays
    /// - Separate from PlayerStatConfig (which defines player stats)
    /// 
    /// USAGE:
    /// Create via: Assets → Create → StatSystem/Config/Item Stat Config
    /// </summary>
    [CreateAssetMenu(fileName = "ItemStatConfig", menuName = "StatSystem/Config/Item Stat Config")]
    public class ItemStatConfig : ScriptableObject
    {
        [Header("Item Stats")]
        [Tooltip("Định nghĩa tất cả stats của items")]
        public ItemStatDefinition[] ItemStats;

        #region Item Stat Helpers

        /// <summary>
        /// Get item stat definition by type
        /// </summary>
        public ItemStatDefinition GetItemStatDefinition(ItemStatType type)
        {
            if (ItemStats == null)
                return default;

            foreach (var stat in ItemStats)
            {
                if (stat.Type == type)
                    return stat;
            }

            Debug.LogWarning($"[ItemStatConfig] Item stat definition not found for type: {type}");
            return default;
        }

        /// <summary>
        /// Check if item stat exists in config
        /// </summary>
        public bool HasItemStat(ItemStatType type)
        {
            if (ItemStats == null)
                return false;

            foreach (var stat in ItemStats)
            {
                if (stat.Type == type)
                    return true;
            }

            return false;
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            // Check for duplicate item stats
            if (ItemStats != null)
            {
                for (int i = 0; i < ItemStats.Length; i++)
                {
                    for (int j = i + 1; j < ItemStats.Length; j++)
                    {
                        if (ItemStats[i].Type == ItemStats[j].Type)
                        {
                            Debug.LogError($"[ItemStatConfig] Duplicate item stat definition: {ItemStats[i].Type}");
                        }
                    }
                }
            }
        }

        #endregion

        #region Editor Setup

#if UNITY_EDITOR
        [ContextMenu("Setup Default Item Stats")]
        private void SetupDefaultItemStats()
        {
            // Tự động tạo ItemStats theo toàn bộ enum ItemStatType
            var types = (ItemStatType[])System.Enum.GetValues(typeof(ItemStatType));
            ItemStats = new ItemStatDefinition[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];

                // Giá trị mặc định
                var def = new ItemStatDefinition
                {
                    Type = t,
                    DisplayName = t.ToString(),   // Có thể chỉnh lại trong Inspector
                    DisplayColor = Color.white,
                    DisplayFormat = "0",
                    IsPositiveStat = true
                };

                switch (t)
                {
                    // Weapon stats
                    case ItemStatType.Damage:
                        def.DisplayColor = new Color(1f, 0.3f, 0.3f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.FireRate:
                        def.DisplayColor = new Color(1f, 0.6f, 0.2f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.Accuracy:
                        def.DisplayColor = new Color(0.3f, 1f, 0.3f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.Recoil:
                    case ItemStatType.Spread:
                        def.DisplayColor = new Color(1f, 0.4f, 0.4f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = false; // thấp hơn là tốt
                        break;

                    case ItemStatType.Range:
                        def.DisplayColor = new Color(0.4f, 0.8f, 1f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    // Armor
                    case ItemStatType.ArmorValue:
                        def.DisplayColor = new Color(0.6f, 0.6f, 0.6f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    // Weight & penalties
                    case ItemStatType.Weight:
                    case ItemStatType.StaminaPenalty:
                    case ItemStatType.MovementSpeedPenalty:
                        def.DisplayColor = new Color(0.8f, 0.8f, 0.5f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = false;
                        break;

                    case ItemStatType.WeightCapacityBonus:
                        def.DisplayColor = new Color(1f, 0.8f, 0.2f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    // Others (light, reload, durability, ...)
                    default:
                        def.DisplayColor = Color.white;
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;
                }

                ItemStats[i] = def;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ItemStatConfig] Auto-setup item stats from ItemStatType enum complete!");
        }
#endif

        #endregion
    }

    #region Stat Definitions

    [System.Serializable]
    public struct ItemStatDefinition
    {
        [Tooltip("Loại stat")]
        public ItemStatType Type;

        [Tooltip("Tên hiển thị")]
        public string DisplayName;

        [Tooltip("Icon")]
        public Sprite Icon;

        [Tooltip("Màu sắc")]
        public Color DisplayColor;

        [Tooltip("Format string")]
        public string DisplayFormat;

        [Tooltip("Stat này có phải là positive stat không (tăng = tốt)")]
        public bool IsPositiveStat;
    }

    #endregion
}
