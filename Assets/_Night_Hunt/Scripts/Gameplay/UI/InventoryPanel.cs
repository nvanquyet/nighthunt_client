using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Core;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Loot;
using NightHunt.Data;
using FishNet;
 
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
        [SerializeField] private Transform inventoryPanel; // Inventory grid container (top-left)
        [SerializeField] private Transform shopContainer; // Shop container (below inventory) - separate from loot
        [SerializeField] private Transform lootContainer; // Loot container (below inventory) - separate from shop
        [SerializeField] private Transform quickSlotsPanel; // Quick slots panel (below shop/loot)
        [SerializeField] private Transform characterPanel; // Character equipment panel (right side)

        [Header("Inventory Grid")]
        [SerializeField] private Transform inventoryGridParent;
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GridLayoutGroup gridLayout;
        
        [Header("UI Grid Settings")]
        [SerializeField] private int uiGridWidth = 4;
        [SerializeField] private int uiGridHeight = 3;

        [Header("UI Components")]
        [SerializeField] private ItemTooltip itemTooltip; // Floating tooltip
        [SerializeField] private NestedEquipmentPanel nestedEquipmentLeft; // Panel for inventory items (center-left)
        [SerializeField] private NestedEquipmentPanel nestedEquipmentRight; // Panel for equipped items (right side)
        [SerializeField] private EquipmentPanel equipmentPanel;
        [SerializeField] private LootContainerPanel lootContainerPanel;
        [SerializeField] private ShopPanel shopPanel;
        [SerializeField] private DragDropHandler dragDropHandler;
        [SerializeField] private TrashSlotUI trashSlot; // Trash slot for dropping items (inventory, quick slots, equipment)

        private NetworkPlayer localPlayer;
        private InventoryService inventorySystem;
        private GridInventoryComponent inventoryGrid;
        private List<ItemCell> slotUIs = new List<ItemCell>();
        private ItemCell selectedSlot;
        private bool isOpen = false;
        private InventoryMode? currentMode = null; // null = không có mode nào (chỉ equipment panel)
        
        // Direct input handling for inventory toggle
        private InputAction openInventoryAction;
        private bool isInputSubscribed = false;

        private void Awake()
        {
            // Register this panel in the static registry (no FindObject needed)
            UIRegistry.RegisterInventoryPanel(this);
        }

        private void OnDestroy()
        {
            // Unregister when destroyed
            UIRegistry.UnregisterInventoryPanel(this);
            UnsubscribeFromInputActions();
            UnsubscribeFromLogicEvents();
        }

        private void OnEnable()
        {
            // UI components should not run on server (headless server compatibility)
            if (InstanceFinder.IsServer)
            {
                Debug.Log($"[InventoryPanel] Running on server - disabling UI component (headless server compatibility)");
                enabled = false;
                return;
            }
            
            SubscribeToLogicEvents();
            if (!isSubscribed)
            {
                SubscribeToEvents();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromLogicEvents();
            UnsubscribeFromEvents();
            UnsubscribeFromInputActions();
        }

        /// <summary>
        /// Subscribe to Logic Layer events to update UI
        /// </summary>
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

        /// <summary>
        /// Unsubscribe from Logic Layer events
        /// </summary>
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

        // Event handlers from Logic Layer
        private void HandleItemAdded(ItemInstance item)
        {
            RefreshInventoryGrid();
        }

        private void HandleItemRemoved(ItemInstance item, int removedQuantity)
        {
            RefreshInventoryGrid();
        }

        private void HandleItemQuantityChanged(ItemInstance item, int newQuantity)
        {
            RefreshInventoryGrid();
        }

        private void HandleInventoryChanged()
        {
            RefreshInventoryGrid();
        }

        private void HandleItemMoved(string itemId, int fromX, int fromY, int toX, int toY)
        {
            RefreshInventoryGrid();
        }

        private void HandleItemEquipped(string itemId, EquipmentSlotType slotType)
        {
            // Update equipment panel if needed
            if (equipmentPanel != null)
            {
                equipmentPanel.RefreshSlots();
            }
        }

        private void HandleItemUnequipped(string itemId, EquipmentSlotType slotType)
        {
            // Update equipment panel if needed
            if (equipmentPanel != null)
            {
                equipmentPanel.RefreshSlots();
            }
        }

        private void HandleQuickSlotAssigned(int slotIndex, string itemId)
        {
            // Update quick slots if needed
            if (equipmentPanel != null)
            {
                equipmentPanel.RefreshQuickSlots();
            }
        }

        private void HandleQuickSlotCleared(int slotIndex)
        {
            // Update quick slots if needed
            if (equipmentPanel != null)
            {
                equipmentPanel.RefreshQuickSlots();
            }
        }

        private void HandleItemUseStarted(string itemId)
        {
            // Show progress bar or update UI
            Debug.Log($"[InventoryPanel] Item use started: {itemId}");
        }

        private void HandleItemUseCompleted(string itemId)
        {
            // Hide progress bar or update UI
            Debug.Log($"[InventoryPanel] Item use completed: {itemId}");
            RefreshInventoryGrid();
        }

        private void HandleItemUseCancelled(string itemId)
        {
            // Hide progress bar or update UI
            Debug.Log($"[InventoryPanel] Item use cancelled: {itemId}");
        }

        private void HandleItemUseProgress(string itemId, float progress)
        {
            // Update progress bar
            // TODO: Show progress bar in HUD or on item icon
        }

        /// <summary>
        /// Initialize inventory panel
        /// </summary>
        public void Initialize(NetworkPlayer player, InventoryService inventory)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot initialize: Not local player! player={player != null}, IsLocalPlayer={player?.IsLocalPlayer ?? false}");
                return;
            }

            localPlayer = player;
            inventorySystem = inventory;
            inventoryGrid = inventorySystem?.GetGrid();
            
            if (inventoryGrid == null)
            {
                Debug.LogError($"[InventoryPanel] inventoryGrid is NULL! inventorySystem.GetGrid() returned null.");
                Debug.LogError($"[InventoryPanel] Please ensure InventoryService has GridInventoryComponent (not ListInventoryComponent).");
            }
            
            // Ensure GameObject is active
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            
            // Verify UI is visible
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError($"[InventoryPanel] GameObject is not active in hierarchy! Parent: {transform.parent?.name ?? "None"}, Root: {transform.root.name}");
            }

            // Initialize sub-panels
            // ItemTooltip doesn't need Initialize - it's event-driven
            if (itemTooltip != null)
            {
                Debug.Log("[InventoryPanel] ItemTooltip reference found");
            }

            // Initialize NestedEquipmentPanels (left and right)
            if (nestedEquipmentLeft != null)
            {
                nestedEquipmentLeft.Initialize(this);
                Debug.Log("[InventoryPanel] NestedEquipmentPanel (Left) initialized");
            }
            if (nestedEquipmentRight != null)
            {
                nestedEquipmentRight.Initialize(this);
                Debug.Log("[InventoryPanel] NestedEquipmentPanel (Right) initialized");
            }

            if (equipmentPanel != null)
            {
                equipmentPanel.Initialize(this, inventorySystem);
            }

            if (lootContainerPanel != null)
            {
                lootContainerPanel.Initialize(this);
            }

            // Initialize TrashSlot (for dropping items from inventory, quick slots, equipment)
            if (trashSlot != null)
            {
                trashSlot.Initialize(this);
                Debug.Log("[InventoryPanel] TrashSlot initialized");
            }


            if (shopPanel != null)
            {
                shopPanel.Initialize(this);
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

            // Hide container panels initially (only equipment panel visible)
            HideContainerPanels();

            // Subscribe to inventory toggle event (if not already subscribed)
            if (!isSubscribed)
            {
                SubscribeToEvents();
            }
            
            // Subscribe to direct input action from UI map (for closing inventory)
            SubscribeToInputActions();
        }

        private bool isSubscribed = false;

        /// <summary>
        /// Subscribe to inventory events (prevent duplicate subscription)
        /// </summary>
        private void SubscribeToEvents()
        {
            if (isSubscribed)
            {
                Debug.LogWarning("[InventoryPanel] Already subscribed to inventory events, skipping duplicate subscription");
                return;
            }

            // Subscribe to toggle event (for opening/closing inventory)
            InventoryEvents.OnInventoryChanged += OnInventoryToggleRequested;
            
            // Subscribe to container opened event (auto-open inventory when container/shop is opened)
            InventoryEvents.OnLootContainerOpened += OnLootContainerOpened;
            InventoryEvents.OnShopOpened += OnShopOpened;
            
            // Subscribe to pickup event (auto-open inventory when item is picked up)
            InventoryEvents.OnItemPickedUp += OnItemPickedUp;
            
            // Subscribe to item change events (for refreshing UI when items are added/removed/changed)
            InventoryEvents.OnItemAdded += OnItemAdded;
            InventoryEvents.OnItemRemoved += OnItemRemoved;
            InventoryEvents.OnItemQuantityChanged += OnItemQuantityChanged;
            
            isSubscribed = true;
            Debug.Log("[InventoryPanel] Subscribed to inventory events: OnInventoryChanged, OnItemAdded, OnItemRemoved, OnItemQuantityChanged");
        }

        /// <summary>
        /// Unsubscribe from inventory events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (!isSubscribed)
                return;

            InventoryEvents.OnInventoryChanged -= OnInventoryToggleRequested;
            InventoryEvents.OnLootContainerOpened -= OnLootContainerOpened;
            InventoryEvents.OnShopOpened -= OnShopOpened;
            InventoryEvents.OnItemPickedUp -= OnItemPickedUp;
            InventoryEvents.OnItemAdded -= OnItemAdded;
            InventoryEvents.OnItemRemoved -= OnItemRemoved;
            InventoryEvents.OnItemQuantityChanged -= OnItemQuantityChanged;
            
            isSubscribed = false;
            Debug.Log("[InventoryPanel] Unsubscribed from all inventory events");
        }

        /// <summary>
        /// Handle item added event - refresh UI
        /// </summary>
        private void OnItemAdded(ItemInstance item)
        {
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Handle item removed event - refresh UI
        /// </summary>
        private void OnItemRemoved(ItemInstance item, int removedQuantity)
        {
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Handle item quantity changed event - refresh UI
        /// </summary>
        private void OnItemQuantityChanged(ItemInstance item, int newQuantity)
        {
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Subscribe to direct input actions from UI map (for closing inventory when UI map is active)
        /// </summary>
        private void SubscribeToInputActions()
        {
            if (isInputSubscribed)
            {
                Debug.LogWarning("[InventoryPanel] Already subscribed to input actions, skipping duplicate subscription");
                return;
            }

            if (localPlayer == null)
            {
                Debug.LogWarning("[InventoryPanel] Cannot subscribe to input actions: localPlayer is null");
                return;
            }

            // Get InputActionAsset from InputLayerManager
            var inputManager = InputLayerManager.Instance;
            if (inputManager == null)
            {
                Debug.LogWarning("[InventoryPanel] InputLayerManager.Instance is null! Cannot subscribe to input actions.");
                return;
            }

            // Get UI map controller
            var uiMapController = inputManager.GetActionMapController("UI");
            if (uiMapController == null)
            {
                Debug.LogWarning("[InventoryPanel] UI map controller not found! Cannot subscribe to OpenInventory action.");
                return;
            }

            // Get OpenInventory action directly from controller
            openInventoryAction = uiMapController.GetAction("OpenInventory");
            if (openInventoryAction != null)
            {
                openInventoryAction.performed += OnInventoryInputPerformed;
                isInputSubscribed = true;
                Debug.Log("[InventoryPanel] Subscribed to OpenInventory action from UI map (for closing inventory)");
            }
            else
            {
                Debug.LogWarning("[InventoryPanel] OpenInventory action not found in UI map! Inventory cannot be closed with Tab key when open. " +
                               "Please ensure 'OpenInventory' action exists in UI action map in InputActionAsset.");
            }
        }

        /// <summary>
        /// Unsubscribe from input actions
        /// </summary>
        private void UnsubscribeFromInputActions()
        {
            if (!isInputSubscribed)
                return;

            if (openInventoryAction != null)
            {
                openInventoryAction.performed -= OnInventoryInputPerformed;
            }

            isInputSubscribed = false;
            Debug.Log("[InventoryPanel] Unsubscribed from input actions");
        }

        /// <summary>
        /// Handle direct input from UI map (for closing inventory)
        /// </summary>
        private void OnInventoryInputPerformed(InputAction.CallbackContext context)
        {
            Debug.Log($"[InventoryPanel] OnInventoryInputPerformed (from UI map) - isOpen: {isOpen}");
            ToggleInventory();
        }

        /// <summary>
        /// Handle inventory toggle event from input system (from Player map via InventoryEvents)
        /// </summary>
        private void OnInventoryToggleRequested()
        {
            Debug.Log($"[InventoryPanel] OnInventoryToggleRequested (from InventoryEvents) - isOpen: {isOpen}");
            ToggleInventory();
        }

        /// <summary>
        /// Handle loot container opened event - DO NOT auto-open inventory
        /// Container opens first (state syncs to all clients), player must press Tab to open inventory
        /// </summary>
        private void OnLootContainerOpened(ILootContainer container)
        {
            if (container == null)
                return;

            Debug.Log($"[InventoryPanel] OnLootContainerOpened: {container.GetDisplayName()} - Container opened, but NOT auto-opening inventory. Player must press Tab to open inventory.");

            // Container state is now OPENED (syncIsOpened = true), all clients can see it

            // DO NOT auto-open inventory - player must press Tab manually
            // LootContainerPanel will check if container is opened when inventory opens

            // If inventory is already open, load container immediately
            if (isOpen && lootContainerPanel != null)
            {
                var lootContainer = container as NetworkLootContainer;
                if (lootContainer != null)
                {
                    lootContainerPanel.LoadContainer(lootContainer);
                    lootContainerPanel.Show();
                    SetMode(InventoryMode.Loot);
                    Debug.Log($"[InventoryPanel] Inventory already open, loaded container into LootContainerPanel");
                }
            }
            else
            {
                Debug.Log($"[InventoryPanel] Inventory not open, container will be loaded when player opens inventory (Tab key)");
            }
        }

        /// <summary>
        /// Handle shop opened event - auto-open inventory and load shop
        /// </summary>
        private void OnShopOpened(object shop)
        {
            if (shop == null)
                return;

            Debug.Log($"[InventoryPanel] OnShopOpened: {shop.GetType().Name}");

            // No need to manually call OpenShopContainer - package already handled it

            // Auto-open inventory if not already open
            if (!isOpen)
            {
                OpenInventory();
            }

            // Load shop into shop panel
            if (shopPanel != null)
            {
                // Cast to ShopContainer (specific implementation)
                var shopContainer = shop as NightHunt.InteractionSystem.Shop.ShopContainer;
                if (shopContainer != null)
                {
                    // Get shop ID (NetworkObject ID)
                    string shopId = shopContainer.NetworkObject.ObjectId.ToString();
                    shopPanel.LoadShop(shopId, shopContainer);
                    shopPanel.Show();
                    
                    // Switch to Shop mode (this will hide loot panel and equipment panel)
                    SetMode(InventoryMode.Shop);
                }
            }
        }

        /// <summary>
        /// Handle item picked up event - auto-open inventory when item is picked up
        /// </summary>
        private void OnItemPickedUp(ItemInstance item, string pickupableName)
        {
            // Only refresh inventory grid if already open (no auto-open)
            if (isOpen)
            {
                Debug.Log($"[InventoryPanel] Inventory already open, refreshing display...");
                RefreshInventoryGrid();
            }
            else
            {
                Debug.Log($"[InventoryPanel] Inventory is closed. Item picked up but not auto-opening inventory. Player can press Tab to open manually.");
            }
        }

        private void Update()
        {
            // Input handling is done via:
            // 1. InventoryEvents (from Player map via InteractionInputHandler) - for opening
            // 2. Direct UI map subscription (for closing when inventory is open)
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
            if (isOpen)
            {
                Debug.Log("[InventoryPanel] OpenInventory called but inventory is already open");
                return;
            }

            // Re-check localPlayer if null or not local
            if (localPlayer == null || (!localPlayer.IsOwner && !localPlayer.IsLocalPlayer))
            {
                Debug.LogWarning($"[InventoryPanel] localPlayer is null or not local player! " +
                    $"localPlayer={localPlayer != null}, " +
                    $"IsOwner={localPlayer?.IsOwner ?? false}, " +
                    $"IsLocalPlayer={localPlayer?.IsLocalPlayer ?? false}, " +
                    $"IsSpawned={localPlayer?.IsSpawned ?? false}");
                
                // Try to get from PlayerUIManager
                var uiManager = PlayerUIManager.Instance;
                if (uiManager != null)
                {
                    localPlayer = uiManager.GetLocalPlayer();
                    inventorySystem = uiManager.GetInventorySystem();
                    Debug.Log($"[InventoryPanel] Retrieved from PlayerUIManager: " +
                        $"localPlayer={localPlayer != null}, " +
                        $"IsOwner={localPlayer?.IsOwner ?? false}, " +
                        $"IsLocalPlayer={localPlayer?.IsLocalPlayer ?? false}");
                }
                
                // Final check - use IsOwner as primary check (more reliable in FishNet)
                if (localPlayer == null || (!localPlayer.IsOwner && !localPlayer.IsLocalPlayer))
                {
                    Debug.LogError($"[InventoryPanel] Cannot open inventory: Still not local player after retry! " +
                        $"localPlayer={localPlayer != null}, " +
                        $"IsOwner={localPlayer?.IsOwner ?? false}, " +
                        $"IsLocalPlayer={localPlayer?.IsLocalPlayer ?? false}");
                    return;
                }
            }
            
            // Ensure inventoryGrid is set before opening
            if (inventoryGrid == null && inventorySystem != null)
            {
                inventoryGrid = inventorySystem.GetGrid();
            }
            
            // If still null, try to get from ComponentRegistry
            if (inventoryGrid == null && localPlayer != null)
            {
                if (inventorySystem == null)
                {
                    inventorySystem = ComponentRegistry.GetInventoryService(localPlayer);
                }
                if (inventorySystem != null)
                {
                    inventoryGrid = inventorySystem.GetGrid();
                    Debug.Log($"[InventoryPanel] Got inventoryGrid from ComponentRegistry: {inventoryGrid != null}");
                }
            }

            Debug.Log("[InventoryPanel] Opening inventory...");
            isOpen = true;

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // Switch input state - update both InputLayerManager (global) and InputRouter (per-player)
            // IMPORTANT: This disables Player map and enables UI map
            var inputManager = InputLayerManager.Instance;
            if (inputManager != null)
            {
                bool transitioned = inputManager.TransitionToState(InputState.InventoryOpen);
                Debug.Log($"[InventoryPanel] InputLayerManager transitioned to InventoryOpen state: {transitioned}");
                
                // After state transition, UI map should be enabled
                // Subscribe to UI map action for closing inventory
                if (transitioned && !isInputSubscribed)
                {
                    SubscribeToInputActions();
                }
            }
            else
            {
                Debug.LogWarning("[InventoryPanel] InputLayerManager.Instance is null!");
            }

            // Also update InputRouter (per-player state management)
            // Use ComponentRegistry instead of GetComponent (event-based, no FindObject)
            var inputRouter = ComponentRegistry.GetInputRouter(localPlayer);
            if (inputRouter != null)
            {
                inputRouter.TransitionToState(InputState.InventoryOpen);
                Debug.Log($"[InventoryPanel] InputRouter transitioned to InventoryOpen state");
            }
            else
            {
                Debug.LogWarning("[InventoryPanel] InputRouter not found on localPlayer!");
            }

            // Refresh inventory display
            RefreshInventoryGrid();

            // Equipment panel (right side) ALWAYS shows when inventory is open
            // It's part of the main inventory UI, not a mode-specific panel
            if (equipmentPanel != null)
            {
                equipmentPanel.Show();
            }

            // Check if any container is opened and load it into LootContainerPanel
            if (lootContainerPanel != null)
            {
                lootContainerPanel.CheckAndLoadOpenedContainer();
                
                // If container was loaded, set mode to Loot, otherwise hide container panels
                if (lootContainerPanel.IsContainerLoaded())
                {
                    Debug.Log($"[InventoryPanel] OpenInventory - Container loaded, setting mode to Loot");
                    SetMode(InventoryMode.Loot);
                }
                else
                {
                    Debug.Log($"[InventoryPanel] OpenInventory - No container loaded, hiding container panels");
                    HideContainerPanels();
                }
            }
            else
            {
                // No loot container panel, hide container panels
                HideContainerPanels();
            }
        }

        /// <summary>
        /// Close inventory panel
        /// </summary>
        public void CloseInventory()
        {
            if (!isOpen)
            {
                Debug.Log("[InventoryPanel] CloseInventory called but inventory is already closed");
                return;
            }

            Debug.Log("[InventoryPanel] Closing inventory...");
            isOpen = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // Switch input state back - update both InputLayerManager (global) and InputRouter (per-player)
            var inputManager = InputLayerManager.Instance;
            if (inputManager != null)
            {
                bool transitioned = inputManager.TransitionToState(InputState.PlayerAlive);
                Debug.Log($"[InventoryPanel] InputLayerManager transitioned to PlayerAlive state: {transitioned}");
            }
            else
            {
                Debug.LogWarning("[InventoryPanel] InputLayerManager.Instance is null!");
            }

            // Also update InputRouter (per-player state management)
            // Use ComponentRegistry instead of GetComponent (event-based, no FindObject)
            var inputRouter = ComponentRegistry.GetInputRouter(localPlayer);
            if (inputRouter != null)
            {
                inputRouter.TransitionToState(InputState.PlayerAlive);
                Debug.Log($"[InventoryPanel] InputRouter transitioned to PlayerAlive state");
            }
            else
            {
                Debug.LogWarning("[InventoryPanel] InputRouter not found on localPlayer!");
            }

            // Hide loot container UI if open, but DON'T close the container itself
            // Container remains opened for re-interaction until player moves away
            if (currentMode == InventoryMode.Loot)
            {
                // Just hide the loot panel UI
                if (lootContainerPanel != null)
                {
                    lootContainerPanel.Hide();
                }
                // Script will handle panelRoot visibility internally
                // Update mode but don't trigger container close
                currentMode = null;
                Debug.Log("[InventoryPanel] Hid loot panel UI, but container remains opened for re-interaction");
            }

            // Deselect item when closing inventory
            if (selectedSlot != null)
            {
                SelectItem(null);
            }

            // Hide tooltip
            if (itemTooltip != null)
            {
                itemTooltip.HideTooltip();
            }

            // Hide nested equipment panels
            if (nestedEquipmentLeft != null)
            {
                nestedEquipmentLeft.Hide();
            }
            if (nestedEquipmentRight != null)
            {
                nestedEquipmentRight.Hide();
            }
        }

        /// <summary>
        /// Set inventory mode (Equipment, Loot, or Shop)
        /// NOTE: Equipment panel (right side) ALWAYS shows when inventory is open
        /// Only shop/loot container toggles based on mode
        /// </summary>
        /// <summary>
        /// Set inventory mode (Loot or Shop)
        /// Equipment panel always shows regardless of mode
        /// </summary>
        public void SetMode(InventoryMode mode)
        {
            // Deselect item when switching modes (to avoid confusion)
            if (currentMode != mode && selectedSlot != null)
            {
                SelectItem(null);
            }

            currentMode = mode;

            // Equipment panel (right side) ALWAYS shows when inventory is open
            // It's part of the main inventory UI, not a mode-specific panel
            if (equipmentPanel != null && isOpen)
            {
                equipmentPanel.Show();
            }

            switch (mode)
            {
                case InventoryMode.Loot:
                    // Show loot container, hide shop
                    if (lootContainerPanel != null)
                    {
                        lootContainerPanel.Show();
                    }
                    if (shopPanel != null)
                    {
                        shopPanel.Hide();
                    }
                    break;

                case InventoryMode.Shop:
                    // Show shop, hide loot
                    if (shopPanel != null)
                    {
                        shopPanel.Show();
                    }
                    if (lootContainerPanel != null)
                    {
                        lootContainerPanel.Hide();
                    }
                    break;
            }
        }

        /// <summary>
        /// Hide both loot and shop panels (no mode active, only equipment panel visible)
        /// </summary>
        public void HideContainerPanels()
        {
            if (lootContainerPanel != null)
            {
                lootContainerPanel.Hide();
            }
            if (shopPanel != null)
            {
                shopPanel.Hide();
            }
            currentMode = null;
        }

        /// <summary>
        /// Check if selected slot is an equipped item
        /// </summary>
        private bool IsEquippedItem(ItemCell slot)
        {
            if (slot == null) return false;

            // Check if slot is in equipment panel
            if (equipmentPanel != null)
            {
                // Check if slot is part of equipment slots
                var equipmentSlots = equipmentPanel.GetEquipmentSlots();
                if (equipmentSlots != null)
                {
                    foreach (var eqSlot in equipmentSlots)
                    {
                        if (eqSlot != null && eqSlot == slot)
                        {
                            return true;
                        }
                    }
                }

                // Check if slot is part of weapon slots
                var weaponSlots = equipmentPanel.GetWeaponSlots();
                if (weaponSlots != null)
                {
                    foreach (var wpSlot in weaponSlots)
                    {
                        if (wpSlot != null && wpSlot == slot)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Show nested equipment panel based on item location
        /// </summary>
        private void ShowNestedEquipmentPanel(InventorySlot slotData, bool isEquippedItem)
        {
            if (slotData == null || slotData.IsEmpty)
            {
                // Hide both panels if no item
                if (nestedEquipmentLeft != null) nestedEquipmentLeft.Hide();
                if (nestedEquipmentRight != null) nestedEquipmentRight.Hide();
                return;
            }

            // Get item config to check for equipment slots
            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);

            if (itemData == null)
            {
                // Hide both panels if item config not found
                if (nestedEquipmentLeft != null) nestedEquipmentLeft.Hide();
                if (nestedEquipmentRight != null) nestedEquipmentRight.Hide();
                return;
            }

            // Check if item has equipment slots
            // TODO: When ItemDataBase is extended with equipmentSlots property, use it directly
            // Note: ItemDataBase doesn't have equipmentSlots property yet
            // For now, assume no equipment slots until ItemDataBase is extended
            bool hasEquipmentSlots = false;
            // var equipmentSlots = GetItemEquipmentSlots(itemData);

            if (!hasEquipmentSlots)
            {
                // Hide both panels if item doesn't have equipment slots
                if (nestedEquipmentLeft != null) nestedEquipmentLeft.Hide();
                if (nestedEquipmentRight != null) nestedEquipmentRight.Hide();
                return;
            }

            // Show appropriate panel based on item location
            if (isEquippedItem)
            {
                // Show right panel for equipped items
                if (nestedEquipmentRight != null)
                {
                    nestedEquipmentRight.ShowForItem(slotData);
                }
                if (nestedEquipmentLeft != null)
                {
                    nestedEquipmentLeft.Hide();
                }
            }
            else
            {
                // Show left panel for inventory items
                if (nestedEquipmentLeft != null)
                {
                    nestedEquipmentLeft.ShowForItem(slotData);
                }
                if (nestedEquipmentRight != null)
                {
                    nestedEquipmentRight.Hide();
                }
            }
        }

        /// <summary>
        /// Get equipment slots from item config
        /// </summary>
        private List<string> GetItemEquipmentSlots(ItemConfigData itemConfig)
        {
            if (itemConfig == null) return null;

            // Check if item has equipment slots defined
            if (itemConfig.equipmentSlots != null && itemConfig.equipmentSlots.Count > 0)
            {
                // Return list of equipment slot IDs
                List<string> slotIds = new List<string>();
                foreach (var slotConfig in itemConfig.equipmentSlots)
                {
                    if (slotConfig != null && !string.IsNullOrEmpty(slotConfig.slotId))
                    {
                        slotIds.Add(slotConfig.slotId);
                    }
                }
                return slotIds.Count > 0 ? slotIds : null;
            }

            return null;
        }

        /// <summary>
        /// Refresh inventory grid display
        /// </summary>
        /// <summary>
        /// Get slot index from ItemCell (for network events)
        /// </summary>
        public int GetSlotIndex(ItemCell cell)
        {
            if (cell == null)
                return -1;
            return slotUIs.IndexOf(cell);
        }

        /// <summary>
        /// Get grid position from cell index in list (for events)
        /// </summary>
        public (int x, int y) GetGridPositionFromIndex(int index)
        {
            int uiGridWidth = 4; // Default grid width
            return (index % uiGridWidth, index / uiGridWidth);
        }

        public void RefreshInventoryGrid()
        {
            // Try to refresh inventoryGrid reference if null
            if (inventoryGrid == null)
            {
                // Try to get from inventorySystem
                if (inventorySystem != null)
                {
                    inventoryGrid = inventorySystem.GetGrid();
                }
                
                // If still null, try to get inventorySystem from localPlayer
                if (inventoryGrid == null && localPlayer != null)
                {
                    inventorySystem = ComponentRegistry.GetInventoryService(localPlayer);
                    if (inventorySystem != null)
                    {
                        inventoryGrid = inventorySystem.GetGrid();
                    }
                }
                
                // If still null, try to get from PlayerUIManager
                if (inventoryGrid == null)
                {
                    var uiManager = PlayerUIManager.Instance;
                    if (uiManager != null)
                    {
                        localPlayer = uiManager.GetLocalPlayer();
                        inventorySystem = uiManager.GetInventorySystem();
                        if (inventorySystem != null)
                        {
                            inventoryGrid = inventorySystem.GetGrid();
                        }
                    }
                }
            }
            
            if (inventoryGrid == null)
            {
                Debug.LogError("[InventoryPanel] RefreshInventoryGrid: inventoryGrid is still NULL after refresh attempts! Cannot refresh UI.");
                Debug.LogError("[InventoryPanel] Please ensure InventoryPanel.Initialize() was called with valid InventoryService.");
                return;
            }
            
            if (inventoryGridParent == null)
            {
                Debug.LogError("[InventoryPanel] RefreshInventoryGrid: inventoryGridParent is NULL! Cannot refresh UI.");
                return;
            }

            // Get all items from inventory (not based on grid position)
            var allItems = inventoryGrid.Items;
            
            // Calculate how many slots we need
            int defaultSlotCount = uiGridWidth * uiGridHeight;
            int totalSlotsNeeded = Mathf.Max(defaultSlotCount, allItems?.Count ?? 0);
            
            // Clear existing slots
            foreach (var slotUI in slotUIs)
            {
                if (slotUI != null)
                {
                    Destroy(slotUI.gameObject);
                }
            }
            slotUIs.Clear();

            if (inventorySlotPrefab == null)
            {
                Debug.LogError("[InventoryPanel] inventorySlotPrefab is NULL! Cannot create slots.");
                return;
            }

            int itemIndex = 0;
            int slotCreated = 0;
            
            // Create slots: first create default grid (4x3), then add extra if needed
            for (int slotIndex = 0; slotIndex < totalSlotsNeeded; slotIndex++)
            {
                int x = slotIndex % uiGridWidth;
                int y = slotIndex / uiGridWidth;
                
                // Create wrapper InventorySlot for UI compatibility
                InventorySlot slot = new InventorySlot();
                
                // Fill slot with item if available
                if (allItems != null && itemIndex < allItems.Count)
                {
                    var item = allItems[itemIndex];
                    
                    // Load ItemDataBase directly from ItemDataRegistry
                    var itemData = GetItemDataFromRegistry(item.itemDataId);
                    
                    if (itemData != null)
                    {
                        slot.SetItem(itemData, item.quantity);
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryPanel] Item NOT FOUND in ItemDataRegistry: {item.itemDataId}");
                    }
                    
                    itemIndex++;
                }

                // Create slot UI
                GameObject slotObj = Instantiate(inventorySlotPrefab, inventoryGridParent);
                slotObj.SetActive(true); // Ensure prefab is active even if it was disabled
                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Initialize with ItemCellLocation.Inventory and index in list
                slotUI.Initialize(slot, this, ItemCellLocation.Inventory, slotUIs.Count);
                slotUIs.Add(slotUI);
            }
            
            // Auto-select first item if available
            if (slotUIs.Count > 0)
            {
                // Find first slot with item
                ItemCell firstItemSlot = null;
                foreach (var slotUI in slotUIs)
                {
                    if (slotUI != null && !slotUI.IsEmpty())
                    {
                        firstItemSlot = slotUI;
                        break;
                    }
                }
                
                // REMOVED: Auto-select first item - user requested to disable this
                // No longer auto-select first item when opening inventory
                if (itemTooltip != null)
                {
                    itemTooltip.HideTooltip();
                }
            }
            else
            {
                // No slots created - hide tooltip
                if (itemTooltip != null)
                {
                    itemTooltip.HideTooltip();
                    Debug.Log("[InventoryPanel] No slots created - hiding tooltip");
                }
            }
        }

        /// <summary>
        /// Select item slot (can be null to deselect)
        /// </summary>
        public void SelectItem(ItemCell slot)
        {
            // Deselect previous
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
                // Hide tooltip for previous selection
                if (itemTooltip != null)
                {
                    itemTooltip.HideTooltip();
                }
                // Hide nested equipment panels for previous selection
                if (nestedEquipmentLeft != null)
                {
                    nestedEquipmentLeft.Hide();
                }
                if (nestedEquipmentRight != null)
                {
                    nestedEquipmentRight.Hide();
                }
            }

            // Select new
            selectedSlot = slot;
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(true);
                
                var slotData = selectedSlot.GetSlot();
                if (slotData != null && !slotData.IsEmpty)
                {
                    // Show tooltip for selected item
                    if (itemTooltip != null)
                    {
                        // Get mouse position for tooltip positioning
                        Vector2 mousePos = Vector2.zero;
                        if (UnityEngine.InputSystem.Mouse.current != null)
                        {
                            mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                        }
                        itemTooltip.ShowTooltip(slotData, mousePos);
                    }

                    // Show nested equipment panel if item has equipment slots
                    // Determine which panel to show based on where item is located
                    bool isEquippedItem = IsEquippedItem(selectedSlot);
                    ShowNestedEquipmentPanel(slotData, isEquippedItem);
                }
            }
            else
            {
                // Deselect - hide tooltip and nested equipment panels
                if (itemTooltip != null)
                {
                    itemTooltip.HideTooltip();
                }
                if (nestedEquipmentLeft != null)
                {
                    nestedEquipmentLeft.Hide();
                }
                if (nestedEquipmentRight != null)
                {
                    nestedEquipmentRight.Hide();
                }
            }
        }

        /// <summary>
        /// Get ItemTooltip reference (for InventorySlotUI)
        /// </summary>
        public ItemTooltip GetItemTooltip()
        {
            return itemTooltip;
        }

        /// <summary>
        /// Get LootContainerPanel reference (for DragDropHandler)
        /// </summary>
        public LootContainerPanel GetLootContainerPanel()
        {
            return lootContainerPanel;
        }

        /// <summary>
        /// Get ShopPanel reference
        /// </summary>
        public ShopPanel GetShopPanel()
        {
            return shopPanel;
        }

        /// <summary>
        /// Handle slot right click
        /// </summary>
        public void HandleSlotRightClick(ItemCell slot)
        {
            if (slot == null || slot.IsEmpty())
                return;

            // TODO: Implement right click actions (split stack, drop, etc.)
            var slotData = slot.GetSlot();
            if (slotData != null && !slotData.IsEmpty)
            {
                // Right click actions (split stack, drop, etc.)
                UseItem(slotData);
            }
        }

        /// <summary>
        /// Handle slot double click - split logic cho inventory open vs HUD/home
        /// </summary>
        public void HandleSlotDoubleClick(ItemCell slot)
        {
            if (slot == null || slot.IsEmpty())
                return;

            var slotData = slot.GetSlot();
            if (slotData == null || slotData.IsEmpty)
                return;

            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null)
                return;

            bool isInventoryOpen = isOpen;
            ItemCellLocation location = slot.GetLocation();

            if (isInventoryOpen)
            {
                // Inventory panel is open - handle equip/use logic
                HandleDoubleClickInInventory(slot, itemData, location);
            }
            else
            {
                // HUD/Home - inventory closed - handle use/reload logic
                HandleDoubleClickInHUD(slot, itemData, location);
            }
        }
        

        /// <summary>
        /// Use item based on item type - fires UI event instead of direct call
        /// </summary>
        private void UseItem(InventorySlot slotData)
        {
            if (slotData == null || slotData.IsEmpty)
                return;

            var item = slotData.Item;
            if (item == null)
                return;

            // Fire UI event - Logic layer will handle it
            InventoryUIEvents.RequestUseItem(item.ItemId);
        }

        /// <summary>
        /// Get ItemDataBase from ItemDataRegistry
        /// </summary>
        private NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase GetItemDataFromRegistry(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            if (registry != null)
            {
                return registry.GetById(itemId);
            }

            return null;
        }

        /// <summary>
        /// Handle double click in inventory panel (open)
        /// Auto-equip weapon/equipment/attachment, use consumable
        /// </summary>
        private void HandleDoubleClickInInventory(ItemCell slot, NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData, ItemCellLocation location)
        {
            var category = itemData.Category;

            switch (category)
            {
                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Consumable:
                    if (itemData.IsConsumable)
                    {
                        Debug.Log($"[InventoryPanel] TODO: Implement consumable usage for {itemData.ItemId}");
                        InventoryUIEvents.RequestUseItem(itemData.ItemId);
                    }
                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon:
                    AutoEquipWeapon(slot);
                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor:
                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet:
                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack:
                    AutoEquipEquipment(slot, category);
                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Attachment:
                    AutoEquipAttachment(slot);
                    break;

                case NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Misc:
                    Debug.Log($"[InventoryPanel] TODO: Implement throwable/other misc item usage for {itemData.ItemId}");
                    break;
            }
        }

        /// <summary>
        /// Handle double click in HUD/Home (inventory closed)
        /// Use consumable/throwable, reload weapon từ quick/weapon slots
        /// </summary>
        private void HandleDoubleClickInHUD(ItemCell slot, NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData, ItemCellLocation location)
        {
            var category = itemData.Category;

            switch (location)
            {
                case ItemCellLocation.QuickSlot:
                    if (itemData.IsConsumable)
                    {
                        Debug.Log($"[InventoryPanel] TODO: Implement consumable usage from quick slot for {itemData.ItemId}");
                        InventoryUIEvents.RequestUseItem(itemData.ItemId);
                    }
                    else if (category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Misc)
                    {
                        Debug.Log($"[InventoryPanel] TODO: Implement throwable usage from quick slot for {itemData.ItemId}");
                        InventoryUIEvents.RequestUseItem(itemData.ItemId);
                    }
                    break;

                case ItemCellLocation.Weapon:
                    Debug.Log($"[InventoryPanel] TODO: Implement weapon reload for {itemData.ItemId}");
                    break;
            }
        }

        /// <summary>
        /// Auto-equip weapon: Find empty weapon slot hoặc replace first weapon
        /// </summary>
        private void AutoEquipWeapon(ItemCell slot)
        {
            var equipmentPanel = GetEquipmentPanel();
            if (equipmentPanel == null)
            {
                Debug.LogWarning("[InventoryPanel] AutoEquipWeapon: EquipmentPanel is null!");
                return;
            }

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

            // No empty slot - replace first weapon
            EquipWeapon(slot, 0);
        }

        /// <summary>
        /// Auto-equip equipment: Find empty equipment slot hoặc replace matching equipment
        /// </summary>
        private void AutoEquipEquipment(ItemCell slot, NightHunt.InteractionSystem.Core.Abstractions.ItemCategory category)
        {
            var equipmentPanel = GetEquipmentPanel();
            if (equipmentPanel == null)
            {
                Debug.LogWarning("[InventoryPanel] AutoEquipEquipment: EquipmentPanel is null!");
                return;
            }

            EquipmentSlotType slotType = GetEquipmentSlotTypeFromCategory(category);
            var equipmentSlot = equipmentPanel.GetEquipmentSlot(slotType);
            if (equipmentSlot != null)
            {
                EquipItem(slot, equipmentSlot);
            }
        }

        /// <summary>
        /// Auto-equip attachment: Find compatible attachment slot
        /// </summary>
        private void AutoEquipAttachment(ItemCell slot)
        {
            Debug.Log($"[InventoryPanel] TODO: Implement AutoEquipAttachment for {slot.GetSlot().Item.ItemId}");
        }

        /// <summary>
        /// Get EquipmentSlotType from ItemCategory
        /// </summary>
        private EquipmentSlotType GetEquipmentSlotTypeFromCategory(NightHunt.InteractionSystem.Core.Abstractions.ItemCategory category)
        {
            return category switch
            {
                NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor => EquipmentSlotType.Armor,
                NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet => EquipmentSlotType.Helmet,
                NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack => EquipmentSlotType.Backpack,
                _ => EquipmentSlotType.Armor
            };
        }

        // Drag & Drop handlers
        public void StartDrag(ItemCell slot, PointerEventData eventData)
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

        public void EndDrag(ItemCell sourceSlot, PointerEventData eventData)
        {
            if (dragDropHandler != null)
            {
                dragDropHandler.EndDrag(sourceSlot, eventData);
            }
        }

        /// <summary>
        /// Move item between slots
        /// </summary>
        /// <summary>
        /// Move or swap item between inventory slots
        /// If target slot has item, swap them; otherwise just move
        /// </summary>
        public void MoveItem(ItemCell from, ItemCell to)
        {
            if (from == null || to == null || from.IsEmpty())
                return;

            var fromSlot = from.GetSlot();
            if (fromSlot == null || fromSlot.IsEmpty)
                return;

            // Get indices in list (logic chỉ cần trong List/Array)
            int fromIndex = slotUIs.IndexOf(from);
            int toIndex = slotUIs.IndexOf(to);

            // Only move if both are valid inventory cells
            if (fromIndex >= 0 && toIndex >= 0 && from.GetLocation() == ItemCellLocation.Inventory && to.GetLocation() == ItemCellLocation.Inventory)
            {
                var (fromX, fromY) = GetGridPositionFromIndex(fromIndex);
                var (toX, toY) = GetGridPositionFromIndex(toIndex);
                var toSlot = to.GetSlot();
                
                // Check if target slot has item - if yes, swap; if no, just move
                if (toSlot != null && !toSlot.IsEmpty)
                {
                    // SWAP: Both slots have items - swap their positions
                    Debug.Log($"[InventoryPanel] Swapping items: {fromSlot.Item.ItemId} (at {fromX},{fromY}) <-> {toSlot.Item.ItemId} (at {toX},{toY})");
                    InventoryUIEvents.RequestSwapItems(fromSlot.Item.ItemId, fromX, fromY, toSlot.Item.ItemId, toX, toY);
                }
                else
                {
                    // MOVE: Target is empty - just move item
                    Debug.Log($"[InventoryPanel] Moving item: {fromSlot.Item.ItemId} from ({fromX},{fromY}) to ({toX},{toY})");
                    InventoryUIEvents.RequestMoveItem(fromSlot.Item.ItemId, fromX, fromY, toX, toY);
                }
            }
        }

        /// <summary>
        /// Swap items between two inventory slots
        /// </summary>
        public void SwapItems(ItemCell from, ItemCell to)
        {
            if (from == null || to == null || from.IsEmpty() || to.IsEmpty())
                return;

            var fromSlot = from.GetSlot();
            var toSlot = to.GetSlot();
            
            if (fromSlot == null || fromSlot.IsEmpty || toSlot == null || toSlot.IsEmpty)
                return;

            // Get indices in list (logic chỉ cần trong List/Array)
            int fromIndex = slotUIs.IndexOf(from);
            int toIndex = slotUIs.IndexOf(to);
            
            if (fromIndex < 0 || toIndex < 0 || from.GetLocation() != ItemCellLocation.Inventory || to.GetLocation() != ItemCellLocation.Inventory)
                return;

            var (fromX, fromY) = GetGridPositionFromIndex(fromIndex);
            var (toX, toY) = GetGridPositionFromIndex(toIndex);
            {
                Debug.Log($"[InventoryPanel] SwapItems: {fromSlot.Item.ItemId} <-> {toSlot.Item.ItemId}");
                InventoryUIEvents.RequestSwapItems(fromSlot.Item.ItemId, fromX, fromY, toSlot.Item.ItemId, toX, toY);
            }
        }

        /// <summary>
        /// Assign item to quick slot (only consumable items like medkit, grenade, etc.)
        /// </summary>
        public void AssignQuickSlot(ItemCell slot, int quickSlotIndex)
        {
            if (slot == null || slot.IsEmpty())
                return;

            var slotData = slot.GetSlot();
            if (slotData == null || slotData.IsEmpty)
                return;

            // Get item data to validate item type
            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot assign item {slotData.Item.ItemId} to quick slot - item data not found");
                return;
            }

            // Quick slot is only for consumable items (medkit, grenade, etc.)
            // Check if item is consumable using ItemDataBase.IsConsumable
            if (!itemData.IsConsumable)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot assign {slotData.Item.ItemId} to quick slot - only consumable items can be assigned to quick slots!");
                // TODO: Show error message to user (e.g., via UI notification)
                return;
            }

            // Fire UI event - Logic layer will handle it
            InventoryUIEvents.RequestAssignQuickSlot(slotData.Item.ItemId, quickSlotIndex);
        }

        /// <summary>
        /// Equip weapon to weapon slot (not equipment slot)
        /// </summary>
        public void EquipWeapon(ItemCell slot, int weaponSlotIndex)
        {
            if (slot == null || slot.IsEmpty())
                return;

            var slotData = slot.GetSlot();
            if (slotData == null || slotData.IsEmpty)
                return;

            // Get item data to validate item type
            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot equip weapon {slotData.Item.ItemId} - item data not found");
                return;
            }

            // Weapon slot is only for weapons
            // Check if item is a weapon by checking Category
            bool isWeapon = false;
            if (itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon)
            {
                isWeapon = true;
                Debug.Log($"[InventoryPanel] Item {slotData.Item.ItemId} is a weapon (Category: Weapon)");
            }
            else
            {
                // TODO: Check equipment slots when ItemDataBase is extended with equipmentSlots property
                // Fallback: Check if item has weapon-related slotId in equipmentSlots (e.g., "Weapon", "Primary", "Secondary")
                // if (itemData.equipmentSlots != null)
                // {
                //     foreach (var slotConfig in itemData.equipmentSlots)
                //     {
                //         if (slotConfig != null)
                //         {
                //             string slotId = slotConfig.slotId?.ToLower() ?? "";
                //             if (slotId == "weapon" || slotId == "primary" || slotId == "secondary")
                //             {
                //                 isWeapon = true;
                //                 Debug.Log($"[InventoryPanel] Item {slotData.Item.ItemId} is a weapon (has weapon slotId: {slotConfig.slotId})");
                //                 break;
                //             }
                //         }
                //     }
                // }
            }
            
            if (!isWeapon)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot equip {slotData.Item.ItemId} to weapon slot - only weapons can be equipped to weapon slots!");
                // TODO: Show error message to user (e.g., via UI notification)
                return;
            }

            // Check if weapon slot already has weapon - if yes, swap
            var equipmentPanel = GetEquipmentPanel();
            if (equipmentPanel != null)
            {
                var weaponSlotUI = equipmentPanel.GetWeaponSlot(weaponSlotIndex);
                if (weaponSlotUI != null && !weaponSlotUI.IsEmpty())
                {
                    // SWAP: Weapon slot has weapon - swap them
                    var equippedWeaponSlot = weaponSlotUI.GetSlot();
                    if (equippedWeaponSlot != null && !equippedWeaponSlot.IsEmpty)
                    {
                        int slotIndex = slotUIs.IndexOf(slot);
                        var (inventoryX, inventoryY) = slotIndex >= 0 ? GetGridPositionFromIndex(slotIndex) : (0, 0);
                        var equippedWeaponId = equippedWeaponSlot.Item.ItemId;
                        var equippedWeaponQuantity = equippedWeaponSlot.Quantity;
                        
                        // Both are weapons - can swap
                        Debug.Log($"[InventoryPanel] Swapping weapons: {slotData.Item.ItemId} (inventory at {inventoryX},{inventoryY}) <-> {equippedWeaponId} (equipped)");
                        
                        // Step 1: Remove new weapon from inventory (temporarily)
                        // Step 2: Add old weapon to inventory at new weapon's position
                        // Step 3: Equip new weapon
                        
                        if (inventorySystem != null)
                        {
                            // Get item data for old weapon to add it back
                            var oldWeaponData = GetItemDataFromRegistry(equippedWeaponId);
                            
                            if (oldWeaponData != null)
                            {
                                // Move new weapon out first (will be equipped)
                                // Then add old weapon to that position
                                // Then equip new weapon
                                
                                // For swap: We need to:
                                // 1. Remove new weapon from inventory (it will be equipped)
                                // 2. Add old weapon to inventory at new weapon's position
                                // 3. Equip new weapon
                                
                                // Since EquipWeapon will handle removing from inventory,
                                // we just need to add old weapon to the position first
                                // But we can't add if slot is occupied...
                                
                                // Better approach: 
                                // 1. Move new weapon to a temp position (or just let EquipWeapon handle it)
                                // 2. Add old weapon to new weapon's original position
                                // 3. Equip new weapon
                                
                                // Actually, simplest: 
                                // 1. Equip new weapon (removes it from inventory)
                                // 2. Add old weapon to the now-empty slot
                                
                                bool equipSuccess = inventorySystem.EquipWeapon(weaponSlotIndex, slotData.Item.ItemId);
                                if (equipSuccess)
                                {
                                    // Now add old weapon to inventory at the position where new weapon was
                                    Debug.Log($"[InventoryPanel] Weapon swap: Equipped {slotData.Item.ItemId}, adding old weapon {equippedWeaponId} to inventory slot ({inventoryX}, {inventoryY})");
                                    // Add old weapon to inventory at the position
                                    InventoryUIEvents.RequestMoveItemToSlot(equippedWeaponId, inventoryX, inventoryY);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // EQUIP: Weapon slot is empty - just equip
                    Debug.Log($"[InventoryPanel] Equipping weapon {slotData.Item.ItemId} to weapon slot {weaponSlotIndex}");
                    if (inventorySystem != null)
                    {
                        bool equipSuccess = inventorySystem.EquipWeapon(weaponSlotIndex, slotData.Item.ItemId);
                        if (!equipSuccess)
                        {
                            Debug.LogWarning($"[InventoryPanel] EquipWeapon failed for {slotData.Item.ItemId} to slot {weaponSlotIndex}");
                        }
                        else
                        {
                            Debug.Log($"[InventoryPanel] EquipWeapon succeeded for {slotData.Item.ItemId} to slot {weaponSlotIndex}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryPanel] EquipWeapon: inventorySystem is null!");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[InventoryPanel] EquipWeapon: equipmentPanel is null!");
            }
            
            // Refresh inventory after equip to sync with server
            StartCoroutine(RefreshInventoryAfterMove());
        }

        /// <summary>
        /// Swap weapon: Equip new weapon and move old weapon to inventory
        /// </summary>
        public void SwapWeapon(ItemCell slot, int weaponSlotIndex)
        {
            // Same logic as EquipWeapon but explicitly for swap
            EquipWeapon(slot, weaponSlotIndex);
        }

        /// <summary>
        /// Swap equipment: Equip new equipment and move old equipment to inventory
        /// </summary>
        public void SwapEquipment(ItemCell slot, ItemCell equipmentSlot)
        {
            // Same logic as EquipItem but explicitly for swap
            EquipItem(slot, equipmentSlot);
        }

        /// <summary>
        /// Move item from inventory to container
        /// </summary>
        public void MoveItemToContainer(ItemCell sourceCell, ItemCell targetCell)
        {
            if (sourceCell == null || targetCell == null || sourceCell.IsEmpty())
                return;

            var slotData = sourceCell.GetSlot();
            if (slotData == null || slotData.IsEmpty)
                return;

            var lootPanel = GetLootContainerPanel();
            if (lootPanel == null || !lootPanel.IsContainerLoaded())
            {
                Debug.LogWarning("[InventoryPanel] MoveItemToContainer: LootContainerPanel is null or container not loaded");
                return;
            }

            // Get grid position from source cell index
            int sourceIndex = slotUIs.IndexOf(sourceCell);
            var (fromX, fromY) = sourceIndex >= 0 ? GetGridPositionFromIndex(sourceIndex) : (0, 0);

            // Fire UI event - Logic layer will handle it
            lootPanel.MoveItemToContainer(slotData.Item.ItemId, fromX, fromY);
        }

        /// <summary>
        /// Move item from container to inventory
        /// </summary>
        public void MoveItemFromContainer(ItemCell sourceCell, ItemCell targetCell)
        {
            if (sourceCell == null || targetCell == null || sourceCell.IsEmpty())
                return;

            var slotData = sourceCell.GetSlot();
            if (slotData == null || slotData.IsEmpty)
                return;

            var lootPanel = GetLootContainerPanel();
            if (lootPanel == null || !lootPanel.IsContainerLoaded())
            {
                Debug.LogWarning("[InventoryPanel] MoveItemFromContainer: LootContainerPanel is null or container not loaded");
                return;
            }

            // Get grid position from target cell index
            int targetIndex = slotUIs.IndexOf(targetCell);
            var (toX, toY) = targetIndex >= 0 ? GetGridPositionFromIndex(targetIndex) : (0, 0);

            // Fire UI event - Logic layer will handle it
            lootPanel.MoveItemFromContainer(slotData.Item.ItemId, toX, toY);
            
            // Refresh inventory after move to sync with server
            StartCoroutine(RefreshInventoryAfterMove());
        }
        
        /// <summary>
        /// Refresh inventory after move operation (wait for server sync)
        /// </summary>
        private IEnumerator RefreshInventoryAfterMove()
        {
            yield return new WaitForSeconds(0.2f); // Wait for server RPC to process
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Check if item can be equipped to slot type
        /// Equipment slots are for equipment items (helmet, armor, etc.) - NOT weapons
        /// Weapons go to weapon slots, not equipment slots
        /// </summary>
        private bool CanEquipItemToSlot(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData, EquipmentSlotType slotType)
        {
            if (itemData == null)
                return false;

            // Equipment slots are NOT for weapons - weapons have their own weapon slots
            // Check if item is a weapon by checking Category
            if (itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot equip weapon {itemData.ItemId} to equipment slot {slotType} - weapons must go to weapon slots!");
                return false;
            }

            // Equipment slots are for equipment items (helmet, armor, backpack, etc.)
            // Check Category to see if it's an equipment type
            var category = itemData.Category;
            bool isEquipment = category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor ||
                              category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet ||
                              category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack;

            if (!isEquipment)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot equip {itemData.ItemId} (Category: {category}) to equipment slot {slotType} - only equipment items can be equipped!");
                return false;
            }

            // TODO: When ItemDataBase is extended with equipmentSlots, check if item can be equipped to this slot type
            // For now, allow if it's an equipment category
            return true;
        }

        /// <summary>
        /// Equip item to equipment slot (with swap support)
        /// If equipment slot already has item, swap them
        /// </summary>
        public void EquipItem(ItemCell slot, ItemCell equipmentSlot)
        {
            if (slot == null || slot.IsEmpty() || equipmentSlot == null)
                return;

            var slotData = slot.GetSlot();
            if (slotData == null || slotData.IsEmpty)
                return;

            // Get item data
            var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[InventoryPanel] Cannot equip item {slotData.Item.ItemId} - item data not found");
                return;
            }

            // Validate item can be equipped to this slot type
            EquipmentSlotType slotType = equipmentSlot.GetEquipmentSlotType();
            if (!CanEquipItemToSlot(itemData, slotType))
            {
                Debug.LogWarning($"[InventoryPanel] Cannot equip {slotData.Item.ItemId} to {slotType} slot - item is not compatible with this slot type!");
                return;
            }

            // Check if equipment slot already has item - if yes, swap
            var equippedSlot = equipmentSlot.GetSlot();
            if (equippedSlot != null && !equippedSlot.IsEmpty)
            {
                // SWAP: Equipment slot has item - swap them
                int slotIndex = slotUIs.IndexOf(slot);
                var (inventoryX, inventoryY) = slotIndex >= 0 ? GetGridPositionFromIndex(slotIndex) : (0, 0);
                
                // Validate items are same type for swap
                if (AreItemsCompatibleForSwap(slotData.Item, equippedSlot.Item, slotType))
                {
                    Debug.Log($"[InventoryPanel] Swapping equipment: {slotData.Item.ItemId} (inventory) <-> {equippedSlot.Item.ItemId} (equipped)");
                    // Unequip current item to inventory slot position
                    InventoryUIEvents.RequestUnequipItem(equippedSlot.Item.ItemId, slotType);
                    // Equip new item
                    InventoryUIEvents.RequestEquipItem(slotData.Item.ItemId, slotType);
                    // Move unequipped item to inventory slot
                    InventoryUIEvents.RequestMoveItemToSlot(equippedSlot.Item.ItemId, inventoryX, inventoryY);
                }
                else
                {
                    Debug.LogWarning($"[InventoryPanel] Cannot swap - items are not compatible: {slotData.Item.ItemId} and {equippedSlot.Item.ItemId}");
                }
            }
            else
            {
                // EQUIP: Equipment slot is empty - just equip
                Debug.Log($"[InventoryPanel] Equipping item: {slotData.Item.ItemId} to {slotType} slot");
                InventoryUIEvents.RequestEquipItem(slotData.Item.ItemId, slotType);
            }
            
            RefreshInventoryGrid();
        }

        /// <summary>
        /// Check if two items are compatible for swap (same type: weapon with weapon, vest with vest, etc.)
        /// </summary>
        private bool AreItemsCompatibleForSwap(InventoryItem item1, InventoryItem item2, EquipmentSlotType slotType)
        {
            if (item1 == null || item2 == null)
                return false;

            // Same item ID - always compatible
            if (item1.ItemId == item2.ItemId)
                return true;

            // Get item configs
            var itemData1 = GetItemDataFromRegistry(item1.ItemId);
            var itemData2 = GetItemDataFromRegistry(item2.ItemId);

            if (itemData1 == null || itemData2 == null)
                return false;

            // Check if both items can be equipped to the same slot type
            // This ensures they are the same category (vest with vest, armor with armor, etc.)
            bool item1CanEquip = CanEquipItemToSlot(itemData1, slotType);
            bool item2CanEquip = CanEquipItemToSlot(itemData2, slotType);

            return item1CanEquip && item2CanEquip;
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
        public void MoveFromQuickSlotToInventory(int quickSlotIndex, ItemCell targetSlot)
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
            int targetIndex = slotUIs.IndexOf(targetSlot);
            var (toX, toY) = targetIndex >= 0 ? GetGridPositionFromIndex(targetIndex) : (0, 0);
            if (toX >= 0 && toY >= 0)
            {
                // Find empty slot or swap
                var grid = inventorySystem.GetGrid();
                if (grid == null) return;
                
                var targetItemInstance = grid.GetItemAt(toX, toY);
                
                Debug.LogWarning("[InventoryPanel] MoveFromQuickSlotToInventory: Quick slot system not yet implemented");
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Unequip weapon to inventory
        /// </summary>
        public void UnequipWeaponToInventory(int weaponSlotIndex, ItemCell targetSlot)
        {
            if (inventorySystem == null || targetSlot == null)
                return;

            // TODO: Get equipped weapon from combat system
            int targetIndex = slotUIs.IndexOf(targetSlot);
            var (toX, toY) = targetIndex >= 0 ? GetGridPositionFromIndex(targetIndex) : (0, 0);
            if (toX >= 0 && toY >= 0)
            {
                // Similar logic to MoveFromQuickSlotToInventory
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Unequip item to inventory
        /// </summary>
        public void UnequipItemToInventory(ItemCell sourceSlot, ItemCell targetSlot)
        {
            if (sourceSlot == null || targetSlot == null || inventorySystem == null)
                return;

            var slot = sourceSlot.GetSlot();
            if (slot == null || slot.IsEmpty)
                return;

            // Move equipment item to inventory
            int targetIndex = slotUIs.IndexOf(targetSlot);
            var (toX, toY) = targetIndex >= 0 ? GetGridPositionFromIndex(targetIndex) : (0, 0);
            if (toX >= 0 && toY >= 0)
            {
                var grid = inventorySystem.GetGrid();
                if (grid == null) return;
                
                var targetItemInstance = grid.GetItemAt(toX, toY);
                
                Debug.LogWarning("[InventoryPanel] UnequipItemToInventory: Equipment system not yet fully implemented");
                RefreshInventoryGrid();
            }
        }

        /// <summary>
        /// Check if inventory is open
        /// </summary>
        public bool IsOpen() => isOpen;

        /// <summary>
        /// Get current mode (null if no mode active, only equipment panel visible)
        /// </summary>
        public InventoryMode? GetMode() => currentMode;

        /// <summary>
        /// Get equipment panel
        /// </summary>
        public EquipmentPanel GetEquipmentPanel() => equipmentPanel;

        /// <summary>
        /// Force disable panel (for non-owner players)
        /// </summary>
        public void ForceDisable()
        {
            isOpen = false;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Inventory panel modes
    /// </summary>
    public enum InventoryMode
    {
        Loot,   // Hiển thị loot container panel (khi đang loot container)
        Shop    // Hiển thị shop panel (khi đang mua bán)
    }
} 
 