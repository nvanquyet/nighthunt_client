using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.Gameplay.StatSystem.Configs
{
    /// <summary>
    /// Defines how a stat should be displayed in UI
    /// </summary>
    public enum StatDisplayType
    {
        /// <summary>
        /// Display as slider with current/max (Health/MaxHealth)
	/// </summary>
        SliderWithMax,
        
        /// <summary>
        /// Display as slider with min/max range (MovementSpeed: 0-20)
        /// </summary>
        SliderWithRange,
        
        /// <summary>
        /// Display as plain text (Armor: 50)
        /// </summary>
        Text,
        
        /// <summary>
        /// Display as progress bar (Weight: current/capacity)
        /// </summary>
        ProgressBar
    }

    /// <summary>
    /// UI display configuration for player stats.
    ///
    /// RESPONSIBILITIES:
    /// - Display name, icon, colors (accent + text)
    /// - Format string, display type (slider / text / progress bar)
    /// - Whether to show max value in UI
    /// - Which stat provides the max reference for the slider (RelatedMaxStatType)
    /// - ShowInUI flag
    ///
    /// Kept completely separate from PlayerStatConfig (gameplay data).
    ///
    /// USAGE:
    /// Create via: Assets → Create → StatSystem/Config/Player Stat UI Config
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatUIConfig", menuName = "NightHunt/StatSystem/Player Stat UI Config")]
    public class PlayerStatUIConfig : ScriptableObject
    {
        [Header("UI Definitions")]
        [Tooltip("UI display settings for each player stat")]
        public PlayerStatUIDefinition[] Stats;

        #region Helpers

        /// <summary>
        /// Get UI definition by stat type. Returns default if not found.
        /// </summary>
        public PlayerStatUIDefinition GetUIDefinition(PlayerStatType type)
        {
            if (Stats == null) return default;

            foreach (var def in Stats)
            {
                if (def.Type == type)
                    return def;
            }

            Debug.LogWarning($"[PlayerStatUIConfig] UI definition not found: {type}");
            return default;
        }

        /// <summary>
        /// Returns true if a UI definition exists for this stat type.
        /// </summary>
        public bool HasUIDefinition(PlayerStatType type)
        {
            if (Stats == null) return false;

            foreach (var def in Stats)
            {
                if (def.Type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the related max stat type for slider display purposes.
        /// Returns null if RelatedMaxStatType == Type (no dynamic max).
        /// </summary>
        public PlayerStatType? GetRelatedMaxStat(PlayerStatType type)
        {
            var def = GetUIDefinition(type);

            if (def.RelatedMaxStatType == type)
                return null;

            return def.RelatedMaxStatType;
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            if (Stats == null) return;

            for (int i = 0; i < Stats.Length; i++)
            {
                for (int j = i + 1; j < Stats.Length; j++)
                {
                    if (Stats[i].Type == Stats[j].Type)
                    {
                        Debug.LogError($"[PlayerStatUIConfig] Duplicate UI definition: {Stats[i].Type}");
                    }
                }
            }
        }

        #endregion

        #region Editor Setup

#if UNITY_EDITOR
        [ContextMenu("Setup Default UI Stats")]
        private void SetupDefaultUIStats()
        {
            Stats = new PlayerStatUIDefinition[]
            {
                // Core Vitals
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.Health,
                    DisplayName = "Health",
                    ShowInUI = true,
                    DisplayColor = new Color(0.8f, 0.2f, 0.2f),
                    TextColor = new Color(1f, 0.6f, 0.6f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.SliderWithMax,
                    RelatedMaxStatType = PlayerStatType.MaxHealth,
                    ShowMaxValue = true
                },
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.MaxHealth,
                    DisplayName = "Max Health",
                    ShowInUI = false,
                    DisplayColor = new Color(0.8f, 0.2f, 0.2f),
                    TextColor = new Color(1f, 0.6f, 0.6f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.MaxHealth,
                    ShowMaxValue = false
                },
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.Stamina,
                    DisplayName = "Stamina",
                    ShowInUI = true,
                    DisplayColor = new Color(0.2f, 0.8f, 0.2f),
                    TextColor = new Color(0.6f, 1f, 0.6f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.SliderWithMax,
                    RelatedMaxStatType = PlayerStatType.MaxStamina,
                    ShowMaxValue = true
                },
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.MaxStamina,
                    DisplayName = "Max Stamina",
                    ShowInUI = false,
                    DisplayColor = new Color(0.2f, 0.8f, 0.2f),
                    TextColor = new Color(0.6f, 1f, 0.6f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.MaxStamina,
                    ShowMaxValue = false
                },

                // Movement
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.MovementSpeed,
                    DisplayName = "Move Speed",
                    ShowInUI = true,
                    DisplayColor = new Color(0.2f, 0.6f, 1f),
                    TextColor = new Color(0.6f, 0.85f, 1f),
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.SliderWithRange,
                    RelatedMaxStatType = PlayerStatType.MovementSpeed,
                    ShowMaxValue = false
                },

                // Weight System
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.WeightCapacity,
                    DisplayName = "Weight Capacity",
                    ShowInUI = false,
                    DisplayColor = new Color(1f, 0.8f, 0.2f),
                    TextColor = new Color(1f, 0.9f, 0.55f),
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.WeightCapacity,
                    ShowMaxValue = false
                },
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.CurrentWeight,
                    DisplayName = "Weight",
                    ShowInUI = true,
                    DisplayColor = new Color(1f, 0.8f, 0.2f),
                    TextColor = new Color(1f, 0.9f, 0.55f),
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.ProgressBar,
                    RelatedMaxStatType = PlayerStatType.WeightCapacity,
                    ShowMaxValue = true
                },

                // Combat - Defense
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.Armor,
                    DisplayName = "Armor",
                    ShowInUI = true,
                    DisplayColor = new Color(0.6f, 0.6f, 0.6f),
                    TextColor = new Color(0.85f, 0.85f, 0.85f),
                    DisplayFormat = "0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.Armor,
                    ShowMaxValue = false
                },

                // Vision
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.VisionRange,
                    DisplayName = "Vision Range",
                    ShowInUI = true,
                    DisplayColor = Color.white,
                    TextColor = new Color(0.9f, 0.9f, 0.9f),
                    DisplayFormat = "0.0",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.VisionRange,
                    ShowMaxValue = false
                },

                // Passive rates
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.HealthRegenRate,
                    DisplayName = "Health Regen",
                    ShowInUI = false,
                    DisplayColor = new Color(0.8f, 0.2f, 0.2f),
                    TextColor = new Color(1f, 0.6f, 0.6f),
                    DisplayFormat = "0.0/s",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.HealthRegenRate,
                    ShowMaxValue = false
                },
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.StaminaRegenRate,
                    DisplayName = "Stamina Regen",
                    ShowInUI = false,
                    DisplayColor = new Color(0.2f, 0.8f, 0.2f),
                    TextColor = new Color(0.6f, 1f, 0.6f),
                    DisplayFormat = "0.0/s",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.StaminaRegenRate,
                    ShowMaxValue = false
                },
                new PlayerStatUIDefinition
                {
                    Type = PlayerStatType.StaminaDrainRate,
                    DisplayName = "Stamina Drain",
                    ShowInUI = false,
                    DisplayColor = new Color(1f, 0.8f, 0.2f),
                    TextColor = new Color(1f, 0.9f, 0.55f),
                    DisplayFormat = "0.0/s",
                    DisplayType = StatDisplayType.Text,
                    RelatedMaxStatType = PlayerStatType.StaminaDrainRate,
                    ShowMaxValue = false
                }
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PlayerStatUIConfig] Default UI stats setup complete.");
        }
#endif

        #endregion
    }

    #region UI Definition Struct

    [System.Serializable]
    public struct PlayerStatUIDefinition
    {
        [Tooltip("Which stat this UI row represents")]
        public PlayerStatType Type;

        [Tooltip("Label shown in UI")]
        public string DisplayName;

        [Tooltip("Whether this stat row is visible in the player stat panel")]
        public bool ShowInUI;

        [Tooltip("Optional icon sprite")]
        public Sprite Icon;

        [Tooltip("Accent color for icon and slider fill")]
        public Color DisplayColor;

        [Tooltip("Text color for label and value. Falls back to DisplayColor if alpha < 0.01")]
        public Color TextColor;

        [Tooltip("Numeric format string (e.g. '0', '0.0', '0%')")]
        public string DisplayFormat;

        [Tooltip("How to render this stat in UI")]
        public StatDisplayType DisplayType;

        [Tooltip("Stat used as the max reference for slider display.\n" +
                 "Set equal to Type when no dynamic max is needed (e.g. plain text stats).")]
        public PlayerStatType RelatedMaxStatType;

        [Tooltip("When true, value text shows 'current/max' format (e.g. '75/100')")]
        public bool ShowMaxValue;
    }

    #endregion
}
