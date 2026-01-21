using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Input;
using NightHunt.Networking;
using System.Collections.Generic;
using NightHunt.UI.Inventory;
using NightHunt.Data;
using NightHunt.Gameplay.Items;

namespace NightHunt.UI
{
    /// <summary>
    /// Inventory UI with grid layout
    /// Shows items, weight, quick slots
    /// Follows MVP pattern - implements IInventoryUIView
    /// </summary>
    public class InventoryUI : MonoBehaviour, IInventoryUIView
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

        [Header("Drop Amount Selector")]
        [SerializeField] private DropAmountSelector dropAmountSelector;

        private NetworkPlayer localPlayer;
        private IInventoryProvider inventoryProvider;
        private InventoryUIPresenter presenter;
        private PlayerInputHandler inputHandler;
        private InventorySync inventorySync;
        private List<ItemSlotUI> itemSlots = new List<ItemSlotUI>();
        private InventorySlot selectedSlot;

        private bool isInventoryOpen = false;
        private bool wasInventoryPressed = false;
        private bool wasQuickSlot1Pressed = false;
        private bool wasQuickSlot2Pressed = false;
        private bool wasQuickSlot3Pressed = false;

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
                        quickSlots[i].Initialize(i, this);
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

            // Initialize presenter (will be set when player is found)
        }

        private void Update()
        {
            // Find input handler if not set
            if (inputHandler == null && localPlayer != null)
            {
                inputHandler = localPlayer.GetComponent<PlayerInputHandler>();
            }

            // Toggle inventory with New Input System
            if (inputHandler != null)
            {
                bool isInventoryPressed = inputHandler.IsInventoryPressed();
                if (isInventoryPressed && !wasInventoryPressed)
                {
                    ToggleInventory();
                }
                wasInventoryPressed = isInventoryPressed;

                // Quick slot usage with New Input System
                bool qs1 = inputHandler.IsQuickSlot1Pressed();
                bool qs2 = inputHandler.IsQuickSlot2Pressed();
                bool qs3 = inputHandler.IsQuickSlot3Pressed();

                if (qs1 && !wasQuickSlot1Pressed)
                {
                    UseQuickSlot(0);
                }
                wasQuickSlot1Pressed = qs1;

                if (qs2 && !wasQuickSlot2Pressed)
                {
                    UseQuickSlot(1);
                }
                wasQuickSlot2Pressed = qs2;

                if (qs3 && !wasQuickSlot3Pressed)
                {
                    UseQuickSlot(2);
                }
                wasQuickSlot3Pressed = qs3;
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
                    inventoryProvider = player.GetComponent<IInventoryProvider>();
                    inventorySync = player.GetComponent<InventorySync>();
                    
                    // Initialize presenter with dependency injection
                    if (inventoryProvider != null && presenter == null)
                    {
                        presenter = new InventoryUIPresenter(inventoryProvider, this);
                    }
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
                if (presenter != null)
                {
                    presenter.RefreshInventory();
                }
            }
        }

        /// <summary>
        /// IInventoryUIView implementation: Display items
        /// </summary>
        public void DisplayItems(List<InventorySlot> items)
        {
            if (itemGridParent == null) return;

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
            foreach (var slot in items)
            {
                if (slot == null || slot.IsEmpty) continue;
                
                if (itemSlotPrefab != null)
                {
                    GameObject slotObj = Instantiate(itemSlotPrefab, itemGridParent);
                    ItemSlotUI slotUI = slotObj.GetComponent<ItemSlotUI>();
                    if (slotUI == null)
                    {
                        slotUI = slotObj.AddComponent<ItemSlotUI>();
                    }

                    slotUI.Initialize(slot, this);
                    itemSlots.Add(slotUI);
                }
            }
        }

        /// <summary>
        /// IInventoryUIView implementation: Update weight
        /// </summary>
        public void UpdateWeight(float current, float max)
        {
            if (weightText != null)
            {
                weightText.text = $"{current:F1} / {max:F1} kg";
            }

            if (weightBar != null)
            {
                weightBar.value = max > 0 ? current / max : 0f;
            }
        }

        /// <summary>
        /// Update inventory display (called from Update)
        /// </summary>
        private void UpdateInventoryDisplay()
        {
            if (presenter != null)
            {
                presenter.RefreshInventory();
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
        /// Select item slot
        /// </summary>
        public void SelectItemSlot(InventorySlot slot)
        {
            selectedSlot = slot;
            if (presenter != null)
            {
                presenter.OnItemSelected(slot);
            }
        }

        /// <summary>
        /// IInventoryUIView implementation: Show item info
        /// </summary>
        public void ShowItemInfo(ItemInfoData info)
        {
            if (itemInfoPanel == null) return;

            itemInfoPanel.SetActive(true);

            if (itemNameText != null)
            {
                itemNameText.text = info.DisplayName;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = $"Quantity: {info.Quantity}\nWeight: {info.Weight} kg\n\n{info.Description}";
            }

            // Setup buttons
            if (useItemButton != null)
            {
                useItemButton.gameObject.SetActive(info.CanUse);
                useItemButton.onClick.RemoveAllListeners();
                useItemButton.onClick.AddListener(() => OnUseItemClicked(info.ItemId));
            }

            if (dropItemButton != null)
            {
                dropItemButton.gameObject.SetActive(info.CanDrop);
                dropItemButton.onClick.RemoveAllListeners();
                dropItemButton.onClick.AddListener(() => OnDropItemClicked(info.ItemId, info.Quantity));
            }
        }

        /// <summary>
        /// IInventoryUIView implementation: Hide item info
        /// </summary>
        public void HideItemInfo()
        {
            if (itemInfoPanel != null)
            {
                itemInfoPanel.SetActive(false);
            }
            selectedSlot = null;
        }

        /// <summary>
        /// IInventoryUIView implementation: Show drop amount selector
        /// </summary>
        public void ShowDropAmountSelector(string itemId, int maxQuantity, System.Action<int> onConfirm)
        {
            if (dropAmountSelector != null)
            {
                dropAmountSelector.Show(itemId, maxQuantity, onConfirm);
            }
        }

        /// <summary>
        /// IInventoryUIView implementation: Hide drop amount selector
        /// </summary>
        public void HideDropAmountSelector()
        {
            if (dropAmountSelector != null)
            {
                dropAmountSelector.Hide();
            }
        }

        /// <summary>
        /// On use item button clicked
        /// </summary>
        private void OnUseItemClicked(string itemId)
        {
            if (localPlayer == null) return;

            // Use item via NetworkItemUsageController
            var usageController = localPlayer.GetComponent<NetworkItemUsageController>();
            if (usageController != null)
            {
                // TODO: Get itemInstanceId from selectedSlot
                // For now, simplified - would need ItemInstance tracking
                Debug.Log($"[InventoryUI] Use item: {itemId}");
            }
        }

        /// <summary>
        /// On drop item button clicked
        /// </summary>
        private void OnDropItemClicked(string itemId, int maxQuantity)
        {
            if (inventorySync == null) return;

            // Show drop amount selector
            ShowDropAmountSelector(itemId, maxQuantity, (dropQty) =>
            {
                // Request drop via server RPC
                Vector3 dropPosition = localPlayer != null ? localPlayer.transform.position + localPlayer.transform.forward * 2f : Vector3.zero;
                inventorySync.ServerRpc_RequestDrop(itemId, dropQty, dropPosition);
                
                // Refresh inventory
                if (presenter != null)
                {
                    presenter.RefreshInventory();
                }
            });
        }
    }

    /// <summary>
    /// Item slot UI component
    /// Follows Single Responsibility Principle
    /// </summary>
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Button slotButton;

        private InventorySlot slot;
        private InventoryUI inventoryUI;

        public void Initialize(InventorySlot slotData, InventoryUI ui)
        {
            slot = slotData;
            inventoryUI = ui;

            if (slot == null || slot.IsEmpty) return;

            // Update icon (would load from config)
            var config = GameConfigLoader.Instance?.GetItemConfig(slot.Item.ItemId);
            if (config != null && itemIcon != null)
            {
                // TODO: Load sprite from config.IconSpriteId
            }

            if (quantityText != null)
            {
                quantityText.text = slot.Quantity > 1 ? slot.Quantity.ToString() : "";
            }

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => ui.SelectItemSlot(slot));
            }
        }
    }

    /// <summary>
    /// Quick slot UI component
    /// Follows Single Responsibility Principle
    /// </summary>
    public class QuickSlotUI : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotNumberText;
        [SerializeField] private Button slotButton;

        private int slotIndex;
        private InventorySlot assignedSlot;
        private InventoryUI inventoryUI;

        public void Initialize(int index, InventoryUI ui)
        {
            slotIndex = index;
            inventoryUI = ui;
            
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

        public void AssignSlot(InventorySlot slot)
        {
            assignedSlot = slot;
            
            if (slot != null && !slot.IsEmpty)
            {
                // Update icon (would load from config)
                var config = GameConfigLoader.Instance?.GetItemConfig(slot.Item.ItemId);
                if (config != null && itemIcon != null)
                {
                    // TODO: Load sprite from config.IconSpriteId
                }
            }
            else
            {
                // Clear icon
                if (itemIcon != null)
                {
                    itemIcon.sprite = null;
                }
            }
        }

        public void UseItem()
        {
            if (assignedSlot == null || assignedSlot.IsEmpty) return;

            // Use item via NetworkItemUsageController
            var player = FindObjectOfType<NetworkPlayer>();
            if (player != null)
            {
                var usageController = player.GetComponent<NetworkItemUsageController>();
                if (usageController != null)
                {
                    // TODO: Get itemInstanceId from assignedSlot
                    Debug.Log($"[QuickSlotUI] Use item: {assignedSlot.Item.ItemId}");
                }
            }
        }
    }
}

