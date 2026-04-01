using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Marks a game object as a potential bullet acquisition target.
    ///
    /// Any object implementing this interface can self-register with
    /// <c>BulletTargetRegistry</c> and will be considered by the firing system
    /// before falling back to a physics raycast.
    ///
    /// Typical implementors: <c>BulletTargetMarker</c> (generic MonoBehaviour),
    /// or directly on domain types (BeaconPlaceable, WorldContainer, …).
    ///
    /// IMPORTANT — registration lifecycle:
    ///   Call <c>BulletTargetRegistry.Register(this)</c>   on OnEnable  / OnStartServer.
    ///   Call <c>BulletTargetRegistry.Unregister(this)</c> on OnDisable / OnStopServer.
    /// </summary>
    public interface IBulletTarget
    {
        /// <summary>
        /// Categorisation used by <see cref="BulletTargetConfig.PriorityOrder"/> to rank
        /// candidates when more than one falls inside the acquisition cone.
        /// </summary>
        HittableTargetType TargetType { get; }

        /// <summary>
        /// The world-space point used for cone-angle and distance comparisons.
        /// Should represent the target's centre of mass (feet + <c>BulletTargetConfig.TargetCentreYOffset</c>)
        /// rather than the pivot / feet position.
        /// </summary>
        Vector3 AcquirePoint { get; }

        /// <summary>
        /// Approximate bounding radius of this target (world units).
        /// Used as a broadphase size hint — larger values make the target easier to acquire.
        /// </summary>
        float AcquireRadius { get; }

        /// <summary>
        /// The <see cref="IHittable"/> that receives damage when this target is acquired.
        /// May be null if the object is not damageable (e.g. a trigger-only zone).
        /// </summary>
        IHittable HitTarget { get; }

        /// <summary>
        /// Whether this target is currently eligible for acquisition
        /// (alive, active, not invulnerable, etc.).
        /// The registry skips candidates that return false.
        /// </summary>
        bool IsAcquirable { get; }
    }
}
