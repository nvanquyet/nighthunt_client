using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Networking;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Core;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Loot;
using NightHunt.Data;
using FishNet;
using NightHunt.InteractionSystem.Core.Abstractions;
 
namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Main inventory panel with grid layout. Manages inventory display, drag & drop, and mode switching
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        [Header("Panel References")] [SerializeField]
        private GameObject panelRoot;

        [SerializeField] private Transform inventoryPanel;
        [SerializeField] private Transform shopContainer;
        [SerializeField] private Transform lootContainer;
        [SerializeField] private Transform quickSlotsPanel;
        [SerializeField] private Transform characterPanel;

        [Header("Inventory Grid")] [SerializeField]
        private Transform inventoryGridParent;

        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private int uiGridWidth = 4;
        [SerializeField] private int uiGridHeight = 3;

        [Header("UI Components")] [SerializeField]
        private ItemTooltip itemTooltip;

        [SerializeField] private NestedEquipmentPanel nestedEquipmentLeft;
        [SerializeField] private NestedEquipmentPanel nestedEquipmentRight;
        [SerializeField] private EquipmentPanel equipmentPanel;
        [SerializeField] private LootContainerPanel lootContainerPanel;
        [SerializeField] private ShopPanel shopPanel;
        [SerializeField] private DragDropHandler dragDropHandler;
        [SerializeField] private TrashSlotUI trashSlot;

        private NetworkPlayer localPlayer;
        private InventoryService inventorySystem;
        private GridInventoryComponent inventoryGrid;
        private List<ItemCell> slotUIs = new List<ItemCell>();
        private ItemCell selectedSlot;
        private Dictionary<string, int> localItemPositions = new Dictionary<string, int>(); // key = ItemInstance.instanceId
        private bool isOpen = false;
        private InventoryMode? currentMode = null;
        private InputAction openInventoryAction;
        private bool isInputSubscribed = false;
        private bool isSubscribed = false;

        // Public accessors
        public List<ItemCell> GetSlotUIs() => slotUIs;
        public ItemTooltip GetItemTooltip() => itemTooltip;
        public LootContainerPanel GetLootContainerPanel() => lootContainerPanel;
        public ShopPanel GetShopPanel() => shopPanel;
        public EquipmentPanel GetEquipmentPanel() => equipmentPanel;
        public bool IsOpen() => isOpen;
        public InventoryMode? GetMode() => currentMode;

        #region Unity Lifecycle

        private void Awake()
        {
            UIRegistry.RegisterInventoryPanel(this);
        }

        private void OnDestroy()
        {
            UIRegistry.UnregisterInventoryPanel(this);
            UnsubscribeFromInputActions();
            UnsubscribeFromLogicEvents();
        }

        private void OnEnable()
        {
            if (InstanceFinder.IsServer)
            {
                enabled = false;
                return;
            }
            
            SubscribeToLogicEvents();
            if (!isSubscribed) SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromLogicEvents();
            UnsubscribeFromEvents();
            UnsubscribeFromInputActions();
        }

        #endregion

        #region Initialization

        public void Initialize(NetworkPlayer player, InventoryService inventory)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                Debug.LogWarning("[InventoryPanel] Cannot initialize: Not local player");
                return;
            }

            localPlayer = player;
            inventorySystem = inventory;
            inventoryGrid = inventorySystem?.GetGrid();
            
            if (inventoryGrid == null)
            {
                Debug.LogError(
                    "[InventoryPanel] inventoryGrid is NULL! Ensure InventoryService has GridInventoryComponent");
                return;
            }

            InitializeSubPanels();
            InitializeDragDrop();

            if (panelRoot != null) panelRoot.SetActive(false);
            HideContainerPanels();

            if (!isSubscribed) SubscribeToEvents();
            SubscribeToInputActions();
        }

        private void InitializeSubPanels()
            {
            nestedEquipmentLeft?.Initialize(this);
            nestedEquipmentRight?.Initialize(this);
            equipmentPanel?.Initialize(this, inventorySystem);
            lootContainerPanel?.Initialize(this);
            shopPanel?.Initialize(this);
            trashSlot?.Initialize(this);
            }

        private void InitializeDragDrop()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (dragDropHandler != null && canvas != null)
            {
                dragDropHandler.Initialize(this, canvas);
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToLogicEvents()
            {
            InventoryLogicEvents.OnItemAdded += HandleItemAdded;
            InventoryLogicEvents.OnItemRemoved += HandleItemRemoved;
            InventoryLogicEvents.OnItemQuantityChanged += HandleItemQuantityChanged;
            InventoryLogicEvents.OnInventoryChanged += HandleInventoryChanged;
            InventoryLogicEvents.OnItemMoved += HandleItemMoved;
            InventoryLogicEvents.OnItemEquipped += HandleItemEquipped;
            InventoryLogicEvents.OnItemUnequipped += HandleItemUnequipped;
            InventoryLogicEvents.OnQuickSlotAssigned += HandleQuickSlotAssigned;
            InventoryLogicEvents.OnQuickSlotCleared += HandleQuickSlotCleared;
            InventoryLogicEvents.OnItemUseStarted += HandleItemUseStarted;
            InventoryLogicEvents.OnItemUseCompleted += HandleItemUseCompleted;
            InventoryLogicEvents.OnItemUseCancelled += HandleItemUseCancelled;
            InventoryLogicEvents.OnItemUseProgress += HandleItemUseProgress;
        }

        private void UnsubscribeFromLogicEvents()
        {
            InventoryLogicEvents.OnItemAdded -= HandleItemAdded;
            InventoryLogicEvents.OnItemRemoved -= HandleItemRemoved;
            InventoryLogicEvents.OnItemQuantityChanged -= HandleItemQuantityChanged;
            InventoryLogicEvents.OnInventoryChanged -= HandleInventoryChanged;
            InventoryLogicEvents.OnItemMoved -= HandleItemMoved;
            InventoryLogicEvents.OnItemEquipped -= HandleItemEquipped;
            InventoryLogicEvents.OnItemUnequipped -= HandleItemUnequipped;
            InventoryLogicEvents.OnQuickSlotAssigned -= HandleQuickSlotAssigned;
            InventoryLogicEvents.OnQuickSlotCleared -= HandleQuickSlotCleared;
            InventoryLogicEvents.OnItemUseStarted -= HandleItemUseStarted;
            InventoryLogicEvents.OnItemUseCompleted -= HandleItemUseCompleted;
            InventoryLogicEvents.OnItemUseCancelled -= HandleItemUseCancelled;
            InventoryLogicEvents.OnItemUseProgress -= HandleItemUseProgress;
        }

        private void SubscribeToEvents()
        {
            if (isSubscribed) return;

            InventoryEvents.OnInventoryChanged += OnInventoryToggleRequested;
            InventoryEvents.OnLootContainerOpened += OnLootContainerOpened;
            InventoryEvents.OnShopOpened += OnShopOpened;
            InventoryEvents.OnItemPickedUp += OnItemPickedUp;
            InventoryEvents.OnItemAdded += OnItemAdded;
            InventoryEvents.OnItemRemoved += OnItemRemoved;
            InventoryEvents.OnItemQuantityChanged += OnItemQuantityChanged;
            
            isSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!isSubscribed) return;

            InventoryEvents.OnInventoryChanged -= OnInventoryToggleRequested;
            InventoryEvents.OnLootContainerOpened -= OnLootContainerOpened;
            InventoryEvents.OnShopOpened -= OnShopOpened;
            InventoryEvents.OnItemPickedUp -= OnItemPickedUp;
            InventoryEvents.OnItemAdded -= OnItemAdded;
            InventoryEvents.OnItemRemoved -= OnItemRemoved;
            InventoryEvents.OnItemQuantityChanged -= OnItemQuantityChanged;
            
            isSubscribed = false;
        }

        private void SubscribeToInputActions()
        {
            if (isInputSubscribed || localPlayer == null) return;

            var inputManager = InputLayerManager.Instance;
            if (inputManager == null) return;

            var uiMapController = inputManager.GetActionMapController("UI");
            openInventoryAction = uiMapController?.GetAction("OpenInventory");

            if (openInventoryAction != null)
            {
                openInventoryAction.performed += OnInventoryInputPerformed;
                isInputSubscribed = true;
            }
        }

        private void UnsubscribeFromInputActions()
        {
            if (!isInputSubscribed) return;

            if (openInventoryAction != null)
            {
                openInventoryAction.performed -= OnInventoryInputPerformed;
            }

            isInputSubscribed = false;
        }

        #endregion

        #region Event Handlers

        private void HandleItemAdded(ItemInstance item) => RefreshInventoryGrid();
        private void HandleItemRemoved(ItemInstance item, int removedQuantity) => RefreshInventoryGrid();
        private void HandleItemQuantityChanged(ItemInstance item, int newQuantity) => RefreshInventoryGrid();
        private void HandleInventoryChanged() => RefreshInventoryGrid();

        private void HandleItemMoved(string itemId, int fromX, int fromY, int toX, int toY)
        {
            // Server confirmed - keep local UI state
        }

        private void HandleItemEquipped(string itemId, EquipmentSlotType slotType)
        {
            equipmentPanel?.RefreshSlots();
        }

        private void HandleItemUnequipped(string itemId, EquipmentSlotType slotType)
        {
            equipmentPanel?.RefreshSlots();
        }

        private void HandleQuickSlotAssigned(int slotIndex, string itemId)
        {
            equipmentPanel?.RefreshQuickSlots();
        }

        private void HandleQuickSlotCleared(int slotIndex)
        {
            equipmentPanel?.RefreshQuickSlots();
        }

        private void HandleItemUseStarted(string itemId)
        {
        }

        private void HandleItemUseCompleted(string itemId) => RefreshInventoryGrid();

        private void HandleItemUseCancelled(string itemId)
        {
        }

        private void HandleItemUseProgress(string itemId, float progress)
        {
        }

        private void OnItemAdded(ItemInstance item)
        {
            if (dragDropHandler == null || !dragDropHandler.IsDragging())
            {
                RefreshInventoryGrid();
            }
        }

        private void OnItemRemoved(ItemInstance item, int removedQuantity)
        {
            if (!string.IsNullOrEmpty(item.instanceId))
            {
                ClearLocalItemPosition(item.instanceId);
            }

            if (dragDropHandler == null || !dragDropHandler.IsDragging())
            {
                RefreshInventoryGrid();
            }
        }

        private void OnItemQuantityChanged(ItemInstance item, int newQuantity) => RefreshInventoryGrid();

        private void OnInventoryInputPerformed(InputAction.CallbackContext context) => ToggleInventory();
        private void OnInventoryToggleRequested() => ToggleInventory();

        private void OnLootContainerOpened(ILootContainer container)
        {
            if (container == null) return;

            if (!isOpen) OpenInventory();

            if (lootContainerPanel != null)
            {
                var lootContainer = container as NetworkLootContainer;
                if (lootContainer != null)
                {
                    lootContainerPanel.LoadContainer(lootContainer);
                    lootContainerPanel.Show();
                    SetMode(InventoryMode.Loot);
                }
            }
        }

        private void OnShopOpened(object shop)
        {
            if (shop == null) return;

            if (!isOpen) OpenInventory();

            if (shopPanel != null)
            {
                var shopContainer = shop as NightHunt.InteractionSystem.Shop.ShopContainer;
                if (shopContainer != null)
                {
                    string shopId = shopContainer.NetworkObject.ObjectId.ToString();
                    shopPanel.LoadShop(shopId, shopContainer);
                    shopPanel.Show();
                    SetMode(InventoryMode.Shop);
                }
            }
        }

        private void OnItemPickedUp(ItemInstance item, string pickupableName)
        {
            if (isOpen) RefreshInventoryGrid();
            }

        #endregion

        #region Inventory Open/Close

        public void ToggleInventory()
        {
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string stack = System.Environment.StackTrace;
                // keep it single-line-ish for ndjson
                stack = stack.Replace("\r", " ").Replace("\n", " ");
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_invui\",\"timestamp\":{ts},\"location\":\"InventoryPanel.cs:ToggleInventory\",\"message\":\"ToggleInventory called\",\"data\":{{\"isOpen\":{isOpen.ToString().ToLower()},\"currentMode\":\"{currentMode?.ToString() ?? "null"}\",\"stack\":\"{stack}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run4\",\"hypothesisId\":\"UI_CLOSE\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
            if (isOpen) CloseInventory();
            else OpenInventory();
        }

        public void OpenInventory()
        {
            if (isOpen) return;

            if (!ValidateLocalPlayer()) return;
            EnsureInventoryGrid();
            SpawnGridSlotsIfNeeded();
            isOpen = true;
            if (panelRoot != null) panelRoot.SetActive(true);

            TransitionInputState(InputState.InventoryOpen);
            RefreshInventoryGrid();

            equipmentPanel?.Show();

            if (lootContainerPanel != null)
            {
                lootContainerPanel.CheckAndLoadOpenedContainer();
                if (lootContainerPanel.IsContainerLoaded())
                {
                    SetMode(InventoryMode.Loot);
                }
                else
                {
                    HideContainerPanels();
                }
            }
            else
            {
                HideContainerPanels();
                }
            }
        private int CalculateRequiredSlotCount(int itemCount)
                {
            int minSlots = uiGridWidth * uiGridHeight;
            return Mathf.Max(minSlots, itemCount + 1);
                }

        private int RoundUpToRow(int count)
            {
            return Mathf.CeilToInt((float)count / uiGridWidth) * uiGridWidth;
        }

        private void EnsureEnoughSlots(int itemCount)
                {
            int requiredSlots = RoundUpToRow(CalculateRequiredSlotCount(itemCount));
            if (slotUIs.Count >= requiredSlots) return;
            CreateEmptySlots(requiredSlots - slotUIs.Count);
            }

        private void SpawnGridSlotsIfNeeded()
            {
            int totalSlots = uiGridWidth * uiGridHeight;

            if (slotUIs.Count == totalSlots)
                return;

            ClearSlots();
            CreateEmptySlots(totalSlots);
            }

        public void CloseInventory()
        {
            if (!isOpen) return;

            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string stack = System.Environment.StackTrace;
                stack = stack.Replace("\r", " ").Replace("\n", " ");
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_invui\",\"timestamp\":{ts},\"location\":\"InventoryPanel.cs:CloseInventory\",\"message\":\"CloseInventory called\",\"data\":{{\"currentMode\":\"{currentMode?.ToString() ?? "null"}\",\"stack\":\"{stack}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run4\",\"hypothesisId\":\"UI_CLOSE\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion

            isOpen = false;
            if (panelRoot != null) panelRoot.SetActive(false);

            TransitionInputState(InputState.PlayerAlive);

            if (currentMode == InventoryMode.Loot)
            {
                lootContainerPanel?.Hide();
                currentMode = null;
            }

            SelectItem(null);
            itemTooltip?.HideTooltip();
            nestedEquipmentLeft?.Hide();
            nestedEquipmentRight?.Hide();
        }

        private bool ValidateLocalPlayer()
        {
            if (localPlayer != null && (localPlayer.IsOwner || localPlayer.IsLocalPlayer))
            {
                return true;
            }

            var uiManager = PlayerUIManager.Instance;
            if (uiManager != null)
            {
                localPlayer = uiManager.GetLocalPlayer();
                inventorySystem = uiManager.GetInventorySystem();
            }

            if (localPlayer == null || (!localPlayer.IsOwner && !localPlayer.IsLocalPlayer))
            {
                Debug.LogError("[InventoryPanel] Cannot open inventory: Not local player");
                return false;
            }

            return true;
        }

        private void EnsureInventoryGrid()
            {
            if (inventoryGrid == null && inventorySystem != null)
                {
                inventoryGrid = inventorySystem.GetGrid();
                }

            if (inventoryGrid == null && localPlayer != null)
            {
                inventorySystem = ComponentRegistry.GetInventoryService(localPlayer);
                inventoryGrid = inventorySystem?.GetGrid();
            }
        }

        private void TransitionInputState(InputState state)
        {
            var inputManager = InputLayerManager.Instance;
            inputManager?.TransitionToState(state);

            var inputRouter = ComponentRegistry.GetInputRouter(localPlayer);
            inputRouter?.TransitionToState(state);

            if (state == InputState.InventoryOpen && !isInputSubscribed)
            {
                SubscribeToInputActions();
            }
        }

        #endregion

        #region Mode Management

        public void SetMode(InventoryMode mode)
        {
            if (currentMode != mode && selectedSlot != null)
            {
                SelectItem(null);
            }

            currentMode = mode;

            if (equipmentPanel != null && isOpen) equipmentPanel.Show();

            switch (mode)
            {
                case InventoryMode.Loot:
                    lootContainerPanel?.Show();
                    shopPanel?.Hide();
                    break;

                case InventoryMode.Shop:
                    shopPanel?.Show();
                    lootContainerPanel?.Hide();
                    break;
            }
        }

        public void HideContainerPanels()
        {
            lootContainerPanel?.Hide();
            shopPanel?.Hide();
            currentMode = null;
        }

        #endregion

        #region Item Position Management

        public void UpdateLocalItemPosition(string instanceId, int slotIndex)
            {
            if (string.IsNullOrEmpty(instanceId) || slotIndex < 0) return;
            localItemPositions[instanceId] = slotIndex;
                        }

        public void ClearLocalItemPosition(string instanceId)
                    {
            if (string.IsNullOrEmpty(instanceId)) return;
            localItemPositions.Remove(instanceId);
        }

        public int GetSlotIndex(ItemCell cell) => cell == null ? -1 : slotUIs.IndexOf(cell);

        public (int x, int y) GetGridPositionFromIndex(int index)
        {
            return (index % uiGridWidth, index / uiGridWidth);
        }

        #endregion

        #region Grid Refresh

        public void RefreshInventoryGrid()
        {
            if (!EnsureGridReferences())
                return;

            var allItems = inventoryGrid.Items.ToList();

            EnsureEnoughSlots(allItems.Count);

            // Clear all current slot contents before re-placing items to avoid "ghost" items
            foreach (var slotUI in slotUIs)
            {
                if (slotUI == null) continue;
                slotUI.SetSlot(new InventorySlot());
                slotUI.UpdateDisplay();
            }

            CleanupLocalPositions(allItems);
            var itemPlacements = CalculateItemPlacements(allItems);
            PlaceItems(itemPlacements);
        }


        //  CalculateItemPlacements - Prioritize local positions
        private List<(ItemInstance, int)> CalculateItemPlacements(List<ItemInstance> allItems)
        {
            var placements = new List<(ItemInstance, int)>();
            var usedSlots = new HashSet<int>();

            // PRIORITY 1: Items with local positions (by instanceId)
            foreach (var item in allItems)
            {
                if (string.IsNullOrEmpty(item.instanceId))
                    continue;

                if (localItemPositions.TryGetValue(item.instanceId, out int targetSlot))
                {
                    if (targetSlot >= 0 && targetSlot < slotUIs.Count &&
                        !usedSlots.Contains(targetSlot))
                    {
                        placements.Add((item, targetSlot));
                        usedSlots.Add(targetSlot);
                    }
                }
            }

            // PRIORITY 2: New items without local positions
            foreach (var item in allItems)
                {
                // Nếu item chưa có instanceId (vd: item spawn cũ), vẫn phải hiển thị
                if (string.IsNullOrEmpty(item.instanceId))
                {
                    for (int i = 0; i < slotUIs.Count; i++)
                    {
                        if (!usedSlots.Contains(i))
                        {
                            placements.Add((item, i));
                            usedSlots.Add(i);
                            break;
                        }
                    }
                    continue;
                }

                // Item có instanceId nhưng chưa có local position
                if (!localItemPositions.ContainsKey(item.instanceId))
                {
                    for (int i = 0; i < slotUIs.Count; i++)
                    {
                        if (!usedSlots.Contains(i))
                        {
                            placements.Add((item, i));
                            usedSlots.Add(i);
                            localItemPositions[item.instanceId] = i; // Save new position
                            break;
                        }
                    }
                }
            }

            return placements;
        }
        private void PlaceItems(List<(ItemInstance item, int targetSlotIndex)> placements)
        {
            foreach (var (item, targetSlotIndex) in placements)
        {
                if (targetSlotIndex < 0 || targetSlotIndex >= slotUIs.Count)
                    continue;

                var itemData = GetItemDataFromRegistry(item.itemDataId);
                if (itemData == null)
                    continue;

                var slot = new InventorySlot();
                // Preserve per-instance identity for local layout
                slot.SetItem(itemData, item.quantity, item.instanceId);

                var slotUI = slotUIs[targetSlotIndex];
                slotUI.SetSlot(slot);
                slotUI.UpdateDisplay();
                    }
                }
        private bool EnsureGridReferences()
        {
                if (inventoryGrid == null)
                {
                inventoryGrid = inventorySystem?.GetGrid() ??
                                ComponentRegistry.GetInventoryService(localPlayer)?.GetGrid() ??
                                PlayerUIManager.Instance?.GetInventorySystem()?.GetGrid();
            }
            
            if (inventoryGrid == null)
            {
                Debug.LogError("[InventoryPanel] Cannot refresh: inventoryGrid is NULL");
                return false;
            }
            
            if (inventoryGridParent == null)
            {
                Debug.LogError("[InventoryPanel] Cannot refresh: inventoryGridParent is NULL");
                return false;
            }

            return true;
        }

        private void ClearSlots()
        {
            foreach (var slotUI in slotUIs)
            {
                if (slotUI != null) Destroy(slotUI.gameObject);
                }

            slotUIs.Clear();
        }

        private void CreateEmptySlots(int count)
        {
            if (inventorySlotPrefab == null)
            {
                Debug.LogError("[InventoryPanel] inventorySlotPrefab is NULL");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                GameObject slotObj = Instantiate(inventorySlotPrefab, inventoryGridParent);
                slotObj.SetActive(true);

                ItemCell slotUI = slotObj.GetComponent<ItemCell>() ?? slotObj.AddComponent<ItemCell>();
                slotUI.Initialize(new InventorySlot(), this, ItemCellLocation.Inventory, slotUIs.Count);
                slotUIs.Add(slotUI);
            }
        }

        private void PopulateSlots(List<ItemInstance> allItems)
                {
            if (allItems == null || allItems.Count == 0)
                    {
                itemTooltip?.HideTooltip();
                return;
                    }

            CleanupLocalPositions(allItems);
            var itemPlacements = CalculateItemPlacements(allItems);
            PlaceItems(itemPlacements);
        }

        private void CleanupLocalPositions(List<ItemInstance> allItems)
                {
            var itemsToRemove = new List<string>();
            foreach (var kvp in localItemPositions)
            {
                bool exists = allItems.Exists(item => item.instanceId == kvp.Key);
                if (!exists) itemsToRemove.Add(kvp.Key);
            }

            itemsToRemove.ForEach(id => localItemPositions.Remove(id));
                }

        #endregion

        #region Item Selection & Tooltip

        public void SelectItem(ItemCell slot)
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
                itemTooltip?.HideTooltip();
                nestedEquipmentLeft?.Hide();
                nestedEquipmentRight?.Hide();
                }

            selectedSlot = slot;
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(true);
                
                var slotData = selectedSlot.GetSlot();
                if (slotData != null && !slotData.IsEmpty)
                {
                    ShowTooltipForSlot(slotData);
                    ShowNestedEquipmentPanel(slotData, IsEquippedItem(selectedSlot));
                }
            }
        }

        private void ShowTooltipForSlot(InventorySlot slotData)
        {
            if (itemTooltip != null && UnityEngine.InputSystem.Mouse.current != null)
                        {
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                        itemTooltip.ShowTooltip(slotData, mousePos);
                    }
        }

        public void ShowNestedEquipmentPanelOnHover(InventorySlot slotData, bool isEquippedItem)
        {
            if (selectedSlot != null && selectedSlot.GetSlot() == slotData) return;
            ShowNestedEquipmentPanel(slotData, isEquippedItem);
        }

        public void HideNestedEquipmentPanelOnUnhover()
        {
            if (selectedSlot == null)
            {
                nestedEquipmentLeft?.Hide();
                nestedEquipmentRight?.Hide();
            }
        }

        private void ShowNestedEquipmentPanel(InventorySlot slotData, bool isEquippedItem)
        {
            if (slotData == null || slotData.IsEmpty)
            {
                nestedEquipmentLeft?.Hide();
                nestedEquipmentRight?.Hide();
                return;
            }

            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null || !HasEquipmentSlots(itemData))
                {
                nestedEquipmentLeft?.Hide();
                nestedEquipmentRight?.Hide();
                return;
            }

            if (isEquippedItem)
            {
                nestedEquipmentRight?.ShowForItem(slotData);
                nestedEquipmentLeft?.Hide();
                }
            else
            {
                nestedEquipmentLeft?.ShowForItem(slotData);
                nestedEquipmentRight?.Hide();
                }
            }

        private bool HasEquipmentSlots(ItemDataBase itemData)
        {
            if (itemData == null) return false;

            // 1. Check if EquipmentDataBase with attachment slots
            if (itemData is EquipmentDataBase equipmentData)
            {
                if (equipmentData.AttachmentSlots != null &&
                    equipmentData.AttachmentSlots.Length > 0)
        {
                    return true;
                }
            }

            // 2. Fallback: All weapons can have attachments
            if (itemData.Category == ItemCategory.Weapon)
            {
                return true;
            }

            // 3. Check equipment categories
            if (itemData.Category == ItemCategory.Armor ||
                itemData.Category == ItemCategory.Helmet ||
                itemData.Category == ItemCategory.Backpack)
            {
                // Check if this specific equipment has attachment slots
                if (itemData is EquipmentDataBase eqData)
        {
                    return eqData.AttachmentSlots != null && eqData.AttachmentSlots.Length > 0;
                }
            }

            return false;
        }

        public bool IsEquippedItem(ItemCell slot)
        {
            if (slot == null || equipmentPanel == null) return false;

            var equipmentSlots = equipmentPanel.GetEquipmentSlots();
            if (equipmentSlots != null && System.Array.Exists(equipmentSlots, s => s == slot))
        {
                return true;
            }

            var weaponSlots = equipmentPanel.GetWeaponSlots();
            return weaponSlots != null && System.Array.Exists(weaponSlots, s => s == slot);
        }

        #endregion

        #region Slot Interactions

        public void HandleSlotRightClick(ItemCell slot)
        {
            if (slot == null || slot.IsEmpty()) return;

            var slotData = slot.GetSlot();
            if (slotData != null && !slotData.IsEmpty)
            {
                UseItem(slotData);
            }
        }

        public void HandleSlotDoubleClick(ItemCell slot)
        {
            if (slot == null || slot.IsEmpty()) return;

            var slotData = slot.GetSlot();
            if (slotData == null || slotData.IsEmpty) return;

            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null) return;

            if (isOpen)
            {
                HandleDoubleClickInInventory(slot, itemData, slot.GetLocation());
            }
            else
            {
                HandleDoubleClickInHUD(slot, itemData, slot.GetLocation());
            }
        }
        
        private void UseItem(InventorySlot slotData)
        {
            if (slotData?.Item == null) return;
            InventoryUIEvents.RequestUseItem(slotData.Item.ItemId);
            }

        private void HandleDoubleClickInInventory(ItemCell slot,
            NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData, ItemCellLocation location)
        {
            switch (itemData.Category)
            {
                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Consumable:
                    if (itemData.IsConsumable)
                    {
                        InventoryUIEvents.RequestUseItem(itemData.ItemId);
                    }

                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon:
                    AutoEquipWeapon(slot);
                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor:
                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet:
                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack:
                    AutoEquipEquipment(slot, itemData.Category);
                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Attachment:
                    AutoEquipAttachment(slot);
                    break;
            }
        }

        private void HandleDoubleClickInHUD(ItemCell slot,
            NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData, ItemCellLocation location)
        {
            if (location == ItemCellLocation.QuickSlot && itemData.IsConsumable)
                    {
                        InventoryUIEvents.RequestUseItem(itemData.ItemId);
                    }
        }

        #endregion

        #region Equipment Operations

        private void AutoEquipWeapon(ItemCell slot)
        {
            if (equipmentPanel == null) return;

            // Find empty weapon slot
            for (int i = 0; i < 2; i++)
            {
                var weaponSlot = equipmentPanel.GetWeaponSlot(i);
                if (weaponSlot != null && weaponSlot.IsEmpty())
                {
                    EquipWeapon(slot, i);
                    return;
                }
            }

            // Replace first weapon
            EquipWeapon(slot, 0);
        }

        private void AutoEquipEquipment(ItemCell slot,
            NightHunt.InteractionSystem.Core.Abstractions.ItemCategory category)
        {
            if (equipmentPanel == null) return;

            EquipmentSlotType slotType = GetEquipmentSlotTypeFromCategory(category);
            var equipmentSlot = equipmentPanel.GetEquipmentSlot(slotType);
            if (equipmentSlot != null)
            {
                EquipItem(slot, equipmentSlot);
            }
        }

        private void AutoEquipAttachment(ItemCell slot)
        {
            // TODO: Implement attachment equipping
        }

        private EquipmentSlotType GetEquipmentSlotTypeFromCategory(
            NightHunt.InteractionSystem.Core.Abstractions.ItemCategory category)
        {
            return category switch
            {
                NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor => EquipmentSlotType.Armor,
                NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet => EquipmentSlotType.Helmet,
                NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack => EquipmentSlotType.Backpack,
                _ => EquipmentSlotType.Armor
            };
        }

        public void EquipWeapon(ItemCell slot, int weaponSlotIndex)
        {
            if (slot == null || slot.IsEmpty() || equipmentPanel == null) return;

            var slotData = slot.GetSlot();
            var itemData = GetItemDataFromRegistry(slotData?.Item?.ItemId);

            if (itemData == null ||
                itemData.Category != NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon)
        {
                Debug.LogWarning("[InventoryPanel] Only weapons can be equipped to weapon slots");
                return;
            }

            var weaponSlotUI = equipmentPanel.GetWeaponSlot(weaponSlotIndex);
            if (weaponSlotUI != null && !weaponSlotUI.IsEmpty())
            {
                // Swap weapons
                var equippedWeaponSlot = weaponSlotUI.GetSlot();
                int slotIndex = slotUIs.IndexOf(slot);
                var (inventoryX, inventoryY) = GetGridPositionFromIndex(slotIndex);

                InventoryUIEvents.RequestEquipWeapon(slotData.Item.ItemId, weaponSlotIndex);
                InventoryUIEvents.RequestMoveItemToSlot(equippedWeaponSlot.Item.ItemId, inventoryX, inventoryY);
            }
            else
            {
                // Equip to empty slot
                InventoryUIEvents.RequestEquipWeapon(slotData.Item.ItemId, weaponSlotIndex);
            }

            StartCoroutine(RefreshInventoryAfterMove());
        }

        public void EquipItem(ItemCell slot, ItemCell equipmentSlot)
            {
            if (slot == null || slot.IsEmpty() || equipmentSlot == null) return;

            var slotData = slot.GetSlot();
            var itemData = GetItemDataFromRegistry(slotData?.Item?.ItemId);

            if (itemData == null) return;

            EquipmentSlotType slotType = equipmentSlot.GetEquipmentSlotType();
            if (!CanEquipItemToSlot(itemData, slotType)) return;

            var equippedSlot = equipmentSlot.GetSlot();
            if (equippedSlot != null && !equippedSlot.IsEmpty)
                {
                // Swap equipment
                int slotIndex = slotUIs.IndexOf(slot);
                var (inventoryX, inventoryY) = GetGridPositionFromIndex(slotIndex);

                InventoryUIEvents.RequestUnequipItem(equippedSlot.Item.ItemId, slotType);
                InventoryUIEvents.RequestEquipItem(slotData.Item.ItemId, slotType);
                InventoryUIEvents.RequestMoveItemToSlot(equippedSlot.Item.ItemId, inventoryX, inventoryY);
                }
                else
                {
                // Equip to empty slot
                InventoryUIEvents.RequestEquipItem(slotData.Item.ItemId, slotType);
            }

            RefreshInventoryGrid();
        }

        private bool CanEquipItemToSlot(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData,
            EquipmentSlotType slotType)
        {
            if (itemData == null) return false;

            if (itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon)
            {
                return false;
            }

            var category = itemData.Category;
            return category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor ||
                   category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet ||
                   category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack;
        }

        /// <summary>
        /// Swap weapon: Equip new weapon and move old weapon to inventory
        /// </summary>
        public void SwapWeapon(ItemCell slot, int weaponSlotIndex)
        {
            EquipWeapon(slot, weaponSlotIndex);
        }

        /// <summary>
        /// Swap equipment: Equip new equipment and move old equipment to inventory
        /// </summary>
        public void SwapEquipment(ItemCell slot, ItemCell equipmentSlot)
        {
            EquipItem(slot, equipmentSlot);
        }

        /// <summary>
        /// Unequip weapon to inventory
        /// </summary>
        public void UnequipWeaponToInventory(int weaponSlotIndex, ItemCell targetSlot)
        {
            if (inventorySystem == null || targetSlot == null) return;

            int targetIndex = slotUIs.IndexOf(targetSlot);
            var (toX, toY) = GetGridPositionFromIndex(targetIndex);
            if (toX >= 0 && toY >= 0)
            {
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Unequip item to inventory
        /// </summary>
        public void UnequipItemToInventory(ItemCell sourceSlot, ItemCell targetSlot)
        {
            if (sourceSlot == null || targetSlot == null || inventorySystem == null) return;

            var slot = sourceSlot.GetSlot();
            if (slot == null || slot.IsEmpty) return;

            int targetIndex = slotUIs.IndexOf(targetSlot);
            var (toX, toY) = GetGridPositionFromIndex(targetIndex);
            if (toX >= 0 && toY >= 0)
            {
                var grid = inventorySystem.GetGrid();
                if (grid == null) return;

                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Move item from quick slot to inventory
        /// </summary>
        public void MoveFromQuickSlotToInventory(int quickSlotIndex, ItemCell targetSlot)
        {
            if (inventorySystem == null || targetSlot == null) return;

            var quickSlots = inventorySystem.GetQuickSlots();
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlots.Length) return;

            var quickSlot = quickSlots[quickSlotIndex];
            if (quickSlot == null || quickSlot.IsEmpty) return;

            int targetIndex = slotUIs.IndexOf(targetSlot);
            var (toX, toY) = GetGridPositionFromIndex(targetIndex);
            if (toX >= 0 && toY >= 0)
            {
                var grid = inventorySystem.GetGrid();
                if (grid == null) return;

                RefreshInventoryGrid();
            }
        }

        #endregion

        #region Drag & Drop

        public void StartDrag(ItemCell slot, PointerEventData eventData)
        {
            dragDropHandler?.StartDrag(slot, eventData);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            dragDropHandler?.UpdateDrag(eventData);
        }

        public void EndDrag(ItemCell sourceSlot, PointerEventData eventData)
        {
            dragDropHandler?.EndDrag(sourceSlot, eventData);
            }

        public void MoveItem(ItemCell from, ItemCell to)
        {
            if (from == null || to == null || from.IsEmpty()) return;

            var fromSlot = from.GetSlot();
            if (fromSlot == null || fromSlot.IsEmpty) return;

            int fromIndex = slotUIs.IndexOf(from);
            int toIndex = slotUIs.IndexOf(to);

            if (fromIndex >= 0 && toIndex >= 0 &&
                from.GetLocation() == ItemCellLocation.Inventory &&
                to.GetLocation() == ItemCellLocation.Inventory)
                        {
                var (fromX, fromY) = GetGridPositionFromIndex(fromIndex);
                var (toX, toY) = GetGridPositionFromIndex(toIndex);
                var toSlot = to.GetSlot();

                if (toSlot != null && !toSlot.IsEmpty)
                {
                    InventoryUIEvents.RequestSwapItems(fromSlot.Item.ItemId, fromX, fromY, toSlot.Item.ItemId, toX,
                        toY);
                }
                else
                {
                    InventoryUIEvents.RequestMoveItem(fromSlot.Item.ItemId, fromX, fromY, toX, toY);
                }
            }
        }

        public void SwapItems(ItemCell from, ItemCell to)
                    {
            if (from == null || to == null || from.IsEmpty() || to.IsEmpty()) return;

            var fromSlot = from.GetSlot();
            var toSlot = to.GetSlot();

            if (fromSlot == null || fromSlot.IsEmpty || toSlot == null || toSlot.IsEmpty) return;

            int fromIndex = slotUIs.IndexOf(from);
            int toIndex = slotUIs.IndexOf(to);

            if (fromIndex < 0 || toIndex < 0 ||
                from.GetLocation() != ItemCellLocation.Inventory ||
                to.GetLocation() != ItemCellLocation.Inventory) return;

            var (fromX, fromY) = GetGridPositionFromIndex(fromIndex);
            var (toX, toY) = GetGridPositionFromIndex(toIndex);

            InventoryUIEvents.RequestSwapItems(fromSlot.Item.ItemId, fromX, fromY, toSlot.Item.ItemId, toX, toY);
        }

        #endregion

        #region Container Operations

        public void MoveItemToContainer(ItemCell sourceCell, ItemCell targetCell)
        {
            if (sourceCell == null || targetCell == null || sourceCell.IsEmpty()) return;

            var slotData = sourceCell.GetSlot();
            if (slotData == null || slotData.IsEmpty) return;

            var lootPanel = GetLootContainerPanel();
            // #region agent log
            long timestamp11 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp11}_inventory\",\"timestamp\":{timestamp11},\"location\":\"InventoryPanel.cs:MoveItemToContainer\",\"message\":\"MoveItemToContainer called\",\"data\":{{\"itemId\":\"{slotData.Item.ItemId}\",\"lootPanel\":{lootPanel != null},\"isContainerLoaded\":{lootPanel?.IsContainerLoaded() ?? false}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"F\"}}\n");
            // #endregion
            if (lootPanel == null || !lootPanel.IsContainerLoaded()) return;

            int sourceIndex = slotUIs.IndexOf(sourceCell);
            var (fromX, fromY) = GetGridPositionFromIndex(sourceIndex);

            // Clear local position when moving item to container
            var instanceId = slotData.Item.InstanceId;
            if (!string.IsNullOrEmpty(instanceId))
            {
                ClearLocalItemPosition(instanceId);
            }

            lootPanel.MoveItemToContainer(slotData.Item.ItemId, fromX, fromY);
        }

        public void MoveItemFromContainer(ItemCell sourceCell, ItemCell targetCell)
        {
            if (sourceCell == null || targetCell == null || sourceCell.IsEmpty()) return;

            var slotData = sourceCell.GetSlot();
            if (slotData == null || slotData.IsEmpty) return;

            var lootPanel = GetLootContainerPanel();
            // #region agent log
            long timestamp12 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp12}_inventory\",\"timestamp\":{timestamp12},\"location\":\"InventoryPanel.cs:MoveItemFromContainer\",\"message\":\"MoveItemFromContainer called\",\"data\":{{\"itemId\":\"{slotData.Item.ItemId}\",\"lootPanel\":{lootPanel != null},\"isContainerLoaded\":{lootPanel?.IsContainerLoaded() ?? false}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"F\"}}\n");
            // #endregion
            if (lootPanel == null || !lootPanel.IsContainerLoaded()) return;

            int targetIndex = slotUIs.IndexOf(targetCell);
            var (toX, toY) = GetGridPositionFromIndex(targetIndex);

            // Save target position BEFORE server sync so RefreshInventoryGrid() can use it
            // This ensures item appears at the correct position when OnItemAdded fires
            var instanceId = slotData.Item.InstanceId;
            if (!string.IsNullOrEmpty(instanceId))
            {
                UpdateLocalItemPosition(instanceId, targetIndex);
            }

            lootPanel.MoveItemFromContainer(slotData.Item.ItemId, toX, toY);
        }

        #endregion

        #region Quick Slot & Drop

        public void AssignQuickSlot(ItemCell slot, int quickSlotIndex)
        {
            if (slot == null || slot.IsEmpty()) return;

            var slotData = slot.GetSlot();
            if (slotData == null || slotData.IsEmpty) return;

            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null || !itemData.IsConsumable)
            {
                Debug.LogWarning("[InventoryPanel] Only consumable items can be assigned to quick slots");
                return;
            }

            InventoryUIEvents.RequestAssignQuickSlot(slotData.Item.ItemId, quickSlotIndex);
        }

        public void DropItem(string itemId, int quantity)
        {
            if (inventorySystem == null || localPlayer == null) return;

            ItemInstance? itemInstance = null;
            var allItems = inventoryGrid?.Items;
            if (allItems != null)
            {
                foreach (var item in allItems)
                {
                    if (item.itemDataId == itemId)
                    {
                        itemInstance = item.WithQuantity(Mathf.Min(item.quantity, quantity));
                        break;
                    }
                }
            }

            if (!itemInstance.HasValue) return;

            var itemDropHandler = localPlayer.GetComponent<NightHunt.InteractionSystem.Items.Drop.ItemDropHandler>() ??
                                  localPlayer
                                      .GetComponentInChildren<NightHunt.InteractionSystem.Items.Drop.ItemDropHandler>();

            if (itemDropHandler != null && itemDropHandler.IsSpawned)
            {
                Vector3 dropPosition = localPlayer.transform.position +
                                       localPlayer.transform.forward * 1.5f +
                                       Vector3.up * 0.5f;

                itemDropHandler.DropItem(itemInstance.Value, dropPosition);
                // Clear local layout for this specific instance
                var instanceId = itemInstance.Value.instanceId;
                if (!string.IsNullOrEmpty(instanceId))
                {
                    ClearLocalItemPosition(instanceId);
                }
            }
            else
            {
                InventoryUIEvents.RequestRemoveItem(itemId, quantity);
            }
        }

        #endregion

        #region Utilities

        private NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase GetItemDataFromRegistry(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            return registry?.GetById(itemId);
        }

        private IEnumerator RefreshInventoryAfterMove()
        {
            yield return new WaitForSeconds(0.2f);
                RefreshInventoryGrid();
            }

        public void ForceDisable()
        {
            isOpen = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            else gameObject.SetActive(false);
            }

        #endregion
    }

    public enum InventoryMode
    {
        Loot,
        Shop
    }
} 