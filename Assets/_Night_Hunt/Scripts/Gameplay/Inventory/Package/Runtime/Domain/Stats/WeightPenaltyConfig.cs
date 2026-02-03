using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Configuration for weight penalty system.
    /// Custom curve for speed penalty based on weight percentage.
    /// </summary>
    [CreateAssetMenu(fileName = "WeightPenaltyConfig", menuName = "Inventory/WeightPenaltyConfig")]
    public class WeightPenaltyConfig : ScriptableObject
    {
        [Header("Capacity Limits")]
        [Tooltip("100% capacity = normal movement")]
        public float normalCapacityPercent = 100f;
        
        [Tooltip("150% capacity = cannot move (speed = 0)")]
        public float maxCapacityPercent = 150f;
        
        [Header("Speed Penalty Curve")]
        [Tooltip("X = weight percentage (0-150), Y = speed multiplier (0-1)")]
        public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 150f, 0f);
        
        [Header("Stamina Penalty (Overweight)")]
        [Tooltip("Stamina drain multiplier when > 100% weight")]
        public float staminaDrainMultiplier = 2f;
        
        [Tooltip("Stamina regen multiplier when > 100% weight")]
        public float staminaRegenMultiplier = 0.5f;
        
        [Header("Movement Restrictions")]
        [Tooltip("Can sprint when overweight?")]
        public bool canSprintWhenOverweight = false;
    }
}
