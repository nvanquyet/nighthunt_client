using UnityEngine;
using System.Collections.Generic;
using FishNet;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Base class for loot managers that handle spawn points.
    /// Provides common functionality for registering, unregistering, and finding spawn points.
    /// </summary>
    /// <typeparam name="TSpawnPoint">Type of spawn point to manage (LootSpawnPoint or LootContainerPoint)</typeparam>
    public abstract class LootManagerBase<TSpawnPoint> : MonoBehaviour where TSpawnPoint : MonoBehaviour
    {
        [Header("Spawn Point Management")]
        [SerializeField] protected bool autoFindSpawnPoints = true;
        [SerializeField] protected TSpawnPoint[] spawnPoints = new TSpawnPoint[0];

        [HideInInspector]
        protected List<TSpawnPoint> activeSpawnPoints = new List<TSpawnPoint>();

        protected virtual void Start()
        {
            // Only execute on server
            if (!InstanceFinder.IsServer)
            {
                return;
            }

            if (autoFindSpawnPoints)
            {
                FindAllSpawnPoints();
            }
            else
            {
                activeSpawnPoints.AddRange(spawnPoints);
            }
        }

        /// <summary>
        /// Find all spawn points of type TSpawnPoint in the scene.
        /// Must be implemented by derived classes to specify the exact type.
        /// </summary>
        protected abstract void FindAllSpawnPoints();

        /// <summary>
        /// Register a spawn point manually.
        /// </summary>
        public void RegisterSpawnPoint(TSpawnPoint spawnPoint)
        {
            if (spawnPoint != null && !activeSpawnPoints.Contains(spawnPoint))
            {
                activeSpawnPoints.Add(spawnPoint);
            }
        }

        /// <summary>
        /// Unregister a spawn point.
        /// </summary>
        public void UnregisterSpawnPoint(TSpawnPoint spawnPoint)
        {
            activeSpawnPoints.Remove(spawnPoint);
        }

        /// <summary>
        /// Get all active spawn points.
        /// </summary>
        public List<TSpawnPoint> GetActiveSpawnPoints()
        {
            return new List<TSpawnPoint>(activeSpawnPoints);
        }
    }
}
