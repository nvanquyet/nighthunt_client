using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Container;
using NightHunt.Inventory.Domain;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Container UI controller with dual panel layout (Inventory + Container).
    /// </summary>
    public class ContainerUIController : MonoBehaviour
    {
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject containerPanel;
        
        private Container.Container currentContainer;
        
        public void OpenContainer(Container.Container container)
        {
            currentContainer = container;
            
            // Show both panels
            inventoryPanel.SetActive(true);
            containerPanel.SetActive(true);
            
            // Populate container panel
            RefreshContainerUI();
            
            // Transition input state
            // TODO: InputRouter.TransitionToState(InputState.InventoryOpen);
        }
        
        public void CloseContainer()
        {
            containerPanel.SetActive(false);
            inventoryPanel.SetActive(false);
            currentContainer = null;
            
            // TODO: InputRouter.TransitionToState(InputState.PlayerAlive);
        }
        
        // Handle drag from inventory to container
        public void OnDragToContainer(DragContext context)
        {
            if (!currentContainer.CanPutIn)
            {
                Debug.LogError("Cannot put items in this container");
                // TODO: UIEvents.OnShowError?.Invoke("Cannot put items in this container");
                return;
            }
            
            // Check weight limit
            float itemWeight = WeightCalculator.CalculateItemWeight(context.ItemInstance);
            if (currentContainer.GetCurrentWeight() + itemWeight > currentContainer.GetMaxWeight())
            {
                Debug.LogError("Container is full (weight limit)");
                // TODO: UIEvents.OnShowError?.Invoke("Container is full (weight limit)");
                return;
            }
            
            // Request transfer
            InventoryEvents.FireRequestTransferToContainer(context.ItemInstance, currentContainer);
        }
        
        // Handle drag from container to inventory
        public void OnDragFromContainer(DragContext context)
        {
            if (!currentContainer.CanTakeOut)
            {
                Debug.LogError("Cannot take items from this container");
                // TODO: UIEvents.OnShowError?.Invoke("Cannot take items from this container");
                return;
            }
            
            // Request transfer
            InventoryEvents.FireRequestTransferFromContainer(context.ItemInstance, currentContainer);
        }
        
        private void RefreshContainerUI()
        {
            // TODO: Implement container panel refresh
        }
    }
}
