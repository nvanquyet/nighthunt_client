using UnityEngine;

namespace NightHunt.Gameplay.Camera.Cinemachine
{
    /// <summary>
    /// Camera utilities
    /// </summary>
    public static class CameraUtils
    {
        /// <summary>
        /// Calculate camera position for 45 degree angle
        /// </summary>
        public static Vector3 CalculateCameraPosition(Vector3 targetPosition, float distance, float height)
        {
            // 45 degree angle: equal height and distance
            float horizontalDistance = distance * Mathf.Cos(45f * Mathf.Deg2Rad);
            return targetPosition + new Vector3(0, height, -horizontalDistance);
        }

        /// <summary>
        /// Calculate camera rotation for 45 degree angle
        /// </summary>
        public static Quaternion CalculateCameraRotation(Vector3 targetPosition, Vector3 cameraPosition)
        {
            Vector3 direction = (targetPosition - cameraPosition).normalized;
            return Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Convert screen point to world point on ground plane
        /// </summary>
        public static Vector3 ScreenToWorldPoint(UnityEngine.Camera camera, Vector3 screenPoint, float groundHeight = 0f)
        {
            Ray ray = camera.ScreenPointToRay(screenPoint);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundHeight, 0));
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Get aim direction from camera to world point
        /// </summary>
        public static Vector3 GetAimDirection(UnityEngine.Camera camera, Vector3 worldPoint, Vector3 origin)
        {
            Vector3 direction = (worldPoint - origin).normalized;
            direction.y = 0f; // Keep on horizontal plane
            return direction;
        }
    }
}

