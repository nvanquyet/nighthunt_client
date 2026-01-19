using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

namespace NightHunt.Gameplay.Core.Networking
{
    /// <summary>
    /// Manages local spawn on client, syncs via network
    /// Reduces server load by spawning visuals locally
    /// </summary>
    public class LocalSpawnManager : MonoBehaviour
    {
        private static LocalSpawnManager _instance;
        public static LocalSpawnManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("LocalSpawnManager");
                    _instance = go.AddComponent<LocalSpawnManager>();
                }
                return _instance;
            }
        }

        private readonly Dictionary<uint, GameObject> localSpawnedObjects = new Dictionary<uint, GameObject>();

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Spawn object locally (client-side only)
        /// </summary>
        public GameObject SpawnLocal(GameObject prefab, Vector3 position, Quaternion rotation, uint networkId)
        {
            if (prefab == null)
            {
                Debug.LogError("[LocalSpawnManager] Prefab is null");
                return null;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            localSpawnedObjects[networkId] = instance;
            return instance;
        }

        /// <summary>
        /// Remove local spawned object
        /// </summary>
        public void RemoveLocal(uint networkId)
        {
            if (localSpawnedObjects.TryGetValue(networkId, out GameObject obj))
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
                localSpawnedObjects.Remove(networkId);
            }
        }

        /// <summary>
        /// Get local spawned object
        /// </summary>
        public GameObject GetLocal(uint networkId)
        {
            localSpawnedObjects.TryGetValue(networkId, out GameObject obj);
            return obj;
        }

        /// <summary>
        /// Clear all local spawned objects
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in localSpawnedObjects)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            localSpawnedObjects.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}

