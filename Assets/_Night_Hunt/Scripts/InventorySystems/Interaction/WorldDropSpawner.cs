using UnityEngine;
using NightHunt.Inventory.Core.Data;
using System.Collections.Generic;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// Spawns items in the world with physics-based dropping.
    /// </summary>
    public class WorldDropSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject worldItemPrefab;
        
        [Header("Drop Settings")]
        [SerializeField] private Vector3 dropForwardOffset = Vector3.forward * 1f;
        [SerializeField] private Vector2 dropForceRange = new Vector2(2f, 5f);
        [SerializeField] private float upwardForce = 2f;
        [SerializeField] private float torqueMultiplier = 5f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        #region Public API
        
        /// <summary>
        /// Drops an item with physics force.
        /// </summary>
        public void DropItem(ItemInstance item, Vector3 playerPosition, Vector3 playerForward)
        {
            if (item == null)
            {
                Debug.LogError("[WorldDropSpawner] Cannot drop null item");
                return;
            }
            
            if (worldItemPrefab == null)
            {
                Debug.LogError("[WorldDropSpawner] World item prefab not assigned!");
                return;
            }
            
            Vector3 dropPos = playerPosition + dropForwardOffset;
            
            // Spawn with random rotation
            Quaternion randomRotation = Random.rotation;
            var worldItemObj = Instantiate(worldItemPrefab, dropPos, randomRotation);
            
            // Initialize with item data
            var worldItemComponent = worldItemObj.GetComponent<WorldItem>();
            if (worldItemComponent != null)
            {
                worldItemComponent.Initialize(item);
            }
            else
            {
                Debug.LogError("[WorldDropSpawner] WorldItem component not found on prefab!");
            }
            
            // Apply physics force
            var rb = worldItemObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                ApplyDropForce(rb, playerForward);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[WorldDropSpawner] Dropped {item.Definition.ItemId}");
            }
        }
        
        /// <summary>
        /// Drops a weapon with all its attachments (each spawns separately).
        /// </summary>
        public void DropWeaponWithAttachments(ItemInstance weapon, Vector3 pos, Vector3 forward)
        {
            if (weapon == null) return;
            
            // Drop weapon itself
            DropItem(weapon, pos, forward);
            
            // Drop each attachment separately with random offset
            foreach (var attachment in weapon.AttachedItems)
            {
                // Random offset to avoid overlap
                Vector3 randomOffset = Random.insideUnitSphere * 0.5f;
                randomOffset.y = Mathf.Abs(randomOffset.y); // Keep above ground
                
                Vector3 randomDir = (forward + randomOffset).normalized;
                
                DropItem(attachment, pos + randomOffset, randomDir);
            }
            
            // Clear attachments from weapon instance
            weapon.AttachedItems.Clear();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[WorldDropSpawner] Dropped weapon with attachments");
            }
        }
        
        /// <summary>
        /// Drops multiple items (e.g., from container).
        /// </summary>
        public void DropItems(List<ItemInstance> items, Vector3 centerPosition, float spreadRadius = 1f)
        {
            if (items == null || items.Count == 0) return;
            
            foreach (var item in items)
            {
                // Random position within radius
                Vector2 randomCircle = Random.insideUnitCircle * spreadRadius;
                Vector3 offset = new Vector3(randomCircle.x, 0f, randomCircle.y);
                Vector3 dropPos = centerPosition + offset;
                
                // Random direction
                Vector3 randomDir = new Vector3(randomCircle.x, 0f, randomCircle.y).normalized;
                if (randomDir == Vector3.zero) randomDir = Vector3.forward;
                
                DropItem(item, dropPos, randomDir);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[WorldDropSpawner] Dropped {items.Count} items");
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void ApplyDropForce(Rigidbody rb, Vector3 direction)
        {
            // Forward force (random magnitude)
            float randomForce = Random.Range(dropForceRange.x, dropForceRange.y);
            Vector3 forceDir = direction * randomForce + Vector3.up * upwardForce;
            rb.AddForce(forceDir, ForceMode.Impulse);
            
            // Random torque for rolling effect
            Vector3 randomTorque = Random.insideUnitSphere * torqueMultiplier;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
        
        #endregion
    }
}