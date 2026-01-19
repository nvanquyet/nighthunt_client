using UnityEngine;

namespace NightHunt.Gameplay.Core.Utils
{
    /// <summary>
    /// Math utilities for gameplay calculations
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// Calculate distance between two points
        /// </summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Calculate distance squared (faster, no sqrt)
        /// </summary>
        public static float DistanceSquared(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude;
        }

        /// <summary>
        /// Check if point is within radius
        /// </summary>
        public static bool IsWithinRadius(Vector3 point, Vector3 center, float radius)
        {
            return DistanceSquared(point, center) <= radius * radius;
        }

        /// <summary>
        /// Calculate angle between two vectors in degrees
        /// </summary>
        public static float Angle(Vector3 from, Vector3 to)
        {
            return Vector3.Angle(from, to);
        }

        /// <summary>
        /// Calculate signed angle between two vectors
        /// </summary>
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            return Vector3.SignedAngle(from, to, axis);
        }

        /// <summary>
        /// Clamp angle to 0-360 range
        /// </summary>
        public static float ClampAngle(float angle)
        {
            while (angle < 0f) angle += 360f;
            while (angle >= 360f) angle -= 360f;
            return angle;
        }

        /// <summary>
        /// Calculate direction from point A to point B
        /// </summary>
        public static Vector3 Direction(Vector3 from, Vector3 to)
        {
            return (to - from).normalized;
        }

        /// <summary>
        /// Project point onto plane
        /// </summary>
        public static Vector3 ProjectOnPlane(Vector3 point, Vector3 planeNormal)
        {
            return Vector3.ProjectOnPlane(point, planeNormal);
        }

        /// <summary>
        /// Check if two floats are approximately equal
        /// </summary>
        public static bool Approximately(float a, float b, float threshold = 0.001f)
        {
            return Mathf.Abs(a - b) < threshold;
        }

        /// <summary>
        /// Linear interpolation with clamping
        /// </summary>
        public static float Lerp(float a, float b, float t)
        {
            return Mathf.Lerp(a, b, Mathf.Clamp01(t));
        }

        /// <summary>
        /// Smooth step interpolation
        /// </summary>
        public static float SmoothStep(float a, float b, float t)
        {
            return Mathf.SmoothStep(a, b, Mathf.Clamp01(t));
        }
    }
}

