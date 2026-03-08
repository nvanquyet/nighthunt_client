using NightHunt.GameplaySystems.Core.Data;
using UnityEngine;

namespace NightHunt.GameplaySystems.QuickSlot
{
    
    /// <summary>
    /// Handler for throwable projectiles (grenades, molotovs, etc.)
    /// Separated from main ItemUseSystem for better organization
    /// </summary>
    public class ThrowableHandler : MonoBehaviour
    {
        private Transform _playerTransform;
        
        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;
        }
        
        /// <summary>
        /// Spawn a throwable projectile aimed toward <paramref name="aimWorldTarget"/>.
        /// Velocity is calculated to match the distance (capped by <see cref="ThrowableDefinition.ThrowForce"/>),
        /// replicating the same pattern as PrCharacterInventory.ThrowGrenadeMaxForce in the PR reference.
        /// </summary>
        public void SpawnProjectile(ThrowableDefinition def, Transform spawnOrigin, Vector3 aimWorldTarget)
        {
            if (def.ProjectilePrefab == null)
            {
                Debug.LogError($"[ThrowableHandler] '{def.DisplayName}' has no ProjectilePrefab!");
                return;
            }

            Vector3 pos      = spawnOrigin.position + spawnOrigin.forward * 0.5f + Vector3.up * 1.5f;
            Vector3 toTarget = aimWorldTarget - pos;
            Vector3 dir      = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : spawnOrigin.forward;
            float   dist     = toTarget.magnitude;
            // Scale force with distance, capped at ThrowForce (mirrors PR's ThrowGrenadeMaxForce).
            float   force    = Mathf.Min(dist * 17f, def.ThrowForce);

            var go = Instantiate(def.ProjectilePrefab, pos, Quaternion.LookRotation(dir));

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = dir * force;

            var proj = go.GetComponent<ProjectileNetworked>();
            if (proj != null)
                proj.Initialize(def);
        }
    }
}