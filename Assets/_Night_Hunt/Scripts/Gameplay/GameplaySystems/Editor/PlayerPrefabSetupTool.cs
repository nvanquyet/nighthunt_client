#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using FishNet.Object;
using NightHunt.StatSystem.Systems;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Equipment;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.GameplaySystems.QuickSlot;
using NightHunt.GameplaySystems.Attachment;
using NightHunt.GameplaySystems.Test;

namespace NightHunt.GameplaySystems.Editor
{
    /// <summary>
    /// Tool to automatically setup player prefab with all required components
    /// Menu: Tools → GameplaySystems → Setup Player Prefab
    /// </summary>
    public class PlayerPrefabSetupTool : EditorWindow
    {
        private GameObject _selectedPrefab;
        private bool _addTestScripts = true;
        private bool _enableDebugUI = true;
        
        [MenuItem("Tools/GameplaySystems/Setup Player Prefab")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayerPrefabSetupTool>("Player Setup");
            window.minSize = new Vector2(450, 700);
            window.Show();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Player Prefab Setup Tool", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "This tool will automatically add and configure all GameplaySystems components to your player prefab.\n\n" +
                "Requirements:\n" +
                "• Player GameObject with NetworkObject component\n" +
                "• Configs created (use Setup Tool first)",
                MessageType.Info);
            
            GUILayout.Space(10);
            
            // Select prefab
            GUILayout.Label("Select Player Prefab", EditorStyles.boldLabel);
            _selectedPrefab = EditorGUILayout.ObjectField("Player Prefab", _selectedPrefab, typeof(GameObject), false) as GameObject;
            
            if (_selectedPrefab != null)
            {
                // Check if has NetworkObject
                var networkObject = _selectedPrefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    EditorGUILayout.HelpBox("⚠️ Selected prefab does not have NetworkObject component!", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("✓ Valid player prefab", MessageType.Info);
                }
            }
            
            GUILayout.Space(10);
            
            // Options
            GUILayout.Label("Options", EditorStyles.boldLabel);
            _addTestScripts = EditorGUILayout.Toggle("Add Test Scripts", _addTestScripts);
            _enableDebugUI = EditorGUILayout.Toggle("Enable Debug UI", _enableDebugUI);
            
            GUILayout.Space(10);
            
            // Component status
            if (_selectedPrefab != null)
            {
                GUILayout.Label("Components Status", EditorStyles.boldLabel);
                DrawComponentStatus();
            }
            
            GUILayout.Space(20);
            
            // Setup button
            GUI.enabled = _selectedPrefab != null;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Setup Player Prefab", GUILayout.Height(40)))
            {
                SetupPlayerPrefab();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            
            GUILayout.Space(10);
            
            // Manual setup guide
            if (GUILayout.Button("Show Manual Setup Guide"))
            {
                ShowManualGuide();
            }
        }
        
        private void DrawComponentStatus()
        {
            DrawComponentCheck<PlayerStatSystem>("PlayerStatSystem");
            DrawComponentCheck<InventorySystem>("InventorySystem");
            DrawComponentCheck<EquipmentSystem>("EquipmentSystem");
            DrawComponentCheck<WeaponSystem>("WeaponSystem");
            DrawComponentCheck<QuickSlotSystem>("QuickSlotSystem");
            DrawComponentCheck<AttachmentSystem>("AttachmentSystem");
        }
        
        private void DrawComponentCheck<T>(string name) where T : Component
        {
            bool has = _selectedPrefab.GetComponent<T>() != null;
            string status = has ? "✓" : "✗";
            Color color = has ? Color.green : Color.yellow;
            
            var oldColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField($"{status} {name}", EditorStyles.miniLabel);
            GUI.color = oldColor;
        }
        
        private void SetupPlayerPrefab()
        {
            if (_selectedPrefab == null)
                return;
            
            Undo.RecordObject(_selectedPrefab, "Setup Player Prefab");
            
            int componentsAdded = 0;
            
            // Load configs
            var playerStatConfig = AssetDatabase.LoadAssetAtPath<PlayerStatConfig>("Assets/Resources/Configs/PlayerStatConfig.asset");
            var gameplayConfig = AssetDatabase.LoadAssetAtPath<GameplayConfig>("Assets/Resources/Configs/GameplayConfig.asset");
            var inventoryConfig = AssetDatabase.LoadAssetAtPath<InventoryConfig>("Assets/Resources/Configs/InventoryConfig.asset");
            
            if (playerStatConfig == null || gameplayConfig == null || inventoryConfig == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Configs not found!\n\nPlease run 'Setup Tool' first to create configs.", 
                    "OK");
                return;
            }
            
            // Add components in order
            componentsAdded += SetupPlayerStatSystem(playerStatConfig, gameplayConfig);
            componentsAdded += SetupInventorySystem(gameplayConfig, inventoryConfig);
            componentsAdded += SetupEquipmentSystem(inventoryConfig);
            componentsAdded += SetupWeaponSystem(inventoryConfig);
            componentsAdded += SetupQuickSlotSystem(inventoryConfig);
            componentsAdded += SetupAttachmentSystem();
            
            // Add test scripts if enabled
            if (_addTestScripts)
            {
                componentsAdded += AddTestScripts();
            }
            
            // Mark as dirty
            EditorUtility.SetDirty(_selectedPrefab);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"<color=green>✓ Setup complete! Added {componentsAdded} components.</color>");
            EditorUtility.DisplayDialog("Success", 
                $"Player prefab setup complete!\n\nAdded {componentsAdded} components.\n\nDon't forget to create ItemDatabase in scene!", 
                "OK");
        }
        
        #region Setup Methods
        
        private int SetupPlayerStatSystem(PlayerStatConfig statConfig, GameplayConfig gameplayConfig)
        {
            var component = _selectedPrefab.GetComponent<PlayerStatSystem>();
            if (component == null)
            {
                component = _selectedPrefab.AddComponent<PlayerStatSystem>();
            }
            
            var so = new SerializedObject(component);
            so.FindProperty("_statConfig").objectReferenceValue = statConfig;
            so.FindProperty("_gameplayConfig").objectReferenceValue = gameplayConfig;
            so.FindProperty("_showDebugUI").boolValue = _enableDebugUI;
            so.ApplyModifiedProperties();
            
            Debug.Log("Setup: PlayerStatSystem");
            return 1;
        }
        
        private int SetupInventorySystem(GameplayConfig gameplayConfig, InventoryConfig inventoryConfig)
        {
            var component = _selectedPrefab.GetComponent<InventorySystem>();
            if (component == null)
            {
                component = _selectedPrefab.AddComponent<InventorySystem>();
            }
            
            var statSystem = _selectedPrefab.GetComponent<PlayerStatSystem>();
            
            var so = new SerializedObject(component);
            so.FindProperty("_gameplayConfig").objectReferenceValue = gameplayConfig;
            so.FindProperty("_inventoryConfig").objectReferenceValue = inventoryConfig;
            so.FindProperty("_statSystem").objectReferenceValue = statSystem;
            so.FindProperty("_showDebugUI").boolValue = _enableDebugUI;
            so.ApplyModifiedProperties();
            
            Debug.Log("Setup: InventorySystem");
            return 1;
        }
        
        private int SetupEquipmentSystem(InventoryConfig inventoryConfig)
        {
            var component = _selectedPrefab.GetComponent<EquipmentSystem>();
            if (component == null)
            {
                component = _selectedPrefab.AddComponent<EquipmentSystem>();
            }
            
            var statSystem = _selectedPrefab.GetComponent<PlayerStatSystem>();
            var inventorySystem = _selectedPrefab.GetComponent<InventorySystem>();
            
            var so = new SerializedObject(component);
            so.FindProperty("_inventoryConfig").objectReferenceValue = inventoryConfig;
            so.FindProperty("_statSystem").objectReferenceValue = statSystem;
            so.FindProperty("_inventorySystem").objectReferenceValue = inventorySystem;
            so.FindProperty("_showDebugUI").boolValue = _enableDebugUI;
            so.ApplyModifiedProperties();
            
            Debug.Log("Setup: EquipmentSystem");
            return 1;
        }
        
        private int SetupWeaponSystem(InventoryConfig inventoryConfig)
        {
            var component = _selectedPrefab.GetComponent<WeaponSystem>();
            if (component == null)
            {
                component = _selectedPrefab.AddComponent<WeaponSystem>();
            }
            
            var statSystem = _selectedPrefab.GetComponent<PlayerStatSystem>();
            var inventorySystem = _selectedPrefab.GetComponent<InventorySystem>();
            
            var so = new SerializedObject(component);
            so.FindProperty("_inventoryConfig").objectReferenceValue = inventoryConfig;
            so.FindProperty("_statSystem").objectReferenceValue = statSystem;
            so.FindProperty("_inventorySystem").objectReferenceValue = inventorySystem;
            so.FindProperty("_showDebugUI").boolValue = _enableDebugUI;
            so.ApplyModifiedProperties();
            
            Debug.Log("Setup: WeaponSystem");
            return 1;
        }
        
        private int SetupQuickSlotSystem(InventoryConfig inventoryConfig)
        {
            var component = _selectedPrefab.GetComponent<QuickSlotSystem>();
            if (component == null)
            {
                component = _selectedPrefab.AddComponent<QuickSlotSystem>();
            }
            
            var inventorySystem = _selectedPrefab.GetComponent<InventorySystem>();
            
            var so = new SerializedObject(component);
            so.FindProperty("_inventoryConfig").objectReferenceValue = inventoryConfig;
            so.FindProperty("_inventorySystem").objectReferenceValue = inventorySystem;
            so.FindProperty("_showDebugUI").boolValue = _enableDebugUI;
            so.ApplyModifiedProperties();
            
            Debug.Log("Setup: QuickSlotSystem");
            return 1;
        }
        
        private int SetupAttachmentSystem()
        {
            var component = _selectedPrefab.GetComponent<AttachmentSystem>();
            if (component == null)
            {
                component = _selectedPrefab.AddComponent<AttachmentSystem>();
            }
            
            var inventorySystem = _selectedPrefab.GetComponent<InventorySystem>();
            
            var so = new SerializedObject(component);
            so.FindProperty("_inventorySystem").objectReferenceValue = inventorySystem;
            so.ApplyModifiedProperties();
            
            Debug.Log("Setup: AttachmentSystem");
            return 1;
        }
        
        private int AddTestScripts()
        {
            int added = 0;
            
            // PlayerStatSystemTest
            if (_selectedPrefab.GetComponent<PlayerStatSystemTest>() == null)
            {
                var test = _selectedPrefab.AddComponent<PlayerStatSystemTest>();
                var so = new SerializedObject(test);
                so.FindProperty("_runTestsOnStart").boolValue = false; // User can enable manually
                so.ApplyModifiedProperties();
                added++;
            }
            
            // InventorySystemTest
            if (_selectedPrefab.GetComponent<InventorySystemTest>() == null)
            {
                var test = _selectedPrefab.AddComponent<InventorySystemTest>();
                var so = new SerializedObject(test);
                so.FindProperty("_testWeaponID").stringValue = "weapon_ak47";
                so.FindProperty("_testArmorID").stringValue = "armor_vest";
                so.FindProperty("_testConsumableID").stringValue = "consumable_medkit";
                so.FindProperty("_runTestsOnStart").boolValue = false;
                so.ApplyModifiedProperties();
                added++;
            }
            
            // EquipmentSystemTest
            if (_selectedPrefab.GetComponent<EquipmentSystemTest>() == null)
            {
                var test = _selectedPrefab.AddComponent<EquipmentSystemTest>();
                var so = new SerializedObject(test);
                so.FindProperty("_testHelmetID").stringValue = "armor_helmet";
                so.FindProperty("_testVestID").stringValue = "armor_vest";
                so.FindProperty("_testBackpackID").stringValue = "armor_backpack";
                so.FindProperty("_runTestsOnStart").boolValue = false;
                so.ApplyModifiedProperties();
                added++;
            }
            
            // ItemStatSystemTest
            if (_selectedPrefab.GetComponent<ItemStatSystemTest>() == null)
            {
                var test = _selectedPrefab.AddComponent<ItemStatSystemTest>();
                var so = new SerializedObject(test);
                so.FindProperty("_testWeaponID").stringValue = "weapon_ak47";
                so.FindProperty("_testScopeID").stringValue = "attachment_reddot";
                so.FindProperty("_testGripID").stringValue = "attachment_grip";
                so.FindProperty("_testSuppressorID").stringValue = "attachment_suppressor";
                so.FindProperty("_runTestsOnStart").boolValue = false;
                so.ApplyModifiedProperties();
                added++;
            }
            
            // IntegrationTest
            if (_selectedPrefab.GetComponent<IntegrationTest>() == null)
            {
                var test = _selectedPrefab.AddComponent<IntegrationTest>();
                var so = new SerializedObject(test);
                so.FindProperty("_weaponID").stringValue = "weapon_ak47";
                so.FindProperty("_armorID").stringValue = "armor_vest";
                so.FindProperty("_backpackID").stringValue = "armor_backpack";
                so.FindProperty("_consumableID").stringValue = "consumable_medkit";
                so.FindProperty("_runTestsOnStart").boolValue = false;
                so.ApplyModifiedProperties();
                added++;
            }
            
            if (added > 0)
            {
                Debug.Log($"Added {added} test scripts");
            }
            
            return added;
        }
        
        #endregion
        
        private void ShowManualGuide()
        {
            string guide = @"MANUAL SETUP GUIDE
================

1. ADD COMPONENTS (in this order):
   - PlayerStatSystem
   - InventorySystem
   - EquipmentSystem
   - WeaponSystem
   - QuickSlotSystem
   - AttachmentSystem

2. ASSIGN REFERENCES:

PlayerStatSystem:
   - Player Stat Config → Assets/Resources/Configs/PlayerStatConfig
   - Gameplay Config → Assets/Resources/Configs/GameplayConfig
   - Show Debug UI → Check if needed

InventorySystem:
   - Gameplay Config → Assets/Resources/Configs/GameplayConfig
   - Inventory Config → Assets/Resources/Configs/InventoryConfig
   - Stat System → (auto-assigned)
   - Show Debug UI → Check if needed

EquipmentSystem:
   - Inventory Config → Assets/Resources/Configs/InventoryConfig
   - Stat System → (auto-assigned)
   - Inventory System → (auto-assigned)
   - Show Debug UI → Check if needed

WeaponSystem:
   - Inventory Config → Assets/Resources/Configs/InventoryConfig
   - Stat System → (auto-assigned)
   - Inventory System → (auto-assigned)
   - Show Debug UI → Check if needed

QuickSlotSystem:
   - Inventory Config → Assets/Resources/Configs/InventoryConfig
   - Inventory System → (auto-assigned)
   - Show Debug UI → Check if needed

AttachmentSystem:
   - Inventory System → (auto-assigned)

3. OPTIONAL - ADD TEST SCRIPTS:
   - PlayerStatSystemTest
   - InventorySystemTest
   - EquipmentSystemTest
   - ItemStatSystemTest
   - IntegrationTest

4. CREATE ITEMDATABASE IN SCENE:
   - Create empty GameObject
   - Name it '[ItemDatabase]'
   - Add ItemDatabase component
   - Check 'Auto Load From Resources'
   - Resources Path: 'Items'
   - Check 'Track Instances'

5. TEST:
   - Play scene
   - Check console for 'Initialized X stats' messages
   - Check debug UIs (if enabled)
   - Run tests via context menus
";
            
            EditorUtility.DisplayDialog("Manual Setup Guide", guide, "OK");
        }
    }
}
#endif