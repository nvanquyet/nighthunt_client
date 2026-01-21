using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using NightHunt.Data;
using NightHunt.Gameplay.Loot;
using NightHunt.Gameplay.Interaction;
using NightHunt.UI;
using NightHunt.UI.Inventory;
using NightHunt.UI.Equipment;
using FishNet.Object;
using TMPro;

namespace NightHunt.Editor
{
    /// <summary>
    /// Editor tool để tự động setup items và UI components từ config data
    /// </summary>
    public class ItemSetupTool : EditorWindow
    {
        private string prefabOutputPath = "Assets/_Night_Hunt/Prefabs/Generated/";
        private string uiOutputPath = "Assets/_Night_Hunt/Prefabs/UI/";
        private bool generateLootPrefabs = true;
        private bool generateUI = true;
        private bool createTestScene = true;

        [MenuItem("Night Hunt/Setup Tools/Item & UI Setup Tool")]
        public static void ShowWindow()
        {
            GetWindow<ItemSetupTool>("Item Setup Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("Item & UI Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Tool này sẽ tự động tạo prefabs và UI components từ config data để test.", MessageType.Info);
            EditorGUILayout.Space();

            prefabOutputPath = EditorGUILayout.TextField("Prefab Output Path:", prefabOutputPath);
            uiOutputPath = EditorGUILayout.TextField("UI Output Path:", uiOutputPath);
            EditorGUILayout.Space();

            generateLootPrefabs = EditorGUILayout.Toggle("Generate Loot Prefabs", generateLootPrefabs);
            generateUI = EditorGUILayout.Toggle("Generate UI Components", generateUI);
            createTestScene = EditorGUILayout.Toggle("Create Test Scene", createTestScene);
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate All", GUILayout.Height(30)))
            {
                GenerateAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            if (GUILayout.Button("Generate Loot Prefabs Only"))
            {
                GenerateLootPrefabs();
            }

            if (GUILayout.Button("Generate UI Components Only"))
            {
                GenerateUIComponents();
            }

            if (GUILayout.Button("Create Test Scene"))
            {
                CreateTestScene();
            }
        }

        /// <summary>
        /// Generate tất cả
        /// </summary>
        private void GenerateAll()
        {
            if (generateLootPrefabs)
            {
                GenerateLootPrefabs();
            }

            if (generateUI)
            {
                GenerateUIComponents();
            }

            if (createTestScene)
            {
                CreateTestScene();
            }

            EditorUtility.DisplayDialog("Success", "Setup completed!", "OK");
        }

        /// <summary>
        /// Generate loot prefabs từ config
        /// </summary>
        private void GenerateLootPrefabs()
        {
            if (GameConfigLoader.Instance == null)
            {
                EditorUtility.DisplayDialog("Error", "GameConfigLoader.Instance is null. Please ensure config is loaded.", "OK");
                return;
            }

            var configData = GameConfigLoader.Instance.ConfigData;
            if (configData?.ItemConfig == null)
            {
                EditorUtility.DisplayDialog("Error", "ItemConfig is null or empty.", "OK");
                return;
            }

            // Create output directory
            if (!Directory.Exists(prefabOutputPath))
            {
                Directory.CreateDirectory(prefabOutputPath);
            }

            int createdCount = 0;

            foreach (var itemConfig in configData.ItemConfig)
            {
                if (itemConfig == null || string.IsNullOrEmpty(itemConfig.ItemId)) continue;

                // Create prefab
                GameObject prefab = CreateLootPrefab(itemConfig);
                if (prefab != null)
                {
                    string prefabPath = $"{prefabOutputPath}Loot_{itemConfig.ItemId}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                    DestroyImmediate(prefab);
                    createdCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ItemSetupTool] Created {createdCount} loot prefabs");
        }

        /// <summary>
        /// Tạo loot prefab từ config
        /// </summary>
        private GameObject CreateLootPrefab(ItemConfigData config)
        {
            GameObject prefab = new GameObject($"Loot_{config.ItemId}");
            
            // Add NetworkObject
            NetworkObject netObj = prefab.AddComponent<NetworkObject>();
            
            // Add NetworkLootItem
            NetworkLootItem lootItem = prefab.AddComponent<NetworkLootItem>();
            SerializedObject so = new SerializedObject(lootItem);
            so.FindProperty("itemId").stringValue = config.ItemId;
            so.FindProperty("quantity").intValue = 1;
            so.FindProperty("isTestItem").boolValue = true;
            so.ApplyModifiedProperties();

            // Add InteractionTargetAdapter
            InteractionTargetAdapter adapter = prefab.AddComponent<InteractionTargetAdapter>();

            // Add Collider
            SphereCollider collider = prefab.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            // Set layer và tag
            prefab.layer = LayerMask.NameToLayer("InteractableLoot");
            if (LayerMask.NameToLayer("InteractableLoot") == -1)
            {
                Debug.LogWarning("[ItemSetupTool] Layer 'InteractableLoot' not found. Please create this layer.");
            }
            prefab.tag = "Loot";

            // Add visual representation (placeholder cube)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(prefab.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 0.5f;
            
            // Remove collider from visual (already have on parent)
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                DestroyImmediate(visualCollider);
            }

            // Add VFX placeholder (ParticleSystem)
            GameObject vfxObj = new GameObject("PickupEffect");
            vfxObj.transform.SetParent(prefab.transform);
            ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = GetRarityColor(config.Rarity);
            main.startSize = 0.2f;
            main.startLifetime = 1f;
            ps.Stop(); // Start stopped

            SerializedObject lootSo = new SerializedObject(lootItem);
            lootSo.FindProperty("pickupEffect").objectReferenceValue = ps;
            lootSo.ApplyModifiedProperties();

            return prefab;
        }

        /// <summary>
        /// Get color based on rarity
        /// </summary>
        private Color GetRarityColor(string rarity)
        {
            switch (rarity?.ToLower())
            {
                case "common": return Color.white;
                case "uncommon": return Color.green;
                case "rare": return Color.blue;
                case "epic": return Color.magenta;
                case "legendary": return Color.yellow;
                default: return Color.white;
            }
        }

        /// <summary>
        /// Generate UI components
        /// </summary>
        private void GenerateUIComponents()
        {
            if (!Directory.Exists(uiOutputPath))
            {
                Directory.CreateDirectory(uiOutputPath);
            }

            // Create InventoryUI prefab
            CreateInventoryUIPrefab();
            
            // Create EquipmentUI prefab
            CreateEquipmentUIPrefab();
            
            // Create DropAmountSelector prefab
            CreateDropAmountSelectorPrefab();

            AssetDatabase.Refresh();
            Debug.Log("[ItemSetupTool] UI components created");
        }

        /// <summary>
        /// Tạo InventoryUI prefab
        /// </summary>
        private void CreateInventoryUIPrefab()
        {
            GameObject uiRoot = new GameObject("InventoryUI");
            InventoryUI inventoryUI = uiRoot.AddComponent<InventoryUI>();

            // Create Canvas if needed
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Create inventory panel
            GameObject panel = new GameObject("InventoryPanel");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.8f);

            // Create item grid
            GameObject gridObj = new GameObject("ItemGrid");
            gridObj.transform.SetParent(panel.transform, false);
            RectTransform gridRect = gridObj.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.1f, 0.2f);
            gridRect.anchorMax = new Vector2(0.6f, 0.8f);
            gridRect.sizeDelta = Vector2.zero;
            
            UnityEngine.UI.GridLayoutGroup grid = gridObj.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(80, 80);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            // Create weight display
            GameObject weightObj = new GameObject("WeightDisplay");
            weightObj.transform.SetParent(panel.transform, false);
            RectTransform weightRect = weightObj.AddComponent<RectTransform>();
            weightRect.anchorMin = new Vector2(0.65f, 0.1f);
            weightRect.anchorMax = new Vector2(0.9f, 0.2f);
            weightRect.sizeDelta = Vector2.zero;

            TMPro.TextMeshProUGUI weightText = weightObj.AddComponent<TMPro.TextMeshProUGUI>();
            weightText.text = "0 / 20 kg";
            weightText.fontSize = 18;
            
            // Load default font if available
            TMP_FontAsset defaultFont = Resources.GetBuiltinResource<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF.asset");
            if (defaultFont != null)
            {
                weightText.font = defaultFont;
            }

            // Create item info panel
            GameObject infoPanel = new GameObject("ItemInfoPanel");
            infoPanel.transform.SetParent(panel.transform, false);
            RectTransform infoRect = infoPanel.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.65f, 0.3f);
            infoRect.anchorMax = new Vector2(0.9f, 0.9f);
            infoRect.sizeDelta = Vector2.zero;
            infoPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Setup SerializedObject để assign references
            SerializedObject so = new SerializedObject(inventoryUI);
            so.FindProperty("inventoryPanel").objectReferenceValue = panel;
            so.FindProperty("itemGridParent").objectReferenceValue = gridObj.transform;
            so.FindProperty("weightText").objectReferenceValue = weightText;
            so.ApplyModifiedProperties();

            // Save prefab
            string prefabPath = $"{uiOutputPath}InventoryUI.prefab";
            PrefabUtility.SaveAsPrefabAsset(uiRoot, prefabPath);
            DestroyImmediate(uiRoot);
        }

        /// <summary>
        /// Tạo EquipmentUI prefab
        /// </summary>
        private void CreateEquipmentUIPrefab()
        {
            GameObject uiRoot = new GameObject("EquipmentUI");
            EquipmentUI equipmentUI = uiRoot.AddComponent<EquipmentUI>();

            // Create Canvas if needed
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Create equipment panel
            GameObject panel = new GameObject("EquipmentPanel");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.1f);
            panelRect.anchorMax = new Vector2(0.5f, 0.9f);
            panelRect.sizeDelta = Vector2.zero;
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.8f);

            // Create equipment slots parent
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

            string prefabPath = $"{uiOutputPath}EquipmentUI.prefab";
            PrefabUtility.SaveAsPrefabAsset(uiRoot, prefabPath);
            DestroyImmediate(uiRoot);
        }

        /// <summary>
        /// Tạo DropAmountSelector prefab
        /// </summary>
        private void CreateDropAmountSelectorPrefab()
        {
            GameObject selectorRoot = new GameObject("DropAmountSelector");
            DropAmountSelector selector = selectorRoot.AddComponent<DropAmountSelector>();

            // Create Canvas if needed
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Create selector panel
            GameObject panel = new GameObject("SelectorPanel");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.4f, 0.4f);
            panelRect.anchorMax = new Vector2(0.6f, 0.6f);
            panelRect.sizeDelta = Vector2.zero;
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            // Create slider
            GameObject sliderObj = new GameObject("AmountSlider");
            sliderObj.transform.SetParent(panel.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.1f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.9f, 0.6f);
            sliderRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Slider slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();

            // Create input field
            GameObject inputObj = new GameObject("AmountInput");
            inputObj.transform.SetParent(panel.transform, false);
            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.1f, 0.7f);
            inputRect.anchorMax = new Vector2(0.9f, 0.8f);
            inputRect.sizeDelta = Vector2.zero;
            TMPro.TMP_InputField inputField = inputObj.AddComponent<TMPro.TMP_InputField>();

            // Create buttons
            GameObject confirmBtn = CreateButton("Confirm", panel.transform, new Vector2(0.1f, 0.2f), new Vector2(0.45f, 0.4f));
            GameObject cancelBtn = CreateButton("Cancel", panel.transform, new Vector2(0.55f, 0.2f), new Vector2(0.9f, 0.4f));

            SerializedObject so = new SerializedObject(selector);
            so.FindProperty("selectorPanel").objectReferenceValue = panel;
            so.FindProperty("amountSlider").objectReferenceValue = slider;
            so.FindProperty("amountInput").objectReferenceValue = inputField;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn.GetComponent<UnityEngine.UI.Button>();
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn.GetComponent<UnityEngine.UI.Button>();
            so.ApplyModifiedProperties();

            string prefabPath = $"{uiOutputPath}DropAmountSelector.prefab";
            PrefabUtility.SaveAsPrefabAsset(selectorRoot, prefabPath);
            DestroyImmediate(selectorRoot);
        }

        /// <summary>
        /// Tạo button helper
        /// </summary>
        private GameObject CreateButton(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject btnObj = new GameObject($"Button_{text}");
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = anchorMin;
            btnRect.anchorMax = anchorMax;
            btnRect.sizeDelta = Vector2.zero;

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image btnImage = btnObj.AddComponent<UnityEngine.UI.Image>();
            btnImage.color = Color.gray;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TMPro.TextMeshProUGUI btnText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 14;
            btnText.alignment = TMPro.TextAlignmentOptions.Center;
            
            // Load default font if available
            TMP_FontAsset defaultFont = Resources.GetBuiltinResource<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF.asset");
            if (defaultFont != null)
            {
                btnText.font = defaultFont;
            }

            return btnObj;
        }

        /// <summary>
        /// Tạo test scene với items spawn sẵn
        /// </summary>
        private void CreateTestScene()
        {
            // Create new scene
            UnityEngine.SceneManagement.Scene testScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single
            );

            // Create ground plane
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = Vector3.one * 10f;
            ground.layer = LayerMask.NameToLayer("Default");

            // Create spawn points
            GameObject spawnPointsParent = new GameObject("LootSpawnPoints");
            for (int i = 0; i < 10; i++)
            {
                GameObject spawnPoint = new GameObject($"SpawnPoint_{i}");
                spawnPoint.transform.SetParent(spawnPointsParent.transform);
                spawnPoint.transform.position = new Vector3(
                    Random.Range(-10f, 10f),
                    0.5f,
                    Random.Range(-10f, 10f)
                );

                // Add spawn point marker
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.SetParent(spawnPoint.transform);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localScale = Vector3.one * 0.3f;
                marker.name = "Marker";
            }

            // Create test player spawn
            GameObject playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.position = new Vector3(0, 1, 0);

            // Create UI Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Load và instantiate InventoryUI prefab nếu có
            string inventoryUIPath = $"{uiOutputPath}InventoryUI.prefab";
            if (File.Exists(inventoryUIPath))
            {
                GameObject inventoryUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(inventoryUIPath);
                if (inventoryUIPrefab != null)
                {
                    GameObject inventoryUI = Instantiate(inventoryUIPrefab);
                    inventoryUI.name = "InventoryUI";
                }
            }

            // Save scene
            string scenePath = "Assets/_Night_Hunt/Scenes/Test_ItemSetup.unity";
            string sceneDir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(testScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[ItemSetupTool] Test scene created at: {scenePath}");
            EditorUtility.DisplayDialog("Success", $"Test scene created at:\n{scenePath}", "OK");
        }
    }
}

