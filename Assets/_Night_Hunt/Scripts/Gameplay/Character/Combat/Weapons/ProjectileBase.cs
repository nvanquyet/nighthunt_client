using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Base class for all projectiles (bullets, grenades, smoke…).
    ///
    /// Prefab structure:
    ///   MuzzleFlash    — particle child, inactive by default; played once at spawn then hidden.
    ///   MainVisual     — mesh + trail, active by default; hidden on impact if hideTrailOnImpact = true.
    ///   DetonationVFX  — single particle child, inactive by default; activated on impact/explosion.
    ///
    /// VFX ownership: muzzle flash, trail, and detonation are ALL managed here — not on the weapon.
    /// The projectile is spawned at the muzzle point, so muzzle flash plays correctly at spawn.
    /// </summary>
    public class ProjectileBase : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Inspector — VFX children (assign in prefab)
        // -----------------------------------------------------------------
        [Header("VFX Children")]
        [Tooltip("Child particle for muzzle flash — inactive by default, played once on spawn.")]
        public GameObject muzzleFlashChild;

        [Tooltip("Child containing mesh/trail — active while projectile is in flight.")]
        public GameObject mainVisualChild;

        [Tooltip("Single particle child for impact/explosion/smoke — inactive by default.")]
        public GameObject detonationVFXChild;

        // -----------------------------------------------------------------
        // Inspector — Config
        // -----------------------------------------------------------------
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

        // -----------------------------------------------------------------
        // Internal reset — called in OnEnable so pool reuse works correctly
        // -----------------------------------------------------------------
        protected virtual void OnEnable()
        {
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
            if (muzzleFlashChild == null) return;
            muzzleFlashChild.SetActive(true);
            RestartParticleSystems(muzzleFlashChild);
            StartCoroutine(HideMuzzleFlashAfter(muzzleFlashDuration));
        }

        private System.Collections.IEnumerator HideMuzzleFlashAfter(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
            if (muzzleFlashChild != null)
                muzzleFlashChild.SetActive(false);
        }

        /// <summary>Restart main visual (trail/mesh). Called when spawning/reusing from pool.</summary>
        public virtual void PlayMainVisual()
        {
            if (mainVisualChild == null) return;
            mainVisualChild.SetActive(true);
            RestartParticleSystems(mainVisualChild);
        }

        /// <summary>
        /// Activate DetonationVFX at the impact position.
        /// Hides MainVisual if hideTrailOnImpact = true.
        /// No Instantiate — only toggles children.
        /// </summary>
        public virtual void TriggerDetonation(Vector3 position, Quaternion rotation)
        {
            if (hideTrailOnImpact && mainVisualChild != null)
                mainVisualChild.SetActive(false);

            if (detonationVFXChild != null)
            {
                detonationVFXChild.transform.position = position;
                detonationVFXChild.transform.rotation = rotation;
                detonationVFXChild.SetActive(true);
                RestartParticleSystems(detonationVFXChild);
            }
        }

        // -----------------------------------------------------------------
        // Helper
        // -----------------------------------------------------------------
        protected void RestartParticleSystems(GameObject root)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }
}
