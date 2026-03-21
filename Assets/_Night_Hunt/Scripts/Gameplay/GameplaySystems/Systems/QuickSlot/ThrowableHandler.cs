using NightHunt.GameplaySystems.Core.Data;
using UnityEngine;
using NightHunt.Utilities;
using FishNet;

namespace NightHunt.GameplaySystems.QuickSlot
{
    /// <summary>
    /// Handler for throwable projectiles (grenades, molotovs, etc.)
    /// Separated from main ItemUseSystem for better organization.
    /// </summary>
    public class ThrowableHandler : MonoBehaviour
    {
        private Transform _playerTransform;

        // Cached per-owner collider list — populated once on Initialize and reused.
        // Avoids GetComponentsInChildren GC allocation on every throw.
        private Collider[] _ownerColliders;

        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            // Cache all colliders on the owner hierarchy once.
            // Call again from the outside if the character's colliders ever change at runtime.
            if (playerTransform != null)
                _ownerColliders = playerTransform.root.GetComponentsInChildren<Collider>();
        }

        /// <summary>
        /// Spawn a throwable projectile aimed toward <paramref name="aimWorldTarget"/>.
        /// Velocity is calculated via the ballistic arc formula so the projectile lands exactly
        /// at the aim target, capped by the effective throw range.
        /// </summary>
        public void SpawnProjectile(ThrowableDefinition def, Transform spawnOrigin, Vector3 aimWorldTarget)
        {
            if (def.ProjectilePrefab == null)
            {
                Debug.LogError($"[ThrowableHandler] '{def.DisplayName}' has no ProjectilePrefab!");
                return;
            }

            Vector3 pos = spawnOrigin.position + spawnOrigin.forward * 0.5f + Vector3.up * 1.5f;
            Vector3 toTarget     = aimWorldTarget - pos;
            Vector3 horizToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            Vector3 horizDir     = horizToTarget.sqrMagnitude > 0.001f
                                   ? horizToTarget.normalized
                                   : spawnOrigin.forward;

            Vector3 velocity = CalculateBallisticVelocity(pos, aimWorldTarget, def.LaunchAngleDeg);
            if (velocity == Vector3.zero)
            {
                // Target directly overhead / unreachable at this angle — arc forward at full force.
                float angleRad = def.LaunchAngleDeg * Mathf.Deg2Rad;
                velocity = (horizDir * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad)) * def.ThrowForce;
            }

            var go = Instantiate(def.ProjectilePrefab, pos, Quaternion.LookRotation(velocity.normalized));

            // Ignore collision between the spawned projectile and the owner.
            // FishNet calls Physics.Simulate synchronously on the same tick as Spawn(),
            // so without this the grenade would immediately hit the thrower.
            var projColliders = go.GetComponentsInChildren<Collider>(includeInactive: true);
            if (_ownerColliders == null)
                _ownerColliders = spawnOrigin.root.GetComponentsInChildren<Collider>();
            foreach (var oc in _ownerColliders)
                foreach (var pc in projColliders)
                    Physics.IgnoreCollision(oc, pc, ignore: true);

            // Network-spawn the projectile so all clients see it.
            var nob = go.GetComponent<FishNet.Object.NetworkObject>();
            if (nob != null && InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.Spawn(go);

            // Set velocity AFTER spawn so FishNet's first simulate tick uses the correct value.
            var rb = go.GetComponent<Rigidbody>()
                      ?? go.GetComponentInChildren<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = velocity;

            var proj = go.GetComponent<ProjectileNetworked>()
                       ?? go.GetComponentInChildren<ProjectileNetworked>();
            if (proj != null)
                proj.Initialize(def);
        }

        /// <summary>
        /// Calculate the initial velocity vector for a ballistic arc that lands at <paramref name="target"/>.
        /// Uses the projectile motion equation:
        ///   v = sqrt( g * dx² / (2 * cos²(θ) * (dx*tan(θ) - dy)) )
        /// Returns Vector3.zero when the target is unreachable with the given angle
        /// (e.g. the target is above the effective arc ceiling, or horizontal distance is negligible).
        /// </summary>
        private static Vector3 CalculateBallisticVelocity(Vector3 from, Vector3 to, float launchAngleDeg)
        {
            float g = -Physics.gravity.y; // positive (~9.81)

            Vector3 toTarget = to - from;
            Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
            float dx = toTargetXZ.magnitude;
            float dy = toTarget.y;

            if (dx < 0.01f)
                return Vector3.zero; // directly overhead / underfoot — no horizontal arc possible

            float angleRad = launchAngleDeg * Mathf.Deg2Rad;
            float cosA = Mathf.Cos(angleRad);
            float tanA = Mathf.Tan(angleRad);

            float denom = dx * tanA - dy;
            if (denom <= 0f)
                return Vector3.zero; // target is above the apex of this angle — unreachable

            float vMag = Mathf.Sqrt(0.5f * g * dx * dx / (cosA * cosA * denom));

            Vector3 horizDir = toTargetXZ.normalized;
            return horizDir * (vMag * cosA) + Vector3.up * (vMag * Mathf.Sin(angleRad));
        }
    }
}