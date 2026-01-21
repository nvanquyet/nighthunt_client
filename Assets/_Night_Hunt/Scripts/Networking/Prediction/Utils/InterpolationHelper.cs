using UnityEngine;

namespace NightHunt.Networking.Prediction.Utils
{
    /// <summary>
    /// Helper methods cho interpolation và extrapolation trong prediction system.
    /// </summary>
    public static class InterpolationHelper
    {
        /// <summary>
        /// Interpolate giữa 2 Vector3 positions.
        /// </summary>
        /// <param name="from">Position bắt đầu</param>
        /// <param name="to">Position kết thúc</param>
        /// <param name="t">Interpolation factor (0-1)</param>
        /// <returns>Interpolated position</returns>
        public static Vector3 Lerp(Vector3 from, Vector3 to, float t)
        {
            return Vector3.Lerp(from, to, t);
        }

        /// <summary>
        /// Interpolate giữa 2 Quaternions.
        /// </summary>
        /// <param name="from">Rotation bắt đầu</param>
        /// <param name="to">Rotation kết thúc</param>
        /// <param name="t">Interpolation factor (0-1)</param>
        /// <returns>Interpolated rotation</returns>
        public static Quaternion Lerp(Quaternion from, Quaternion to, float t)
        {
            return Quaternion.Lerp(from, to, t);
        }

        /// <summary>
        /// Interpolate giữa 2 states với timestamp.
        /// </summary>
        /// <param name="from">State bắt đầu</param>
        /// <param name="to">State kết thúc</param>
        /// <param name="fromTime">Timestamp của state bắt đầu</param>
        /// <param name="toTime">Timestamp của state kết thúc</param>
        /// <param name="currentTime">Timestamp hiện tại</param>
        /// <returns>Interpolated state</returns>
        public static float CalculateInterpolationFactor(float fromTime, float toTime, float currentTime)
        {
            if (toTime <= fromTime)
                return 1f;

            float t = (currentTime - fromTime) / (toTime - fromTime);
            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// Extrapolate từ state với velocity.
        /// </summary>
        /// <param name="position">Position hiện tại</param>
        /// <param name="velocity">Velocity</param>
        /// <param name="deltaTime">Thời gian để extrapolate</param>
        /// <returns>Extrapolated position</returns>
        public static Vector3 Extrapolate(Vector3 position, Vector3 velocity, float deltaTime)
        {
            return position + velocity * deltaTime;
        }

        /// <summary>
        /// Extrapolate từ state với velocity và max distance.
        /// </summary>
        /// <param name="position">Position hiện tại</param>
        /// <param name="velocity">Velocity</param>
        /// <param name="deltaTime">Thời gian để extrapolate</param>
        /// <param name="maxDistance">Khoảng cách tối đa để extrapolate</param>
        /// <returns>Extrapolated position</returns>
        public static Vector3 Extrapolate(Vector3 position, Vector3 velocity, float deltaTime, float maxDistance)
        {
            Vector3 extrapolated = position + velocity * deltaTime;
            float distance = Vector3.Distance(position, extrapolated);
            
            if (distance > maxDistance)
            {
                // Clamp về max distance
                Vector3 direction = (extrapolated - position).normalized;
                return position + direction * maxDistance;
            }

            return extrapolated;
        }

        /// <summary>
        /// Smooth interpolation với easing curve.
        /// </summary>
        /// <param name="from">Value bắt đầu</param>
        /// <param name="to">Value kết thúc</param>
        /// <param name="t">Interpolation factor (0-1)</param>
        /// <param name="easing">Easing function</param>
        /// <returns>Interpolated value với easing</returns>
        public static float SmoothLerp(float from, float to, float t, EasingType easing = EasingType.Linear)
        {
            float easedT = ApplyEasing(t, easing);
            return Mathf.Lerp(from, to, easedT);
        }

        /// <summary>
        /// Apply easing function cho interpolation factor.
        /// </summary>
        /// <param name="t">Interpolation factor (0-1)</param>
        /// <param name="easing">Easing type</param>
        /// <returns>Eased interpolation factor</returns>
        public static float ApplyEasing(float t, EasingType easing)
        {
            t = Mathf.Clamp01(t);

            switch (easing)
            {
                case EasingType.Linear:
                    return t;
                case EasingType.EaseIn:
                    return t * t;
                case EasingType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case EasingType.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }
    }

    /// <summary>
    /// Easing types cho interpolation.
    /// </summary>
    public enum EasingType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }
}

