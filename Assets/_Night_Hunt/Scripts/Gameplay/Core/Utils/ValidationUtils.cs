using UnityEngine;

namespace NightHunt.Gameplay.Core.Utils
{
    /// <summary>
    /// Input validation and bounds checking utilities
    /// </summary>
    public static class ValidationUtils
    {
        /// <summary>
        /// Validate vector is not zero
        /// </summary>
        public static bool IsValidVector(Vector3 vector, float minMagnitude = 0.001f)
        {
            return vector.sqrMagnitude >= minMagnitude * minMagnitude;
        }

        /// <summary>
        /// Validate position is within bounds
        /// </summary>
        public static bool IsWithinBounds(Vector3 position, Bounds bounds)
        {
            return bounds.Contains(position);
        }

        /// <summary>
        /// Validate position is within sphere
        /// </summary>
        public static bool IsWithinSphere(Vector3 position, Vector3 center, float radius)
        {
            return MathUtils.DistanceSquared(position, center) <= radius * radius;
        }

        /// <summary>
        /// Validate value is within range
        /// </summary>
        public static bool IsInRange(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// Clamp value to range
        /// </summary>
        public static float ClampToRange(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Validate string is not null or empty
        /// </summary>
        public static bool IsValidString(string str)
        {
            return !string.IsNullOrEmpty(str);
        }

        /// <summary>
        /// Validate GameObject is not null and active
        /// </summary>
        public static bool IsValidGameObject(GameObject obj)
        {
            return obj != null && obj.activeInHierarchy;
        }

        /// <summary>
        /// Validate component exists
        /// </summary>
        public static bool HasComponent<T>(GameObject obj) where T : Component
        {
            return obj != null && obj.GetComponent<T>() != null;
        }
    }
}

