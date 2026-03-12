using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Scene-level singleton object pool for ProjectileComponent instances.
    ///
    /// Setup: Add this component to a persistent scene GameObject (e.g. "Systems").
    ///
    /// Usage:
    ///   ProjectilePool.Instance.Get(prefab, pos, rot) → activated instance
    ///   ProjectilePool.Instance.Return(proj)           → deactivated, re-queued
    ///
    /// ProjectileComponent.Despawn() calls Return() automatically; callers do NOT
    /// need to return instances manually.
    ///
    /// Instances are reset via ProjectileBase.OnEnable() each time they are reused.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        // prefab → inactive instances
        private readonly Dictionary<GameObject, Queue<ProjectileComponent>> _pools
            = new Dictionary<GameObject, Queue<ProjectileComponent>>();

        // instance → source prefab (to return to the correct queue)
        private readonly Dictionary<ProjectileComponent, GameObject> _prefabMap
            = new Dictionary<ProjectileComponent, GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieve an instance from the pool (or instantiate a new one).
        /// Call <see cref="ProjectileComponent.Initialize"/> immediately after.
        /// </summary>
        public ProjectileComponent Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError("[ProjectilePool] prefab is null.");
                return null;
            }

            ProjectileComponent proj = null;

            if (_pools.TryGetValue(prefab, out var queue))
            {
                // Drain any instances that were manually destroyed outside the pool.
                while (queue.Count > 0 && queue.Peek() == null)
                    queue.Dequeue();

                if (queue.Count > 0)
                    proj = queue.Dequeue();
            }

            if (proj == null)
            {
                var go = Instantiate(prefab, position, rotation);
                proj = go.GetComponent<ProjectileComponent>();
                if (proj == null)
                {
                    Debug.LogError($"[ProjectilePool] '{prefab.name}' has no ProjectileComponent.");
                    Destroy(go);
                    return null;
                }
                _prefabMap[proj] = prefab;
            }
            else
            {
                proj.transform.SetPositionAndRotation(position, rotation);
                proj.gameObject.SetActive(true);
            }

            return proj;
        }

        /// <summary>
        /// Return a projectile to the pool. Called automatically by ProjectileComponent.Despawn.
        /// </summary>
        public void Return(ProjectileComponent proj)
        {
            if (proj == null) return;

            if (!_prefabMap.TryGetValue(proj, out var prefab))
            {
                // Not tracked — was created outside the pool; just deactivate.
                proj.gameObject.SetActive(false);
                return;
            }

            if (!_pools.ContainsKey(prefab))
                _pools[prefab] = new Queue<ProjectileComponent>();

            proj.gameObject.SetActive(false);
            _pools[prefab].Enqueue(proj);
        }
    }
}
