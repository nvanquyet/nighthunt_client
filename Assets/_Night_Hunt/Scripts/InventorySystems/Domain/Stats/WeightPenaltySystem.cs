using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Domain.Stats;

namespace NightHunt.Inventory.Domain.Stats
{
    /// <summary>
    /// Applies movement penalties based on weight.
    /// Uses configurable animation curves for custom penalty profiles.
    /// </summary>
    public class WeightPenaltySystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WeightPenaltyConfig config;
        
        [Header("Stats Reference")]
        [SerializeField] private StatModifierStack characterStats;
        
        [Header("Current State")]
        [SerializeField] private float currentWeight;
        [SerializeField] private float maxCapacity;
        [SerializeField] private float currentSpeedMultiplier = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private const string SPEED_SOURCE_ID = "Weight:Penalty";
        private const string STAMINA_SOURCE_ID = "Weight:StaminaPenalty";
        
        #region Lifecycle
        
        void OnEnable()
        {
            // Subscribe to inventory changes
            InventoryEvents.OnInventoryChanged += OnInventoryChanged;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnInventoryChanged -= OnInventoryChanged;
        }
        
        #endregion
        
        #region Weight Updates
        
        private void OnInventoryChanged(Core.Data.InventoryData data)
        {
            // Recalculate weight and apply penalty
            UpdateWeightAndPenalty();
        }
        
        /// <summary>
        /// Updates current weight and applies penalties.
        /// </summary>
        public void UpdateWeightAndPenalty()
        {
            if (config == null || characterStats == null) return;
            
            // Get current weight (should be calculated by calling system)
            // For now, we assume this is updated externally
            
            // Get weight capacity
            maxCapacity = characterStats.CalculateFinalStat(CharacterStatType.WeightCapacity, 50f);
            
            // Apply penalty
            ApplyPenalty(currentWeight);
        }
        
        /// <summary>
        /// Sets current weight and updates penalties.
        /// </summary>
        public void SetCurrentWeight(float weight)
        {
            currentWeight = weight;
            UpdateWeightAndPenalty();
        }
        
        #endregion
        
        #region Penalty Application
        
        private void ApplyPenalty(float weight)
        {
            float speedMult = GetSpeedMultiplier(weight, maxCapacity);
            currentSpeedMultiplier = speedMult;
            
            // Remove old speed modifier
            characterStats.RemoveModifier(CharacterStatType.MoveSpeed, SPEED_SOURCE_ID);
            
            if (speedMult < 1f)
            {
                // Apply penalty (as percentage reduction)
                float penaltyValue = speedMult - 1f; // e.g., 0.8 - 1 = -0.2 (20% reduction)
                characterStats.AddModifier(CharacterStatType.MoveSpeed, penaltyValue, SPEED_SOURCE_ID);
                
                if (enableDebugLogs)
                    Debug.Log($"[WeightPenaltySystem] Applied speed penalty: {penaltyValue * 100:F0}%");
            }
            
            // Apply stamina penalty if overweight
            if (IsOverweight(weight, maxCapacity))
            {
                characterStats.RemoveModifier(CharacterStatType.Stamina, STAMINA_SOURCE_ID);
                
                // Apply drain multiplier
                float staminaPenalty = -config.staminaDrainMultiplier + 1f;
                characterStats.AddModifier(CharacterStatType.Stamina, staminaPenalty, STAMINA_SOURCE_ID);
                
                if (enableDebugLogs)
                    Debug.Log($"[WeightPenaltySystem] Applied stamina penalty (overweight)");
            }
            else
            {
                // Remove stamina penalty if not overweight
                characterStats.RemoveModifier(CharacterStatType.Stamina, STAMINA_SOURCE_ID);
            }
            
            // Fire stats changed event
            CharacterStatsEvents.InvokeStatsChanged();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Gets speed multiplier based on weight percentage.
        /// </summary>
        public float GetSpeedMultiplier(float weight, float capacity)
        {
            if (config == null) return 1f;
            
            float weightPercent = (weight / capacity) * 100f;
            
            // Clamp to max limit
            weightPercent = Mathf.Min(weightPercent, config.maxCapacityPercent);
            
            // Evaluate curve
            float speedMult = config.speedCurve.Evaluate(weightPercent);
            
            return Mathf.Clamp01(speedMult);
        }
        
        /// <summary>
        /// Checks if player is overweight.
        /// </summary>
        public bool IsOverweight(float weight, float capacity)
        {
            if (config == null) return false;
            
            float percent = (weight / capacity) * 100f;
            return percent > config.normalCapacityPercent;
        }
        
        /// <summary>
        /// Checks if player can sprint.
        /// </summary>
        public bool CanSprint(float weight, float capacity)
        {
            if (!IsOverweight(weight, capacity))
                return true;
            
            return config.canSprintWhenOverweight;
        }
        
        /// <summary>
        /// Gets current speed multiplier.
        /// </summary>
        public float GetCurrentSpeedMultiplier() => currentSpeedMultiplier;
        
        /// <summary>
        /// Gets current weight percentage.
        /// </summary>
        public float GetWeightPercentage()
        {
            if (maxCapacity == 0) return 0f;
            return (currentWeight / maxCapacity) * 100f;
        }
        
        #endregion
    }
}