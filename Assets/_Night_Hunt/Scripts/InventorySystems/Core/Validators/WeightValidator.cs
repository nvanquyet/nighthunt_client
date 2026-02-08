using UnityEngine;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Validators
{
    /// <summary>
    /// Validates weight-based inventory operations.
    /// Handles: Weight limits, overweight penalties, capacity calculations.
    /// </summary>
    public static class WeightValidator
    {
        // === Weight Limit Validation ===
        
        /// <summary>
        /// Check if player can carry additional weight.
        /// </summary>
        public static bool CanCarryWeight(float currentWeight, float itemWeight, float maxCapacity)
        {
            return (currentWeight + itemWeight) <= maxCapacity;
        }
        
        /// <summary>
        /// Check if adding item would exceed weight limit.
        /// </summary>
        public static bool WouldExceedCapacity(float currentWeight, ItemInstance item, float maxCapacity)
        {
            if (item == null)
                return false;
            
            float itemWeight = item.GetTotalWeight();
            return (currentWeight + itemWeight) > maxCapacity;
        }
        
        /// <summary>
        /// Get remaining weight capacity.
        /// </summary>
        public static float GetRemainingCapacity(float currentWeight, float maxCapacity)
        {
            return Mathf.Max(0f, maxCapacity - currentWeight);
        }
        
        /// <summary>
        /// Get weight capacity percentage (0-1).
        /// </summary>
        public static float GetCapacityPercentage(float currentWeight, float maxCapacity)
        {
            if (maxCapacity <= 0f)
                return 0f;
            
            return Mathf.Clamp01(currentWeight / maxCapacity);
        }
        
        // === Overweight Penalties ===
        
        /// <summary>
        /// Calculate overweight penalty multiplier.
        /// Returns 1.0 if not overweight, decreases as excess weight increases.
        /// Example: 0.5 = 50% movement speed penalty
        /// </summary>
        public static float CalculateOverweightPenalty(float currentWeight, float maxCapacity)
        {
            if (currentWeight <= maxCapacity)
                return 1f; // No penalty
            
            float excess = currentWeight - maxCapacity;
            float excessRatio = excess / maxCapacity;
            
            // Penalty increases with excess weight
            // Max penalty: 50% speed reduction
            float penaltyPercent = Mathf.Min(excessRatio, 0.5f);
            
            return 1f - penaltyPercent;
        }
        
        /// <summary>
        /// Calculate overweight penalty with custom max penalty.
        /// </summary>
        public static float CalculateOverweightPenalty(float currentWeight, float maxCapacity, float maxPenalty)
        {
            if (currentWeight <= maxCapacity)
                return 1f;
            
            float excess = currentWeight - maxCapacity;
            float excessRatio = excess / maxCapacity;
            
            float penaltyPercent = Mathf.Min(excessRatio, maxPenalty);
            
            return 1f - penaltyPercent;
        }
        
        /// <summary>
        /// Check if player is overweight.
        /// </summary>
        public static bool IsOverweight(float currentWeight, float maxCapacity)
        {
            return currentWeight > maxCapacity;
        }
        
        /// <summary>
        /// Get overweight severity level (0-3).
        /// 0 = Not overweight
        /// 1 = Slightly overweight (0-25% over)
        /// 2 = Moderately overweight (25-50% over)
        /// 3 = Heavily overweight (50%+ over)
        /// </summary>
        public static int GetOverweightSeverity(float currentWeight, float maxCapacity)
        {
            if (currentWeight <= maxCapacity)
                return 0;
            
            float excess = currentWeight - maxCapacity;
            float excessRatio = excess / maxCapacity;
            
            if (excessRatio < 0.25f)
                return 1;
            else if (excessRatio < 0.5f)
                return 2;
            else
                return 3;
        }
        
        // === Weight Calculation Helpers ===
        
        /// <summary>
        /// Calculate total weight of item including attachments.
        /// </summary>
        public static float CalculateItemWeight(ItemInstance item)
        {
            if (item == null || item.Definition == null)
                return 0f;
            
            return item.GetTotalWeight();
        }
        
        /// <summary>
        /// Calculate combined weight of multiple items.
        /// </summary>
        public static float CalculateTotalWeight(ItemInstance[] items)
        {
            if (items == null || items.Length == 0)
                return 0f;
            
            float total = 0f;
            foreach (var item in items)
            {
                if (item != null)
                {
                    total += item.GetTotalWeight();
                }
            }
            
            return total;
        }
        
        /// <summary>
        /// Calculate weight that would be freed by removing item.
        /// Useful for UI feedback when considering dropping items.
        /// </summary>
        public static float CalculateWeightReduction(ItemInstance item)
        {
            if (item == null)
                return 0f;
            
            return item.GetTotalWeight();
        }
        
        // === Capacity Modification ===
        
        /// <summary>
        /// Calculate modified capacity with equipment bonuses.
        /// Example: Backpack adds +20kg capacity
        /// </summary>
        public static float CalculateModifiedCapacity(float baseCapacity, float flatBonus, float percentageBonus)
        {
            float modified = baseCapacity + flatBonus;
            modified *= (1f + percentageBonus);
            
            return Mathf.Max(0f, modified);
        }
        
        // === Validation Messages ===
        
        /// <summary>
        /// Get user-friendly weight status message.
        /// </summary>
        public static string GetWeightStatusMessage(float currentWeight, float maxCapacity)
        {
            if (!IsOverweight(currentWeight, maxCapacity))
            {
                float remaining = GetRemainingCapacity(currentWeight, maxCapacity);
                return $"Weight: {currentWeight:F1}/{maxCapacity:F1}kg ({remaining:F1}kg remaining)";
            }
            else
            {
                float excess = currentWeight - maxCapacity;
                int severity = GetOverweightSeverity(currentWeight, maxCapacity);
                
                string severityText = severity switch
                {
                    1 => "Slightly Overweight",
                    2 => "Moderately Overweight",
                    3 => "Heavily Overweight",
                    _ => "Overweight"
                };
                
                return $"{severityText}: {currentWeight:F1}/{maxCapacity:F1}kg (+{excess:F1}kg over limit)";
            }
        }
        
        /// <summary>
        /// Get penalty description for UI.
        /// </summary>
        public static string GetPenaltyDescription(float penalty)
        {
            if (penalty >= 1f)
                return "No penalty";
            
            float penaltyPercent = (1f - penalty) * 100f;
            return $"-{penaltyPercent:F0}% movement speed";
        }
        
        // === Advanced Validation ===
        
        /// <summary>
        /// Validate weight operation with detailed result.
        /// </summary>
        public static WeightValidationResult ValidateWeightOperation(
            float currentWeight,
            ItemInstance itemToAdd,
            float maxCapacity,
            bool allowOverweight)
        {
            var result = new WeightValidationResult();
            
            if (itemToAdd == null)
            {
                result.IsValid = false;
                result.Reason = "Item is null";
                return result;
            }
            
            float itemWeight = itemToAdd.GetTotalWeight();
            float newWeight = currentWeight + itemWeight;
            
            result.CurrentWeight = currentWeight;
            result.ItemWeight = itemWeight;
            result.NewWeight = newWeight;
            result.MaxCapacity = maxCapacity;
            result.WouldBeOverweight = newWeight > maxCapacity;
            
            if (result.WouldBeOverweight)
            {
                result.ExcessWeight = newWeight - maxCapacity;
                result.Penalty = CalculateOverweightPenalty(newWeight, maxCapacity);
                
                if (!allowOverweight)
                {
                    result.IsValid = false;
                    result.Reason = $"Would exceed weight limit by {result.ExcessWeight:F1}kg";
                    return result;
                }
                else
                {
                    result.IsValid = true;
                    result.Reason = $"Will be overweight ({GetPenaltyDescription(result.Penalty)})";
                    return result;
                }
            }
            
            result.IsValid = true;
            result.Reason = "Within weight limit";
            return result;
        }
    }
    
    /// <summary>
    /// Result structure for weight validation.
    /// </summary>
    public struct WeightValidationResult
    {
        public bool IsValid;
        public string Reason;
        public float CurrentWeight;
        public float ItemWeight;
        public float NewWeight;
        public float MaxCapacity;
        public bool WouldBeOverweight;
        public float ExcessWeight;
        public float Penalty;
    }
}