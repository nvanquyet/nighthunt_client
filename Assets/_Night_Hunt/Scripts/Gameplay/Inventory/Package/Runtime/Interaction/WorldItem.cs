using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Networking;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// World item that can be picked up.
    /// Implements IInteractable for instant pickup.
    /// </summary>
    public class WorldItem : MonoBehaviour, IInteractable
    {
        public ItemInstance ItemData { get; private set; }
        
        [SerializeField] private SpriteRenderer iconRenderer; // Or 3D model
        
        public void Initialize(ItemInstance item)
        {
            ItemData = item;
            
            // Set visual (icon or 3D model)
            if (iconRenderer != null)
            {
                iconRenderer.sprite = item.Definition.Icon;
            }
            
            // TODO: Spawn 3D model if available
        }
        
        #region IInteractable Implementation
        
        public InteractionType GetInteractionType() => InteractionType.InstantPickup;
        
        public string GetInteractText() => $"Press F to pickup {ItemData.Definition.ItemId}";
        
        public Sprite GetInteractIcon()
        {
            // Hand icon for pickup
            // TODO: Return from IconLibrary when available
            return null;
        }
        
        public Vector3 GetPosition() => transform.position;
        
        public void OnInstantInteract(NetworkPlayer player)
        {
            // Try add to player inventory using ComponentFinder
            var inventoryManager = ComponentFinder.FindInHierarchy<Domain.InventoryManager>(player);
            if (inventoryManager != null)
            {
                bool success = inventoryManager.TryAddItem(ItemData);
                
                if (success)
                {
                    // Remove from world
                    Destroy(gameObject);
                }
                else
                {
                    // Inventory full
                    Debug.Log("Inventory full! Cannot pickup item.");
                    // TODO: Fire UIEvents.OnShowMessage when available
                }
            }
        }
        
        // Hold interaction not used for world items
        public float GetHoldDuration() => 0f;
        public void OnHoldStart(NetworkPlayer player) { }
        public void OnHoldProgress(float progress) { }
        public void OnHoldComplete(NetworkPlayer player) { }
        public void OnHoldCancelled() { }
        
        #endregion
        
        // NO despawn - item exists until picked up
    }
}
