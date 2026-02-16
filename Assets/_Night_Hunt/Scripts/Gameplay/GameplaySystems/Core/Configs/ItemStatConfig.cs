using GameplaySystems.Stat;
using UnityEngine;

namespace GameplaySystems.Core.Configs
{
    [CreateAssetMenu(fileName = "ItemStatConfig", menuName = "GameplaySystems/Config/Item Stat Config")]
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

            Debug.LogWarning($"[StatConfig] Item stat definition not found for type: {type}");
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
                            Debug.LogError($"[StatConfig] Duplicate item stat definition: {ItemStats[i].Type}");
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
            ItemStats = new ItemStatDefinition[]
            {
                // Weapon Stats
                new ItemStatDefinition
                {
                    Type = ItemStatType.Damage,
                    DisplayName = "Damage",
                    DisplayColor = new Color(1f, 0.3f, 0.3f),
                    DisplayFormat = "0",
                    IsPositiveStat = true
                },
                new ItemStatDefinition
                {
                    Type = ItemStatType.FireRate,
                    DisplayName = "Fire Rate",
                    DisplayColor = new Color(1f, 0.6f, 0.2f),
                    DisplayFormat = "0",
                    IsPositiveStat = true
                },
                new ItemStatDefinition
                {
                    Type = ItemStatType.Accuracy,
                    DisplayName = "Accuracy",
                    DisplayColor = new Color(0.3f, 1f, 0.3f),
                    DisplayFormat = "0",
                    IsPositiveStat = true
                },
                new ItemStatDefinition
                {
                    Type = ItemStatType.Recoil,
                    DisplayName = "Recoil",
                    DisplayColor = new Color(1f, 0.4f, 0.4f),
                    DisplayFormat = "0",
                    IsPositiveStat = false // Lower is better
                },
                new ItemStatDefinition
                {
                    Type = ItemStatType.Spread,
                    DisplayName = "Spread",
                    DisplayColor = new Color(1f, 0.5f, 0.5f),
                    DisplayFormat = "0.0",
                    IsPositiveStat = false
                },
                new ItemStatDefinition
                {
                    Type = ItemStatType.Range,
                    DisplayName = "Range",
                    DisplayColor = new Color(0.4f, 0.8f, 1f),
                    DisplayFormat = "0",
                    IsPositiveStat = true
                },

                // Armor Stats
                new ItemStatDefinition
                {
                    Type = ItemStatType.ArmorValue,
                    DisplayName = "Armor Value",
                    DisplayColor = new Color(0.6f, 0.6f, 0.6f),
                    DisplayFormat = "0",
                    IsPositiveStat = true
                },

                // Weight
                new ItemStatDefinition
                {
                    Type = ItemStatType.Weight,
                    DisplayName = "Weight",
                    DisplayColor = new Color(0.8f, 0.8f, 0.5f),
                    DisplayFormat = "0.0",
                    IsPositiveStat = false
                }
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[StatConfig] Setup default item stats complete!");
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