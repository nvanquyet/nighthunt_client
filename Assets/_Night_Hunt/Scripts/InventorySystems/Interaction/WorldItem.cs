using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Interfaces;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// Represents an item in the world that can be picked up.
    /// Implements IInteractable for instant pickup.
    /// Items are persistent (no despawn).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldItem : MonoBehaviour, IInteractable
    {
        [Header("Visual")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private GameObject modelPrefab;
        
        [Header("Interaction")]
        [SerializeField] private Sprite pickupIcon;
        
        private ItemInstance itemData;
        private GameObject spawnedModel;
        
        #region Initialization
        
        /// <summary>
        /// Initializes the world item with item data.
        /// </summary>
        public void Initialize(ItemInstance item)
        {
            itemData = item;
            
            // Set visual (icon or 3D model)
            if (item.Definition.WorldModelPrefab != null)
            {
                // Spawn 3D model
                spawnedModel = Instantiate(item.Definition.WorldModelPrefab, transform);
                
                // Hide sprite renderer if using 3D model
                if (iconRenderer != null)
                {
                    iconRenderer.enabled = false;
                }
            }
            else if (iconRenderer != null)
            {
                // Use sprite icon
                iconRenderer.sprite = item.Definition.Icon;
            }
            
            // Set collider layer to interactable
            gameObject.layer = LayerMask.NameToLayer("Interactable");
            
            Debug.Log($"[WorldItem] Initialized: {item.Definition.ItemId}");
        }
        
        #endregion
        
        #region IInteractable Implementation
        
        public InteractionType GetInteractionType() => InteractionType.InstantPickup;
        
        public string GetInteractText()
        {
            if (itemData == null) return "Press F to pickup item";
            
            string itemName = itemData.Definition.ItemId;
            
            if (itemData.Definition.IsStackable && itemData.StackSize > 1)
            {
                return $"Press F to pickup {itemName} x{itemData.StackSize}";
            }
            
            return $"Press F to pickup {itemName}";
        }
        
        public Sprite GetInteractIcon()
        {
            // Return hand/pickup icon
            return pickupIcon;
        }
        
        public Vector3 GetPosition() => transform.position;
        
        public void OnInstantInteract(object player)
        {
            if (itemData == null)
            {
                Debug.LogWarning("[WorldItem] Cannot pickup - no item data");
                return;
            }
            
            // Try add to player inventory (via event)
            InventoryEvents.InvokeRequestAddItem(itemData);
            
            // Note: We should wait for confirmation before destroying,
            // but for simplicity we destroy immediately.
            // In production, subscribe to OnItemAdded event and check if our item was added.
            
            Debug.Log($"[WorldItem] Picked up: {itemData.Definition.ItemId}");
            
            // Remove from world
            Destroy(gameObject);
        }
        
        // Hold interaction not used for world items
        public float GetHoldDuration() => 0f;
        public void OnHoldStart(object player) { }
        public void OnHoldProgress(float progress) { }
        public void OnHoldComplete(object player) { }
        public void OnHoldCancelled() { }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Gets the item data.
        /// </summary>
        public ItemInstance GetItemData() => itemData;
        
        #endregion
    }
}