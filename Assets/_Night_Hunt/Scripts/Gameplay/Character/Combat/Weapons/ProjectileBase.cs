using UnityEngine;
using NightHunt.Gameplay.ClientEffects;
using NightHunt.Diagnostics;
using NightHunt.GameplaySystems.Core.Configs;

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
        [Header("VFX Particle Systems")]
        [Tooltip("Primary muzzle flash ParticleSystem. The projectile restarts it explicitly on every spawn.")]
        public ParticleSystem muzzleFlashSystem;

        [Tooltip("Primary detonation ParticleSystem. The projectile restarts it explicitly on every impact.")]
        public ParticleSystem detonationVFXSystem;

        [Header("Main Visual")]
        [Tooltip("Child containing projectile model/mesh/trail. Active while projectile is in flight.")]
        public GameObject mainVisualChild;

        [Header("Legacy VFX Containers (auto-resolved)")]
        [HideInInspector]
        [Tooltip("Child particle for muzzle flash. Inactive by default, played once on spawn.")]
        public GameObject muzzleFlashChild;

        [HideInInspector]
        [Tooltip("Single particle child for impact/explosion/smoke. Inactive by default.")]
        public GameObject detonationVFXChild;

        // -----------------------------------------------------------------
        // Hit VFX Prefabs  (assigned on the projectile prefab in the Inspector)
        // -----------------------------------------------------------------
        [Header("Hit VFX Prefabs (pooled — leave null to skip)")]
        [Tooltip("Particle prefab spawned at the hit point when the bullet hits an IHittable (player, deployable, boss).\n" +
                 "Leave null to use detonationVFXChild for all hit types.")]
        public ParticleSystem bloodVFXSystemPrefab;

        [Tooltip("Particle prefab spawned at the hit point when the bullet hits environment geometry (wall, floor, obstacle).\n" +
                 "Leave null to use detonationVFXChild for all hit types.")]
        public ParticleSystem impactVFXSystemPrefab;

        [Header("Legacy Hit VFX GameObject Prefabs")]
        [HideInInspector]
        [Tooltip("Legacy GameObject prefab for blood VFX. Prefer bloodVFXSystemPrefab for new setup.")]
        public GameObject bloodVFXPrefab;

        [HideInInspector]
        [Tooltip("Legacy GameObject prefab for impact VFX. Prefer impactVFXSystemPrefab for new setup.")]
        public GameObject impactVFXPrefab;

        [Tooltip("Seconds before the spawned hit-VFX instance is returned to the pool / destroyed.")]
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
            PrepareVisualStateForLaunch();
        }

        // -----------------------------------------------------------------
        // Visual state gates
        // -----------------------------------------------------------------
        protected void PrepareVisualStateForLaunch()
        {
            if (_muzzleFlashRoutine != null)
            {
                StopCoroutine(_muzzleFlashRoutine);
                _muzzleFlashRoutine = null;
            }

            ResetDetachedVfx();
            EnsureVfxReferences();

            GameObject muzzleRoot = GetVfxRoot(muzzleFlashSystem, muzzleFlashChild);
            GameObject detonationRoot = GetVfxRoot(detonationVFXSystem, detonationVFXChild);

            StopAndHideVfx(muzzleRoot);
            StopAndHideVfx(detonationRoot);
            StopAndHideOwnedOneShotVfx(muzzleRoot, detonationRoot);
            StopAndHideNestedOneShotVfx(mainVisualChild, muzzleRoot, detonationRoot);

            if (mainVisualChild != null)
            {
                StopParticleSystems(mainVisualChild);
                mainVisualChild.SetActive(false);
            }
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
            GameObject muzzleRoot = GetVfxRoot(muzzleFlashSystem, muzzleFlashChild);
            if (muzzleRoot == null) return;

            // Enforce a minimum so the flash is visible at typical frame rates.
            // 0.05 s (3 frames @ 60 fps) is imperceptible; clamp to at least 0.15 s.
            float effectiveDuration = Mathf.Max(muzzleFlashDuration, 0.15f);

            CacheOriginalTransform(muzzleRoot, ref _muzzleOriginalParent,
                ref _muzzleOriginalLocalPosition, ref _muzzleOriginalLocalRotation, ref _muzzleOriginalLocalScale);
            muzzleRoot.transform.SetParent(null, true);
            muzzleRoot.SetActive(true);
            RestartParticleSystems(muzzleRoot);
            if (_muzzleFlashRoutine != null)
                StopCoroutine(_muzzleFlashRoutine);
            _muzzleFlashRoutine = StartCoroutine(HideMuzzleFlashAfter(effectiveDuration));
        }

        private System.Collections.IEnumerator HideMuzzleFlashAfter(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
            _muzzleFlashRoutine = null;
            GameObject muzzleRoot = GetVfxRoot(muzzleFlashSystem, muzzleFlashChild);
            if (muzzleRoot != null)
            {
                StopAndHideVfx(muzzleRoot);
                RestoreOriginalTransform(muzzleRoot, _muzzleOriginalParent,
                    _muzzleOriginalLocalPosition, _muzzleOriginalLocalRotation, _muzzleOriginalLocalScale);
            }
        }

        /// <summary>Restart main visual (trail/mesh). Called when spawning/reusing from pool.</summary>
        public virtual void PlayMainVisual()
        {
            ShowMainVisual();
        }

        /// <summary>
        /// Activate DetonationVFX at the impact position.
        /// Hides MainVisual if hideTrailOnImpact = true.
        /// No instantiate; only toggles children.
        /// </summary>
        public virtual void TriggerDetonation(Vector3 position, Quaternion rotation)
        {
            EnsureVfxReferences();
            if (hideTrailOnImpact)
                HideMainVisual();

            GameObject detonationRoot = GetVfxRoot(detonationVFXSystem, detonationVFXChild);
            if (detonationRoot != null)
            {
                CacheOriginalTransform(detonationRoot, ref _detonationOriginalParent,
                    ref _detonationOriginalLocalPosition, ref _detonationOriginalLocalRotation, ref _detonationOriginalLocalScale);
                detonationRoot.transform.SetParent(null, true);
                detonationRoot.transform.position = position;
                detonationRoot.transform.rotation = rotation;
                detonationRoot.SetActive(true);
                RestartParticleSystems(detonationRoot);
            }
        }

        public virtual void ResetVisualStateForPool()
        {
            // Keep the projectile model/trail inactive in the pool. The next Initialize()
            // shows it and clears any trails/child systems without relying on PlayOnAwake.
            PrepareVisualStateForLaunch();
        }

        // -----------------------------------------------------------------
        // Hit VFX chooser
        // -----------------------------------------------------------------

        /// <summary>
        /// Spawn the correct impact VFX at <paramref name="hitPoint"/>:
        ///   • <paramref name="hitAnIHittable"/> == true  → spawn <see cref="bloodVFXSystemPrefab"/> or legacy <see cref="bloodVFXPrefab"/>
        ///   • <paramref name="hitAnIHittable"/> == false → spawn <see cref="impactVFXSystemPrefab"/> or legacy <see cref="impactVFXPrefab"/>
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
            ParticleSystem systemPrefab = hitAnIHittable ? bloodVFXSystemPrefab : impactVFXSystemPrefab;
            GameObject prefabToSpawn = systemPrefab != null
                ? systemPrefab.gameObject
                : (hitAnIHittable ? bloodVFXPrefab : impactVFXPrefab);

            // Some older prefabs serialized detonationVFXChild into impactVFXPrefab.
            // Treat owned child refs as the in-place fallback instead of pooling a copy.
            if (prefabToSpawn != null && IsOwnedVfxChild(prefabToSpawn))
                prefabToSpawn = null;

            if (prefabToSpawn != null)
            {
                // Use the hit-normal to orient the effect flush with the surface.
                Quaternion rot = hitNormal.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(hitNormal)
                    : Quaternion.identity;

                var pool = SimpleEffectPool.Instance;
                if (pool != null)
                {
                    if (systemPrefab != null)
                        pool.Play(systemPrefab, hitPoint, rot, hitVFXLifetime);
                    else
                        pool.Play(prefabToSpawn, hitPoint, rot, hitVFXLifetime);
                }
                else
                {
                    var go = Instantiate(prefabToSpawn, hitPoint, rot);
                    RestartParticleSystems(go);
                    if (hitVFXLifetime > 0f)
                        Destroy(go, hitVFXLifetime);
                }
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
            if (root == null)
                return;

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

        private void RestartParticleSystemsExcept(GameObject root, params GameObject[] excludedRoots)
        {
            if (root == null)
                return;

            Transform rootTransform = root.transform;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (IsExcludedVfx(ps.transform, excludedRoots) || IsLikelyOneShotVfx(ps.transform, rootTransform))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    continue;
                }

                ActivateHierarchyUpTo(ps.transform, rootTransform);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (IsExcludedVfx(trail.transform, excludedRoots) || IsLikelyOneShotVfx(trail.transform, rootTransform))
                {
                    trail.Clear();
                    continue;
                }

                ActivateHierarchyUpTo(trail.transform, rootTransform);
                trail.Clear();
            }
        }

        protected void StopParticleSystems(GameObject root)
        {
            if (root == null)
                return;

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                trail.Clear();
        }

        protected static GameObject GetVfxRoot(ParticleSystem system, GameObject legacyRoot)
        {
            if (legacyRoot != null)
                return legacyRoot;

            return system != null ? system.gameObject : null;
        }

        protected void ShowMainVisual()
        {
            EnsureVfxReferences();
            if (mainVisualChild == null)
            {
                Debug.LogWarning($"[ProjectileBase] PlayMainVisual: mainVisualChild is NULL on '{name}' - assign the projectile model/mesh/trail GameObject in the prefab Inspector.");
                return;
            }

            GameObject muzzleRoot = GetVfxRoot(muzzleFlashSystem, muzzleFlashChild);
            GameObject detonationRoot = GetVfxRoot(detonationVFXSystem, detonationVFXChild);
            GameObject muzzleExclude = IsNestedVfxRoot(muzzleRoot, mainVisualChild) ? muzzleRoot : null;
            GameObject detonationExclude = IsNestedVfxRoot(detonationRoot, mainVisualChild) ? detonationRoot : null;

            mainVisualChild.SetActive(true);
            RestartParticleSystemsExcept(mainVisualChild, muzzleExclude, detonationExclude);

            if (muzzleExclude != null)
                StopAndHideVfx(muzzleExclude);
            if (detonationExclude != null)
                StopAndHideVfx(detonationExclude);

            StopAndHideOwnedOneShotVfx(muzzleRoot, detonationRoot);
            StopAndHideNestedOneShotVfx(mainVisualChild, muzzleExclude, detonationExclude);
        }

        protected void HideMainVisual()
        {
            EnsureVfxReferences();
            if (mainVisualChild == null)
                return;

            StopParticleSystems(mainVisualChild);
            mainVisualChild.SetActive(false);
        }

        protected void StopAndHideVfx(GameObject root)
        {
            if (root == null)
                return;

            StopParticleSystems(root);
            root.SetActive(false);
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

        private static bool IsNestedVfxRoot(GameObject candidate, GameObject mainRoot)
        {
            return candidate != null &&
                   mainRoot != null &&
                   candidate != mainRoot &&
                   candidate.transform.IsChildOf(mainRoot.transform);
        }

        private static bool IsExcludedVfx(Transform child, GameObject[] excludedRoots)
        {
            if (child == null || excludedRoots == null)
                return false;

            for (int i = 0; i < excludedRoots.Length; i++)
            {
                var root = excludedRoots[i];
                if (root == null)
                    continue;

                if (child == root.transform || child.IsChildOf(root.transform))
                    return true;
            }

            return false;
        }

        private void StopAndHideNestedOneShotVfx(GameObject root, params GameObject[] explicitRoots)
        {
            if (root == null)
                return;

            int stopped = 0;
            Transform rootTransform = root.transform;

            if (explicitRoots != null)
            {
                for (int i = 0; i < explicitRoots.Length; i++)
                {
                    var explicitRoot = explicitRoots[i];
                    if (!IsNestedVfxRoot(explicitRoot, root))
                        continue;

                    StopAndHideVfx(explicitRoot);
                    stopped++;
                }
            }

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!IsLikelyOneShotVfx(ps.transform, rootTransform))
                    continue;

                Transform oneShotRoot = FindOneShotVfxRoot(ps.transform, rootTransform);
                if (oneShotRoot != null && oneShotRoot != rootTransform)
                {
                    StopAndHideVfx(oneShotRoot.gameObject);
                    stopped++;
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    stopped++;
                }
            }

            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (!IsLikelyOneShotVfx(trail.transform, rootTransform))
                    continue;

                trail.Clear();
                Transform oneShotRoot = FindOneShotVfxRoot(trail.transform, rootTransform);
                if (oneShotRoot != null && oneShotRoot != rootTransform)
                    oneShotRoot.gameObject.SetActive(false);
                stopped++;
            }

            if (stopped > 0 && ProjectileDebugEnabled())
            {
                Debug.Log($"[PROJ_VFX] One-shot VFX stopped root={root.name} count={stopped} projectile={name}", this);
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Projectile,
                    "ProjectileOneShotVfxStopped",
                    $"projectile={name} root={root.name} count={stopped}",
                    this);
            }
        }

        private void StopAndHideOwnedOneShotVfx(params GameObject[] explicitRoots)
        {
            int stopped = 0;
            var stoppedRoots = new System.Collections.Generic.HashSet<Transform>();

            if (explicitRoots != null)
            {
                for (int i = 0; i < explicitRoots.Length; i++)
                {
                    var explicitRoot = explicitRoots[i];
                    if (explicitRoot == null ||
                        explicitRoot == mainVisualChild ||
                        !IsOwnedVfxChild(explicitRoot) ||
                        !stoppedRoots.Add(explicitRoot.transform))
                        continue;

                    StopAndHideVfx(explicitRoot);
                    stopped++;
                }
            }

            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                Transform oneShotRoot = FindOneShotVfxRoot(ps.transform, transform);
                if (oneShotRoot == null ||
                    oneShotRoot == transform ||
                    (mainVisualChild != null && oneShotRoot == mainVisualChild.transform) ||
                    !stoppedRoots.Add(oneShotRoot))
                    continue;

                StopAndHideVfx(oneShotRoot.gameObject);
                stopped++;
            }

            foreach (var trail in GetComponentsInChildren<TrailRenderer>(true))
            {
                Transform oneShotRoot = FindOneShotVfxRoot(trail.transform, transform);
                if (oneShotRoot == null ||
                    oneShotRoot == transform ||
                    (mainVisualChild != null && oneShotRoot == mainVisualChild.transform) ||
                    !stoppedRoots.Add(oneShotRoot))
                    continue;

                trail.Clear();
                oneShotRoot.gameObject.SetActive(false);
                stopped++;
            }

            if (stopped > 0 && ProjectileDebugEnabled())
            {
                Debug.Log($"[PROJ_VFX] Owned one-shot VFX stopped root={name} count={stopped}", this);
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Projectile,
                    "ProjectileOwnedOneShotVfxStopped",
                    $"projectile={name} count={stopped}",
                    this);
            }
        }

        private static bool IsLikelyOneShotVfx(Transform child, Transform stopAt)
        {
            for (Transform cursor = child; cursor != null && cursor != stopAt; cursor = cursor.parent)
            {
                if (IsOneShotVfxName(cursor.name))
                    return true;
            }

            return false;
        }

        private static Transform FindOneShotVfxRoot(Transform child, Transform stopAt)
        {
            Transform match = null;
            for (Transform cursor = child; cursor != null && cursor != stopAt; cursor = cursor.parent)
            {
                if (IsOneShotVfxName(cursor.name))
                    match = cursor;
            }

            return match;
        }

        private static bool IsOneShotVfxName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return IsMuzzleVfxName(name)
                || ContainsIgnoreCase(name, "impact")
                || ContainsIgnoreCase(name, "detonation")
                || ContainsIgnoreCase(name, "explosion")
                || ContainsIgnoreCase(name, "explode")
                || IsHitVfxName(name)
                || ContainsIgnoreCase(name, "blood");
        }

        private static bool IsMuzzleVfxName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string lower = name.ToLowerInvariant();
            return ContainsNameToken(lower, "muzzle")
                || lower.Contains("gunfire")
                || lower.Contains("gun_fire")
                || ContainsNameToken(lower, "flash");
        }

        private static bool IsHitVfxName(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower == "hit"
                || lower == "hitfx"
                || lower == "hitvfx"
                || lower.StartsWith("hit_")
                || lower.StartsWith("hit-")
                || lower.StartsWith("[hit")
                || lower.EndsWith("_hit")
                || lower.EndsWith("-hit")
                || lower.Contains("hitfx")
                || lower.Contains("hitvfx")
                || lower.Contains("hit_fx")
                || lower.Contains("hit_vfx")
                || ContainsNameToken(lower, "hit");
        }

        private static bool ContainsNameToken(string lowerValue, string token)
        {
            if (string.IsNullOrEmpty(lowerValue) || string.IsNullOrEmpty(token))
                return false;

            int index = lowerValue.IndexOf(token, System.StringComparison.Ordinal);
            while (index >= 0)
            {
                int before = index - 1;
                int after = index + token.Length;
                bool beforeBoundary = before < 0 || !char.IsLetterOrDigit(lowerValue[before]);
                bool afterBoundary = after >= lowerValue.Length || !char.IsLetterOrDigit(lowerValue[after]);
                if (beforeBoundary && afterBoundary)
                    return true;

                index = lowerValue.IndexOf(token, index + 1, System.StringComparison.Ordinal);
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ProjectileDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && (cfg.EnableProjectileDebugLogs || PhaseTestLog.IsEnabled(PhaseTestLogCategory.Projectile));
        }

        private void ResetDetachedVfx()
        {
            GameObject muzzleRoot = GetVfxRoot(muzzleFlashSystem, muzzleFlashChild);
            if (muzzleRoot != null && _muzzleOriginalParent != null && CanRestoreOwnedVfx(_muzzleOriginalParent))
                RestoreOriginalTransform(muzzleRoot, _muzzleOriginalParent,
                    _muzzleOriginalLocalPosition, _muzzleOriginalLocalRotation, _muzzleOriginalLocalScale);
            else if (muzzleRoot == null || !IsOwnedVfxChild(muzzleRoot))
                ClearMuzzleTransformCache();

            GameObject detonationRoot = GetVfxRoot(detonationVFXSystem, detonationVFXChild);
            if (detonationRoot != null && _detonationOriginalParent != null && CanRestoreOwnedVfx(_detonationOriginalParent))
                RestoreOriginalTransform(detonationRoot, _detonationOriginalParent,
                    _detonationOriginalLocalPosition, _detonationOriginalLocalRotation, _detonationOriginalLocalScale);
            else if (detonationRoot == null || !IsOwnedVfxChild(detonationRoot))
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
            {
                detonationVFXChild = FindChildGameObject(
                    "[DetonationVFX]",
                    "DetonationVFX",
                    "[ImpactVFX]",
                    "ImpactVFX",
                    "FX_Hit 1",
                    "FX_Hit",
                    "FX_Impact",
                    "FX_ExplosionRoundFire",
                    "Hit",
                    "Impact",
                    "Explosion");
            }

            if (detonationVFXChild == null)
                detonationVFXChild = FindFirstOneShotChild(excludeMuzzle: true);

            if (muzzleFlashSystem != null && !IsOwnedOrDetachedVfxChild(muzzleFlashSystem.gameObject, _muzzleOriginalParent))
            {
                WarnExternalVfxReference(nameof(muzzleFlashSystem), muzzleFlashSystem.gameObject);
                muzzleFlashSystem = null;
            }

            if (detonationVFXSystem != null && !IsOwnedOrDetachedVfxChild(detonationVFXSystem.gameObject, _detonationOriginalParent))
            {
                WarnExternalVfxReference(nameof(detonationVFXSystem), detonationVFXSystem.gameObject);
                detonationVFXSystem = null;
            }

            if (muzzleFlashSystem == null)
                muzzleFlashSystem = FindParticleSystem(muzzleFlashChild);

            if (detonationVFXSystem == null)
                detonationVFXSystem = FindParticleSystem(detonationVFXChild);

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

        private GameObject FindFirstOneShotChild(bool excludeMuzzle)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform)
                    continue;

                if (mainVisualChild != null &&
                    (child.gameObject == mainVisualChild || child.IsChildOf(mainVisualChild.transform)))
                    continue;

                if (excludeMuzzle && IsMuzzleVfxName(child.name))
                    continue;

                if (IsOneShotVfxName(child.name))
                    return child.gameObject;
            }

            return null;
        }

        private static ParticleSystem FindParticleSystem(GameObject root)
        {
            return root != null ? root.GetComponentInChildren<ParticleSystem>(true) : null;
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
