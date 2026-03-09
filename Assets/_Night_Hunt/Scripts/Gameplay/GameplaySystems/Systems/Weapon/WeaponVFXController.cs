using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Handles weapon visual effects for the local player's character.
    ///
    /// Attach to the same GameObject as WeaponSystem (or any child with access to it).
    ///
    /// Architecture:
    ///   • Subscribes to <see cref="IWeaponSystem.OnShotFired"/>.
    ///   • On each shot: reads <see cref="WeaponDefinition.MuzzleFlashPrefab"/>,
    ///     <see cref="WeaponDefinition.ProjectilePrefab"/>, <see cref="WeaponDefinition.BulletTrailPrefab"/>,
    ///     <see cref="WeaponDefinition.HitEffectPrefab"/> from the active weapon's definition.
    ///   • Spawns / plays them from <see cref="_muzzlePoint"/> — no centralized prefab refs on Character.
    ///
    /// Inspector setup:
    ///   • _weaponSystemSource – MonoBehaviour implementing IWeaponSystem (same GO as WeaponSystem)
    ///   • _muzzlePoint        – Transform at the barrel tip (child of weapon model)
    ///   • _muzzleFlashPool    – optional pool parent; prefabs are instantiated here if supplied
    /// </summary>
    public class WeaponVFXController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (e.g. the WeaponSystem component).")]
        [SerializeField] private MonoBehaviour _weaponSystemSource;

        [Tooltip("Transform at weapon barrel tip — origin for muzzle flash and projectile direction.")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("Parent transform used to keep instantiated VFX organised. If null, spawns at scene root.")]
        [SerializeField] private Transform _vfxPoolParent;

        // ── Runtime ──────────────────────────────────────────────────────────
        private IWeaponSystem _weaponSystem;

        // ── Per-prefab pool ───────────────────────────────────────────────────
        // Key = source prefab, Value = queue of dormant instances.
        // Avoids GC pressure from Instantiate/Destroy on every shot.
        private readonly Dictionary<GameObject, Queue<GameObject>> _vfxPool
            = new Dictionary<GameObject, Queue<GameObject>>();

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemSource as IWeaponSystem;
            if (_weaponSystem == null)
                Debug.LogWarning("[WeaponVFXController] _weaponSystemSource does not implement IWeaponSystem.");
        }

        private void OnEnable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired    += HandleShotFired;
                _weaponSystem.OnHitscanResult += HandleHitscanResult;
            }
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired    -= HandleShotFired;
                _weaponSystem.OnHitscanResult -= HandleHitscanResult;
            }
        }

        // ── Event Handler ─────────────────────────────────────────────────────

        private void HandleShotFired(WeaponSlotType slot, Vector3 aimDirection)
        {
            // All VFX (muzzle flash, trail, hit effect) are children of the bullet prefab
            // spawned by ProjectileWeapon + ProjectileSpawner. Nothing to do here.
        }

        /// <summary>
        /// Draws a bullet-trail prefab from muzzle origin to the confirmed ray endpoint.
        /// Subscribed to <see cref="IWeaponSystem.OnHitscanResult"/>.
        /// </summary>
        private void HandleHitscanResult(WeaponSlotType slot, Vector3 origin, Vector3 endpoint)
        {
            // All visual feedback (trail, hit effect) is owned by the bullet prefab.
            // Reserved for future central hooks (e.g. camera shake, audio).
        }

        // ── Pooling helpers ───────────────────────────────────────────────────

        /// <summary>Get a pooled instance of <paramref name="prefab"/>, or instantiate a new one.</summary>
        private GameObject PoolGet(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!_vfxPool.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _vfxPool[prefab] = queue;
            }

            GameObject instance;
            while (queue.Count > 0)
            {
                instance = queue.Dequeue();
                if (instance != null) // guard against destroyed objects
                {
                    instance.transform.SetPositionAndRotation(position, rotation);
                    instance.SetActive(true);
                    // Replay particles if present.
                    var ps = instance.GetComponentInChildren<ParticleSystem>();
                    ps?.Play(true);
                    return instance;
                }
            }

            return Instantiate(prefab, position, rotation, _vfxPoolParent);
        }

        /// <summary>Return <paramref name="instance"/> to the pool after <paramref name="delay"/> seconds.</summary>
        private void PoolReturn(GameObject instance, GameObject prefab, float delay)
        {
            StartCoroutine(ReturnAfterDelay(instance, prefab, delay));
        }

        private IEnumerator ReturnAfterDelay(GameObject instance, GameObject prefab, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (instance == null) yield break;
            var ps = instance.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.SetActive(false);
            if (_vfxPool.TryGetValue(prefab, out var queue))
                queue.Enqueue(instance);
        }

        // ── Legacy (kept for external callers) ────────────────────────────────
        private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return PoolGet(prefab, position, rotation);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Override the muzzle point at runtime (e.g. after weapon model swap).
        /// </summary>
        public void SetMuzzlePoint(Transform muzzle) => _muzzlePoint = muzzle;
    }
}
