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
        
        public void SpawnProjectile(ThrowableDefinition def, Transform spawnOrigin)
        {
            if (def.ProjectilePrefab == null)
            {
                Debug.LogError($"[ThrowableHandler] '{def.DisplayName}' has no ProjectilePrefab!");
                return;
            }
            
            Vector3 pos = spawnOrigin.position + spawnOrigin.forward * 0.5f + Vector3.up * 1.5f;
            Quaternion rot = Quaternion.LookRotation(spawnOrigin.forward);
            
            var go = Instantiate(def.ProjectilePrefab, pos, rot);
            
            // Apply force
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = spawnOrigin.forward * def.ThrowForce;
            
            // Initialize projectile
            var proj = go.GetComponent<ProjectileNetworked>();
            if (proj != null)
                proj.Initialize(def);
            
            Debug.Log($"[ThrowableHandler] Spawned projectile '{def.DisplayName}' (force {def.ThrowForce})");
        }
    }
}