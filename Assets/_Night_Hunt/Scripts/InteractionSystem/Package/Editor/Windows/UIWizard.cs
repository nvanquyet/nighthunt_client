using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace NightHunt.InteractionSystem.Editor.Windows
{
    /// <summary>
    /// Wizard to generate basic UI prefabs for testing the Interaction System.
    /// </summary>
    public class UIWizard : EditorWindow
    {
        private string savePath = "Assets/_Night_Hunt/UI/InteractionSystem/";
        private bool createInventoryUI = true;
        private bool createContainerUI = true;
        private bool createInteractionUI = true;

        [MenuItem("NightHunt/InteractionSystem/UI Wizard")]
        public static void ShowWindow()
        {
            GetWindow<UIWizard>("UI Wizard");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UI Wizard - Generate Test UI", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This wizard creates basic UI prefabs for testing the Interaction System.\n" +
                "You can customize the UI later or create your own UI and assign references to the components.",
                MessageType.Info
            );

            EditorGUILayout.Space();

            // Save path
            EditorGUILayout.LabelField("Save Path:", EditorStyles.boldLabel);
            savePath = EditorGUILayout.TextField("Path", savePath);

            EditorGUILayout.Space();

            // Options
            EditorGUILayout.LabelField("UI to Generate:", EditorStyles.boldLabel);
            createInventoryUI = EditorGUILayout.Toggle("Inventory UI", createInventoryUI);
            createContainerUI = EditorGUILayout.Toggle("Container/Loot UI", createContainerUI);
            createInteractionUI = EditorGUILayout.Toggle("Interaction Prompt UI", createInteractionUI);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Generate button
            if (GUILayout.Button("Generate UI Prefabs", GUILayout.Height(30)))
            {
                GenerateUI();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Note: After generating, you need to:\n" +
                "1. Create gameplay UI scripts that subscribe to InventoryEvents/InteractionEvents\n" +
                "2. Customize the UI design to match your game style\n" +
                "3. Implement grid/item slot rendering (currently placeholder)\n" +
                "Note: UI is handled in gameplay layer, not in package.",
                MessageType.Warning
            );
        }

        private void GenerateUI()
        {
            // Create directory if it doesn't exist
            if (!AssetDatabase.IsValidFolder(savePath))
            {
                string[] folders = savePath.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    if (!AssetDatabase.IsValidFolder(currentPath + "/" + folders[i]))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath += "/" + folders[i];
                }
            }

            if (createInventoryUI)
            {
                CreateInventoryUI();
            }

            if (createContainerUI)
            {
                CreateContainerUI();
            }

            if (createInteractionUI)
            {
                CreateInteractionUI();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", "UI prefabs generated successfully!", "OK");
        }

        private void CreateInventoryUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("InventoryCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create Inventory Panel
            GameObject panelObj = new GameObject("InventoryPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 400);
            panelRect.anchoredPosition = Vector2.zero;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panelObj.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Inventory";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 24;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = new Vector2(0, 40);
            titleRect.anchoredPosition = new Vector2(0, -20);

            // Weight Text
            GameObject weightTextObj = new GameObject("WeightText");
            weightTextObj.transform.SetParent(panelObj.transform, false);
            Text weightText = weightTextObj.AddComponent<Text>();
            weightText.text = "Weight: 0 / 20 kg";
            weightText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            weightText.fontSize = 14;
            weightText.color = Color.white;

            RectTransform weightRect = weightTextObj.GetComponent<RectTransform>();
            weightRect.anchorMin = new Vector2(0, 0);
            weightRect.anchorMax = new Vector2(0.5f, 0);
            weightRect.sizeDelta = new Vector2(200, 30);
            weightRect.anchoredPosition = new Vector2(100, 20);

            // Weight Slider
            GameObject sliderObj = new GameObject("WeightSlider");
            sliderObj.transform.SetParent(panelObj.transform, false);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;

            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0, 0);
            sliderRect.anchorMax = new Vector2(1, 0);
            sliderRect.sizeDelta = new Vector2(-20, 20);
            sliderRect.anchoredPosition = new Vector2(0, 50);

            // Slider background
            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(sliderObj.transform, false);
            Image bgImage = sliderBg.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f);
            RectTransform bgRect = sliderBg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            slider.targetGraphic = bgImage;

            // Slider fill
            GameObject sliderFill = new GameObject("Fill");
            sliderFill.transform.SetParent(sliderObj.transform, false);
            Image fillImage = sliderFill.AddComponent<Image>();
            fillImage.color = Color.green;
            RectTransform fillRect = sliderFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1);
            fillRect.sizeDelta = Vector2.zero;
            slider.fillRect = fillRect;

            // Slot Count Text
            GameObject slotTextObj = new GameObject("SlotCountText");
            slotTextObj.transform.SetParent(panelObj.transform, false);
            Text slotText = slotTextObj.AddComponent<Text>();
            slotText.text = "Slots: 0 / 12";
            slotText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            slotText.fontSize = 14;
            slotText.color = Color.white;

            RectTransform slotRect = slotTextObj.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0);
            slotRect.anchorMax = new Vector2(1, 0);
            slotRect.sizeDelta = new Vector2(200, 30);
            slotRect.anchoredPosition = new Vector2(-100, 20);

            // Close Button
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(panelObj.transform, false);
            Button closeBtn = closeBtnObj.AddComponent<Button>();
            Image btnImage = closeBtnObj.AddComponent<Image>();
            btnImage.color = new Color(0.8f, 0.2f, 0.2f);

            RectTransform btnRect = closeBtnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 1);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.sizeDelta = new Vector2(30, 30);
            btnRect.anchoredPosition = new Vector2(-15, -15);

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(closeBtnObj.transform, false);
            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.text = "X";
            btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btnText.fontSize = 18;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            // Note: UI is handled in gameplay layer, not in package.
            // Create a gameplay UI script that subscribes to InventoryEvents:
            // - InventoryEvents.OnWeightChanged -> Update weightText and weightSlider
            // - InventoryEvents.OnSlotCountChanged -> Update slotCountText
            // Assign references: weightText, weightSlider, slotCountText

            // Hide by default
            panelObj.SetActive(false);

            // Save prefab
            string prefabPath = savePath + "InventoryUI.prefab";
            PrefabUtility.SaveAsPrefabAsset(canvasObj, prefabPath);
            DestroyImmediate(canvasObj);

            Debug.Log($"[UIWizard] Created Inventory UI prefab at: {prefabPath}");
        }

        private void CreateContainerUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("ContainerCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create Container Panel
            GameObject panelObj = new GameObject("ContainerPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            // Left side - Container Grid (placeholder)
            GameObject containerGridObj = new GameObject("ContainerGrid");
            containerGridObj.transform.SetParent(panelObj.transform, false);
            RectTransform containerGridRect = containerGridObj.GetComponent<RectTransform>();
            containerGridRect.anchorMin = new Vector2(0, 0);
            containerGridRect.anchorMax = new Vector2(0.5f, 1);
            containerGridRect.sizeDelta = Vector2.zero;
            containerGridRect.anchoredPosition = Vector2.zero;

            // Right side - Inventory Grid (placeholder)
            GameObject inventoryGridObj = new GameObject("InventoryGrid");
            inventoryGridObj.transform.SetParent(panelObj.transform, false);
            RectTransform inventoryGridRect = inventoryGridObj.GetComponent<RectTransform>();
            inventoryGridRect.anchorMin = new Vector2(0.5f, 0);
            inventoryGridRect.anchorMax = new Vector2(1, 1);
            inventoryGridRect.sizeDelta = Vector2.zero;
            inventoryGridRect.anchoredPosition = Vector2.zero;

            // Note: Container UI is now handled by LootContainerPanel in Gameplay.UI namespace
            // This is just a placeholder prefab structure

            // Hide by default
            panelObj.SetActive(false);

            // Save prefab
            string prefabPath = savePath + "ContainerUI.prefab";
            PrefabUtility.SaveAsPrefabAsset(canvasObj, prefabPath);
            DestroyImmediate(canvasObj);

            Debug.Log($"[UIWizard] Created Container UI prefab at: {prefabPath}");
        }

        private void CreateInteractionUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("InteractionCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Interaction Prompt Panel
            GameObject promptPanelObj = new GameObject("InteractionPromptPanel");
            promptPanelObj.transform.SetParent(canvasObj.transform, false);
            Image promptImage = promptPanelObj.AddComponent<Image>();
            promptImage.color = new Color(0, 0, 0, 0.7f);

            RectTransform promptRect = promptPanelObj.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0);
            promptRect.anchorMax = new Vector2(0.5f, 0);
            promptRect.sizeDelta = new Vector2(300, 50);
            promptRect.anchoredPosition = new Vector2(0, 100);

            // Interaction Text
            GameObject textObj = new GameObject("InteractionText");
            textObj.transform.SetParent(promptPanelObj.transform, false);
            Text interactionText = textObj.AddComponent<Text>();
            interactionText.text = "Press E to interact";
            interactionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            interactionText.fontSize = 16;
            interactionText.alignment = TextAnchor.MiddleCenter;
            interactionText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Progress Bar Panel
            GameObject progressPanelObj = new GameObject("ProgressBarPanel");
            progressPanelObj.transform.SetParent(canvasObj.transform, false);
            Image progressBgImage = progressPanelObj.AddComponent<Image>();
            progressBgImage.color = new Color(0, 0, 0, 0.7f);

            RectTransform progressRect = progressPanelObj.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.5f, 0);
            progressRect.anchorMax = new Vector2(0.5f, 0);
            progressRect.sizeDelta = new Vector2(300, 30);
            progressRect.anchoredPosition = new Vector2(0, 50);

            // Progress Bar Fill
            GameObject fillObj = new GameObject("ProgressFill");
            fillObj.transform.SetParent(progressPanelObj.transform, false);
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = Color.cyan;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;

            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1);
            fillRect.sizeDelta = Vector2.zero;

            // Progress Text
            GameObject progressTextObj = new GameObject("ProgressText");
            progressTextObj.transform.SetParent(progressPanelObj.transform, false);
            Text progressText = progressTextObj.AddComponent<Text>();
            progressText.text = "50%";
            progressText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            progressText.fontSize = 14;
            progressText.alignment = TextAnchor.MiddleCenter;
            progressText.color = Color.white;

            RectTransform progressTextRect = progressTextObj.GetComponent<RectTransform>();
            progressTextRect.anchorMin = Vector2.zero;
            progressTextRect.anchorMax = Vector2.one;
            progressTextRect.sizeDelta = Vector2.zero;

            // Note: UI is handled in gameplay layer, not in package.
            // Create a gameplay UI script that subscribes to InteractionEvents:
            // - InteractionEvents.OnInteractionTargetChanged -> Show/Hide promptPanelObj
            // - InteractionEvents.OnHoldInteractionProgress -> Update progressBarFill and progressText
            // Assign references: promptPanelObj, interactionText, progressPanelObj, fillImage, progressText

            // Hide by default
            promptPanelObj.SetActive(false);
            progressPanelObj.SetActive(false);

            // Save prefab
            string prefabPath = savePath + "InteractionUI.prefab";
            PrefabUtility.SaveAsPrefabAsset(canvasObj, prefabPath);
            DestroyImmediate(canvasObj);

            Debug.Log($"[UIWizard] Created Interaction UI prefab at: {prefabPath}");
        }
    }
}
