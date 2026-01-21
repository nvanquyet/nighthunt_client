using System;

namespace NightHunt.Networking.Prediction.Attributes
{
    /// <summary>
    /// Attribute để đánh dấu fields/properties cần được predict.
    /// Dùng cho code generation hoặc reflection-based prediction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class PredictedAttribute : Attribute
    {
        /// <summary>
        /// Priority của field (cao hơn = predict trước).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Interpolation type cho field này.
        /// </summary>
        public InterpolationType InterpolationType { get; set; } = InterpolationType.Linear;

        /// <summary>
        /// Threshold để trigger reconciliation.
        /// </summary>
        public float ReconciliationThreshold { get; set; } = 0.1f;

        public PredictedAttribute()
        {
        }

        public PredictedAttribute(int priority)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Interpolation types cho predicted fields.
    /// </summary>
    public enum InterpolationType
    {
        None,       // Không interpolate
        Linear,     // Linear interpolation
        Smooth,     // Smooth interpolation
        Extrapolate // Extrapolation
    }
}

