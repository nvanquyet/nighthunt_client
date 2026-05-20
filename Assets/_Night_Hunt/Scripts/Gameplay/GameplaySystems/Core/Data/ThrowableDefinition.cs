using UnityEngine;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Definition asset for throwable items (grenades, molotovs, flashbangs …).
    ///
    /// UsageDuration (from UsableItemDefinition) maps to the prepare / cook time —
    /// the window during which the player holds the throwable before releasing it.
    /// CanCancelUsage lets the player cancel the throw and re-holster if true.
    ///
    /// The projectile's flight and detonation VFX are managed by the prefab's
    /// ProjectileComponent (which extends ProjectileBase).
    /// </summary>
    [CreateAssetMenu(fileName = "Throwable_", menuName = "NightHunt/Items/Throwable Definition")]
    public class ThrowableDefinition : UsableItemDefinition
    {
        public override ItemType Type => ItemType.Throwable;

        [Header("Stat Configuration")]
        [Tooltip("Optional stats-only config (used for Weight and any custom stats).")]
        public ThrowableStatConfig StatConfig;

        [Header("Throw Physics")]
        [Tooltip("Prefab spawned at the throw origin. Must have a Rigidbody and a ProjectileComponent. " +
                 "Use Projectile_Physics_Template as a starting point " +
                 "(NightHunt/Tools/Build Template Prefabs). " +
                 "Set lifetimeAfterImpact ≥ explosion ParticleSystem duration (2–4s). " +
                 "For grenades: gravityScale=1.5, projectileSpeed=15–25. " +
                 "For molotovs / flashbangs: adjust DetonationVFX particle system accordingly.")]
        public GameObject ProjectilePrefab;

        [Tooltip("Launch speed in m/s. The actual ballistic velocity is solved per-throw so the arc " +
                 "reaches the aim target; this value caps the maximum arm strength.")]
        [Min(1f)] public float ThrowForce = 15f;

        [Tooltip("Upward launch angle in degrees above the horizontal (10–80°). " +
                 "45° gives maximum flat-ground range.")]
        [Range(10f, 80f)] public float LaunchAngleDeg = 45f;

        [Header("Detonation")]
        [Tooltip("Governs how and when the projectile detonates.")]
        public ThrowableType ThrowableType = ThrowableType.Grenade;

        [Tooltip("Base damage at the explosion centre.")]
        [Min(0f)] public float Damage = 100f;

        [Tooltip("AoE radius for damage and effect falloff. 0 = no area effect.")]
        [Min(0f)] public float ExplosionRadius = 5f;

        [Tooltip("Seconds after spawn before the projectile automatically detonates (fuse). " +
                 "0 = impact-only detonation.")]
        [Min(0f)] public float FuseTime = 3f;

        [Tooltip("(Proximity type only) Trigger radius for an approaching enemy. " +
                 "0 = auto-set to 50% of ExplosionRadius.")]
        [Min(0f)] public float ProximityDetectionRadius = 0f;

        [Tooltip("Allow the projectile to bounce off surfaces rather than detonating on first contact.")]
        public bool CanBounce = true;

        [Header("Audio")]
        [Tooltip("Played the moment the player releases the item.")]
        public AudioClip ThrowSound;

        [Tooltip("Played on impact or detonation.")]
        public AudioClip ImpactSound;

        [Header("Area Effects")]
        [Tooltip("Off = affect enemies only. On = thrower/team/any player can trigger or be damaged.")]
        public bool AllowFriendlyFire = false;

        [Tooltip("Persistent area duration after detonation. 0 = only instant damage.")]
        [Min(0f)] public float AreaEffectDuration = 0f;

        [Tooltip("How often persistent area effects tick while active.")]
        [Min(0.05f)] public float AreaEffectTickInterval = 0.25f;

        [Tooltip("Stat modifiers applied while a player stands inside the persistent area.")]
        public PlayerStatModifier[] AreaPlayerModifiers;

        [Tooltip("Health delta per second while inside the persistent area. Negative damages, positive heals.")]
        public float AreaHealthDeltaPerSecond = 0f;

        [Tooltip("Stamina delta per second while inside the persistent area. Negative drains, positive restores.")]
        public float AreaStaminaDeltaPerSecond = 0f;

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Alias for UsageDuration — seconds of wind-up before the throw executes.</summary>
        public float PrepareTime { get => UsageDuration; set => UsageDuration = value; }

        /// <summary>
        /// Maximum horizontal throw distance on flat ground using projectile-motion formula
        /// d = v² × sin(2θ) / g. Used to clamp the aim-indicator radius.
        /// </summary>
        public float GetMaxThrowDistance()
        {
            float g = -Physics.gravity.y;
            float a = LaunchAngleDeg * Mathf.Deg2Rad;
            return ThrowForce * ThrowForce * Mathf.Sin(2f * a) / g;
        }

        public float GetStatValue(ItemStatType statType)
            => StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;

        public bool HasStat(ItemStatType statType)
            => StatConfig != null && StatConfig.HasStat(statType);

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            bool needsFuse = ThrowableType != ThrowableType.Impact
                          && ThrowableType != ThrowableType.Proximity
                          && ThrowableType != ThrowableType.Sticky;

            if (needsFuse && FuseTime <= 0f)
                Debug.LogWarning($"[ThrowableDefinition] '{name}': ThrowableType={ThrowableType} " +
                                 "but FuseTime=0 — projectile will never detonate! Set FuseTime > 0.", this);
        }
#endif
    }

    /// <summary>
    /// Throwable behaviour categories. Never reorder — values are serialised.
    /// </summary>
    public enum ThrowableType
    {
        Grenade    = 0,  // Timed AoE explosion
        Flashbang  = 1,  // Blind / stun
        Smoke      = 2,  // Visibility screen
        Incendiary = 3,  // Fire — damage over time
        Gas        = 4,  // Poison cloud
        Sticky     = 5,  // Attaches to surface then detonates
        Impact     = 6,  // Detonates on first collision
        Timed      = 7,  // Explicit countdown alias
        Proximity  = 8,  // Detonates when an enemy enters the detection radius
    }
}
