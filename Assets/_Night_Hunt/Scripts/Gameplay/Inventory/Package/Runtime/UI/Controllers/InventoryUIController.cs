using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Main inventory UI controller.
    /// Manages inventory panel display and refresh.
    /// UI is separate from player Canvas, uses PlayerInventoryCache to access player components.
    /// </summary>
    public class InventoryUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform inventoryContentParent;
        [SerializeField] private GameObject inventoryCellPrefab;
        [SerializeField] private Button sortButton;
        [SerializeField] private Button autoStackButton;
        
        private List<InventoryCellUI> cellUIs = new List<InventoryCellUI>();
        private bool isInventoryOpen = false;
        
        public static InventoryUIController Instance { get; private set; }
        public bool IsInventoryOpen => isInventoryOpen;
        
        void Awake()
        {
            Instance = this;
        }
        
        /// <summary>
        /// Get inventory manager from cache.
        /// </summary>
        private InventoryManager GetInventoryManager()
        {
            if (PlayerInventoryCache.Instance == null || !PlayerInventoryCache.Instance.IsCacheValid())
                return null;
            
            return PlayerInventoryCache.Instance.InventoryManager;
        }
        
        void OnEnable()
        {
            InventoryEvents.OnInventoryChanged += RefreshInventory;
            InventoryEvents.OnInventoryOpened += OnInventoryOpened;
            InventoryEvents.OnInventoryClosed += OnInventoryClosed;
            InventoryEvents.OnRequestOpenInventory += OpenInventory;
            InventoryEvents.OnRequestCloseInventory += CloseInventory;
            
            if (sortButton != null)
                sortButton.onClick.AddListener(OnSortClicked);
            
            if (autoStackButton != null)
                autoStackButton.onClick.AddListener(OnAutoStackClicked);
        }
        
        void OnDisable()
        {
            InventoryEvents.OnInventoryChanged -= RefreshInventory;
            InventoryEvents.OnInventoryOpened -= OnInventoryOpened;
            InventoryEvents.OnInventoryClosed -= OnInventoryClosed;
            InventoryEvents.OnRequestOpenInventory -= OpenInventory;
            InventoryEvents.OnRequestCloseInventory -= CloseInventory;
        }
        
        void OpenInventory()
        {
            if (isInventoryOpen) return;
            
            isInventoryOpen = true;
            inventoryPanel.SetActive(true);
            RefreshInventory(null);
            InventoryEvents.FireInventoryOpened();
        }
        
        void CloseInventory()
        {
            if (!isInventoryOpen) return;
            
            isInventoryOpen = false;
            inventoryPanel.SetActive(false);
            InventoryEvents.FireInventoryClosed();
        }
        
        void OnInventoryOpened()
        {
            isInventoryOpen = true;
        }
        
        void OnInventoryClosed()
        {
            isInventoryOpen = false;
        }
        
        void RefreshInventory(InventorySnapshot snapshot)
        {
            var inventoryManager = GetInventoryManager();
            if (inventoryManager == null) return;
            
            var slots = inventoryManager.GetAllSlots();
            
            // Ensure we have enough cell UIs
            while (cellUIs.Count < slots.Count)
            {
                var cellObj = Instantiate(inventoryCellPrefab, inventoryContentParent);
                var cellUI = cellObj.GetComponent<InventoryCellUI>();
                if (cellUI == null)
                {
                    cellUI = cellObj.AddComponent<InventoryCellUI>();
                }
                cellUIs.Add(cellUI);
                cellUI.gameObject.SetActive(true);
            }
            
            // Update cell UIs
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var cellUI = cellUIs[i];
                
                cellUI.Initialize(slot.Item, SlotLocationType.Inventory, slot.Index);
                cellUI.gameObject.SetActive(true);
            }
            
            // Hide unused cells
            for (int i = slots.Count; i < cellUIs.Count; i++)
            {
                cellUIs[i].gameObject.SetActive(false);
            }
        }
        
        void OnSortClicked()
        {
            var inventoryManager = GetInventoryManager();
            if (inventoryManager != null)
            {
                inventoryManager.SortInventory();
            }
        }
        
        void OnAutoStackClicked()
        {
            var inventoryManager = GetInventoryManager();
            if (inventoryManager != null)
            {
                inventoryManager.AutoStackInventory();
            }
        }
    }
}
