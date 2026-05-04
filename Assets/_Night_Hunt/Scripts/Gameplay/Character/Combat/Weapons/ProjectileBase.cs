using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Base class for all projectiles (bullets, grenades, smoke…).
    ///
    /// Prefab structure:
    ///   MuzzleFlash: particle child, inactive by default; played once at spawn then hidden.
    ///   MainVisual: mesh and trail, active by default; hidden on impact if hideTrailOnImpact is true.
    ///   DetonationVFX: single particle child, inactive by default; activated on impact/explosion.
    ///
    /// VFX ownership: muzzle flash, trail, and detonation are managed here, not on the weapon.
    /// The projectile is spawned at the muzzle point, so muzzle flash plays correctly at spawn.
    /// </summary>
    public class ProjectileBase : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // VFX children assigned in the projectile prefab.
        // -----------------------------------------------------------------
        [Header("VFX Children")]
        [Tooltip("Child particle for muzzle flash. Inactive by default, played once on spawn.")]
        public GameObject muzzleFlashChild;

        [Tooltip("Child containing mesh/trail. Active while projectile is in flight.")]
        public GameObject mainVisualChild;

        [Tooltip("Single particle child for impact/explosion/smoke. Inactive by default.")]
        public GameObject detonationVFXChild;

        // -----------------------------------------------------------------
        // Hit VFX Prefabs  (assigned on the projectile prefab in the Inspector)
        // -----------------------------------------------------------------
        [Header("Hit VFX Prefabs (pooled — leave null to skip)")]
        [Tooltip("Particle prefab spawned at the hit point when the bullet hits an IHittable (player, deployable, boss).\n" +
                 "Leave null to use detonationVFXChild for all hit types.")]
        public GameObject bloodVFXPrefab;

        [Tooltip("Particle prefab spawned at the hit point when the bullet hits environment geometry (wall, floor, obstacle).\n" +
                 "Leave null to use detonationVFXChild for all hit types.")]
        public GameObject impactVFXPrefab;

        [Tooltip("Seconds before the spawned hit-VFX GameObject is returned to the pool / destroyed.")]
        public float hitVFXLifetime = 2f;

        [Header("Detonation Config")]
        [Tooltip("Detonate immediately on collision. False = wait for fuseTime only.")]
        public bool isImpact = true;

        [Tooltip("Seconds before auto-detonation. 0 = disabled (impact only).")]
        public float fuseTime = 0f;

        [Tooltip("Seconds the projectile stays active after detonation (lets VFX finish).")]
        public float lifetimeAfterImpact = 3f;

        [Tooltip("Hide MainVisual immediately on impact so only DetonationVFX is visible.")]
        public bool hideTrailOnImpact = true;

        [Tooltip("Duration in seconds to show muzzle flash before hiding it. 0 = single frame.")]
        public float muzzleFlashDuration = 0.05f;

        private Transform _muzzleOriginalParent;
        private Vector3 _muzzleOriginalLocalPosition;
        private Quaternion _muzzleOriginalLocalRotation;
        private Vector3 _muzzleOriginalLocalScale;
        private Coroutine _muzzleFlashRoutine;

        private Transform _detonationOriginalParent;
        private Vector3 _detonationOriginalLocalPosition;
        private Quaternion _detonationOriginalLocalRotation;
        private Vector3 _detonationOriginalLocalScale;

        // -----------------------------------------------------------------
        // Called in OnEnable so pool reuse resets visual state.
        // -----------------------------------------------------------------
        protected virtual void OnEnable()
        {
            ResetDetachedVfx();
            EnsureVfxReferences();

            if (muzzleFlashChild != null)
                muzzleFlashChild.SetActive(false);

            if (mainVisualChild != null)
                mainVisualChild.SetActive(true);

            if (detonationVFXChild != null)
                detonationVFXChild.SetActive(false);
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Play muzzle flash once on spawn. Hides itself after muzzleFlashDuration seconds.
        /// The projectile is spawned at the muzzle point, so position is always correct.
        /// </summary>
        public virtual void PlayMuzzleFlash()
        {
            EnsureVfxReferences();
            if (muzzleFlashChild == null) return;

            // Enforce a minimum so the flash is visible at typical frame rates.
            // 0.05 s (3 frames @ 60 fps) is imperceptible; clamp to at least 0.15 s.
            float effectiveDuration = Mathf.Max(muzzleFlashDuration, 0.15f);

            CacheOriginalTransform(muzzleFlashChild, ref _muzzleOriginalParent,
                ref _muzzleOriginalLocalPosition, ref _muzzleOriginalLocalRotation, ref _muzzleOriginalLocalScale);
            muzzleFlashChild.transform.SetParent(null, true);
            muzzleFlashChild.SetActive(true);
            RestartParticleSystems(muzzleFlashChild);
            if (_muzzleFlashRoutine != null)
                StopCoroutine(_muzzleFlashRoutine);
            _muzzleFlashRoutine = StartCoroutine(HideMuzzleFlashAfter(effectiveDuration));
        }

        private System.Collections.IEnumerator HideMuzzleFlashAfter(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
            _muzzleFlashRoutine = null;
            if (muzzleFlashChild != null)
            {
                muzzleFlashChild.SetActive(false);
                RestoreOriginalTransform(muzzleFlashChild, _muzzleOriginalParent,
                    _muzzleOriginalLocalPosition, _muzzleOriginalLocalRotation, _muzzleOriginalLocalScale);
            }
        }

        /// <summary>Restart main visual (trail/mesh). Called when spawning/reusing from pool.</summary>
        public virtual void PlayMainVisual()
        {
            EnsureVfxReferences();
            if (mainVisualChild == null)
            {
                Debug.LogWarning($"[ProjectileBase] PlayMainVisual: mainVisualChild is NULL on '{name}' - assign it in the prefab Inspector.");
                return;
            }
            mainVisualChild.SetActive(true);
            RestartParticleSystems(mainVisualChild);
        }

        /// <summary>
        /// Activate DetonationVFX at the impact position.
        /// Hides MainVisual if hideTrailOnImpact = true.
        /// No instantiate; only toggles children.
        /// </summary>
        public virtual void TriggerDetonation(Vector3 position, Quaternion rotation)
        {
            EnsureVfxReferences();
            if (hideTrailOnImpact && mainVisualChild != null)
                mainVisualChild.SetActive(false);

            if (detonationVFXChild != null)
            {
                CacheOriginalTransform(detonationVFXChild, ref _detonationOriginalParent,
                    ref _detonationOriginalLocalPosition, ref _detonationOriginalLocalRotation, ref _detonationOriginalLocalScale);
                detonationVFXChild.transform.SetParent(null, true);
                detonationVFXChild.transform.position = position;
                detonationVFXChild.transform.rotation = rotation;
                detonationVFXChild.SetActive(true);
                RestartParticleSystems(detonationVFXChild);
            }
        }

        public virtual void ResetVisualStateForPool()
        {
            if (_muzzleFlashRoutine != null)
            {
                StopCoroutine(_muzzleFlashRoutine);
                _muzzleFlashRoutine = null;
            }

            ResetDetachedVfx();
            EnsureVfxReferences();
            if (muzzleFlashChild != null) muzzleFlashChild.SetActive(false);
            if (detonationVFXChild != null) detonationVFXChild.SetActive(false);
            // Keep mainVisualChild INACTIVE so OnEnable() can toggle it on with a proper
            // SetActive(false→true) cycle that fires OnEnable on child ParticleSystem
            // components and respects playOnAwake.  This is the key fix for VFX not
            // replaying when a pooled projectile is reused during rapid auto-fire.
            if (mainVisualChild != null) mainVisualChild.SetActive(false);
        }

        // -----------------------------------------------------------------
        // Hit VFX chooser
        // -----------------------------------------------------------------

        /// <summary>
        /// Spawn the correct impact VFX at <paramref name="hitPoint"/>:
        ///   • <paramref name="hitAnIHittable"/> == true  → spawn <see cref="bloodVFXPrefab"/> (player / deployable hit)
        ///   • <paramref name="hitAnIHittable"/> == false → spawn <see cref="impactVFXPrefab"/> (environment hit)
        ///
        /// Falls back to activating <see cref="detonationVFXChild"/> in-place if neither prefab is assigned,
        /// preserving the original single-VFX behaviour.
        ///
        /// Call this from <see cref="ProjectileComponent.HandleImpact"/> instead of letting
        /// <see cref="TriggerDetonation"/> handle everything so the blood/impact split works for
        /// both hitscan visual bullets and ballistic projectiles.
        /// </summary>
        public bool SpawnHitVFX(Vector3 hitPoint, Vector3 hitNormal, bool hitAnIHittable)
        {
            GameObject prefabToSpawn = hitAnIHittable ? bloodVFXPrefab : impactVFXPrefab;

            if (prefabToSpawn != null)
            {
                // Use the hit-normal to orient the effect flush with the surface.
                Quaternion rot = hitNormal.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(hitNormal)
                    : Quaternion.identity;

                var go = Instantiate(prefabToSpawn, hitPoint, rot);
                if (hitVFXLifetime > 0f)
                    Destroy(go, hitVFXLifetime);
                return true;
            }

            // Fallback: use the built-in detonation child (original behaviour, no VFX split).
            TriggerDetonation(hitPoint, Quaternion.LookRotation(hitNormal.sqrMagnitude > 0.001f ? hitNormal : Vector3.up));
            return false;
        }

        // -----------------------------------------------------------------
        // Helper
        // -----------------------------------------------------------------
        protected void RestartParticleSystems(GameObject root)
        {
            Transform rootTransform = root.transform;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ActivateHierarchyUpTo(ps.transform, rootTransform);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                ActivateHierarchyUpTo(trail.transform, rootTransform);
                trail.Clear();
            }
        }

        private static void ActivateHierarchyUpTo(Transform child, Transform root)
        {
            while (child != null)
            {
                child.gameObject.SetActive(true);
                if (child == root)
                    break;

                child = child.parent;
            }
        }

        private void ResetDetachedVfx()
        {
            if (muzzleFlashChild != null && _muzzleOriginalParent != null && CanRestoreOwnedVfx(_muzzleOriginalParent))
                RestoreOriginalTransform(muzzleFlashChild, _muzzleOriginalParent,
                    _muzzleOriginalLocalPosition, _muzzleOriginalLocalRotation, _muzzleOriginalLocalScale);
            else if (muzzleFlashChild == null || !IsOwnedVfxChild(muzzleFlashChild))
                ClearMuzzleTransformCache();

            if (detonationVFXChild != null && _detonationOriginalParent != null && CanRestoreOwnedVfx(_detonationOriginalParent))
                RestoreOriginalTransform(detonationVFXChild, _detonationOriginalParent,
                    _detonationOriginalLocalPosition, _detonationOriginalLocalRotation, _detonationOriginalLocalScale);
            else if (detonationVFXChild == null || !IsOwnedVfxChild(detonationVFXChild))
                ClearDetonationTransformCache();
        }

        private void EnsureVfxReferences()
        {
            if (!IsOwnedOrDetachedVfxChild(muzzleFlashChild, _muzzleOriginalParent))
            {
                WarnExternalVfxReference(nameof(muzzleFlashChild), muzzleFlashChild);
                muzzleFlashChild = null;
                ClearMuzzleTransformCache();
            }

            if (!IsOwnedVfxChild(mainVisualChild))
            {
                WarnExternalVfxReference(nameof(mainVisualChild), mainVisualChild);
                mainVisualChild = null;
            }

            if (!IsOwnedOrDetachedVfxChild(detonationVFXChild, _detonationOriginalParent))
            {
                WarnExternalVfxReference(nameof(detonationVFXChild), detonationVFXChild);
                detonationVFXChild = null;
                ClearDetonationTransformCache();
            }

            if (muzzleFlashChild == null)
                muzzleFlashChild = FindChildGameObject("[MuzzleFlash]", "MuzzleFlash");

            if (mainVisualChild == null)
                mainVisualChild = FindChildGameObject("[MainVisual]", "MainVisual", "[Model]", "Model");

            if (detonationVFXChild == null)
                detonationVFXChild = FindChildGameObject("[DetonationVFX]", "DetonationVFX", "[ImpactVFX]", "ImpactVFX");

            if (mainVisualChild == null)
            {
                var trail = GetComponentInChildren<TrailRenderer>(true);
                if (trail != null)
                    mainVisualChild = trail.gameObject;
            }

            if (mainVisualChild == null)
            {
                var renderer = GetComponentInChildren<Renderer>(true);
                if (renderer != null && renderer.gameObject != gameObject)
                    mainVisualChild = renderer.gameObject;
            }
        }

        private bool IsOwnedVfxChild(GameObject candidate)
        {
            return candidate == null ||
                   (candidate.transform != transform && candidate.transform.IsChildOf(transform));
        }

        private bool IsOwnedOrDetachedVfxChild(GameObject candidate, Transform originalParent)
        {
            if (IsOwnedVfxChild(candidate))
                return true;

            return candidate != null &&
                   originalParent != null &&
                   candidate.transform != transform &&
                   CanRestoreOwnedVfx(originalParent);
        }

        private bool CanRestoreOwnedVfx(Transform originalParent)
        {
            return originalParent != null &&
                   (originalParent == transform || originalParent.IsChildOf(transform));
        }

        private void WarnExternalVfxReference(string fieldName, GameObject candidate)
        {
            if (candidate == null)
                return;

            Debug.LogWarning(
                $"[ProjectileBase] {fieldName} on '{name}' points outside this projectile instance " +
                $"('{candidate.name}'). Ignoring it to prevent pooled projectile VFX from detaching weapon transforms.",
                this);
        }

        private void ClearMuzzleTransformCache()
        {
            _muzzleOriginalParent = null;
            _muzzleOriginalLocalPosition = Vector3.zero;
            _muzzleOriginalLocalRotation = Quaternion.identity;
            _muzzleOriginalLocalScale = Vector3.one;
        }

        private void ClearDetonationTransformCache()
        {
            _detonationOriginalParent = null;
            _detonationOriginalLocalPosition = Vector3.zero;
            _detonationOriginalLocalRotation = Quaternion.identity;
            _detonationOriginalLocalScale = Vector3.one;
        }

        private GameObject FindChildGameObject(params string[] names)
        {
            if (names == null || names.Length == 0)
                return null;

            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform)
                    continue;

                for (int i = 0; i < names.Length; i++)
                {
                    if (child.name == names[i])
                        return child.gameObject;
                }
            }

            return null;
        }

        private static void CacheOriginalTransform(
            GameObject child,
            ref Transform parent,
            ref Vector3 localPosition,
            ref Quaternion localRotation,
            ref Vector3 localScale)
        {
            if (child == null || parent != null)
                return;

            var t = child.transform;
            parent = t.parent;
            localPosition = t.localPosition;
            localRotation = t.localRotation;
            localScale = t.localScale;
        }

        private static void RestoreOriginalTransform(
            GameObject child,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            if (child == null || parent == null)
                return;

            var t = child.transform;
            if (t.parent != parent)
                t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;
        }
    }
}
