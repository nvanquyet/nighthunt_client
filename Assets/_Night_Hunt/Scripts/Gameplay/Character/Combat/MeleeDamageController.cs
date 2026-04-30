using FishNet.Object;
using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Server-authoritative melee hit window. Animation clips call RequestMeleeHit()
    /// through CharacterAnimationController.OnAnimEventMeleeHit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeDamageController : NetworkBehaviour
    {
        [Header("Hit Query")]
        [SerializeField, Min(0.1f)] private float _range = 1.8f;
        [SerializeField, Min(0.05f)] private float _radius = 0.55f;
        [SerializeField] private Vector3 _originOffset = new Vector3(0f, 1.1f, 0.35f);
        [SerializeField] private LayerMask _hitLayers = ~0;
        [SerializeField, Min(0f)] private float _minSecondsBetweenHits = 0.18f;

        [Header("Fallback")]
        [SerializeField, Min(0f)] private float _fallbackDamage = 25f;
        [SerializeField] private string _fallbackWeaponId = "melee";

        private readonly Collider[] _hits = new Collider[16];
        private IWeaponSystem _weaponSystem;
        private float _lastHitRequestTime = -999f;

        private void Awake()
        {
            _weaponSystem = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrDefault(null)
                .Resolve();
        }

        public void RequestMeleeHit()
        {
            if (Time.time - _lastHitRequestTime < _minSecondsBetweenHits)
                return;

            _lastHitRequestTime = Time.time;

            if (IsServerStarted)
            {
                PerformMeleeHitServer();
                return;
            }

            if (IsOwner)
                PerformMeleeHitServerRpc();
        }

        [ServerRpc(RequireOwnership = true)]
        private void PerformMeleeHitServerRpc()
        {
            PerformMeleeHitServer();
        }

        [Server]
        private void PerformMeleeHitServer()
        {
            if (!TryBuildDamage(out var damage, out var weaponId))
                return;

            Vector3 origin = transform.TransformPoint(_originOffset);
            Vector3 center = origin + transform.forward * (_range * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(
                center,
                _radius,
                _hits,
                _hitLayers,
                QueryTriggerInteraction.Collide);

            Collider bestCollider = null;
            PlayerHitboxMarker bestHitbox = null;
            IHittable bestHittable = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _hits[i];
                if (col == null || IsSelfCollider(col.transform))
                    continue;

                Vector3 closest = col.ClosestPoint(origin);
                Vector3 toTarget = closest - origin;
                float forward = Vector3.Dot(transform.forward, toTarget);
                if (forward < 0f || forward > _range)
                    continue;

                var hitbox = ComponentResolver.Find<PlayerHitboxMarker>(col)
                    .OnSelf()
                    .InParent()
                    .Resolve();

                var hittable = hitbox != null && hitbox.HealthSystem != null
                    ? hitbox.HealthSystem
                    : col.GetComponentInParent<IHittable>();

                if (hittable == null)
                    continue;

                float lateral = Vector3.ProjectOnPlane(toTarget, transform.forward).sqrMagnitude;
                float score = forward + lateral;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestCollider = col;
                bestHitbox = hitbox;
                bestHittable = hittable;
            }

            if (bestCollider == null || bestHittable == null)
                return;

            Vector3 hitPoint = bestCollider.ClosestPoint(origin);
            Vector3 normal = (origin - hitPoint).sqrMagnitude > 0.001f
                ? (origin - hitPoint).normalized
                : -transform.forward;

            var info = new DamageInfo
            {
                Damage = damage,
                IsHeadshot = false,
                HitPoint = hitPoint,
                HitNormal = normal,
                ShooterNetworkObjectId = (int)ObjectId,
                WeaponId = weaponId,
            };

            if (bestHitbox != null && bestHitbox.HealthSystem != null)
                bestHitbox.HealthSystem.ApplyDamageServer(info);
            else
                bestHittable.RequestDamage(info);
        }

        private bool TryBuildDamage(out float damage, out string weaponId)
        {
            damage = _fallbackDamage;
            weaponId = _fallbackWeaponId;

            var active = _weaponSystem?.GetActiveWeapon();
            if (active == null)
                return true;

            var def = ItemDatabase.GetDefinition(active.DefinitionID) as WeaponDefinition;
            if (def != null && def.WeaponClass != WeaponClass.Melee)
                return false;

            weaponId = active.DefinitionID;
            float statDamage = active.GetComputedStat(ItemStatType.Damage);
            if (statDamage > 0f)
                damage = statDamage;

            return true;
        }

        private bool IsSelfCollider(Transform target)
        {
            return target == transform || target.IsChildOf(transform) || transform.IsChildOf(target);
        }
    }
}
