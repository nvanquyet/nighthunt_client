using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Inventory;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.UI
{
    /// <summary>
    /// Inventory UI with grid layout
    /// Shows items, weight, quick slots
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform itemGridParent;
        [SerializeField] private GameObject itemSlotPrefab;

        [Header("Weight Display")]
        [SerializeField] private TextMeshProUGUI weightText;
        [SerializeField] private Slider weightBar;

        [Header("Quick Slots")]
        [SerializeField] private Transform quickSlotParent;
        [SerializeField] private GameObject quickSlotPrefab;
        [SerializeField] private QuickSlotUI[] quickSlots;

        [Header("Item Info")]
        [SerializeField] private GameObject itemInfoPanel;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Button useItemButton;
        [SerializeField] private Button dropItemButton;

        private NetworkPlayer localPlayer;
        private InventorySystem inventorySystem;
        private List<ItemSlotUI> itemSlots = new List<ItemSlotUI>();
        private InventoryItem selectedItem;

        private bool isInventoryOpen = false;

        private void Start()
        {
            // Setup quick slots
            if (quickSlotParent != null && quickSlotPrefab != null)
            {
                quickSlots = new QuickSlotUI[3];
                for (int i = 0; i < 3; i++)
                {
                    GameObject slotObj = Instantiate(quickSlotPrefab, quickSlotParent);
                    quickSlots[i] = slotObj.GetComponent<QuickSlotUI>();
                    if (quickSlots[i] != null)
                    {
                        quickSlots[i].Initialize(i);
                    }
                }
            }

            // Hide inventory initially
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }

            if (itemInfoPanel != null)
            {
                itemInfoPanel.SetActive(false);
            }
        }

        private void Update()
        {
            // Toggle inventory with Tab key
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleInventory();
            }

            // Quick slot usage (1, 2, 3 keys)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                UseQuickSlot(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                UseQuickSlot(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                UseQuickSlot(2);
            }

            if (isInventoryOpen)
            {
                UpdateInventoryDisplay();
            }
        }

        /// <summary>
        /// Find local player and inventory
        /// </summary>
        private void FindLocalPlayer()
        {
            if (localPlayer != null) return;

            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.IsLocalPlayer)
                {
                    localPlayer = player;
                    inventorySystem = player.GetComponent<InventorySystem>();
                    break;
                }
            }
        }

        /// <summary>
        /// Toggle inventory panel
        /// </summary>
        public void ToggleInventory()
        {
            isInventoryOpen = !isInventoryOpen;
            
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isInventoryOpen);
            }

            if (isInventoryOpen)
            {
                FindLocalPlayer();
                RefreshInventory();
            }
        }

        /// <summary>
        /// Refresh inventory display
        /// </summary>
        private void RefreshInventory()
        {
            if (inventorySystem == null || itemGridParent == null) return;

            // Clear existing slots
            foreach (var slot in itemSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            itemSlots.Clear();

            // Create slots for items
            var items = inventorySystem.GetItems();
            foreach (var item in items)
            {
                if (itemSlotPrefab != null)
                {
                    GameObject slotObj = Instantiate(itemSlotPrefab, itemGridParent);
                    ItemSlotUI slotUI = slotObj.GetComponent<ItemSlotUI>();
                    if (slotUI != null)
                    {
                        slotUI.Initialize(item, this);
                        itemSlots.Add(slotUI);
                    }
                }
            }
        }

        /// <summary>
        /// Update inventory display
        /// </summary>
        private void UpdateInventoryDisplay()
        {
            if (inventorySystem == null) return;

            // Update weight
            if (weightText != null)
            {
                float currentWeight = inventorySystem.GetCurrentWeight();
                float maxWeight = inventorySystem.GetWeightCapacity();
                weightText.text = $"{currentWeight:F1} / {maxWeight:F1} kg";
            }

            if (weightBar != null)
            {
                weightBar.value = inventorySystem.GetWeightPercentage();
            }
        }

        /// <summary>
        /// Use quick slot
        /// </summary>
        private void UseQuickSlot(int index)
        {
            if (quickSlots == null || index < 0 || index >= quickSlots.Length) return;
            if (quickSlots[index] == null) return;

            quickSlots[index].UseItem();
        }

        /// <summary>
        /// Select item
        /// </summary>
        public void SelectItem(InventoryItem item)
        {
            selectedItem = item;
            ShowItemInfo(item);
        }

        /// <summary>
        /// Show item info panel
        /// </summary>
        private void ShowItemInfo(InventoryItem item)
        {
            if (itemInfoPanel == null || item == null) return;

            itemInfoPanel.SetActive(true);

            if (itemNameText != null)
            {
                itemNameText.text = item.Config.DisplayName;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = $"Quantity: {item.Quantity}\nWeight: {item.Config.Weight} kg";
            }

            // Setup buttons
            if (useItemButton != null)
            {
                useItemButton.onClick.RemoveAllListeners();
                useItemButton.onClick.AddListener(() => UseItem(item));
            }

            if (dropItemButton != null)
            {
                dropItemButton.onClick.RemoveAllListeners();
                dropItemButton.onClick.AddListener(() => DropItem(item));
            }
        }

        /// <summary>
        /// Use item
        /// </summary>
        private void UseItem(InventoryItem item)
        {
            if (inventorySystem == null || item == null) return;

            inventorySystem.UseItem(item.ItemId);
            RefreshInventory();
        }

        /// <summary>
        /// Drop item
        /// </summary>
        private void DropItem(InventoryItem item)
        {
            if (inventorySystem == null || item == null) return;

            // Would need to spawn item in world
            inventorySystem.RemoveItem(item.ItemId, 1);
            RefreshInventory();
        }
    }

    /// <summary>
    /// Item slot UI component
    /// </summary>
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Button slotButton;

        private InventoryItem item;
        private InventoryUI inventoryUI;

        public void Initialize(InventoryItem itemData, InventoryUI ui)
        {
            item = itemData;
            inventoryUI = ui;

            if (quantityText != null)
            {
                quantityText.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
            }

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => ui.SelectItem(item));
            }
        }
    }

    /// <summary>
    /// Quick slot UI component
    /// </summary>
    public class QuickSlotUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotNumberText;
        [SerializeField] private Button slotButton;

        private int slotIndex;
        private InventoryItem assignedItem;

        public void Initialize(int index)
        {
            slotIndex = index;
            
            if (slotNumberText != null)
            {
                slotNumberText.text = (index + 1).ToString();
            }

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(UseItem);
            }
        }

        public void AssignItem(InventoryItem item)
        {
            assignedItem = item;
            // Update icon, etc.
        }

        public void UseItem()
        {
            if (assignedItem == null) return;

            // Use item from inventory
            var player = FindObjectOfType<NetworkPlayer>();
            if (player != null)
            {
                var inventory = player.GetComponent<InventorySystem>();
                if (inventory != null)
                {
                    inventory.UseItem(assignedItem.ItemId);
                }
            }
        }
    }
}

