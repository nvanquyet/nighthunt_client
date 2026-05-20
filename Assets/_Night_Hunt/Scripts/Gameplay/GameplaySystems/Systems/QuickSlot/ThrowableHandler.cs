using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using UnityEngine;
using NightHunt.Utilities;
using FishNet;
using NightHunt.Diagnostics;

namespace NightHunt.GameplaySystems.ItemUse
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
            if (!InstanceFinder.IsServerStarted)
            {
                Debug.LogError($"[THROW_FLOW] SpawnProjectile blocked: server is not started. def={def?.ItemID ?? "null"}");
                return;
            }

            if (def == null)
            {
                Debug.LogError("[THROW_FLOW] SpawnProjectile blocked: throwable definition is null.");
                return;
            }

            if (spawnOrigin == null)
            {
                Debug.LogError($"[THROW_FLOW] SpawnProjectile blocked: spawn origin is null. def={def.ItemID}");
                return;
            }

            if (def.ProjectilePrefab == null)
            {
                Debug.LogError($"[ThrowableHandler] '{def.DisplayName}' has no ProjectilePrefab!");
                return;
            }

            Vector3 basePos      = spawnOrigin.position + Vector3.up * 1.5f;
            Vector3 toAimFromRoot = aimWorldTarget - spawnOrigin.position;
            Vector3 aimForward   = new Vector3(toAimFromRoot.x, 0f, toAimFromRoot.z);
            Vector3 spawnForward = aimForward.sqrMagnitude > 0.001f
                                   ? aimForward.normalized
                                   : spawnOrigin.forward;
            Vector3 pos = basePos + spawnForward * 0.75f;
            Vector3 toTarget     = aimWorldTarget - pos;
            Vector3 horizToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            float   horizDist    = horizToTarget.magnitude;
            Vector3 horizDir     = horizToTarget.sqrMagnitude > 0.001f
                                   ? horizToTarget.normalized
                                   : spawnOrigin.forward;

            Vector3 velocity = CalculateBallisticVelocity(pos, aimWorldTarget, def.LaunchAngleDeg);
            bool usedFallback = velocity == Vector3.zero;
            if (usedFallback)
            {
                // Target directly overhead / unreachable at this angle — arc forward at full force.
                float angleRad = def.LaunchAngleDeg * Mathf.Deg2Rad;
                velocity = (horizDir * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad)) * def.ThrowForce;
            }

            LogThrowable($"SpawnProjectile\n" +
                      $"  item       = {def.DisplayName} ({def.ThrowableType})\n" +
                      $"  spawnPos   = {pos:F2}   (origin={spawnOrigin.position:F2})\n" +
                      $"  aimTarget  = {aimWorldTarget:F2}\n" +
                      $"  horizDist  = {horizDist:F2} m   vertDelta = {toTarget.y:F2} m\n" +
                      $"  launchAngle= {def.LaunchAngleDeg:F1}°   usedFallback={usedFallback}\n" +
                      $"  velocity   = {velocity:F2}  speed={velocity.magnitude:F2} m/s");

            PhaseTestLog.Log(
                PhaseTestLogCategory.Throwable,
                "SpawnProjectile",
                $"def={def.ItemID} type={def.ThrowableType} spawn={pos:F2} target={aimWorldTarget:F2} horizDist={horizDist:F2} velocity={velocity:F2} speed={velocity.magnitude:F2} usedFallback={usedFallback}",
                this);

            var go = Instantiate(def.ProjectilePrefab, pos, Quaternion.LookRotation(velocity.normalized));

            // Ignore collision between the spawned projectile and the owner.
            // FishNet calls Physics.Simulate synchronously on the same tick as Spawn(),
            // so without this the grenade would immediately hit the thrower.
            var projColliders = go.GetComponentsInChildren<Collider>(includeInactive: true);
            _ownerColliders = spawnOrigin.root.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var oc in _ownerColliders)
            {
                if (oc == null) continue;
                foreach (var pc in projColliders)
                {
                    if (pc == null) continue;
                    Physics.IgnoreCollision(oc, pc, ignore: true);
                }
            }

            // Network-spawn the projectile so all clients see it.
            var nob = go.GetComponent<FishNet.Object.NetworkObject>();
            if (nob != null && InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.Spawn(go);

            // Set velocity AFTER spawn so FishNet's first simulate tick uses the correct value.
            var rb = go.GetComponent<Rigidbody>()
                      ?? go.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = velocity;
                LogThrowable($"Velocity applied — rb={rb.name}  linearVelocity={rb.linearVelocity:F2}");
            }
            else
            {
                Debug.LogWarning($"[ThrowableHandler] No Rigidbody found on '{go.name}' — projectile will not move!");
            }

            var proj = go.GetComponent<ProjectileNetworked>()
                       ?? go.GetComponentInChildren<ProjectileNetworked>();
            if (proj != null)
                proj.Initialize(def, spawnOrigin.root);

            LogThrowable($"Spawn complete — go='{go.name}'  hasRb={rb != null}  hasProj={proj != null}  networked={nob != null}");
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

        private static bool ThrowableDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableThrowableDebugLogs;
        }

        private static void LogThrowable(string message)
        {
            if (ThrowableDebugEnabled())
                Debug.Log($"[ThrowableHandler] {message}");
        }
    }
}
