using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.UI.Cells;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Controllers
{
    /// <summary>
    /// Main controller for inventory UI.
    /// Listens to inventory events and updates UI accordingly.
    /// </summary>
    public class InventoryUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform cellContainer;
        [SerializeField] private GameObject cellPrefab;
        
        [Header("Settings")]
        [SerializeField] private bool openOnStart = false;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private List<InventoryCellUI> cells;
        private bool isOpen;
        
        #region Lifecycle
        
        void Awake()
        {
            cells = new List<InventoryCellUI>();
            
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(openOnStart);
                isOpen = openOnStart;
            }
        }
        
        void OnEnable()
        {
            // Subscribe to inventory events
            InventoryEvents.OnRequestOpenInventory += HandleOpenRequest;
            InventoryEvents.OnRequestCloseInventory += HandleCloseRequest;
            InventoryEvents.OnInventoryChanged += HandleInventoryChanged;
            InventoryEvents.OnItemAdded += HandleItemAdded;
            InventoryEvents.OnItemRemoved += HandleItemRemoved;
            InventoryEvents.OnStackSizeChanged += HandleStackSizeChanged;
            InventoryEvents.OnInventoryFull += HandleInventoryFull;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnRequestOpenInventory -= HandleOpenRequest;
            InventoryEvents.OnRequestCloseInventory -= HandleCloseRequest;
            InventoryEvents.OnInventoryChanged -= HandleInventoryChanged;
            InventoryEvents.OnItemAdded -= HandleItemAdded;
            InventoryEvents.OnItemRemoved -= HandleItemRemoved;
            InventoryEvents.OnStackSizeChanged -= HandleStackSizeChanged;
            InventoryEvents.OnInventoryFull -= HandleInventoryFull;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Opens the inventory UI.
        /// </summary>
        public void Open()
        {
            if (isOpen) return;
            
            inventoryPanel.SetActive(true);
            isOpen = true;
            
            InventoryEvents.InvokeInventoryOpened();
            
            if (enableDebugLogs)
                Debug.Log("[InventoryUIController] Inventory opened");
        }
        
        /// <summary>
        /// Closes the inventory UI.
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            
            inventoryPanel.SetActive(false);
            isOpen = false;
            
            InventoryEvents.InvokeInventoryClosed();
            
            if (enableDebugLogs)
                Debug.Log("[InventoryUIController] Inventory closed");
        }
        
        /// <summary>
        /// Toggles the inventory UI.
        /// </summary>
        public void Toggle()
        {
            if (isOpen)
                Close();
            else
                Open();
        }
        
        /// <summary>
        /// Initializes UI cells from inventory data.
        /// </summary>
        public void InitializeCells(InventoryData inventoryData)
        {
            // Clear existing cells
            foreach (var cell in cells)
            {
                if (cell != null)
                    Destroy(cell.gameObject);
            }
            cells.Clear();
            
            // Create cells for each slot
            var slots = inventoryData.GetAllSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var cellObj = Instantiate(cellPrefab, cellContainer);
                var cell = cellObj.GetComponent<InventoryCellUI>();
                
                if (cell != null)
                {
                    cell.Initialize(slots[i].Item, i, Core.Enums.SlotLocationType.Inventory);
                    cells.Add(cell);
                }
            }
            
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIController] Initialized {cells.Count} cells");
        }
        
        /// <summary>
        /// Refreshes all UI cells.
        /// </summary>
        public void RefreshAllCells()
        {
            foreach (var cell in cells)
            {
                if (cell != null)
                    cell.Refresh();
            }
        }
        
        /// <summary>
        /// Refreshes a specific cell.
        /// </summary>
        public void RefreshCell(int index)
        {
            if (index >= 0 && index < cells.Count && cells[index] != null)
            {
                cells[index].Refresh();
            }
        }
        
        /// <summary>
        /// Checks if inventory is currently open.
        /// </summary>
        public bool IsInventoryOpen => isOpen;
        
        #endregion
        
        #region Event Handlers
        
        private void HandleOpenRequest()
        {
            Open();
        }
        
        private void HandleCloseRequest()
        {
            Close();
        }
        
        private void HandleInventoryChanged(InventoryData data)
        {
            // Update all cells with new data
            var slots = data.GetAllSlots();
            
            for (int i = 0; i < slots.Count && i < cells.Count; i++)
            {
                cells[i].Initialize(slots[i].Item, i, Core.Enums.SlotLocationType.Inventory);
            }
            
            if (enableDebugLogs)
                Debug.Log("[InventoryUIController] Inventory updated");
        }
        
        private void HandleItemAdded(ItemInstance item, int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < cells.Count)
            {
                RefreshCell(slotIndex);
            }
            else
            {
                // Refresh all if slot index unknown
                RefreshAllCells();
            }
            
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIController] Item added: {item.Definition.ItemId}");
        }
        
        private void HandleItemRemoved(string instanceId, int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < cells.Count)
            {
                RefreshCell(slotIndex);
            }
            else
            {
                RefreshAllCells();
            }
            
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIController] Item removed: {instanceId}");
        }
        
        private void HandleStackSizeChanged(ItemInstance item)
        {
            // Find cell with this item and refresh
            foreach (var cell in cells)
            {
                if (cell.GetItemData() == item)
                {
                    cell.Refresh();
                    break;
                }
            }
            
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIController] Stack size changed: {item.Definition.ItemId}");
        }
        
        private void HandleInventoryFull()
        {
            UIEvents.InvokeShowMessage("Inventory is full!");
            
            if (enableDebugLogs)
                Debug.Log("[InventoryUIController] Inventory full");
        }
        
        #endregion
    }
}