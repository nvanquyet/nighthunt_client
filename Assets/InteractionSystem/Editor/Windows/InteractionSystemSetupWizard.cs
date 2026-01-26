#if UNITY_EDITOR
using System.IO;
using NightHunt.InteractionSystem.Combat;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Equipment;
using NightHunt.InteractionSystem.Input;
using NightHunt.InteractionSystem.Interaction;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Items;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Pickup;
using UnityEditor;
using UnityEngine;

namespace NightHunt.InteractionSystem.Editor
{
    public class InteractionSystemSetupWizard : EditorWindow
    {
        private enum SetupStep
        {
            Welcome,
            Dependencies,
            PlayerSetup,
            ItemDatabase,
            ExampleContent,
            Complete
        }
    
        private SetupStep currentStep = SetupStep.Welcome;
        private Vector2 scrollPosition;
    
        // Setup data
        private GameObject playerPrefab;
        private bool createExampleItems = true;
        private bool createExampleInteractables = true;
        private bool setupInputSystem = true;
    
        [MenuItem("NightHunt/Setup/Interaction System Wizard")]
        public static void ShowWindow()
        {
            InteractionSystemSetupWizard window = GetWindow<InteractionSystemSetupWizard>("Setup Wizard");
            window.minSize = new Vector2(600, 400);
        }
    
        private void OnGUI()
        {
            DrawHeader();
        
            EditorGUILayout.Space(10);
        
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
            switch (currentStep)
            {
                case SetupStep.Welcome:
                    DrawWelcomeStep();
                    break;
                case SetupStep.Dependencies:
                    DrawDependenciesStep();
                    break;
                case SetupStep.PlayerSetup:
                    DrawPlayerSetupStep();
                    break;
                case SetupStep.ItemDatabase:
                    DrawItemDatabaseStep();
                    break;
                case SetupStep.ExampleContent:
                    DrawExampleContentStep();
                    break;
                case SetupStep.Complete:
                    DrawCompleteStep();
                    break;
            }
        
            EditorGUILayout.EndScrollView();
        
            EditorGUILayout.Space(10);
            DrawNavigationButtons();
        }
    
        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 18;
            headerStyle.alignment = TextAnchor.MiddleCenter;
        
            EditorGUILayout.LabelField("NightHunt Interaction System", headerStyle, GUILayout.Height(30));
            EditorGUILayout.LabelField($"Step {(int)currentStep + 1} of {System.Enum.GetValues(typeof(SetupStep)).Length}", EditorStyles.centeredGreyMiniLabel);
        
            EditorGUILayout.EndVertical();
        }
    
        private void DrawWelcomeStep()
        {
            EditorGUILayout.LabelField("Welcome!", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        
            EditorGUILayout.HelpBox(
                "This wizard will help you set up the NightHunt Interaction System.\n\n" +
                "The system includes:\n" +
                "• Pickup System (manual & auto)\n" +
                "• Interaction System (doors, containers, NPCs)\n" +
                "• Loot & Container System\n" +
                "• Universal Attachment System\n" +
                "• Grid-based Inventory\n" +
                "• Equipment & Combat Systems\n" +
                "• Debug Tools & Visualizers\n\n" +
                "Click 'Next' to begin setup.",
                MessageType.Info
            );
        }
    
        private void DrawDependenciesStep()
        {
            EditorGUILayout.LabelField("Dependencies Check", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        
            bool fishNetInstalled = CheckDependency("FishNet");
            bool inputSystemInstalled = CheckDependency("Unity.InputSystem");
        
            DrawDependencyStatus("FishNet Networking", fishNetInstalled);
            DrawDependencyStatus("Unity Input System", inputSystemInstalled);
        
            EditorGUILayout.Space();
        
            if (!fishNetInstalled || !inputSystemInstalled)
            {
                EditorGUILayout.HelpBox(
                    "Missing dependencies detected!\n\n" +
                    "Please install:\n" +
                    (!fishNetInstalled ? "• FishNet (Package Manager or Asset Store)\n" : "") +
                    (!inputSystemInstalled ? "• Unity Input System (Package Manager)\n" : ""),
                    MessageType.Error
                );
            
                if (GUILayout.Button("Open Package Manager"))
                {
                    UnityEditor.PackageManager.UI.Window.Open("com.unity.inputsystem");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("All dependencies installed! ✓", MessageType.Info);
            }
        }
    
        private void DrawPlayerSetupStep()
        {
            EditorGUILayout.LabelField("Player Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        
            EditorGUILayout.HelpBox(
                "Select your player prefab to automatically add required components.",
                MessageType.Info
            );
        
            playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player Prefab", playerPrefab, typeof(GameObject), false);
        
            EditorGUILayout.Space();
        
            setupInputSystem = EditorGUILayout.Toggle("Setup Input System", setupInputSystem);
        
            EditorGUILayout.Space();
        
            if (playerPrefab != null)
            {
                EditorGUILayout.LabelField("Components to add:", EditorStyles.boldLabel);
            
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawComponentStatus(playerPrefab, typeof(PickupDetector));
                DrawComponentStatus(playerPrefab, typeof(InteractionDetector));
                DrawComponentStatus(playerPrefab, typeof(PickupHandler));
                DrawComponentStatus(playerPrefab, typeof(InputRouter));
                DrawComponentStatus(playerPrefab, typeof(GridInventoryComponent));
                DrawComponentStatus(playerPrefab, typeof(EquipmentManager));
                DrawComponentStatus(playerPrefab, typeof(PlayerHealthComponent));
                DrawComponentStatus(playerPrefab, typeof(ArmorComponent));
                EditorGUILayout.EndVertical();
            
                EditorGUILayout.Space();
            
                if (GUILayout.Button("Add Components to Player", GUILayout.Height(30)))
                {
                    SetupPlayerPrefab();
                }
            }
        }
    
        private void DrawItemDatabaseStep()
        {
            EditorGUILayout.LabelField("Item Database Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        
            EditorGUILayout.HelpBox(
                "The Item Database Manager loads all items on startup.\n" +
                "It should be placed in your game's persistent scene.",
                MessageType.Info
            );
        
            EditorGUILayout.Space();
        
            if (GUILayout.Button("Create Item Database Manager", GUILayout.Height(30)))
            {
                CreateItemDatabaseManager();
            }
        
            EditorGUILayout.Space();
        
            if (FindObjectOfType<ItemDatabaseManager>() != null)
            {
                EditorGUILayout.HelpBox("Item Database Manager found in scene ✓", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No Item Database Manager in scene", MessageType.Warning);
            }
        }
    
        private void DrawExampleContentStep()
        {
            EditorGUILayout.LabelField("Example Content", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        
            EditorGUILayout.HelpBox(
                "Create example items and interactables to test the system.",
                MessageType.Info
            );
        
            createExampleItems = EditorGUILayout.Toggle("Create Example Items", createExampleItems);
            createExampleInteractables = EditorGUILayout.Toggle("Create Example Interactables", createExampleInteractables);
        
            EditorGUILayout.Space();
        
            if (createExampleItems)
            {
                EditorGUILayout.LabelField("Will create:", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("• AK-47 (Weapon with attachment slots)");
                EditorGUILayout.LabelField("• Red Dot Sight (Attachment)");
                EditorGUILayout.LabelField("• Suppressor (Attachment)");
                EditorGUILayout.LabelField("• Tactical Helmet (with attachment slots)");
                EditorGUILayout.LabelField("• Flashlight (Attachment)");
                EditorGUILayout.LabelField("• Plate Carrier (Armor with attachment slots)");
                EditorGUILayout.LabelField("• Ceramic Plate (Attachment)");
                EditorGUILayout.LabelField("• Ammo 7.62");
                EditorGUILayout.LabelField("• Medkit");
                EditorGUILayout.EndVertical();
            }
        
            EditorGUILayout.Space();
        
            if (GUILayout.Button("Create Example Content", GUILayout.Height(30)))
            {
                CreateExampleContent();
            }
        }
    
        private void DrawCompleteStep()
        {
            EditorGUILayout.LabelField("Setup Complete! 🎉", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        
            EditorGUILayout.HelpBox(
                "The NightHunt Interaction System is now set up!\n\n" +
                "Next steps:\n" +
                "1. Review the example content in Assets/NightHunt/Examples/\n" +
                "2. Test pickup and interaction in Play mode\n" +
                "3. Check the Debug Tools (NightHunt menu)\n" +
                "4. Read the documentation for advanced features\n\n" +
                "Happy developing!",
                MessageType.Info
            );
        
            EditorGUILayout.Space();
        
            if (GUILayout.Button("Open Documentation"))
            {
                Application.OpenURL("https://docs.nighthunt.dev/interaction-system");
            }
        
            if (GUILayout.Button("Close Wizard", GUILayout.Height(30)))
            {
                Close();
            }
        }
    
        private void DrawNavigationButtons()
        {
            EditorGUILayout.BeginHorizontal();
        
            GUI.enabled = currentStep > SetupStep.Welcome;
            if (GUILayout.Button("← Previous", GUILayout.Width(100)))
            {
                currentStep--;
            }
            GUI.enabled = true;
        
            GUILayout.FlexibleSpace();
        
            if (currentStep < SetupStep.Complete)
            {
                bool canProceed = CanProceedToNextStep();
                GUI.enabled = canProceed;
            
                if (GUILayout.Button("Next →", GUILayout.Width(100)))
                {
                    currentStep++;
                }
            
                GUI.enabled = true;
            }
        
            EditorGUILayout.EndHorizontal();
        }
    
        private bool CanProceedToNextStep()
        {
            switch (currentStep)
            {
                case SetupStep.Dependencies:
                    return CheckDependency("FishNet") && CheckDependency("Unity.InputSystem");
                default:
                    return true;
            }
        }
    
        private bool CheckDependency(string assemblyName)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            return System.Array.Exists(assemblies, a => a.GetName().Name.Contains(assemblyName));
        }
    
        private void DrawDependencyStatus(string name, bool installed)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(200));
        
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = installed ? Color.green : Color.red;
            EditorGUILayout.LabelField(installed ? "✓ Installed" : "✗ Missing", style);
        
            EditorGUILayout.EndHorizontal();
        }
    
        private void DrawComponentStatus(GameObject prefab, System.Type componentType)
        {
            bool hasComponent = prefab.GetComponent(componentType) != null;
        
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(componentType.Name, GUILayout.Width(250));
        
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = hasComponent ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(hasComponent ? "✓ Exists" : "○ Will Add", style);
        
            EditorGUILayout.EndHorizontal();
        }
    
        private void SetupPlayerPrefab()
        {
            if (playerPrefab == null) return;
        
            Undo.RecordObject(playerPrefab, "Setup Player Components");
        
            // Add components if they don't exist
            AddComponentIfMissing<PickupDetector>(playerPrefab);
            AddComponentIfMissing<InteractionDetector>(playerPrefab);
            AddComponentIfMissing<PickupHandler>(playerPrefab);
            AddComponentIfMissing<InputRouter>(playerPrefab);
            AddComponentIfMissing<GridInventoryComponent>(playerPrefab);
            AddComponentIfMissing<EquipmentManager>(playerPrefab);
            AddComponentIfMissing<PlayerHealthComponent>(playerPrefab);
            AddComponentIfMissing<ArmorComponent>(playerPrefab);
        
            EditorUtility.SetDirty(playerPrefab);
        
            Debug.Log($"Player prefab '{playerPrefab.name}' setup complete!");
            EditorUtility.DisplayDialog("Success", "Player prefab components added successfully!", "OK");
        }
    
        private T AddComponentIfMissing<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }
    
        private void CreateItemDatabaseManager()
        {
            GameObject managerObj = new GameObject("ItemDatabaseManager");
            managerObj.AddComponent<ItemDatabaseManager>();
        
            Undo.RegisterCreatedObjectUndo(managerObj, "Create Item Database Manager");
        
            Selection.activeGameObject = managerObj;
        
            Debug.Log("Item Database Manager created in scene!");
        }
    
        private void CreateExampleContent()
        {
            string basePath = "Assets/NightHunt/Examples/";
        
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
        
            if (createExampleItems)
            {
                CreateExampleItems(basePath + "Items/");
            }
        
            if (createExampleInteractables)
            {
                CreateExampleInteractables(basePath + "Prefabs/");
            }
        
            AssetDatabase.Refresh();
        
            Debug.Log("Example content created!");
            EditorUtility.DisplayDialog("Success", "Example content created successfully!", "OK");
        }
    
        private void CreateExampleItems(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        
            // Create AK-47
            WeaponData ak47 = ScriptableObject.CreateInstance<WeaponData>();
            ak47.itemId = "weapon_ak47";
            ak47.displayName = "AK-47";
            ak47.description = "Assault rifle with moderate damage and good accuracy";
            ak47.category = ItemCategory.Weapon;
            ak47.weaponType = WeaponType.Rifle;
            ak47.damage = 45f;
            ak47.fireRate = 600f;
            ak47.attachmentSlots = new AttachmentSlotDefinition[]
            {
                new AttachmentSlotDefinition
                {
                    slotType = AttachmentSlotType.Scope,
                    acceptedTypes = new AttachmentType[] { AttachmentType.Optic }
                },
                new AttachmentSlotDefinition
                {
                    slotType = AttachmentSlotType.Barrel,
                    acceptedTypes = new AttachmentType[] { AttachmentType.Suppressor }
                }
            };
        
            AssetDatabase.CreateAsset(ak47, path + "AK47.asset");
        
            // Create Red Dot Sight
            AttachmentData redDot = ScriptableObject.CreateInstance<AttachmentData>();
            redDot.itemId = "attachment_reddot";
            redDot.displayName = "Red Dot Sight";
            redDot.attachmentType = AttachmentType.Optic;
            redDot.compatibleSlots = new AttachmentSlotType[] { AttachmentSlotType.Scope };
            redDot.modifiers = new StatModifier[]
            {
                new StatModifier
                {
                    statType = StatType.Accuracy,
                    modifierType = ModifierType.Additive,
                    value = 15f
                }
            };
        
            AssetDatabase.CreateAsset(redDot, path + "RedDotSight.asset");
        
            // Create more items...
            Debug.Log($"Created example items in {path}");
        }
    
        private void CreateExampleInteractables(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        
            // Create Door prefab
            GameObject doorPrefab = new GameObject("ExampleDoor");
            doorPrefab.AddComponent<BoxCollider>();
            DoorInteractable doorScript = doorPrefab.AddComponent<DoorInteractable>();
        
            PrefabUtility.SaveAsPrefabAsset(doorPrefab, path + "ExampleDoor.prefab");
            DestroyImmediate(doorPrefab);
        
            // Create Chest prefab
            GameObject chestPrefab = new GameObject("ExampleChest");
            chestPrefab.AddComponent<BoxCollider>();
            chestPrefab.AddComponent<LootContainer>();
            chestPrefab.AddComponent<ContainerInteractable>();
        
            PrefabUtility.SaveAsPrefabAsset(chestPrefab, path + "ExampleChest.prefab");
            DestroyImmediate(chestPrefab);
        
            Debug.Log($"Created example interactables in {path}");
        }
    }
}
#endif