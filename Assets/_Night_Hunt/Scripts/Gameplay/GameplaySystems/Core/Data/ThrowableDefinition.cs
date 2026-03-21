using UnityEngine;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Throwable item definition (grenades, molotovs, flashbangs …)
    /// NEW FILE – drop into Core/Data alongside ConsumableDefinition.
    /// </summary>
    [CreateAssetMenu(fileName = "Throwable_", menuName = "NightHunt/Items/Throwable Definition")]
    public class ThrowableDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Throwable;

        [Header("Stat Configuration")]
        [Tooltip("Optional stats-only config (typically Weight).")]
        public ThrowableStatConfig StatConfig;

        [Header("Throw Settings")]
        [Tooltip("Prefab spawned at throw point. Must have Rigidbody + Projectile component.")]
        public GameObject ProjectilePrefab;

        [Tooltip("Launch force applied to the Rigidbody (m/s). Acts as the player's max arm strength — " +
                 "ballistic velocity is computed per-throw to hit aim target; this caps it.")]
        [Min(1f)] public float ThrowForce = 15f;

        [Tooltip("Time the player holds the item before they can throw (prepare animation window).")]
        [Min(0f)] public float PrepareTime = 0.5f;

        [Tooltip("Launch angle in degrees above horizontal for the ballistic arc (10–80°). " +
                 "Higher values = steeper lob, shorter range. 45° = maximum range.")]
        [Range(10f, 80f)] public float LaunchAngleDeg = 45f;

        [Header("Projectile Behaviour")]
        [Tooltip("Governs what the projectile does on impact / detonation.")]
        public ThrowableType ThrowableType = ThrowableType.Grenade;

        [Tooltip("Base damage at explosion centre.")]
        [Min(0f)] public float Damage = 100f;

        [Tooltip("Radius for AoE damage / effect falloff (0 = no AoE).")]
        [Min(0f)] public float ExplosionRadius = 5f;

        [Tooltip("Seconds after spawn before automatic detonation. 0 = impact-only.\n" +
                 "NOTE: Must be > 0 for Grenade/Timed/Flashbang/Smoke/Gas/Incendiary types.")]
        [Min(0f)] public float FuseTime = 3f;

        [Tooltip("(Proximity type only) Enemy detection radius — triggers detonation when any IDamageable enters.\n" +
                 "0 = auto: uses 50% of ExplosionRadius.")]
        [Min(0f)] public float ProximityDetectionRadius = 0f;

        [Tooltip("Allow projectile to bounce off surfaces (physic material ignored if false).")]
        public bool CanBounce = true;

        [Header("FX")]
        [Tooltip("Sound played the moment the player releases the item.")]
        public AudioClip ThrowSound;

        [Tooltip("Sound played on impact or explosion.")]
        public AudioClip ImpactSound;
        
        /// <summary>
        /// Calculate the maximum horizontal throw distance on flat ground for this item
        /// using the projectile motion formula: d = v² × sin(2θ) / g.
        /// This is used by <see cref="QuickSlotAimController"/> to clamp how far the
        /// player can aim — keeps aim radius in sync with actual physics reach.
        /// </summary>
        public float GetMaxThrowDistance()
        {
            float g        = -Physics.gravity.y;                 // ~9.81
            float angleRad = LaunchAngleDeg * Mathf.Deg2Rad;
            return (ThrowForce * ThrowForce * Mathf.Sin(2f * angleRad)) / g;
        }

        /// <summary>
        /// Get stat value from StatConfig
        /// </summary>
        public float GetStatValue(ItemStatType statType)
        {
            return StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;
        }
        
        /// <summary>
        /// Check if item has specific stat
        /// </summary>
        public bool HasStat(ItemStatType statType)
        {
            return StatConfig != null && StatConfig.HasStat(statType);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Types that need a fuse timer — warn loudly if FuseTime was left at 0.
            bool needsFuse = ThrowableType != ThrowableType.Impact
                          && ThrowableType != ThrowableType.Proximity
                          && ThrowableType != ThrowableType.Sticky;

            if (needsFuse && FuseTime <= 0f)
            {
                Debug.LogWarning(
                    $"[ThrowableDefinition] '{name}': ThrowableType={ThrowableType} but FuseTime=0 " +
                    "— projectile will NEVER detonate! Set FuseTime > 0.", this);
            }
        }
#endif
    }

    /// <summary>
    /// Throwable behaviour categories.
    /// ⚠ Never reorder or renumber existing entries – only append new ones.
    /// </summary>
    public enum ThrowableType
    {
        Grenade     = 0,   // Timed AoE explosion
        Flashbang   = 1,   // Blind / stun
        Smoke       = 2,   // Smoke screen
        Incendiary  = 3,   // Fire – damage over time
        Gas         = 4,   // Poison cloud
        Sticky      = 5,   // Attaches to surface before detonating
        Impact      = 6,   // Detonates on first collision
        Timed       = 7,   // Explicit timer alias
        Proximity   = 8,   // Detonates when enemy enters radius
    }
}