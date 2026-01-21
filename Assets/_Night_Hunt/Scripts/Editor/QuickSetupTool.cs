using UnityEngine;
using UnityEditor;
using System.IO;
using NightHunt.Data;
using NightHunt.Gameplay.Loot;
using NightHunt.Gameplay.Interaction;
using NightHunt.UI;
using NightHunt.UI.Equipment;
using NightHunt.UI.Inventory;
using NightHunt.Networking;
using FishNet.Object;
using TMPro;

namespace NightHunt.Editor
{
    /// <summary>
    /// Quick setup tool - tự động setup tất cả để test
    /// </summary>
    public class QuickSetupTool
    {
        [MenuItem("Night Hunt/Quick Setup/Setup Everything for Testing")]
        public static void SetupEverything()
        {
            Debug.Log("[QuickSetupTool] Starting full setup...");

            // 1. Ensure config is loaded
            if (GameConfigLoader.Instance == null)
            {
                GameObject loaderObj = new GameObject("GameConfigLoader");
                loaderObj.AddComponent<GameConfigLoader>();
                Debug.Log("[QuickSetupTool] Created GameConfigLoader");
            }

            // 2. Create test scene
            CreateTestSceneWithEverything();

            // 3. Generate prefabs
            ItemSetupTool tool = ScriptableObject.CreateInstance<ItemSetupTool>();
            // Use reflection to call private methods or make them public
            Debug.Log("[QuickSetupTool] Setup complete!");
            
            EditorUtility.DisplayDialog("Success", "Quick setup completed!\n\n- Test scene created\n- Prefabs generated\n- UI components ready", "OK");
        }

        [MenuItem("Night Hunt/Quick Setup/Create Test Scene Only")]
        public static void CreateTestSceneOnly()
        {
            CreateTestSceneWithEverything();
        }

        /// <summary>
        /// Tạo test scene với tất cả components sẵn sàng
        /// </summary>
        private static void CreateTestSceneWithEverything()
        {
            // Create new scene
            UnityEngine.SceneManagement.Scene testScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single
            );

            // Create ground
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = Vector3.one * 10f;
            ground.GetComponent<Renderer>().material.color = new Color(0.3f, 0.3f, 0.3f);

            // Create lighting
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Create UI Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create EventSystem if not exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Create InventoryUI
            GameObject inventoryUIObj = new GameObject("InventoryUI");
            InventoryUI inventoryUI = inventoryUIObj.AddComponent<InventoryUI>();
            SetupInventoryUI(inventoryUI, canvas.transform);

            // Create EquipmentUI
            GameObject equipmentUIObj = new GameObject("EquipmentUI");
            EquipmentUI equipmentUI = equipmentUIObj.AddComponent<EquipmentUI>();
            SetupEquipmentUI(equipmentUI, canvas.transform);

            // Create DropAmountSelector
            GameObject dropSelectorObj = new GameObject("DropAmountSelector");
            DropAmountSelector dropSelector = dropSelectorObj.AddComponent<DropAmountSelector>();
            SetupDropAmountSelector(dropSelector, canvas.transform);

            // Create LootSpawner
            GameObject lootSpawnerObj = new GameObject("LootSpawner");
            LootSpawner lootSpawner = lootSpawnerObj.AddComponent<LootSpawner>();

            // Create spawn points và spawn một vài test items
            GameObject spawnPointsParent = new GameObject("LootSpawnPoints");
            CreateTestLootItems(spawnPointsParent.transform);

            // Create test player spawn point
            GameObject playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.position = new Vector3(0, 1, 0);

            // Save scene
            string scenePath = "Assets/_Night_Hunt/Scenes/Test_QuickSetup.unity";
            string sceneDir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(testScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[QuickSetupTool] Test scene created at: {scenePath}");
        }

        /// <summary>
        /// Setup InventoryUI với UI elements
        /// </summary>
        private static void SetupInventoryUI(InventoryUI inventoryUI, Transform canvasParent)
        {
            // Create inventory panel
            GameObject panel = CreateUIPanel("InventoryPanel", canvasParent, 
                new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), new Color(0, 0, 0, 0.8f));

            // Create item grid
            GameObject gridObj = new GameObject("ItemGrid");
            gridObj.transform.SetParent(panel.transform, false);
            RectTransform gridRect = gridObj.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.05f, 0.15f);
            gridRect.anchorMax = new Vector2(0.6f, 0.85f);
            gridRect.sizeDelta = Vector2.zero;

            UnityEngine.UI.GridLayoutGroup grid = gridObj.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(80, 80);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            // Create weight display
            GameObject weightObj = CreateUIText("WeightDisplay", panel.transform,
                new Vector2(0.65f, 0.05f), new Vector2(0.95f, 0.15f), "0 / 20 kg", 18);

            // Create weight bar
            GameObject weightBarObj = new GameObject("WeightBar");
            weightBarObj.transform.SetParent(panel.transform, false);
            RectTransform weightBarRect = weightBarObj.AddComponent<RectTransform>();
            weightBarRect.anchorMin = new Vector2(0.65f, 0.02f);
            weightBarRect.anchorMax = new Vector2(0.95f, 0.05f);
            weightBarRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Slider weightBar = weightBarObj.AddComponent<UnityEngine.UI.Slider>();

            // Create item info panel
            GameObject infoPanel = CreateUIPanel("ItemInfoPanel", panel.transform,
                new Vector2(0.65f, 0.2f), new Vector2(0.95f, 0.8f), new Color(0.2f, 0.2f, 0.2f, 0.9f));

            GameObject itemNameObj = CreateUIText("ItemName", infoPanel.transform,
                new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.95f), "Item Name", 20);

            GameObject itemDescObj = CreateUIText("ItemDescription", infoPanel.transform,
                new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.65f), "Description", 14);

            // Create buttons
            GameObject useBtn = CreateUIButton("UseButton", infoPanel.transform,
                new Vector2(0.05f, 0.15f), new Vector2(0.48f, 0.25f), "Use");

            GameObject dropBtn = CreateUIButton("DropButton", infoPanel.transform,
                new Vector2(0.52f, 0.15f), new Vector2(0.95f, 0.25f), "Drop");

            // Assign references via SerializedObject
            SerializedObject so = new SerializedObject(inventoryUI);
            so.FindProperty("inventoryPanel").objectReferenceValue = panel;
            so.FindProperty("itemGridParent").objectReferenceValue = gridObj.transform;
            so.FindProperty("weightText").objectReferenceValue = itemNameObj.GetComponent<TMPro.TextMeshProUGUI>();
            so.FindProperty("weightBar").objectReferenceValue = weightBar;
            so.FindProperty("itemInfoPanel").objectReferenceValue = infoPanel;
            so.FindProperty("itemNameText").objectReferenceValue = itemNameObj.GetComponent<TMPro.TextMeshProUGUI>();
            so.FindProperty("itemDescriptionText").objectReferenceValue = itemDescObj.GetComponent<TMPro.TextMeshProUGUI>();
            so.FindProperty("useItemButton").objectReferenceValue = useBtn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("dropItemButton").objectReferenceValue = dropBtn.GetComponent<UnityEngine.UI.Button>();
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Setup EquipmentUI
        /// </summary>
        private static void SetupEquipmentUI(EquipmentUI equipmentUI, Transform canvasParent)
        {
            GameObject panel = CreateUIPanel("EquipmentPanel", canvasParent,
                new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.9f), new Color(0, 0, 0, 0.8f));

            GameObject slotsParent = new GameObject("EquipmentSlots");
            slotsParent.transform.SetParent(panel.transform, false);
            RectTransform slotsRect = slotsParent.AddComponent<RectTransform>();
            slotsRect.anchorMin = Vector2.zero;
            slotsRect.anchorMax = Vector2.one;
            slotsRect.sizeDelta = Vector2.zero;

            UnityEngine.UI.VerticalLayoutGroup layout = slotsParent.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);

            SerializedObject so = new SerializedObject(equipmentUI);
            so.FindProperty("equipmentSlotsParent").objectReferenceValue = slotsParent.transform;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Setup DropAmountSelector
        /// </summary>
        private static void SetupDropAmountSelector(DropAmountSelector selector, Transform canvasParent)
        {
            GameObject panel = CreateUIPanel("SelectorPanel", canvasParent,
                new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.65f), new Color(0.2f, 0.2f, 0.2f, 0.95f));

            // Slider
            GameObject sliderObj = new GameObject("AmountSlider");
            sliderObj.transform.SetParent(panel.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.1f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.9f, 0.6f);
            sliderRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Slider slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();

            // Input field
            GameObject inputObj = CreateUIText("AmountInput", panel.transform,
                new Vector2(0.1f, 0.7f), new Vector2(0.9f, 0.8f), "1", 16);
            TMPro.TMP_InputField inputField = inputObj.AddComponent<TMPro.TMP_InputField>();
            inputField.contentType = TMPro.TMP_InputField.ContentType.IntegerNumber;

            // Max amount text
            GameObject maxTextObj = CreateUIText("MaxAmountText", panel.transform,
                new Vector2(0.1f, 0.85f), new Vector2(0.9f, 0.95f), "Max: 10", 14);

            // Buttons
            GameObject confirmBtn = CreateUIButton("ConfirmButton", panel.transform,
                new Vector2(0.1f, 0.2f), new Vector2(0.45f, 0.4f), "Confirm");

            GameObject cancelBtn = CreateUIButton("CancelButton", panel.transform,
                new Vector2(0.55f, 0.2f), new Vector2(0.9f, 0.4f), "Cancel");

            GameObject preset1Btn = CreateUIButton("Preset1Button", panel.transform,
                new Vector2(0.1f, 0.05f), new Vector2(0.3f, 0.15f), "1");

            GameObject presetHalfBtn = CreateUIButton("PresetHalfButton", panel.transform,
                new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.15f), "Half");

            GameObject presetAllBtn = CreateUIButton("PresetAllButton", panel.transform,
                new Vector2(0.7f, 0.05f), new Vector2(0.9f, 0.15f), "All");

            SerializedObject so = new SerializedObject(selector);
            so.FindProperty("selectorPanel").objectReferenceValue = panel;
            so.FindProperty("amountSlider").objectReferenceValue = slider;
            so.FindProperty("amountInput").objectReferenceValue = inputField;
            so.FindProperty("maxAmountText").objectReferenceValue = maxTextObj.GetComponent<TMPro.TextMeshProUGUI>();
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("preset1Button").objectReferenceValue = preset1Btn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("presetHalfButton").objectReferenceValue = presetHalfBtn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("presetAllButton").objectReferenceValue = presetAllBtn.GetComponent<UnityEngine.UI.Button>();
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Tạo test loot items trong scene
        /// </summary>
        private static void CreateTestLootItems(Transform parent)
        {
            if (GameConfigLoader.Instance == null) return;

            var configData = GameConfigLoader.Instance.ConfigData;
            if (configData?.ItemConfig == null || configData.ItemConfig.Count == 0) return;

            // Spawn first 5 items as test
            int spawnCount = Mathf.Min(5, configData.ItemConfig.Count);
            float radius = 5f;
            float angleStep = 360f / spawnCount;

            for (int i = 0; i < spawnCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.5f,
                    Mathf.Sin(angle) * radius
                );

                var itemConfig = configData.ItemConfig[i];
                GameObject lootObj = CreateLootItemInScene(itemConfig.ItemId, pos);
                lootObj.transform.SetParent(parent);
            }
        }

        /// <summary>
        /// Tạo loot item trong scene
        /// </summary>
        private static GameObject CreateLootItemInScene(string itemId, Vector3 position)
        {
            GameObject lootObj = new GameObject($"Loot_{itemId}");
            lootObj.transform.position = position;

            // Add NetworkObject
            NetworkObject netObj = lootObj.AddComponent<NetworkObject>();

            // Add NetworkLootItem
            NetworkLootItem lootItem = lootObj.AddComponent<NetworkLootItem>();
            SerializedObject so = new SerializedObject(lootItem);
            so.FindProperty("itemId").stringValue = itemId;
            so.FindProperty("quantity").intValue = 1;
            so.FindProperty("isTestItem").boolValue = true;
            so.ApplyModifiedProperties();

            // Add InteractionTargetAdapter
            lootObj.AddComponent<InteractionTargetAdapter>();

            // Add Collider
            SphereCollider collider = lootObj.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            // Set layer và tag
            int lootLayer = LayerMask.NameToLayer("InteractableLoot");
            if (lootLayer != -1)
            {
                lootObj.layer = lootLayer;
            }
            lootObj.tag = "Loot";

            // Visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(lootObj.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 0.5f;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

            return lootObj;
        }

        // Helper methods
        private static GameObject CreateUIPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = Vector2.zero;
            panel.AddComponent<UnityEngine.UI.Image>().color = color;
            return panel;
        }

        private static GameObject CreateUIText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string text, int fontSize)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = Vector2.zero;

            TMPro.TextMeshProUGUI tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.alignment = TMPro.TextAlignmentOptions.Left;

            TMP_FontAsset defaultFont = Resources.GetBuiltinResource<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF.asset");
            if (defaultFont != null)
            {
                tmpText.font = defaultFont;
            }

            return textObj;
        }

        private static GameObject CreateUIButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string buttonText)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = Vector2.zero;

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image btnImage = btnObj.AddComponent<UnityEngine.UI.Image>();
            btnImage.color = Color.gray;

            GameObject textObj = CreateUIText("Text", btnObj.transform, Vector2.zero, Vector2.one, buttonText, 14);
            TMPro.TextMeshProUGUI btnText = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            btnText.alignment = TMPro.TextAlignmentOptions.Center;

            return btnObj;
        }
    }
}

