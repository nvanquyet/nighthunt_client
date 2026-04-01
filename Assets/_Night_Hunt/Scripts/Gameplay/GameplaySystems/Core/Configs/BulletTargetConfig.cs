using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.Character.Combat;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Configures the behaviour of <c>BulletTargetRegistry.FindBestTarget()</c>.
    ///
    /// CONCEPT — Target Acquisition Registry:
    ///   Instead of relying on a single physics raycast (which breaks on multi-level maps
    ///   with roofs, platforms, and targets at different Y elevations), every hittable
    ///   object registers itself with <c>BulletTargetRegistry</c>.
    ///
    ///   On fire, the registry finds candidates that are:
    ///     1. Within weapon max-range (3-D distance).
    ///     2. Within <see cref="MaxAcquireAngleDegrees"/> of the fire direction (3-D angle,
    ///        so elevation delta is handled naturally — no flat-plane assumption).
    ///     3. Optionally unobstructed by solid geometry (line-of-sight check).
    ///
    ///   Among qualifying candidates, the best target is chosen via:
    ///     1. <see cref="PriorityOrder"/> — Character > Beacon > Deployable > …
    ///     2. Tiebreaker controlled by <see cref="TiebreakerMode"/>.
    ///
    ///   If no candidate passes all filters, the caller falls back to a regular
    ///   physics raycast for environment hits (walls, floors, etc.).
    ///
    /// CREATE:  Assets → Create → NightHunt/Config/Bullet Target Config
    /// </summary>
    [CreateAssetMenu(fileName = "BulletTargetConfig",
                     menuName  = "NightHunt/Config/Bullet Target Config")]
    public class BulletTargetConfig : ScriptableObject
    {
        // ── Acquisition Cone ─────────────────────────────────────────────────

        [Header("Acquisition Cone")]
        [Tooltip("Half-angle (degrees) of the cone around the fire direction within which " +
                 "targets are considered.\n" +
                 "3-D angle — automatically handles targets above or below the shooter.\n" +
                 "Typical values: 10–25°. 0 = exact direction only.")]
        [Range(0f, 90f)]
        public float MaxAcquireAngleDegrees = 20f;

        // ── Priority ─────────────────────────────────────────────────────────

        [Header("Priority Order")]
        [Tooltip("Target types ordered from HIGHEST to LOWEST priority.\n" +
                 "When multiple candidates pass the cone filter, the type that appears " +
                 "earliest in this list wins.\n" +
                 "Drag to reorder, or add/remove entries to customise.")]
        public List<HittableTargetType> PriorityOrder = new List<HittableTargetType>
        {
            HittableTargetType.Character,
            HittableTargetType.Beacon,
            HittableTargetType.Deployable,
            HittableTargetType.WorldObject,
            HittableTargetType.Structure,
            HittableTargetType.Misc,
        };

        // ── Tiebreaker ───────────────────────────────────────────────────────

        [Header("Tiebreaker")]
        [Tooltip("When two candidates share the same priority level, how to pick the winner.")]
        public TiebreakerMode TiebreakerMode = TiebreakerMode.BestAngle;

        // ── Line-of-Sight ─────────────────────────────────────────────────────

        [Header("Line-of-Sight")]
        [Tooltip("If true, candidates blocked by solid geometry between the muzzle " +
                 "and the target's AcquirePoint are discarded.")]
        public bool RequireLineOfSight = true;

        [Tooltip("Layers treated as solid blockers for the line-of-sight check.\n" +
                 "Typically: Default + Wall + Floor. Exclude: Players, Triggers.")]
        public LayerMask LoSBlockLayers = ~0;

        [Tooltip("Use a SphereCast (radius = LoSSphereRadius) instead of a Raycast for " +
                 "the line-of-sight check. More forgiving around thin cover.")]
        public bool UseSphereCastForLoS = false;

        [Tooltip("Sphere radius used when UseSphereCastForLoS is true.")]
        [Range(0.05f, 1f)]
        public float LoSSphereRadius = 0.2f;

        // ── Acquire Point Offset ─────────────────────────────────────────────

        [Header("Acquire Point")]
        [Tooltip("World-space Y offset added to a target's transform.position when computing " +
                 "the acquire point.\n" +
                 "Set to ~half character height so the angle check aims at centre-of-mass, " +
                 "not the feet.")]
        public float TargetCentreYOffset = 1.0f;

        // ── Debug ─────────────────────────────────────────────────────────────

        [Header("Debug (Editor / Development only)")]
        [Tooltip("Draw gizmos in Scene view while playing: cone, candidates, chosen target.")]
        public bool DrawDebugGizmos = false;

        [Tooltip("Log the chosen target and reason to the console on each shot.")]
        public bool LogAcquisitionResult = false;

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the priority index of <paramref name="type"/> in <see cref="PriorityOrder"/>,
        /// or int.MaxValue if not found (treated as lowest priority).
        /// </summary>
        public int GetPriorityIndex(HittableTargetType type)
        {
            int idx = PriorityOrder.IndexOf(type);
            return idx < 0 ? int.MaxValue : idx;
        }
    }

    /// <summary>When two candidates share the same <see cref="HittableTargetType"/> priority.</summary>
    public enum TiebreakerMode
    {
        /// <summary>Prefer the candidate whose direction most closely matches the fire direction.</summary>
        BestAngle    = 0,

        /// <summary>Prefer the nearest candidate (straight-line 3-D distance).</summary>
        Closest      = 1,

        /// <summary>Prefer the candidate with the smallest combined score: angle * distance.</summary>
        BestScore    = 2,
    }
}
