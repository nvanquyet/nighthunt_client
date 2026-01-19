using System.Collections.Generic;
using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Weight calculation from items
    /// </summary>
    public static class WeightCalculator
    {
        /// <summary>
        /// Calculate total weight from items
        /// </summary>
        public static float CalculateTotalWeight(List<ItemConfigData> items)
        {
            float totalWeight = 0f;

            foreach (var item in items)
            {
                if (item != null)
                {
                    totalWeight += item.Weight;
                }
            }

            return totalWeight;
        }

        /// <summary>
        /// Calculate weight percentage
        /// </summary>
        public static float CalculateWeightPercentage(float currentWeight, float weightCapacity)
        {
            if (weightCapacity <= 0f) return 0f;
            return Mathf.Clamp01(currentWeight / weightCapacity);
        }

        /// <summary>
        /// Check if can add item (weight check)
        /// </summary>
        public static bool CanAddItem(float currentWeight, float itemWeight, float weightCapacity)
        {
            return currentWeight + itemWeight <= weightCapacity;
        }

        /// <summary>
        /// Get weight penalty multiplier
        /// </summary>
        public static float GetWeightPenaltyMultiplier(float currentWeight, float weightCapacity)
        {
            if (weightCapacity <= 0f) return 1f;

            float weightPercentage = currentWeight / weightCapacity;

            if (weightPercentage < 0.8f)
            {
                return 1f; // No penalty
            }
            else if (weightPercentage <= 1.0f)
            {
                return 0.9f; // -10% speed
            }
            else if (weightPercentage <= 1.4f)
            {
                return 0.8f; // -20% speed
            }
            else
            {
                return 0f; // Cannot move
            }
        }
    }
}

