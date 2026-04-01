using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// General-purpose MonoBehaviour that makes any game object registers as a bullet-acquisition
    /// target in <c>BulletTargetRegistry</c>.
    ///
    /// USAGE:
    ///   1. Attach to the root (or any child) of the object you want to be acquirable.
    ///   2. Set <see cref="TargetType"/> and optionally override <see cref="AcquireRadius"/>.
    ///   3. If the object has a <see cref="PlayerHealthSystem"/> or any <see cref="IHittable"/>,
    ///      assign it to <see cref="_hitTargetRef"/> (or leave null for auto-resolve via parent).
    ///
    /// ALTERNATIVE:
    ///   Domain classes (e.g. BeaconPlaceable) can implement <see cref="IBulletTarget"/> directly
    ///   and call Register/Unregister themselves, avoiding this component.
    ///
    /// NOTE — character players already have <see cref="PlayerHitboxMarker"/> on each hitbox collider.
    ///   Add ONE <c>BulletTargetMarker</c> to the CHARACTER ROOT (not each hitbox). The registry
    ///   uses the marker for acquisition; the actual damage path still uses PlayerHitboxMarker.
    ///   Set <see cref="_hitTargetOverride"/> to the root PlayerHealthSystem in that case.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletTargetMarker : MonoBehaviour, IBulletTarget
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Target Classification")]
        [Tooltip("Type of this target, used to determine acquisition priority order.")]
        [SerializeField] private HittableTargetType _targetType = HittableTargetType.WorldObject;

        [Tooltip("Approximate bounding radius (world units). " +
                 "Acts as broadphase hint — larger = easier to acquire.")]
        [SerializeField] [Range(0.1f, 5f)] private float _acquireRadius = 0.5f;

        [Header("Hit Routing")]
        [Tooltip("The IHittable that receives damage on acquisition. " +
                 "If null, ComponentResolver tries InSelf → InParent → InRootChildren.")]
        [SerializeField] private MonoBehaviour _hitTargetRef;

        [Header("Acquire Point Offset")]
        [Tooltip("Local-space offset from this transform's position used as the acquire point.\n" +
                 "Leave at zero and rely on BulletTargetConfig.TargetCentreYOffset for a uniform lift,\n" +
                 "or set a per-object override here (e.g. (0, 1, 0) for a character).")]
        [SerializeField] private Vector3 _acquirePointLocalOffset = Vector3.zero;

        // ── IBulletTarget ─────────────────────────────────────────────────────

        public HittableTargetType TargetType   => _targetType;
        public float              AcquireRadius => _acquireRadius;

        public Vector3 AcquirePoint =>
            transform.position + transform.TransformDirection(_acquirePointLocalOffset);

        public bool IsAcquirable => isActiveAndEnabled;

        public IHittable HitTarget
        {
            get
            {
                if (_resolvedHitTarget != null) return _resolvedHitTarget;

                // Resolve once from inspector ref or hierarchy search.
                if (_hitTargetRef is IHittable direct)
                {
                    _resolvedHitTarget = direct;
                }
                else
                {
                    _resolvedHitTarget = ComponentResolver.Find<IHittable>(this)
                        .OnSelf()
                        .InParent()
                        .InRootChildren()
                        .OrLogWarning($"[BulletTargetMarker] IHittable not found on '{gameObject.name}'.")
                        .Resolve();
                }

                return _resolvedHitTarget;
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private IHittable _resolvedHitTarget;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            BulletTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            BulletTargetRegistry.Unregister(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(AcquirePoint, _acquireRadius);
        }
#endif
    }
}
