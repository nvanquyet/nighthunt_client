using UnityEngine;
using Object = UnityEngine.Object;

namespace NightHunt.Gameplay.Core.Utils
{
    /// <summary>
    /// Helper functions for local spawn
    /// </summary>
    public static class SpawnUtils
    {
        /// <summary>
        /// Spawn prefab at position with rotation
        /// </summary>
        public static GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[SpawnUtils] Prefab is null");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
            return instance;
        }

        /// <summary>
        /// Spawn prefab at position
        /// </summary>
        public static GameObject SpawnPrefab(GameObject prefab, Vector3 position, Transform parent = null)
        {
            return SpawnPrefab(prefab, position, Quaternion.identity, parent);
        }

        /// <summary>
        /// Spawn prefab at transform position
        /// </summary>
        public static GameObject SpawnPrefab(GameObject prefab, Transform parent)
        {
            if (parent == null)
            {
                Debug.LogError("[SpawnUtils] Parent transform is null");
                return null;
            }

            return SpawnPrefab(prefab, parent.position, parent.rotation, parent);
        }

        /// <summary>
        /// Destroy GameObject safely
        /// </summary>
        public static void DestroySafely(GameObject obj)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
            }
        }

        /// <summary>
        /// Destroy GameObject immediately
        /// </summary>
        public static void DestroyImmediate(GameObject obj)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}

