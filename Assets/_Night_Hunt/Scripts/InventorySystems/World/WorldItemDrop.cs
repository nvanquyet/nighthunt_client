using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Systems;

namespace NightHunt.Inventory.World
{
    /// <summary>
    /// Component for items dropped in the world.
    /// Handles pickup interaction, despawn timer, and visual representation.
    /// NOTE: Network sync will be added in Phase 3 (FishNet integration).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldItemDrop : MonoBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private ItemInstance itemInstance;
        
        [Header("Pickup Settings")]
        [SerializeField] private float pickupRadius = 1.5f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private bool requireInteraction = false; // If true, player must press E to pickup
        
        [Header("Despawn Settings")]
        [SerializeField] private float despawnTime = 300f; // 5 minutes
        [SerializeField] private bool enableDespawn = true;
        
        [Header("Visual")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private Transform modelParent;
        
        [Header("Effects")]
        [SerializeField] private ParticleSystem pickupEffect;
        [SerializeField] private AudioClip pickupSound;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private GameObject spawnedModel;
        private float spawnTime;
        private bool isPickedUp = false;
        
        // === Lifecycle ===
        
        void Start()
        {
            spawnTime = Time.time;
            
            if (enableDespawn && despawnTime > 0)
            {
                Invoke(nameof(Despawn), despawnTime);
            }
            
            // Make trigger collider
            GetComponent<Collider>().isTrigger = true;
        }
        
        void Update()
        {
            // Rotate item for visual effect
            if (modelParent != null)
            {
                modelParent.Rotate(Vector3.up, 30f * Time.deltaTime);
            }
            
            // Check for nearby players (if auto-pickup)
            if (!requireInteraction && !isPickedUp)
            {
                CheckAutoPickup();
            }
        }
        
        // === Initialization ===
        
        /// <summary>
        /// Initialize WorldItemDrop with item instance.
        /// Called by PlayerInventoryController when dropping item.
        /// </summary>
        public void Initialize(ItemInstance item)
        {
            itemInstance = item;
            
            // Spawn visual model
            SpawnItemModel();
            
            Log($"Initialized WorldItemDrop for {item.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Spawn 3D model for item.
        /// </summary>
        private void SpawnItemModel()
        {
            if (itemInstance?.Definition?.WorldModelPrefab == null)
            {
                LogWarning("No world model prefab assigned for item");
                return;
            }
            
            // Destroy old model if exists
            if (spawnedModel != null)
            {
                Destroy(spawnedModel);
            }
            
            // Spawn new model
            Transform parent = modelParent != null ? modelParent : transform;
            spawnedModel = Instantiate(itemInstance.Definition.WorldModelPrefab, parent);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
            
            Log($"Spawned world model for {itemInstance.Definition.DisplayName}");
        }
        
        // === Pickup Logic ===
        
        /// <summary>
        /// Check for nearby players and auto-pickup if close enough.
        /// </summary>
        private void CheckAutoPickup()
        {
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, pickupRadius, playerLayer);
            
            foreach (var col in nearbyColliders)
            {
                var inventoryController = col.GetComponent<PlayerInventoryController>();
                if (inventoryController != null)
                {
                    TryPickup(inventoryController);
                    break;
                }
            }
        }
        
        /// <summary>
        /// Trigger-based pickup.
        /// </summary>
        void OnTriggerEnter(Collider other)
        {
            if (isPickedUp)
                return;
            
            // Check if player
            var inventoryController = other.GetComponent<PlayerInventoryController>();
            if (inventoryController == null)
                return;
            
            // Auto-pickup or show prompt
            if (requireInteraction)
            {
                // TODO: Show "Press E to pickup" UI
                // For now, just try pickup immediately
                TryPickup(inventoryController);
            }
            else
            {
                TryPickup(inventoryController);
            }
        }
        
        /// <summary>
        /// Attempt to pickup item.
        /// </summary>
        private void TryPickup(PlayerInventoryController inventoryController)
        {
            if (isPickedUp || itemInstance == null)
                return;
            
            // Try to add to inventory
            var result = inventoryController.PickupItem(itemInstance);
            
            if (result == Core.Enums.OperationResult.Success)
            {
                OnPickupSuccess(inventoryController);
            }
            else
            {
                OnPickupFailed(inventoryController, result);
            }
        }
        
        /// <summary>
        /// Called when pickup succeeds.
        /// </summary>
        private void OnPickupSuccess(PlayerInventoryController inventoryController)
        {
            isPickedUp = true;
            
            // Play effects
            if (pickupEffect != null)
            {
                Instantiate(pickupEffect, transform.position, Quaternion.identity);
            }
            
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }
            
            Log($"Player picked up {itemInstance.Definition.DisplayName}");
            
            // Destroy this drop
            Despawn();
        }
        
        /// <summary>
        /// Called when pickup fails.
        /// </summary>
        private void OnPickupFailed(PlayerInventoryController inventoryController, Core.Enums.OperationResult reason)
        {
            Log($"Pickup failed: {reason}");
            
            // TODO: Show UI feedback to player
            // Examples:
            // - "Inventory Full"
            // - "Too Heavy"
            // - "Cannot Carry This Item"
        }
        
        // === Despawn ===
        
        /// <summary>
        /// Despawn this item drop.
        /// </summary>
        private void Despawn()
        {
            CancelInvoke(); // Cancel despawn timer if still running
            
            Log($"Despawning WorldItemDrop for {itemInstance?.Definition.DisplayName ?? "Unknown"}");
            
            // TODO: Network despawn in Phase 3
            // For now, just destroy locally
            Destroy(gameObject);
        }
        
        // === Public API ===
        
        /// <summary>
        /// Get item instance.
        /// </summary>
        public ItemInstance GetItem()
        {
            return itemInstance;
        }
        
        /// <summary>
        /// Get time until despawn.
        /// </summary>
        public float GetTimeUntilDespawn()
        {
            if (!enableDespawn || despawnTime <= 0)
                return -1f;
            
            float elapsed = Time.time - spawnTime;
            return Mathf.Max(0f, despawnTime - elapsed);
        }
        
        /// <summary>
        /// Force despawn immediately.
        /// </summary>
        public void ForceDespawn()
        {
            Despawn();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[WorldItemDrop] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[WorldItemDrop] {message}");
        }
        
        void OnDrawGizmosSelected()
        {
            // Draw pickup radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}