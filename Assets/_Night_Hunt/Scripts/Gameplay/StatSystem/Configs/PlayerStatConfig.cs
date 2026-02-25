using UnityEngine;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// Gameplay configuration for player stats.
    ///
    /// RESPONSIBILITIES:
    /// - Default value, min/max per stat
    /// - Whether this stat is a "current" value clamped by another stat (e.g. Health clamped by MaxHealth)
    /// - RelatedMaxStatType: which stat acts as the dynamic ceiling for clamping
    ///
    /// NOT included here (see PlayerStatUIConfig):
    /// - Display names, colors, icons, format strings, slider types
    ///
    /// USAGE:
    /// Create via: Assets → Create → StatSystem/Config/Player Stat Config
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatConfig", menuName = "StatSystem/Config/Player Stat Config")]
    public class PlayerStatConfig : ScriptableObject
    {
        [Header("Player Stats")]
        [Tooltip("Gameplay definitions for all player stats")]
        public PlayerStatDefinition[] Stats;

        #region Helpers

        /// <summary>
        /// Get definition by type. Returns default if not found.
        /// </summary>
        public PlayerStatDefinition GetStatDefinition(PlayerStatType type)
        {
            if (Stats == null) return default;

            foreach (var stat in Stats)
            {
                if (stat.Type == type)
                    return stat;
            }

            Debug.LogWarning($"[PlayerStatConfig] Stat definition not found: {type}");
            return default;
        }

        /// <summary>
        /// Returns true if the stat is defined in this config.
        /// </summary>
        public bool HasStat(PlayerStatType type)
        {
            if (Stats == null) return false;

            foreach (var stat in Stats)
            {
                if (stat.Type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the related max stat type if defined (i.e. RelatedMaxStatType != type itself).
        /// Used by PlayerStatSystem to clamp current value dynamically.
        /// Returns null if no dynamic max stat.
        /// </summary>
        public PlayerStatType? GetRelatedMaxStat(PlayerStatType type)
        {
            var def = GetStatDefinition(type);

            // Convention: if RelatedMaxStatType == Type → no dynamic max
            if (def.RelatedMaxStatType == type)
                return null;

            if (HasStat(def.RelatedMaxStatType))
                return def.RelatedMaxStatType;

            return null;
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
                        Debug.LogError($"[PlayerStatConfig] Duplicate stat definition: {Stats[i].Type}");
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
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 1000f,
                    IsCurrentValue = true,
                    RelatedMaxStatType = PlayerStatType.MaxHealth
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MaxHealth,
                    DefaultValue = 100f,
                    MinValue = 1f,
                    MaxValue = 1000f,
                    IsCurrentValue = false,
                    RelatedMaxStatType = PlayerStatType.MaxHealth
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.Stamina,
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    IsCurrentValue = true,
                    RelatedMaxStatType = PlayerStatType.MaxStamina
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MaxStamina,
                    DefaultValue = 100f,
                    MinValue = 1f,
                    MaxValue = 500f,
                    IsCurrentValue = false,
                    RelatedMaxStatType = PlayerStatType.MaxStamina
                },

                // Movement
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.MovementSpeed,
                    DefaultValue = 5f,
                    MinValue = 0f,
                    MaxValue = 20f,
                    IsCurrentValue = false,
                    RelatedMaxStatType = PlayerStatType.MovementSpeed
                },

                // Weight System
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.WeightCapacity,
                    DefaultValue = 100f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    IsCurrentValue = false,
                    RelatedMaxStatType = PlayerStatType.WeightCapacity
                },
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.CurrentWeight,
                    DefaultValue = 0f,
                    MinValue = 0f,
                    MaxValue = 9999f,
                    IsCurrentValue = true,
                    RelatedMaxStatType = PlayerStatType.WeightCapacity
                },

                // Combat - Defense
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.Armor,
                    DefaultValue = 0f,
                    MinValue = 0f,
                    MaxValue = 500f,
                    IsCurrentValue = false,
                    RelatedMaxStatType = PlayerStatType.Armor
                },

                // Vision
                new PlayerStatDefinition
                {
                    Type = PlayerStatType.VisionRange,
                    DefaultValue = 50f,
                    MinValue = 0f,
                    MaxValue = 200f,
                    IsCurrentValue = false,
                    RelatedMaxStatType = PlayerStatType.VisionRange
                }
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PlayerStatConfig] Default stats setup complete.");
        }
#endif

        #endregion
    }

    #region Stat Definition

    [System.Serializable]
    public struct PlayerStatDefinition
    {
        [Tooltip("Stat type")]
        public PlayerStatType Type;

        [Tooltip("Initial value when the player spawns")]
        public float DefaultValue;

        [Tooltip("Absolute minimum value (clamp floor)")]
        public float MinValue;

        [Tooltip("Absolute maximum value (clamp ceiling) – for IsCurrentValue stats this is overridden at runtime by RelatedMaxStatType")]
        public float MaxValue;

        [Tooltip("True = this is a 'current' value (e.g. Health, Stamina, CurrentWeight) whose runtime ceiling is determined by RelatedMaxStatType.\n" +
                 "False = this is a derived/flat stat (e.g. MaxHealth, MovementSpeed) clamped by MinValue/MaxValue only.")]
        public bool IsCurrentValue;

        [Tooltip("Which stat provides the dynamic ceiling for clamping when IsCurrentValue = true.\n" +
                 "Set equal to Type to indicate no dynamic max (stat is clamped by MaxValue instead).")]
        public PlayerStatType RelatedMaxStatType;
    }

    #endregion
}
