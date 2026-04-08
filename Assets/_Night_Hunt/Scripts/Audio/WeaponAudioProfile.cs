using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Audio
{
    /// <summary>
    /// Per-weapon-class audio profile ScriptableObject.
    ///
    /// CREATE: Right-click Project → NightHunt/Audio/Weapon Audio Profile
    /// NAMING CONVENTION:
    ///   WAP_Pistol.asset, WAP_Rifle.asset, WAP_SMG.asset, WAP_Sniper.asset,
    ///   WAP_Shotgun.asset, WAP_Melee.asset, WAP_Launcher.asset
    ///
    /// SETUP:
    ///   One profile per WeaponClass. WeaponAudioController selects the matching profile
    ///   when OnActiveWeaponChanged fires.
    ///
    /// OPTIONAL PER-WEAPON OVERRIDE:
    ///   For weapons that need unique sounds beyond the class default (e.g., a specific sniper),
    ///   add a WeaponAudioProfileOverride field to WeaponDefinition (future extension).
    /// </summary>
    [CreateAssetMenu(fileName = "WAP_", menuName = "NightHunt/Audio/Weapon Audio Profile")]
    public class WeaponAudioProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which WeaponClass this profile applies to. WeaponAudioController matches by this.")]
        public WeaponClass weaponClass = WeaponClass.Rifle;

        [Header("Fire Sounds (3D)")]
        [Tooltip("Gunshot variants. At least 1 required. Multiple = random per shot for variety.")]
        public AudioClip[] fireClips;

        [Tooltip("Suppressed fire variant (if weapon uses suppressor attachment). Leave null if unsupported.")]
        public AudioClip[] fireSuppressedClips;

        [Tooltip("Volume scale for fire sound (0–1). Pistol = 0.7, Rifle = 1.0, Sniper = 1.2 (louder, lower pitch).")]
        [Range(0.3f, 1.5f)]
        public float fireVolume = 1f;

        [Tooltip("Pitch base for fire sound. Snipers/beefier guns: 0.85. SMG/pistol: 1.05.")]
        [Range(0.7f, 1.3f)]
        public float firePitch = 1f;

        [Tooltip("Random pitch variance ± applied per shot. Adds subtle realism.")]
        [Range(0f, 0.08f)]
        public float firePitchVariance = 0.03f;

        [Header("Reload Sounds (3D)")]
        [Tooltip("Played at reload START (mag drop animation).")]
        public AudioClip reloadStartClip;

        [Tooltip("Played at reload END (slide/bolt slam animation event or reload complete).")]
        public AudioClip reloadEndClip;

        [Tooltip("Optional: tactical reload clip (reload with rounds remaining — shorter animation).")]
        public AudioClip reloadTacticalClip;

        [Header("Equip/Draw Sounds (3D)")]
        [Tooltip("Played when switching TO this weapon (draw).")]
        public AudioClip drawClip;

        [Tooltip("Played when holstering this weapon.")]
        public AudioClip holsterClip;

        [Header("Empty/Depleted Sound (3D)")]
        [Tooltip("Metallic click when magazine is fully empty and player tries to fire.")]
        public AudioClip emptyClip;

        [Header("Impact / Bullet Whiz (3D)")]
        [Tooltip("Override bullet impact for this weapon class. Leave null to use AudioLibrary.bulletImpact.")]
        public AudioClip bulletImpactOverride;

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Get a random fire clip. Uses suppressed set if suppressed=true and set is non-empty.
        /// Returns null if no clips assigned.
        /// </summary>
        public AudioClip GetFireClip(bool suppressed = false)
        {
            AudioClip[] set = (suppressed && fireSuppressedClips is { Length: > 0 })
                ? fireSuppressedClips
                : fireClips;

            if (set == null || set.Length == 0) return null;
            return set[Random.Range(0, set.Length)];
        }

        /// <summary>Pitch with random variance applied.</summary>
        public float GetFirePitch()
            => firePitch + Random.Range(-firePitchVariance, firePitchVariance);
    }
}
