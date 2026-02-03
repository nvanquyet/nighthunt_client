using System.Collections.Generic;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Interaction;
using NightHunt.Inventory.Attachment;

namespace NightHunt.Inventory.WorldDrop
{
    /// <summary>
    /// Spawns items in the world with physics-based drops.
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
        
        public void DropItem(ItemInstance item, Vector3 playerPosition, Vector3 playerForward)
        {
            Vector3 dropPos = playerPosition + dropForwardOffset;
            
            // Spawn with random rotation
            Quaternion randomRotation = Random.rotation;
            var worldItem = Instantiate(worldItemPrefab, dropPos, randomRotation);
            
            // Initialize with full item state
            var worldItemComponent = worldItem.GetComponent<WorldItem>();
            worldItemComponent.Initialize(item);
            
            // Apply physics force
            var rb = worldItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Forward force (random magnitude)
                float randomForce = Random.Range(dropForceRange.x, dropForceRange.y);
                Vector3 forceDir = playerForward * randomForce + Vector3.up * upwardForce;
                rb.AddForce(forceDir, ForceMode.Impulse);
                
                // Random torque for rolling effect
                Vector3 randomTorque = Random.insideUnitSphere * torqueMultiplier;
                rb.AddTorque(randomTorque, ForceMode.Impulse);
            }
        }
        
        /// <summary>
        /// Drop weapon with attachments (each spawns separately).
        /// </summary>
        public void DropWeaponWithAttachments(ItemInstance weapon, Vector3 pos, Vector3 forward)
        {
            // Drop weapon itself
            DropItem(weapon, pos, forward);
            
            // Drop each attachment separately (with random offset to avoid overlap)
            foreach (var attachment in weapon.AttachedItems)
            {
                // Random offset and direction
                Vector3 randomOffset = Random.insideUnitSphere * 0.5f;
                Vector3 randomDir = (forward + randomOffset).normalized;
                
                DropItem(attachment, pos + randomOffset, randomDir);
            }
            
            // Clear attachments from weapon instance
            weapon.AttachedItems.Clear();
        }
    }
}
