using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    /// <summary>
    /// Movement weight-penalty helpers.
    ///
    /// NOTE: These utilities are NOT yet wired into the movement simulation.
    /// When the weight system is fully integrated, call via
    /// BaseCharacterPredictedMovement.SetWeightPenalty() and
    /// SetStaminaDrainMultiplier() — currently both are no-op stubs pending this work.
    /// </summary>
    public static class MovementUtils
    {
        /// <summary>
        /// Calculate weight penalty percentage
        /// </summary>
        public static float CalculateWeightPenalty(float currentWeight, float weightCapacity)
        {
            if (weightCapacity <= 0f) return 0f;

            float weightPercentage = currentWeight / weightCapacity;

            if (weightPercentage < 0.8f)
            {
                return 0f; // No penalty
            }
            else if (weightPercentage <= 1.0f)
            {
                // 80-100%: -10% speed
                return 0.1f;
            }
            else if (weightPercentage <= 1.4f)
            {
                // 100-140%: -20% speed, +25% stamina drain
                return 0.2f;
            }
            else
            {
                // >140%: Cannot run
                return 1f; // Maximum penalty
            }
        }

        /// <summary>
        /// Calculate stamina drain multiplier based on weight
        /// </summary>
        public static float CalculateStaminaDrainMultiplier(float currentWeight, float weightCapacity)
        {
            if (weightCapacity <= 0f) return 1f;

            float weightPercentage = currentWeight / weightCapacity;

            if (weightPercentage <= 1.0f)
            {
                return 1f; // Normal drain
            }
            else if (weightPercentage <= 1.4f)
            {
                // 100-140%: +25% stamina drain
                return 1.25f;
            }
            else
            {
                // >140%: Cannot sprint anyway
                return 1.25f;
            }
        }

        /// <summary>
        /// Check if can sprint based on weight
        /// </summary>
        public static bool CanSprint(float currentWeight, float weightCapacity)
        {
            if (weightCapacity <= 0f) return true;
            return currentWeight / weightCapacity <= 1.4f; // Can sprint if <= 140%
        }
    }
}

