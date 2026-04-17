namespace NightHunt.GameplaySystems.Core.Configs
{
    using UnityEngine;
    
    /// <summary>
    /// Main gameplay configuration
    /// Controls weight system, movement penalties, and other gameplay parameters
    /// Create via: Assets → Create → GameplaySystems/Config/Gameplay Config
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "NightHunt/Gameplay/Gameplay Config")]
    public class GameplayConfig : ScriptableObject
    {
        #region Weight System
        
        [Header("Weight System")]
        [Tooltip("Base weight capacity cho mỗi player (kg)")]
        [Min(0f)]
        public float BaseWeightCapacity = 100f;
        
        [Tooltip("Minimum movement speed khi overweight (% of normal speed)")]
        [Range(0.01f, 1f)]
        public float MinMovementSpeedPercent = 0.1f; // 10% min speed
        
        [Tooltip("Weight percent tối đa before đạt min speed (1.5 = 150%)")]
        [Min(1f)]
        public float MaxOverweightPercent = 1.5f; // 150% weight
        
        [Tooltip("Hiển thị warning khi weight vượt quá % này")]
        [Range(0.5f, 1f)]
        public float WeightWarningThreshold = 0.9f; // 90%
        
        #endregion
        
        #region Item Usage
        
        [Header("Item Usage")]
        [Tooltip("Damage có interrupt item usage không")]
        public bool DamageInterruptsUsage = true;
        
        [Tooltip("Movement có interrupt item usage không")]
        public bool MovementInterruptsUsage = false;
        
        [Tooltip("Khoảng cách tối đa có thể di chuyển during usage (meters)")]
        [Min(0f)]
        public float MaxUsageMovementDistance = 2f;
        
        [Tooltip("Có thể cancel usage bằng ESC không")]
        public bool AllowManualCancel = true;
        
        #endregion
        
        #region Weight Calculation Methods
        
        /// <summary>
        /// Tính movement speed multiplier dựa trên weight percent
        /// Formula:
        /// - 0-100%: Normal speed (1.0)
        /// - 100-150%: Linear decrease từ 1.0 → 0.1
        /// - >= 150%: Minimum speed (0.1)
        /// </summary>
        public float CalculateMovementSpeedMultiplier(float weightPercent)
        {
            // Normal weight (0-100%)
            if (weightPercent <= 1f)
                return 1f;
            
            // At or above max overweight (>= 150%)
            if (weightPercent >= MaxOverweightPercent)
                return MinMovementSpeedPercent;
            
            // Overweight range (100% - 150%)
            // Linear interpolation from 1.0 → MinMovementSpeedPercent
            float overweightRange = MaxOverweightPercent - 1f; // 0.5 (50%)
            float currentOverweight = weightPercent - 1f;
            float t = currentOverweight / overweightRange; // 0.0 → 1.0
            
            return Mathf.Lerp(1f, MinMovementSpeedPercent, t);
        }
        
        /// <summary>
        /// Check if weight is in warning range
        /// </summary>
        public bool IsWeightInWarningRange(float weightPercent)
        {
            return weightPercent >= WeightWarningThreshold && weightPercent < 1f;
        }
        
        /// <summary>
        /// Check if overweight
        /// </summary>
        public bool IsOverweight(float weightPercent)
        {
            return weightPercent > 1f;
        }
        
        /// <summary>
        /// Check if at max overweight (cannot move properly)
        /// </summary>
        public bool IsMaxOverweight(float weightPercent)
        {
            return weightPercent >= MaxOverweightPercent;
        }
        
        /// <summary>
        /// Get weight status color for UI
        /// </summary>
        public Color GetWeightStatusColor(float weightPercent)
        {
            if (weightPercent >= MaxOverweightPercent)
                return new Color(0.8f, 0f, 0f); // Dark red - max overweight
            
            if (weightPercent > 1f)
                return new Color(1f, 0.5f, 0f); // Orange - overweight
            
            if (weightPercent >= WeightWarningThreshold)
                return new Color(1f, 1f, 0f); // Yellow - warning
            
            return new Color(0.2f, 1f, 0.2f); // Green - normal
        }
        
        /// <summary>
        /// Get weight status text
        /// </summary>
        public string GetWeightStatusText(float weightPercent)
        {
            if (weightPercent >= MaxOverweightPercent)
                return "CRITICALLY OVERWEIGHT";
            
            if (weightPercent > 1f)
                return "OVERWEIGHT";
            
            if (weightPercent >= WeightWarningThreshold)
                return "WARNING";
            
            return "NORMAL";
        }
        
        #endregion
        
        #region Anti-Camping

        [Header("Anti-Camping")]
        [Tooltip("Seconds a player must stay within positionThreshold before being flagged as camping.")]
        [Min(1f)]
        public float CampingTimeThreshold = 90f;

        [Tooltip("Meters — player movement within this radius counts as 'stationary'.")]
        [Min(0.1f)]
        public float CampingPositionThreshold = 5f;

        [Tooltip("Radius (meters) around camping player that gets revealed to enemies.")]
        [Min(1f)]
        public float CampingRevealRadius = 30f;

        [Tooltip("Seconds between camping detection ticks (lower = more responsive, higher = cheaper).")]
        [Min(0.5f)]
        public float CampingUpdateInterval = 5f;

        #endregion

        #region Validation
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure sane values
            BaseWeightCapacity = Mathf.Max(0f, BaseWeightCapacity);
            MaxOverweightPercent = Mathf.Max(1f, MaxOverweightPercent);
            MinMovementSpeedPercent = Mathf.Clamp(MinMovementSpeedPercent, 0.01f, 1f);
            WeightWarningThreshold = Mathf.Clamp(WeightWarningThreshold, 0.5f, 1f);
            MaxUsageMovementDistance = Mathf.Max(0f, MaxUsageMovementDistance);
            CampingTimeThreshold = Mathf.Max(1f, CampingTimeThreshold);
            CampingPositionThreshold = Mathf.Max(0.1f, CampingPositionThreshold);
            CampingRevealRadius = Mathf.Max(1f, CampingRevealRadius);
            CampingUpdateInterval = Mathf.Max(0.5f, CampingUpdateInterval);
        }
        
        [ContextMenu("Test Weight Calculations")]
        private void TestWeightCalculations()
        {
            Debug.Log("=== Weight System Test ===");
            
            float[] testWeights = { 0f, 50f, 90f, 100f, 110f, 125f, 140f, 150f, 160f };
            
            foreach (float weight in testWeights)
            {
                float percent = weight / 100f;
                float speedMult = CalculateMovementSpeedMultiplier(percent);
                Color color = GetWeightStatusColor(percent);
                string status = GetWeightStatusText(percent);
                
                Debug.Log($"Weight: {weight:F0}kg ({percent:P0}) → Speed: {speedMult:P0} | Status: {status}");
            }
        }
#endif
        
        #endregion
    }
}