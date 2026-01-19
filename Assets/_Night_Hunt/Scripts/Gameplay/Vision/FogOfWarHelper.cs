using UnityEngine;
using System.Reflection;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Helper class for FogOfWar plugin integration
    /// Handles reflection-based access to RayDistance property/field
    /// </summary>
    public static class FogOfWarHelper
    {
        /// <summary>
        /// Set RayDistance on FogOfWarRevealer3D using reflection
        /// </summary>
        public static void SetRayDistance(Component revealer, float distance)
        {
            if (revealer == null) return;

            // Try property first
            var property = revealer.GetType().GetProperty("RayDistance", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(revealer, distance);
                return;
            }

            // Try public field
            var field = revealer.GetType().GetField("RayDistance", BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(revealer, distance);
                return;
            }

            // Try private fields with common naming conventions
            field = revealer.GetType().GetField("m_RayDistance", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = revealer.GetType().GetField("_rayDistance", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = revealer.GetType().GetField("rayDistance", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(revealer, distance);
            }
            else
            {
                Debug.LogWarning($"[FogOfWarHelper] Could not set RayDistance on {revealer.GetType().Name}");
            }
        }

        /// <summary>
        /// Get RayDistance from FogOfWarRevealer3D using reflection
        /// </summary>
        public static float GetRayDistance(Component revealer, float defaultValue = 12f)
        {
            if (revealer == null) return defaultValue;

            // Try property first
            var property = revealer.GetType().GetProperty("RayDistance", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(revealer);
                if (value is float floatValue)
                    return floatValue;
            }

            // Try public field
            var field = revealer.GetType().GetField("RayDistance", BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(revealer);
                if (value is float floatValue)
                    return floatValue;
            }

            // Try private fields
            field = revealer.GetType().GetField("m_RayDistance", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = revealer.GetType().GetField("_rayDistance", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = revealer.GetType().GetField("rayDistance", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                var value = field.GetValue(revealer);
                if (value is float floatValue)
                    return floatValue;
            }

            return defaultValue;
        }
    }
}

