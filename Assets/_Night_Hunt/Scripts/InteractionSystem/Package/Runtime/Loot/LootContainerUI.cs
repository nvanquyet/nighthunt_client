using UnityEngine;
using UnityEngine.UI;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Loot
{
    /// <summary>
    /// UI controller for loot container (dual-grid: Container | Player Inventory).
    /// </summary>
    public class LootContainerUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject containerPanel;
        [SerializeField] private GameObject containerGrid;
        [SerializeField] private GameObject inventoryGrid;
        [SerializeField] private Button closeButton;

        private LootContainer currentContainer;
        private GridInventoryComponent playerInventory;
        private LootTransferHandler transferHandler;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseContainer);
            }

            transferHandler = GetComponent<LootTransferHandler>();
            if (transferHandler == null)
            {
                transferHandler = gameObject.AddComponent<LootTransferHandler>();
            }

            // Hide by default
            if (containerPanel != null)
            {
                containerPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Open container UI.
        /// </summary>
        public void OpenContainer(LootContainer container)
        {
            if (container == null)
                return;

            currentContainer = container;

            // Find player inventory
            var player = FindObjectOfType<GridInventoryComponent>();
            if (player != null)
            {
                playerInventory = player;
            }

            // Show UI
            if (containerPanel != null)
            {
                containerPanel.SetActive(true);
            }

            // Refresh UI
            RefreshContainerUI();
            RefreshInventoryUI();
        }

        /// <summary>
        /// Close container UI.
        /// </summary>
        public void CloseContainer()
        {
            if (containerPanel != null)
            {
                containerPanel.SetActive(false);
            }

            // Invoke close event
            if (currentContainer != null)
            {
                InventoryEvents.InvokeLootContainerClosed(currentContainer);
            }

            currentContainer = null;
        }

        /// <summary>
        /// Refresh container UI.
        /// </summary>
        private void RefreshContainerUI()
        {
            if (currentContainer == null)
                return;

            // TODO: Update grid UI with container items
        }

        /// <summary>
        /// Refresh inventory UI.
        /// </summary>
        private void RefreshInventoryUI()
        {
            if (playerInventory == null)
                return;

            // TODO: Update grid UI with inventory items
        }

        /// <summary>
        /// Transfer item from container to inventory.
        /// </summary>
        public void TransferToInventory(ItemInstance item)
        {
            if (currentContainer == null || playerInventory == null)
                return;

            if (transferHandler != null)
            {
                var containerObj = currentContainer.GetComponent<FishNet.Object.NetworkObject>();
                var inventoryObj = playerInventory.GetComponent<FishNet.Object.NetworkObject>();
                if (containerObj != null && inventoryObj != null)
                {
                    transferHandler.TransferItem(containerObj, inventoryObj, item);
                }
            }
        }

        /// <summary>
        /// Transfer item from inventory to container.
        /// </summary>
        public void TransferToContainer(ItemInstance item)
        {
            if (currentContainer == null || playerInventory == null)
                return;

            if (transferHandler != null)
            {
                var containerObj = currentContainer.GetComponent<FishNet.Object.NetworkObject>();
                var inventoryObj = playerInventory.GetComponent<FishNet.Object.NetworkObject>();
                if (containerObj != null && inventoryObj != null)
                {
                    transferHandler.TransferItemFromInventory(inventoryObj, containerObj, item);
                }
            }
        }
    }
}
