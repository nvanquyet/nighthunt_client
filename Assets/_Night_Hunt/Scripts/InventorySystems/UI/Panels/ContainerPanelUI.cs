using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.UI.Cells;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Controller for container UI panel.
    /// Spawns inventory cells from container inventory data.
    /// Similar to InventoryUIController but for containers (loot boxes, etc.).
    /// </summary>
    public class ContainerPanelUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject containerPanel;
        [SerializeField] private Transform cellContainer;
        [SerializeField] private GameObject cellPrefab;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private List<InventoryCellUI> cells;
        private bool isOpen;
        
        #region Lifecycle
        
        void Awake()
        {
            cells = new List<InventoryCellUI>();
            
            if (containerPanel != null)
            {
                containerPanel.SetActive(false);
                isOpen = false;
            }
        }
        
        void OnEnable()
        {
            // Subscribe to container events (if they exist)
            // For now, using InventoryEvents as placeholder
            // TODO: Create ContainerEvents if needed
            InventoryEvents.OnInventoryChanged += HandleInventoryChanged;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnInventoryChanged -= HandleInventoryChanged;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Opens the container UI panel.
        /// </summary>
        public void Open()
        {
            if (isOpen) return;
            
            if (containerPanel != null)
            {
                containerPanel.SetActive(true);
            }
            
            isOpen = true;
            
            if (enableDebugLogs)
                Debug.Log("[ContainerPanelUI] Container opened");
        }
        
        /// <summary>
        /// Closes the container UI panel.
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            
            if (containerPanel != null)
            {
                containerPanel.SetActive(false);
            }
            
            isOpen = false;
            
            if (enableDebugLogs)
                Debug.Log("[ContainerPanelUI] Container closed");
        }
        
        /// <summary>
        /// Initializes UI cells from container inventory data.
        /// </summary>
        public void InitializeCells(InventoryData containerData)
        {
            // Clear existing cells
            foreach (var cell in cells)
            {
                if (cell != null)
                    Destroy(cell.gameObject);
            }
            cells.Clear();
            
            // Create cells for each slot
            var slots = containerData.GetAllSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var cellObj = Instantiate(cellPrefab, cellContainer);
                var cell = cellObj.GetComponent<InventoryCellUI>();
                
                if (cell != null)
                {
                    // Use Container location type
                    cell.Initialize(slots[i].Item, i, Core.Enums.SlotLocationType.Container);
                    cells.Add(cell);
                }
            }
            
            if (enableDebugLogs)
                Debug.Log($"[ContainerPanelUI] Initialized {cells.Count} cells");
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
        /// Checks if container is currently open.
        /// </summary>
        public bool IsContainerOpen => isOpen;
        
        #endregion
        
        #region Event Handlers
        
        private void HandleInventoryChanged(InventoryData data)
        {
            // Update all cells with new data
            var slots = data.GetAllSlots();
            
            for (int i = 0; i < slots.Count && i < cells.Count; i++)
            {
                cells[i].Initialize(slots[i].Item, i, Core.Enums.SlotLocationType.Container);
            }
            
            if (enableDebugLogs)
                Debug.Log("[ContainerPanelUI] Container inventory updated");
        }
        
        #endregion
    }
}
