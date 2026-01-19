using UnityEngine;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Line of sight checking
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>
        /// Check if position has line of sight from origin
        /// </summary>
        public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask blockingLayers)
        {
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);

            RaycastHit hit;
            if (Physics.Raycast(from, direction, out hit, distance, blockingLayers))
            {
                // Something is blocking line of sight
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if position is within vision radius and has line of sight
        /// </summary>
        public static bool IsVisible(Vector3 from, Vector3 to, float visionRadius, LayerMask blockingLayers)
        {
            float distance = Vector3.Distance(from, to);
            if (distance > visionRadius)
            {
                return false;
            }

            return HasLineOfSight(from, to, blockingLayers);
        }
    }
}

