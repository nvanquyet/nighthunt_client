using System.Collections.Generic;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Scene-level singleton object pool for ProjectileComponent instances.
    ///
    /// Setup: Add this component to a persistent scene GameObject (e.g. "Systems").
    ///
    /// Usage:
    ///   ProjectilePool.Instance.Get(prefab, pos, rot): activated instance
    ///   ProjectilePool.Instance.Return(proj): deactivated, re-queued
    ///
    /// ProjectileComponent.Despawn() calls Return() automatically; callers do NOT
    /// need to return instances manually.
    ///
    /// Instances are reset via ProjectileBase.OnEnable() each time they are reused.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        // Singleton.
        private static ProjectilePool _instance;

        /// <summary>
        /// Scene-level singleton. If no ProjectilePool exists in the scene it is
        /// auto-created at runtime for the current scene only.
        /// Best practice: add ProjectilePool to your "Systems" persistent GameObject
        /// so it initialises at scene load rather than on the first shot.
        /// </summary>
        public static ProjectilePool Instance
        {
            get
            {
                if (_instance == null)
                {
                    #if UNITY_2023_2_OR_NEWER
                    _instance = FindFirstObjectByType<ProjectilePool>();
                    #else
                    _instance = FindObjectOfType<ProjectilePool>();
                    #endif
                    if (_instance == null)
                    {
                        var go = new GameObject("[ProjectilePool – Auto]");
                        _instance = go.AddComponent<ProjectilePool>();
                        Debug.LogWarning(
                            "[ProjectilePool] No ProjectilePool found in scene - " +
                            "auto-created a scene-local one. Add ProjectilePool to your scene Systems object " +
                            "to avoid this message.");
                    }
                }
                return _instance;
            }
        }

        // Prefab to inactive instances.
        private readonly Dictionary<GameObject, Queue<ProjectileComponent>> _pools
            = new Dictionary<GameObject, Queue<ProjectileComponent>>();

        // Instance to source prefab, used to return to the correct queue.
        private readonly Dictionary<ProjectileComponent, GameObject> _prefabMap
            = new Dictionary<ProjectileComponent, GameObject>();

        // Lifecycle.

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // Public API.

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
                proj = ComponentResolver.Find<ProjectileComponent>(go)
                                        .OnSelf()
                                        .InChildren()
                                        .OrLogWarning("[Auto] ProjectileComponent not found")
                                        .Resolve();
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
                proj.transform.SetParent(null, true);
                proj.transform.SetPositionAndRotation(position, rotation);
                ResetPhysics(proj);
                proj.gameObject.SetActive(true);
            }

            proj.transform.SetParent(null, true);
            proj.transform.SetPositionAndRotation(position, rotation);
            Debug.Log($"[PROJ_VFX] Pool.Get prefab='{prefab.name}' instance='{proj.name}' parent={(proj.transform.parent != null ? proj.transform.parent.name : "null")} pos={position:F2} rot={rotation.eulerAngles:F1}");

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
                // Not tracked; created outside the pool, so just deactivate.
                proj.gameObject.SetActive(false);
                return;
            }

            if (!_pools.ContainsKey(prefab))
                _pools[prefab] = new Queue<ProjectileComponent>();

            proj.ResetVisualStateForPool();
            ResetPhysics(proj);
            proj.transform.SetParent(transform, true);
            proj.gameObject.SetActive(false);
            Debug.Log($"[PROJ_VFX] Pool.Return instance='{proj.name}' parent='{transform.name}' pos={proj.transform.position:F2}");
            _pools[prefab].Enqueue(proj);
        }

        private static void ResetPhysics(ProjectileComponent proj)
        {
            if (proj == null)
                return;

            foreach (var rb in proj.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
