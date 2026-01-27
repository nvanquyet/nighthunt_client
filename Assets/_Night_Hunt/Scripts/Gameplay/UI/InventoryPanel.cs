using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Input;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Main inventory panel with grid layout
    /// Manages inventory display, drag & drop, and mode switching
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform leftPanel; // ItemDetailPanel container
        [SerializeField] private Transform centerPanel; // Inventory grid container
        [SerializeField] private Transform rightPanel; // EquipmentPanel/LootContainerPanel container

        [Header("Inventory Grid")]
        [SerializeField] private Transform inventoryGridParent;
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GridLayoutGroup gridLayout;

        [Header("UI Components")]
        [SerializeField] private ItemDetailPanel itemDetailPanel;
        [SerializeField] private EquipmentPanel equipmentPanel;
        [SerializeField] private LootContainerPanel lootContainerPanel;
        [SerializeField] private DragDropHandler dragDropHandler;

        private NetworkPlayer localPlayer;
        private InventoryService inventorySystem;
        private GridInventoryComponent inventoryGrid;
        private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
        private InventorySlotUI selectedSlot;
        private bool isOpen = false;
        private InventoryMode currentMode = InventoryMode.Equipment;

        /// <summary>
        /// Initialize inventory panel
        /// </summary>
        public void Initialize(NetworkPlayer player, InventoryService inventory)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                Debug.LogWarning("[InventoryPanel] Cannot initialize: Not local player!");
                return;
            }

            localPlayer = player;
            inventorySystem = inventory;
            inventoryGrid = inventorySystem?.GetGrid();

            // Initialize sub-panels
            if (itemDetailPanel != null)
            {
                itemDetailPanel.Initialize(this);
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.Initialize(this, inventorySystem);
            }

            if (lootContainerPanel != null)
            {
                lootContainerPanel.Initialize(this);
            }

            // Initialize drag drop handler
            Canvas canvas = GetComponentInParent<Canvas>();
            if (dragDropHandler != null && canvas != null)
            {
                dragDropHandler.Initialize(this, canvas);
            }

            // Hide panel initially
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // Set initial mode
            SetMode(InventoryMode.Equipment);
        }

        private void Update()
        {
            // Handle input for opening/closing inventory
            // This will be handled by InputSystem actions
        }

        /// <summary>
        /// Toggle inventory panel
        /// </summary>
        public void ToggleInventory()
        {
            if (isOpen)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }

        /// <summary>
        /// Open inventory panel
        /// </summary>
        public void OpenInventory()
        {
            if (isOpen || localPlayer == null || !localPlayer.IsLocalPlayer)
                return;

            isOpen = true;

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // Switch input state
            var inputManager = InputLayerManager.Instance;
            if (inputManager != null)
            {
                inputManager.TransitionToState(InputState.InventoryOpen);
            }

            // Refresh inventory display
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Close inventory panel
        /// </summary>
        public void CloseInventory()
        {
            if (!isOpen)
                return;

            isOpen = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // Switch input state back
            var inputManager = InputLayerManager.Instance;
            if (inputManager != null)
            {
                inputManager.TransitionToState(InputState.PlayerAlive);
            }

            // Close loot container if open
            if (currentMode == InventoryMode.Loot)
            {
                SetMode(InventoryMode.Equipment);
            }
        }

        /// <summary>
        /// Set inventory mode (Equipment or Loot)
        /// </summary>
        public void SetMode(InventoryMode mode)
        {
            currentMode = mode;

            switch (mode)
            {
                case InventoryMode.Equipment:
                    if (equipmentPanel != null)
                    {
                        equipmentPanel.Show();
                    }
                    if (lootContainerPanel != null)
                    {
                        lootContainerPanel.Hide();
                    }
                    break;

                case InventoryMode.Loot:
                    if (equipmentPanel != null)
                    {
                        equipmentPanel.Hide();
                    }
                    if (lootContainerPanel != null)
                    {
                        lootContainerPanel.Show();
                    }
                    break;
            }
        }

        /// <summary>
        /// Refresh inventory grid display
        /// </summary>
        public void RefreshInventoryGrid()
        {
            if (inventoryGrid == null || inventoryGridParent == null)
                return;

            // Clear existing slots
            foreach (var slotUI in slotUIs)
            {
                if (slotUI != null)
                {
                    Destroy(slotUI.gameObject);
                }
            }
            slotUIs.Clear();

            // Create slots for grid
            var (width, height) = inventoryGrid.GetGridSize();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var itemInstance = inventoryGrid.GetItemAt(x, y);
                    
                    // Create wrapper InventorySlot for UI compatibility
                    InventorySlot slot = new InventorySlot();
                    if (itemInstance.HasValue)
                    {
                        var item = itemInstance.Value;
                        var itemConfig = NightHunt.Data.GameConfigLoader.Instance?.GetItemConfig(item.itemDataId);
                        if (itemConfig != null)
                        {
                            slot.SetItem(itemConfig, item.quantity);
                        }
                    }

                    // Create slot UI
                    if (inventorySlotPrefab != null)
                    {
                        GameObject slotObj = Instantiate(inventorySlotPrefab, inventoryGridParent);
                        InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                        if (slotUI == null)
                        {
                            slotUI = slotObj.AddComponent<InventorySlotUI>();
                        }

                        slotUI.Initialize(slot, this, x, y);
                        slotUIs.Add(slotUI);
                    }
                }
            }
        }

        /// <summary>
        /// Select item slot
        /// </summary>
        public void SelectItem(InventorySlotUI slot)
        {
            // Deselect previous
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
            }

            // Select new
            selectedSlot = slot;
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(true);
            }

            // Update item detail panel
            // Don't show nested equipment in detail panel - only show tooltip on hover
            if (itemDetailPanel != null && selectedSlot != null)
            {
                bool isNested = selectedSlot.IsNestedEquipment();
                itemDetailPanel.DisplayItem(selectedSlot.GetSlot(), isNested);
            }
        }

        /// <summary>
        /// Handle slot right click
        /// </summary>
        public void HandleSlotRightClick(InventorySlotUI slot)
        {
            if (slot == null || slot.IsEmpty())
                return;

            // TODO: Implement right click actions (use item, split stack, etc.)
            var slotData = slot.GetSlot();
            if (slotData != null && !slotData.IsEmpty)
            {
                // Use item if consumable
                if (inventorySystem != null)
                {
                    inventorySystem.UseItem(slotData.Item.ItemId);
                    RefreshInventoryGrid();
                }
            }
        }

        // Drag & Drop handlers
        public void StartDrag(InventorySlotUI slot, PointerEventData eventData)
        {
            if (dragDropHandler != null)
            {
                dragDropHandler.StartDrag(slot, eventData);
            }
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragDropHandler != null)
            {
                dragDropHandler.UpdateDrag(eventData);
            }
        }

        public void EndDrag(InventorySlotUI sourceSlot, PointerEventData eventData)
        {
            if (dragDropHandler != null)
            {
                dragDropHandler.EndDrag(sourceSlot, eventData);
            }
        }

        /// <summary>
        /// Move item between slots
        /// </summary>
        public void MoveItem(InventorySlotUI from, InventorySlotUI to)
        {
            if (from == null || to == null || inventorySystem == null)
                return;

            var (fromX, fromY) = from.GetGridPosition();
            var (toX, toY) = to.GetGridPosition();

            // Only move if both are valid grid positions (not negative, which indicates loot slots)
            if (fromX >= 0 && fromY >= 0 && toX >= 0 && toY >= 0)
            {
                inventorySystem.MoveItem(fromX, fromY, toX, toY);
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Assign item to quick slot
        /// </summary>
        public void AssignQuickSlot(InventorySlotUI slot, int quickSlotIndex)
        {
            if (slot == null || slot.IsEmpty() || inventorySystem == null)
                return;

            var slotData = slot.GetSlot();
            if (slotData != null && !slotData.IsEmpty)
            {
                inventorySystem.AssignQuickSlot(quickSlotIndex, slotData.Item.ItemId);
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Equip weapon
        /// </summary>
        public void EquipWeapon(InventorySlotUI slot, int weaponSlotIndex)
        {
            if (slot == null || slot.IsEmpty() || inventorySystem == null)
                return;

            var slotData = slot.GetSlot();
            if (slotData != null && !slotData.IsEmpty)
            {
                inventorySystem.EquipWeapon(weaponSlotIndex, slotData.Item.ItemId);
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Equip item to equipment slot
        /// </summary>
        public void EquipItem(InventorySlotUI slot, EquipmentSlotUI equipmentSlot)
        {
            if (slot == null || slot.IsEmpty() || equipmentSlot == null)
                return;

            // TODO: Implement equip item logic
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Drop item
        /// </summary>
        public void DropItem(string itemId, int quantity)
        {
            if (inventorySystem != null)
            {
                inventorySystem.DropItem(itemId, quantity);
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Move item from quick slot to inventory
        /// </summary>
        public void MoveFromQuickSlotToInventory(int quickSlotIndex, InventorySlotUI targetSlot)
        {
            if (inventorySystem == null || targetSlot == null)
                return;

            var quickSlots = inventorySystem.GetQuickSlots();
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlots.Length)
                return;

            var quickSlot = quickSlots[quickSlotIndex];
            if (quickSlot == null || quickSlot.IsEmpty)
                return;

            // Move item from quick slot to inventory
            var (toX, toY) = targetSlot.GetGridPosition();
            if (toX >= 0 && toY >= 0)
            {
                // Find empty slot or swap
                var grid = inventorySystem.GetGrid();
                if (grid == null) return;
                
                var targetItemInstance = grid.GetItemAt(toX, toY);
                
                // TODO: Convert InventorySlot to ItemInstance for quick slot
                // For now, this is a placeholder - quick slot system needs to be implemented
                Debug.LogWarning("[InventoryPanel] MoveFromQuickSlotToInventory: Quick slot system not yet implemented");
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Unequip weapon to inventory
        /// </summary>
        public void UnequipWeaponToInventory(int weaponSlotIndex, InventorySlotUI targetSlot)
        {
            if (inventorySystem == null || targetSlot == null)
                return;

            // TODO: Get equipped weapon from combat system
            // For now, just move to inventory slot
            var (toX, toY) = targetSlot.GetGridPosition();
            if (toX >= 0 && toY >= 0)
            {
                // Similar logic to MoveFromQuickSlotToInventory
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Unequip item to inventory
        /// </summary>
        public void UnequipItemToInventory(EquipmentSlotUI sourceSlot, InventorySlotUI targetSlot)
        {
            if (sourceSlot == null || targetSlot == null || inventorySystem == null)
                return;

            var slot = sourceSlot.GetSlot();
            if (slot == null || slot.IsEmpty)
                return;

            // Move equipment item to inventory
            var (toX, toY) = targetSlot.GetGridPosition();
            if (toX >= 0 && toY >= 0)
            {
                var grid = inventorySystem.GetGrid();
                if (grid == null) return;
                
                var targetItemInstance = grid.GetItemAt(toX, toY);
                
                // TODO: Convert InventorySlot to ItemInstance for equipment
                // For now, this is a placeholder - equipment system needs to be implemented
                Debug.LogWarning("[InventoryPanel] UnequipItemToInventory: Equipment system not yet fully implemented");
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Open loot container
        /// </summary>
        public void OpenLootContainer(object containerData)
        {
            SetMode(InventoryMode.Loot);
            if (lootContainerPanel != null)
            {
                lootContainerPanel.LoadContainer(containerData);
            }
        }

        /// <summary>
        /// Close loot container
        /// </summary>
        public void CloseLootContainer()
        {
            SetMode(InventoryMode.Equipment);
            if (lootContainerPanel != null)
            {
                lootContainerPanel.ClearContainer();
            }
        }

        /// <summary>
        /// Check if inventory is open
        /// </summary>
        public bool IsOpen() => isOpen;

        /// <summary>
        /// Get current mode
        /// </summary>
        public InventoryMode GetMode() => currentMode;
    }

    /// <summary>
    /// Inventory panel modes
    /// </summary>
    public enum InventoryMode
    {
        Equipment,
        Loot
    }
}
