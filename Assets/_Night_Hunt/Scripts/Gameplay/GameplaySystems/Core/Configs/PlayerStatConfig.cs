using UnityEngine;
using GameplaySystems.Stat;

namespace GameplaySystems.Core.Configs
{
    /// <summary>
    /// Configuration for player stat definitions ONLY
    /// Defines default values, min/max, and UI display settings for player stats
    /// 
    /// Create via: Assets → Create → GameplaySystems/Config/Player Stat Config
    /// 
    /// NOTE: Item stats are defined directly in ItemDefinition subclasses (WeaponDefinition, ArmorDefinition)
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatConfig", menuName = "GameplaySystems/Config/Player Stat Config")]
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
                    DisplayFormat = "0"
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MaxHealth,
                    DisplayName = "Max Health",
                    DefaultValue = 100f,
                    MinValue = 1f,
                    MaxValue = 1000f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.8f, 0.2f, 0.2f),
                    DisplayFormat = "0"
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
                    DisplayFormat = "0"
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MaxStamina,
                    DisplayName = "Max Stamina",
                    DefaultValue = 100f,
                    MinValue = 1f,
                    MaxValue = 500f,
                    ShowInUI = true,
                    DisplayColor = new Color(0.2f, 0.8f, 0.2f),
                    DisplayFormat = "0"
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
                    DisplayFormat = "0.0"
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
                    DisplayFormat = "0.0"
                },

                // Weight System
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.WeightCapacity,
                    DisplayName = "Weight Capacity",
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    ShowInUI = true,
                    DisplayColor = new Color(1f, 0.8f, 0.2f), // Yellow
                    DisplayFormat = "0.0"
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
                    DisplayFormat = "0.0"
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
                    DisplayFormat = "0"
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
                    DisplayFormat = "0"
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
                    DisplayFormat = "0.0"
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
                    DisplayFormat = "0"
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
    }

    #endregion
}