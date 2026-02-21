using UnityEngine;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// Defines how a stat should be displayed in UI
    /// </summary>
    public enum StatDisplayType
    {
        /// <summary>
        /// Hiển thị dạng slider với current/max (Health/MaxHealth)
        /// </summary>
        SliderWithMax,
        
        /// <summary>
        /// Hiển thị dạng slider với min/max range (MovementSpeed: 0-20)
        /// </summary>
        SliderWithRange,
        
        /// <summary>
        /// Hiển thị dạng text thường (Armor: 50)
        /// </summary>
        Text,
        
        /// <summary>
        /// Hiển thị dạng progress bar (Weight: current/capacity)
        /// </summary>
        ProgressBar
    }

    /// <summary>
    /// Configuration for player stat definitions
    /// 
    /// RESPONSIBILITIES:
    /// - Defines default values, min/max, and UI display settings for player stats
    /// - Used by PlayerStatSystem for initialization
    /// 
    /// USAGE:
    /// Create via: Assets → Create → StatSystem/Config/Player Stat Config
    /// 
    /// NOTE: Item stats are defined directly in ItemDefinition subclasses (WeaponDefinition, EquipmentDefinition)
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatConfig", menuName = "StatSystem/Config/Player Stat Config")]
    public class PlayerStatConfig : ScriptableObject
    {
        [Header("Player Stats")]
        [Tooltip("Định nghĩa tất cả stats của player")]
        public PlayerStatDefinition[] Stats;

        #region Helpers

        /// <summary>
        /// Get player stat definition by type
        /// </summary>
        public PlayerStatDefinition GetStatDefinition(PlayerStatType type)
        {
            if (Stats == null)
                return default;

            foreach (var stat in Stats)
            {
                if (stat.Type == type)
                    return stat;
            }

            Debug.LogWarning($"[PlayerStatConfig] Stat definition not found for type: {type}");
            return default;
        }

        /// <summary>
        /// Check if stat exists in config
        /// </summary>
        public bool HasStat(PlayerStatType type)
        {
            if (Stats == null)
                return false;

            foreach (var stat in Stats)
            {
                if (stat.Type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get display type for a stat
        /// </summary>
        public StatDisplayType GetDisplayType(PlayerStatType type)
        {
            var def = GetStatDefinition(type);
            return def.DisplayType;
        }

        /// <summary>
        /// Get related max stat for a stat (if exists)
        /// Returns null if stat doesn't have a max stat
        /// </summary>
        public PlayerStatType? GetRelatedMaxStat(PlayerStatType type)
        {
            var def = GetStatDefinition(type);
            
            // Check if RelatedMaxStat is actually a different stat (not just default value)
            // If RelatedMaxStat equals Type, it means no max stat is set
            if (def.RelatedMaxStat == type)
                return null;
            
            // Check if RelatedMaxStat is valid (exists in config)
            if (HasStat(def.RelatedMaxStat))
                return def.RelatedMaxStat;
            
            return null;
        }

        /// <summary>
        /// Check if stat should show max value in UI
        /// </summary>
        public bool ShouldShowMaxValue(PlayerStatType type)
        {
            var def = GetStatDefinition(type);
            return def.ShowMaxValue;
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            // Check for duplicate stats
            if (Stats != null)
            {
                for (int i = 0; i < Stats.Length; i++)
                {
                    for (int j = i + 1; j < Stats.Length; j++)
                    {
                        if (Stats[i].Type == Stats[j].Type)
                        {
                            Debug.LogError($"[PlayerStatConfig] Duplicate stat definition: {Stats[i].Type}");
                        }
                    }
                }
            }
        }

        #endregion

        #region Editor Setup

#if UNITY_EDITOR
        [ContextMenu("Setup Default Stats")]
        private void SetupDefaultStats()
        {
            Stats = new PlayerStatDefinition[]
            {
                // Core Vitals
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.Health,
                    DisplayName = "Health",
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 1000f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.8f, 0.2f, 0.2f), // Red
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.SliderWithMax,
                    RelatedMaxStat = PlayerStatType.MaxHealth,
                    ShowMaxValue = true
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MaxHealth,
                    DisplayName = "Max Health",
                    DefaultValue = 100f,
                    MinValue = 1f,
                    MaxValue = 1000f,
                    ShowInUI = false, // Không hiển thị riêng, chỉ dùng cho Health slider
                    DisplayColor = new Color(0.8f, 0.2f, 0.2f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng set để consistency
                    ShowMaxValue = false
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.Stamina,
                    DisplayName = "Stamina",
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.2f, 0.8f, 0.2f), // Green
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.SliderWithMax,
                    RelatedMaxStat = PlayerStatType.MaxStamina,
                    ShowMaxValue = true
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MaxStamina,
                    DisplayName = "Max Stamina",
                    DefaultValue = 100f,
                    MinValue = 1f,
                    MaxValue = 500f,
                    ShowInUI = false, // Không hiển thị riêng, chỉ dùng cho Stamina slider
                    DisplayColor = new Color(0.2f, 0.8f, 0.2f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Stamina, // Không dùng, nhưng set để consistency
                    ShowMaxValue = false
                },

                // Movement
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MovementSpeed,
                    DisplayName = "Movement Speed",
                    DefaultValue = 5f,
                    MinValue = 0f,
                    MaxValue = 20f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.2f, 0.6f, 1f), // Blue
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.SliderWithRange,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng cần set default
                    ShowMaxValue = false
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.SprintSpeed,
                    DisplayName = "Sprint Speed",
                    DefaultValue = 8f,
                    MinValue = 0f,
                    MaxValue = 30f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.2f, 0.6f, 1f),
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng cần set default
                    ShowMaxValue = false
                },

                // Weight System
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.WeightCapacity,
                    DisplayName = "Weight Capacity",
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    ShowInUI = false, // Không hiển thị riêng, chỉ dùng cho Weight progress bar
                    DisplayColor = new Color(1f, 0.8f, 0.2f), // Yellow
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.CurrentWeight, // Không dùng, nhưng set để consistency
                    ShowMaxValue = false
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.CurrentWeight,
                    DisplayName = "Current Weight",
                    DefaultValue = 0f,
                    MinValue = 0f,
                    MaxValue = 1000f,
                    ShowInUI = true,
                    DisplayColor = new Color(1f, 0.8f, 0.2f),
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.ProgressBar,
                    RelatedMaxStat = PlayerStatType.WeightCapacity,
                    ShowMaxValue = true
                },

                // Combat - Defense
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.Armor,
                    DisplayName = "Armor",
                    DefaultValue = 0f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.6f, 0.6f, 0.6f), // Gray
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng cần set default
                    ShowMaxValue = false
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MagicResist,
                    DisplayName = "Magic Resist",
                    DefaultValue = 0f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    ShowInUI = false,
                    DisplayColor = new Color(0.8f, 0.4f, 1f), // Purple
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng cần set default
                    ShowMaxValue = false
                },

                // Vision
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.VisionRange,
                    DisplayName = "Vision Range",
                    DefaultValue = 50f,
                    MinValue = 0f,
                    MaxValue = 200f,
                    ShowInUI = false,
                    DisplayColor = Color.white,
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng cần set default
                    ShowMaxValue = false
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.NightVision,
                    DisplayName = "Night Vision",
                    DefaultValue = 0f,
                    MinValue = 0f,
                    MaxValue = 100f,
                    ShowInUI = false,
                    DisplayColor = new Color(0.2f, 1f, 0.2f), // Bright green
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStat = PlayerStatType.Health, // Không dùng, nhưng cần set default
                    ShowMaxValue = false
                }
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PlayerStatConfig] Setup default player stats complete!");
        }
#endif

        #endregion
    }

    #region Stat Definition

    [System.Serializable]
    public struct PlayerStatDefinition
    {
        [Tooltip("Loại stat")]
        public PlayerStatType Type;

        [Tooltip("Tên hiển thị trên UI")]
        public string DisplayName;

        [Tooltip("Giá trị mặc định")]
        public float DefaultValue;

        [Tooltip("Giá trị tối thiểu")]
        public float MinValue;

        [Tooltip("Giá trị tối đa")]
        public float MaxValue;

        [Tooltip("Hiển thị trên UI không")]
        public bool ShowInUI;

        [Tooltip("Icon cho stat này")]
        public Sprite Icon;

        [Tooltip("Màu sắc hiển thị")]
        public Color DisplayColor;

        [Tooltip("Format string (e.g., '0', '0.0', '0%')")]
        public string DisplayFormat;
        
        [Tooltip("Cách hiển thị trên UI (Slider, Text, ProgressBar)")]
        public StatDisplayType DisplayType;
        
        [Tooltip("Stat tương ứng để tính max (VD: Health → MaxHealth, null nếu không có)")]
        public PlayerStatType RelatedMaxStat;
        
        [Tooltip("Nếu true, hiển thị cả current và max (VD: '75/100')")]
        public bool ShowMaxValue;
    }

    #endregion
}
