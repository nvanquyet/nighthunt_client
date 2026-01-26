using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Setup script to create and configure inventory UI structure
    /// Creates all necessary UI components with proper anchors and responsive layout
    /// </summary>
    [System.Serializable]
    public class InventoryUISetup : MonoBehaviour
    {
        [Header("Canvas Reference")]
        [SerializeField] private Canvas canvas;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GameObject quickSlotPrefab;
        [SerializeField] private GameObject weaponSlotPrefab;
        [SerializeField] private GameObject equipmentSlotPrefab;
        [SerializeField] private GameObject dragIconPrefab;

        [Header("Layout Settings")]
        [SerializeField] private bool isMobile = false;
        [SerializeField] private float inventorySlotSize = 80f;
        [SerializeField] private float quickSlotSize = 70f;
        [SerializeField] private float spacing = 10f;

        private void Start()
        {
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("[InventoryUISetup] No Canvas found! Please create a Canvas first.");
                    return;
                }
            }

            // Detect platform
            DetectPlatform();

            // Setup UI structure
            SetupUIStructure();
        }

        /// <summary>
        /// Detect platform and adjust settings
        /// </summary>
        private void DetectPlatform()
        {
#if UNITY_ANDROID || UNITY_IOS
            isMobile = true;
#else
            isMobile = false;
#endif

            // Adjust sizes for mobile
            if (isMobile)
            {
                inventorySlotSize = 100f;
                quickSlotSize = 90f;
                spacing = 15f;
            }
        }

        /// <summary>
        /// Setup complete UI structure
        /// </summary>
        public void SetupUIStructure()
        {
            if (canvas == null)
            {
                Debug.LogError("[InventoryUISetup] Canvas is null!");
                return;
            }

            // Create root containers
            GameObject playerHUD = CreateOrFind("PlayerHUD", canvas.transform);
            GameObject inventoryRoot = CreateOrFind("InventoryPanel", canvas.transform);

            // Setup PlayerHUD
            SetupPlayerHUD(playerHUD);

            // Setup InventoryPanel
            SetupInventoryPanel(inventoryRoot);

            Debug.Log("[InventoryUISetup] UI structure setup complete!");
        }

        /// <summary>
        /// Setup PlayerHUD structure
        /// </summary>
        private void SetupPlayerHUD(GameObject root)
        {
            // Health Bar
            GameObject healthBar = CreateOrFind("HealthBar", root.transform);
            SetupHealthBar(healthBar);

            // Stamina Bar
            GameObject staminaBar = CreateOrFind("StaminaBar", root.transform);
            SetupStaminaBar(staminaBar);

            // Prompt Text
            GameObject promptPanel = CreateOrFind("PromptPanel", root.transform);
            SetupPromptPanel(promptPanel);

            // Quick Slots Container
            GameObject quickSlotsContainer = CreateOrFind("QuickSlotsContainer", root.transform);
            SetupQuickSlotsContainer(quickSlotsContainer);

            // Weapon Slots Container
            GameObject weaponSlotsContainer = CreateOrFind("WeaponSlotsContainer", root.transform);
            SetupWeaponSlotsContainer(weaponSlotsContainer);

            // Weight Display
            GameObject weightDisplay = CreateOrFind("WeightDisplay", root.transform);
            SetupWeightDisplay(weightDisplay);

            // Add PlayerHUD component
            if (root.GetComponent<PlayerHUD>() == null)
            {
                root.AddComponent<PlayerHUD>();
            }
        }

        /// <summary>
        /// Setup InventoryPanel structure
        /// </summary>
        private void SetupInventoryPanel(GameObject root)
        {
            // Main panel background
            Image panelBg = root.GetComponent<Image>();
            if (panelBg == null)
            {
                panelBg = root.AddComponent<Image>();
                panelBg.color = new Color(0, 0, 0, 0.8f);
            }

            RectTransform rootRect = root.GetComponent<RectTransform>();
            SetupRectTransform(rootRect, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

            // Left Panel (ItemDetailPanel) - ALWAYS VISIBLE
            GameObject leftPanel = CreateOrFind("LeftPanel", root.transform);
            SetupLeftPanel(leftPanel);

            // Center Panel (Inventory Grid)
            GameObject centerPanel = CreateOrFind("CenterPanel", root.transform);
            SetupCenterPanel(centerPanel);

            // Right Panel (EquipmentPanel/LootContainerPanel)
            GameObject rightPanel = CreateOrFind("RightPanel", root.transform);
            SetupRightPanel(rightPanel);

            // Add InventoryPanel component
            InventoryPanel invPanel = root.GetComponent<InventoryPanel>();
            if (invPanel == null)
            {
                invPanel = root.AddComponent<InventoryPanel>();
            }

            // Hide initially
            root.SetActive(false);
        }

        /// <summary>
        /// Setup Left Panel (ItemDetailPanel)
        /// </summary>
        private void SetupLeftPanel(GameObject panel)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (isMobile)
            {
                // Mobile: Full width at bottom
                SetupRectTransform(rect, new Vector2(0, 0), new Vector2(1, 0.3f), Vector2.zero, Vector2.zero);
            }
            else
            {
                // PC: Left side, 25% width
                SetupRectTransform(rect, new Vector2(0, 0), new Vector2(0.25f, 1), Vector2.zero, Vector2.zero);
            }

            // Add background
            Image bg = panel.GetComponent<Image>();
            if (bg == null)
            {
                bg = panel.AddComponent<Image>();
                bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            }

            // Create ItemDetailPanel content
            GameObject detailPanel = CreateOrFind("ItemDetailPanel", panel.transform);
            SetupItemDetailPanel(detailPanel);
        }

        /// <summary>
        /// Setup Center Panel (Inventory Grid)
        /// </summary>
        private void SetupCenterPanel(GameObject panel)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (isMobile)
            {
                // Mobile: Center, 70% width, 50% height
                SetupRectTransform(rect, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.8f), Vector2.zero, Vector2.zero);
            }
            else
            {
                // PC: Center, 50% width
                SetupRectTransform(rect, new Vector2(0.25f, 0), new Vector2(0.75f, 1), Vector2.zero, Vector2.zero);
            }

            // Create grid layout
            GridLayoutGroup grid = panel.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = panel.AddComponent<GridLayoutGroup>();
            }
            grid.cellSize = new Vector2(inventorySlotSize, inventorySlotSize);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // Add content size fitter for scrolling
            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = panel.AddComponent<ContentSizeFitter>();
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add scroll rect if needed
            ScrollRect scrollRect = panel.GetComponentInParent<ScrollRect>();
            if (scrollRect == null && isMobile)
            {
                GameObject scrollView = CreateOrFind("ScrollView", panel.transform.parent);
                scrollRect = scrollView.AddComponent<ScrollRect>();
                scrollRect.content = rect;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
            }
        }

        /// <summary>
        /// Setup Right Panel (EquipmentPanel/LootContainerPanel)
        /// </summary>
        private void SetupRightPanel(GameObject panel)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (isMobile)
            {
                // Mobile: Right side, 30% width
                SetupRectTransform(rect, new Vector2(0.7f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            }
            else
            {
                // PC: Right side, 25% width
                SetupRectTransform(rect, new Vector2(0.75f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            }

            // Create EquipmentPanel
            GameObject equipmentPanel = CreateOrFind("EquipmentPanel", panel.transform);
            SetupEquipmentPanel(equipmentPanel);

            // Create LootContainerPanel
            GameObject lootPanel = CreateOrFind("LootContainerPanel", panel.transform);
            SetupLootContainerPanel(lootPanel);
        }

        /// <summary>
        /// Setup ItemDetailPanel content
        /// </summary>
        private void SetupItemDetailPanel(GameObject panel)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            SetupRectTransform(rect, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));

            // Item Name
            GameObject nameObj = CreateOrFind("ItemName", panel.transform);
            SetupText(nameObj, "Item Name", 24, TextAlignmentOptions.Center, new Vector2(0, 0.9f), new Vector2(1, 1f));

            // Item Description
            GameObject descObj = CreateOrFind("ItemDescription", panel.transform);
            SetupText(descObj, "Item Description", 16, TextAlignmentOptions.TopLeft, new Vector2(0, 0.5f), new Vector2(1, 0.9f));

            // Stats Container
            GameObject statsContainer = CreateOrFind("StatsContainer", panel.transform);
            SetupStatsContainer(statsContainer);

            // Nested Equipment Container
            GameObject nestedContainer = CreateOrFind("NestedEquipmentContainer", panel.transform);
            SetupNestedEquipmentContainer(nestedContainer);

            // Add ItemDetailPanel component
            if (panel.GetComponent<ItemDetailPanel>() == null)
            {
                panel.AddComponent<ItemDetailPanel>();
            }
        }

        /// <summary>
        /// Setup EquipmentPanel
        /// </summary>
        private void SetupEquipmentPanel(GameObject panel)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            SetupRectTransform(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Weapon Slots Container
            GameObject weaponSlots = CreateOrFind("WeaponSlots", panel.transform);
            SetupWeaponSlotsGrid(weaponSlots);

            // Equipment Slots Container
            GameObject equipmentSlots = CreateOrFind("EquipmentSlots", panel.transform);
            SetupEquipmentSlotsGrid(equipmentSlots);

            // Trash Slot
            GameObject trashSlot = CreateOrFind("TrashSlot", panel.transform);
            SetupTrashSlot(trashSlot);

            // Add EquipmentPanel component
            if (panel.GetComponent<EquipmentPanel>() == null)
            {
                panel.AddComponent<EquipmentPanel>();
            }
        }

        /// <summary>
        /// Setup LootContainerPanel
        /// </summary>
        private void SetupLootContainerPanel(GameObject panel)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            SetupRectTransform(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Container Title
            GameObject titleObj = CreateOrFind("ContainerTitle", panel.transform);
            SetupText(titleObj, "Container", 20, TextAlignmentOptions.Center, new Vector2(0, 0.95f), new Vector2(1, 1f));

            // Loot Grid
            GameObject lootGrid = CreateOrFind("LootGrid", panel.transform);
            SetupLootGrid(lootGrid);

            // Add LootContainerPanel component
            if (panel.GetComponent<LootContainerPanel>() == null)
            {
                panel.AddComponent<LootContainerPanel>();
            }

            // Hide initially
            panel.SetActive(false);
        }

        /// <summary>
        /// Setup Health Bar
        /// </summary>
        private void SetupHealthBar(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (isMobile)
            {
                SetupRectTransform(rect, new Vector2(0, 0.9f), new Vector2(0.5f, 1f), new Vector2(20, -20), new Vector2(-10, -10));
            }
            else
            {
                SetupRectTransform(rect, new Vector2(0, 0.95f), new Vector2(0.3f, 1f), new Vector2(20, -20), new Vector2(-10, -10));
            }

            // Slider
            Slider slider = obj.GetComponent<Slider>();
            if (slider == null)
            {
                slider = obj.AddComponent<Slider>();
            }
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;

            // Health Text
            GameObject textObj = CreateOrFind("HealthText", obj.transform);
            SetupText(textObj, "100/100", 18, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// Setup Stamina Bar
        /// </summary>
        private void SetupStaminaBar(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (isMobile)
            {
                SetupRectTransform(rect, new Vector2(0.5f, 0.9f), new Vector2(1f, 1f), new Vector2(10, -20), new Vector2(-20, -10));
            }
            else
            {
                SetupRectTransform(rect, new Vector2(0.3f, 0.95f), new Vector2(0.6f, 1f), new Vector2(10, -20), new Vector2(-10, -10));
            }

            Slider slider = obj.GetComponent<Slider>();
            if (slider == null)
            {
                slider = obj.AddComponent<Slider>();
            }
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;

            GameObject textObj = CreateOrFind("StaminaText", obj.transform);
            SetupText(textObj, "100/100", 18, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// Setup Prompt Panel
        /// </summary>
        private void SetupPromptPanel(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.2f), Vector2.zero, Vector2.zero);

            GameObject textObj = CreateOrFind("PromptText", obj.transform);
            SetupText(textObj, "Press E to interact", 20, TextAlignmentOptions.Center);

            obj.SetActive(false);
        }

        /// <summary>
        /// Setup Quick Slots Container
        /// </summary>
        private void SetupQuickSlotsContainer(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (isMobile)
            {
                SetupRectTransform(rect, new Vector2(0, 0), new Vector2(0.5f, 0.15f), new Vector2(20, 20), new Vector2(-10, -10));
            }
            else
            {
                SetupRectTransform(rect, new Vector2(0, 0), new Vector2(0.4f, 0.1f), new Vector2(20, 20), new Vector2(-10, -10));
            }

            HorizontalLayoutGroup layout = obj.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<HorizontalLayoutGroup>();
            }
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        /// <summary>
        /// Setup Weapon Slots Container
        /// </summary>
        private void SetupWeaponSlotsContainer(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (isMobile)
            {
                SetupRectTransform(rect, new Vector2(0.5f, 0), new Vector2(1f, 0.15f), new Vector2(10, 20), new Vector2(-20, -10));
            }
            else
            {
                SetupRectTransform(rect, new Vector2(0.4f, 0), new Vector2(0.7f, 0.1f), new Vector2(10, 20), new Vector2(-10, -10));
            }

            HorizontalLayoutGroup layout = obj.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<HorizontalLayoutGroup>();
            }
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.LowerLeft;
        }

        /// <summary>
        /// Setup Weight Display
        /// </summary>
        private void SetupWeightDisplay(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (isMobile)
            {
                SetupRectTransform(rect, new Vector2(0.7f, 0), new Vector2(1f, 0.1f), new Vector2(10, 20), new Vector2(-20, -10));
            }
            else
            {
                SetupRectTransform(rect, new Vector2(0.7f, 0), new Vector2(0.9f, 0.1f), new Vector2(10, 20), new Vector2(-10, -10));
            }

            Slider slider = obj.GetComponent<Slider>();
            if (slider == null)
            {
                slider = obj.AddComponent<Slider>();
            }

            GameObject textObj = CreateOrFind("WeightText", obj.transform);
            SetupText(textObj, "0/20 kg", 14, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// Setup Stats Container
        /// </summary>
        private void SetupStatsContainer(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0, 0.3f), new Vector2(1, 0.5f), new Vector2(10, 10), new Vector2(-10, -10));

            VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<VerticalLayoutGroup>();
            }
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Setup Nested Equipment Container
        /// </summary>
        private void SetupNestedEquipmentContainer(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0, 0), new Vector2(1, 0.3f), new Vector2(10, 10), new Vector2(-10, -10));

            GridLayoutGroup layout = obj.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<GridLayoutGroup>();
            }
            layout.cellSize = new Vector2(60, 60);
            layout.spacing = new Vector2(5, 5);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
        }

        /// <summary>
        /// Setup Weapon Slots Grid
        /// </summary>
        private void SetupWeaponSlotsGrid(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0, 0.7f), new Vector2(1, 1f), new Vector2(10, 10), new Vector2(-10, -10));

            VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<VerticalLayoutGroup>();
            }
            layout.spacing = spacing;
        }

        /// <summary>
        /// Setup Equipment Slots Grid
        /// </summary>
        private void SetupEquipmentSlotsGrid(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0, 0.2f), new Vector2(1, 0.7f), new Vector2(10, 10), new Vector2(-10, -10));

            GridLayoutGroup layout = obj.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<GridLayoutGroup>();
            }
            layout.cellSize = new Vector2(70, 70);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
        }

        /// <summary>
        /// Setup Trash Slot
        /// </summary>
        private void SetupTrashSlot(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0, 0), new Vector2(1, 0.2f), new Vector2(10, 10), new Vector2(-10, -10));

            Image img = obj.GetComponent<Image>();
            if (img == null)
            {
                img = obj.AddComponent<Image>();
                img.color = new Color(0.3f, 0.1f, 0.1f, 0.8f);
            }

            GameObject textObj = CreateOrFind("LabelText", obj.transform);
            SetupText(textObj, "Drop", 16, TextAlignmentOptions.Center);

            if (obj.GetComponent<TrashSlotUI>() == null)
            {
                obj.AddComponent<TrashSlotUI>();
            }
        }

        /// <summary>
        /// Setup Loot Grid
        /// </summary>
        private void SetupLootGrid(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetupRectTransform(rect, new Vector2(0, 0), new Vector2(1, 0.95f), new Vector2(10, 10), new Vector2(-10, -10));

            GridLayoutGroup layout = obj.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = obj.AddComponent<GridLayoutGroup>();
            }
            layout.cellSize = new Vector2(inventorySlotSize, inventorySlotSize);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
        }

        /// <summary>
        /// Setup RectTransform with anchors and offsets
        /// </summary>
        private void SetupRectTransform(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null) return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>
        /// Setup Text component
        /// </summary>
        private void SetupText(GameObject obj, string text, int fontSize, TextAlignmentOptions alignment, Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (anchorMin.HasValue && anchorMax.HasValue)
            {
                SetupRectTransform(rect, anchorMin.Value, anchorMax.Value, Vector2.zero, Vector2.zero);
            }
            else
            {
                SetupRectTransform(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = obj.AddComponent<TextMeshProUGUI>();
            }
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
        }

        /// <summary>
        /// Create or find GameObject
        /// </summary>
        private GameObject CreateOrFind(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
