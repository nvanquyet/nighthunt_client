using UnityEngine;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Gameplay.Character.Stats;

namespace NightHunt.Inventory.Stats
{
    /// <summary>
    /// Manages character stats with modifier tracking by sourceId.
    /// Formula: FinalStat = (BaseStat × Σ Multipliers) + Σ Additions
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        [Header("Base Stats")] [SerializeField]
        private float baseHP = 100f;

        [SerializeField] private float baseStamina = 100f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float baseWeightCapacity = 50f;
        [SerializeField] private float baseVisionRadius = 12f;
        [SerializeField] private float baseNoiseLevel = 1f;

        [Header("Current Runtime Stats")] [SerializeField]
        private float currentHP;

        [SerializeField] private float currentStamina;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        // Modifier storage: sourceId → List of modifiers from that source
        private Dictionary<string, List<StatModifier>> modifiersBySource = new Dictionary<string, List<StatModifier>>();

        // Cached final stats (recalculated when modifiers change)
        private Dictionary<CharacterStatType, float> cachedFinalStats = new Dictionary<CharacterStatType, float>();

        #region Lifecycle

        void Awake()
        {
            InitializeStats();
        }

        void OnEnable()
        {
            SubscribeToEvents();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        void Start()
        {
            currentHP = baseHP;
            currentStamina = baseStamina;
            RecalculateAllStats();
        }

        #endregion

        #region Event Subscription

        void SubscribeToEvents()
        {
            CharacterStatsEvents.OnAddModifier += HandleAddModifier;
            CharacterStatsEvents.OnRemoveModifier += HandleRemoveModifier;
            CharacterStatsEvents.OnRemoveAllModifiers += HandleRemoveAllModifiers;
        }

        void UnsubscribeFromEvents()
        {
            CharacterStatsEvents.OnAddModifier -= HandleAddModifier;
            CharacterStatsEvents.OnRemoveModifier -= HandleRemoveModifier;
            CharacterStatsEvents.OnRemoveAllModifiers -= HandleRemoveAllModifiers;
        }

        #endregion

        #region Initialization

        void InitializeStats()
        {
            // Initialize cached stats with base values
            cachedFinalStats[CharacterStatType.MaxHP] = baseHP;
            cachedFinalStats[CharacterStatType.MaxStamina] = baseStamina;
            cachedFinalStats[CharacterStatType.MoveSpeed] = baseMoveSpeed;
            cachedFinalStats[CharacterStatType.WeightCapacity] = baseWeightCapacity;
            cachedFinalStats[CharacterStatType.VisionRadius] = baseVisionRadius;
            cachedFinalStats[CharacterStatType.NoiseLevel] = baseNoiseLevel;
        }

        #endregion

        #region Event Handlers

        void HandleAddModifier(CharacterStatType statType, ModifierCalculationType calculationType, float value,
            string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                LogWarning("Cannot add modifier with empty sourceId");
                return;
            }

            // Create modifier
            var modifier = new StatModifier
            {
                StatType = statType,
                CalculationType = calculationType,
                Value = value,
                SourceId = sourceId
            };

            // Add to source tracking
            if (!modifiersBySource.ContainsKey(sourceId))
            {
                modifiersBySource[sourceId] = new List<StatModifier>();
            }

            modifiersBySource[sourceId].Add(modifier);

            Log($"Added modifier: {statType} {calculationType} {value:F2} from {sourceId}");

            // Recalculate affected stat
            RecalculateStat(statType);
        }

        void HandleRemoveModifier(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                LogWarning("Cannot remove modifier with empty sourceId");
                return;
            }

            if (!modifiersBySource.ContainsKey(sourceId))
            {
                Log($"No modifiers found for sourceId: {sourceId}");
                return;
            }

            // Get all stats affected by this source
            var affectedStats = modifiersBySource[sourceId]
                .Select(m => m.StatType)
                .Distinct()
                .ToList();

            // Remove all modifiers from this source
            modifiersBySource.Remove(sourceId);

            Log($"Removed all modifiers from source: {sourceId}");

            // Recalculate affected stats
            foreach (var statType in affectedStats)
            {
                RecalculateStat(statType);
            }
        }

        void HandleRemoveAllModifiers()
        {
            modifiersBySource.Clear();
            RecalculateAllStats();
            Log("Removed all modifiers");
        }

        #endregion

        #region Stat Calculation

        /// <summary>
        /// Recalculates a specific stat based on base value + all modifiers.
        /// </summary>
        void RecalculateStat(CharacterStatType statType)
        {
            float baseValue = GetBaseStat(statType);

            // Collect all modifiers for this stat
            var allModifiers = modifiersBySource.Values
                .SelectMany(list => list)
                .Where(m => m.StatType == statType)
                .ToList();

            // Separate by type
            var percentageModifiers = allModifiers.Where(m => m.CalculationType == ModifierCalculationType.Percentage)
                .ToList();
            var flatModifiers = allModifiers.Where(m => m.CalculationType == ModifierCalculationType.Flat).ToList();

            // Calculate: (Base × (1 + Σ Percentage)) + Σ Flat
            float percentageSum = percentageModifiers.Sum(m => m.Value);
            float flatSum = flatModifiers.Sum(m => m.Value);

            float finalValue = (baseValue * (1f + percentageSum)) + flatSum;

            // Clamp to reasonable values
            finalValue = Mathf.Max(0f, finalValue);

            cachedFinalStats[statType] = finalValue;

            Log(
                $"Recalculated {statType}: Base={baseValue:F2}, Percentage={percentageSum:F2}, Flat={flatSum:F2}, Final={finalValue:F2}");

            // Fire stats changed event
            CharacterStatsEvents.InvokeStatsChanged();
        }

        /// <summary>
        /// Recalculates all stats.
        /// </summary>
        void RecalculateAllStats()
        {
            RecalculateStat(CharacterStatType.MaxHP);
            RecalculateStat(CharacterStatType.MaxStamina);
            RecalculateStat(CharacterStatType.MoveSpeed);
            RecalculateStat(CharacterStatType.WeightCapacity);
            RecalculateStat(CharacterStatType.VisionRadius);
            RecalculateStat(CharacterStatType.NoiseLevel);

            Log("Recalculated all stats");
        }

        #endregion

        #region Public API - Get Stats

        /// <summary>
        /// Gets final calculated stat value.
        /// </summary>
        public float GetFinalStat(CharacterStatType statType)
        {
            return cachedFinalStats.ContainsKey(statType) ? cachedFinalStats[statType] : 0f;
        }

        /// <summary>
        /// Gets base stat value (before modifiers).
        /// </summary>
        public float GetBaseStat(CharacterStatType statType)
        {
            switch (statType)
            {
                case CharacterStatType.MaxHP: return baseHP;
                case CharacterStatType.MaxStamina: return baseStamina;
                case CharacterStatType.MoveSpeed: return baseMoveSpeed;
                case CharacterStatType.WeightCapacity: return baseWeightCapacity;
                case CharacterStatType.VisionRadius: return baseVisionRadius;
                case CharacterStatType.NoiseLevel: return baseNoiseLevel;
                default: return 0f;
            }
        }

        // Convenience getters
        public float GetCurrentHP() => currentHP;
        public float GetMaxHP() => GetFinalStat(CharacterStatType.MaxHP);
        public float GetCurrentStamina() => currentStamina;
        public float GetMaxStamina() => GetFinalStat(CharacterStatType.MaxStamina);
        public float GetMoveSpeed() => GetFinalStat(CharacterStatType.MoveSpeed);
        public float GetWeightCapacity() => GetFinalStat(CharacterStatType.WeightCapacity);
        public float GetVisionRadius() => GetFinalStat(CharacterStatType.VisionRadius);
        public float GetNoiseLevel() => GetFinalStat(CharacterStatType.NoiseLevel);

        #endregion

        #region Public API - Modify Runtime Stats

        public void SetHP(float value)
        {
            currentHP = Mathf.Clamp(value, 0f, GetMaxHP());
        }

        public void SetStamina(float value)
        {
            currentStamina = Mathf.Clamp(value, 0f, GetMaxStamina());
        }

        public void TakeDamage(float damage)
        {
            currentHP = Mathf.Max(0f, currentHP - damage);
            CharacterStatsEvents.InvokeHPChanged(currentHP, GetMaxHP());
        }

        public void Heal(float amount)
        {
            currentHP = Mathf.Min(GetMaxHP(), currentHP + amount);
            CharacterStatsEvents.InvokeHPChanged(currentHP, GetMaxHP());
        }

        public bool IsAlive() => currentHP > 0f;

        #endregion

        #region Debug

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[CharacterStats] {message}");
        }

        void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CharacterStats] {message}");
        }

        /// <summary>
        /// Debug: Get all active modifiers.
        /// </summary>
        public Dictionary<string, List<StatModifier>> GetAllModifiers()
        {
            return new Dictionary<string, List<StatModifier>>(modifiersBySource);
        }

        #endregion
    }
}