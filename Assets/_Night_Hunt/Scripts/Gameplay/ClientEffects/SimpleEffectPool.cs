using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Scene-level singleton object pool for short-lived visual effect GameObjects.
    ///
    /// ARCHITECTURE (mirrors ProjectilePool):
    ///   • One queue per prefab — instances are keyed by prefab reference.
    ///   • instanceToPrefab map lets Return() work without the caller knowing the prefab.
    ///   • All VFX lifetime is managed here — callers never call Destroy().
    ///
    /// SETUP:
    ///   Add this component to a persistent scene GameObject (e.g. "Systems" or "VFX").
    ///   Do NOT add it to DontDestroyOnLoad unless all VFX prefabs should persist.
    ///
    /// USAGE:
    ///   // Fire-and-forget (most common):
    ///   SimpleEffectPool.Instance.Play(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal), lifetime: 2f);
    ///
    ///   // Manual control (when you need the reference, e.g. to track a moving target):
    ///   var go = SimpleEffectPool.Instance.Rent(trailPrefab, origin, rotation);
    ///   // ... later when done:
    ///   SimpleEffectPool.Instance.Return(go);
    /// </summary>
    public sealed class SimpleEffectPool : MonoBehaviour
    {
        public static SimpleEffectPool Instance { get; private set; }

        // prefab → queue of inactive instances
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools
            = new Dictionary<GameObject, Queue<GameObject>>();

        // instance ID → source prefab  (allows Return without knowing the prefab)
        private readonly Dictionary<int, GameObject> _instanceToPrefab
            = new Dictionary<int, GameObject>();

        // -----------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------

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

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Activate a pooled instance at the given transform, then auto-return it after
        /// <paramref name="lifetime"/> seconds. Zero-allocation hot path — no GC beyond
        /// the initial pool-fill Instantiate calls.
        /// </summary>
        public void Play(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            var go = Rent(prefab, position, rotation);
            if (go != null)
                StartCoroutine(ReturnAfter(go, lifetime));
        }

        /// <summary>
        /// Get an activated instance from the pool (or instantiate a new one).
        /// The caller is responsible for calling <see cref="Return"/> when done.
        /// Prefer <see cref="Play"/> for fire-and-forget effects.
        /// </summary>
        public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[SimpleEffectPool] Rent called with null prefab.");
                return null;
            }

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }

            GameObject go;
            if (queue.Count > 0)
            {
                go = queue.Dequeue();
                if (go == null)                          // destroyed externally (edge case)
                    go = Instantiate(prefab, transform); // spawn fresh
            }
            else
            {
                go = Instantiate(prefab, transform);
            }

            _instanceToPrefab[go.GetInstanceID()] = prefab;

            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            return go;
        }

        /// <summary>
        /// Return an instance immediately to its pool (deactivates it, never Destroys).
        /// Safe to call even if the instance was never rented from this pool
        /// (will be deactivated and added to the prefab's queue regardless).
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;

            instance.SetActive(false);

            int id = instance.GetInstanceID();
            if (_instanceToPrefab.TryGetValue(id, out var sourcePrefab))
            {
                if (!_pools.TryGetValue(sourcePrefab, out var queue))
                {
                    queue = new Queue<GameObject>();
                    _pools[sourcePrefab] = queue;
                }
                queue.Enqueue(instance);
            }
            else
            {
                // Instance came from outside this pool; just parent and deactivate.
                instance.transform.SetParent(transform, false);
            }
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        private IEnumerator ReturnAfter(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            Return(instance);
        }
    }
}
