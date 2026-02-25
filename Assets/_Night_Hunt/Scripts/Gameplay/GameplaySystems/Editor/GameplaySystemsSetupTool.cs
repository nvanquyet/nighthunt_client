#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Editor
{
    /// <summary>
    /// Editor utility for quickly creating all required ScriptableObjects
    /// Menu: Tools → GameplaySystems → Setup All Configs & Sample Items
    /// </summary>
    public class GameplaySystemsSetupTool : EditorWindow
    {
        private const string CONFIG_PATH = "Assets/Resources/Configs";
        private const string ITEMS_PATH = "Assets/Resources/Items";
        
        private bool _createConfigs = true;
        private bool _createWeapons = true;
        private bool _createArmor = true;
        private bool _createConsumables = true;
        private bool _createAttachments = true;
        private bool _createThrowables = true;
        
        [MenuItem("Tools/GameplaySystems/Setup Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameplaySystemsSetupTool>("Setup Tool");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("GameplaySystems Setup Tool", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "This tool will create all required configs and sample items for testing.\n\n" +
                "Folders will be created in:\n" +
                "- Assets/Resources/Configs\n" +
                "- Assets/Resources/Items",
                MessageType.Info);
            
            GUILayout.Space(10);
            
            // Config section
            GUILayout.Label("Configs", EditorStyles.boldLabel);
            _createConfigs = EditorGUILayout.Toggle("Create Configs", _createConfigs);
            
            if (_createConfigs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("• PlayerStatConfig", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• PlayerStatUIConfig", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• ItemStatUIConfig", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• GameplayConfig", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• InventoryConfig", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            
            GUILayout.Space(10);
            
            // Items section
            GUILayout.Label("Sample Items", EditorStyles.boldLabel);
            _createWeapons = EditorGUILayout.Toggle("Create Weapons (3)", _createWeapons);
            _createArmor = EditorGUILayout.Toggle("Create Armor (5)", _createArmor);
            _createConsumables = EditorGUILayout.Toggle("Create Consumables (2)", _createConsumables);
            _createAttachments = EditorGUILayout.Toggle("Create Attachments (6)", _createAttachments);
            _createThrowables = EditorGUILayout.Toggle("Create Throwables (2)", _createThrowables);
            
            GUILayout.Space(20);
            
            // Create button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Create All Selected", GUILayout.Height(40)))
            {
                CreateSelectedAssets();
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.Space(10);
            
            // Quick actions
            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Open Configs Folder"))
            {
                OpenFolder(CONFIG_PATH);
            }
            
            if (GUILayout.Button("Open Items Folder"))
            {
                OpenFolder(ITEMS_PATH);
            }
            
            if (GUILayout.Button("Ping ItemDatabase in Scene"))
            {
                var database = FindObjectOfType<GameplaySystems.Inventory.ItemDatabase>();
                if (database != null)
                {
                    Selection.activeGameObject = database.gameObject;
                    EditorGUIUtility.PingObject(database.gameObject);
                }
                else
                {
                    Debug.LogWarning("ItemDatabase not found in scene. Please create one.");
                }
            }
        }
        
        private void CreateSelectedAssets()
        {
            int created = 0;
            
            if (_createConfigs)
            {
                created += CreateConfigs();
            }
            
            if (_createWeapons)
            {
                created += CreateWeapons();
            }
            
            if (_createArmor)
            {
                created += CreateArmor();
            }
            
            if (_createConsumables)
            {
                created += CreateConsumables();
            }
            
            if (_createAttachments)
            {
                created += CreateAttachments();
            }
            
            if (_createThrowables)
            {
                created += CreateThrowables();
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"<color=green>✓ Created {created} assets successfully!</color>");
            EditorUtility.DisplayDialog("Success", $"Created {created} assets!\n\nCheck Assets/Resources folder.", "OK");
        }
        
        #region Create Configs
        
        private int CreateConfigs()
        {
            EnsureDirectory(CONFIG_PATH);
            int created = 0;
            
            // PlayerStatConfig (gameplay)
            if (!AssetExists($"{CONFIG_PATH}/PlayerStatConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<PlayerStatConfig>();
                AssetDatabase.CreateAsset(config, $"{CONFIG_PATH}/PlayerStatConfig.asset");
                
                var so = new SerializedObject(config);
                config.GetType().GetMethod("SetupDefaultStats", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(config, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                
                created++;
                Debug.Log("Created: PlayerStatConfig");
            }
            
            // PlayerStatUIConfig (UI display)
            if (!AssetExists($"{CONFIG_PATH}/PlayerStatUIConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<PlayerStatUIConfig>();
                AssetDatabase.CreateAsset(config, $"{CONFIG_PATH}/PlayerStatUIConfig.asset");
                
                var so = new SerializedObject(config);
                config.GetType().GetMethod("SetupDefaultUIStats", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(config, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                
                created++;
                Debug.Log("Created: PlayerStatUIConfig");
            }
            
            // ItemStatUIConfig (item tooltip UI)
            if (!AssetExists($"{CONFIG_PATH}/ItemStatUIConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<ItemStatUIConfig>();
                AssetDatabase.CreateAsset(config, $"{CONFIG_PATH}/ItemStatUIConfig.asset");
                
                var so = new SerializedObject(config);
                config.GetType().GetMethod("SetupDefaultItemStats", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(config, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                
                created++;
                Debug.Log("Created: ItemStatUIConfig");
            }
            
            // GameplayConfig
            if (!AssetExists($"{CONFIG_PATH}/GameplayConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<GameplayConfig>();
                AssetDatabase.CreateAsset(config, $"{CONFIG_PATH}/GameplayConfig.asset");
                EditorUtility.SetDirty(config);
                created++;
                Debug.Log("Created: GameplayConfig");
            }
            
            // InventoryConfig
            if (!AssetExists($"{CONFIG_PATH}/InventoryConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<InventoryConfig>();
                AssetDatabase.CreateAsset(config, $"{CONFIG_PATH}/InventoryConfig.asset");
                
                // Setup via reflection
                var so = new SerializedObject(config);
                config.GetType().GetMethod("SetupDefaultEquipmentSlots", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(config, null);
                config.GetType().GetMethod("SetupDefaultWeaponSlots", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(config, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                
                created++;
                Debug.Log("Created: InventoryConfig");
            }
            
            return created;
        }
        
        #endregion
        
        #region Create Items
        
        private int CreateWeapons()
        {
            EnsureDirectory($"{ITEMS_PATH}/Weapons");
            int created = 0;
            
            // AK-47
            created += CreateWeapon("Weapon_AK47", "weapon_ak47", "SetupDefaultRifleStats");
            
            // Pistol
            created += CreateWeapon("Weapon_Pistol", "weapon_pistol", "SetupDefaultPistolStats");
            
            // M4 (custom)
            if (!AssetExists($"{ITEMS_PATH}/Weapons/Weapon_M4.asset"))
            {
                var m4 = ScriptableObject.CreateInstance<WeaponDefinition>();
                m4.ItemID = "weapon_m4";
                m4.DisplayName = "M4A1";
                m4.Description = "American assault rifle with high accuracy";
                m4.Weight = 3.2f;
                
                AssetDatabase.CreateAsset(m4, $"{ITEMS_PATH}/Weapons/Weapon_M4.asset");
                
                // Setup via static helper from Editor class
                WeaponDefinitionEditor.SetupHelpers.SetupDefaultRifleStats(m4);
                EditorUtility.SetDirty(m4);
                
                created++;
                Debug.Log("Created: M4A1");
            }
            
            return created;
        }
        
        private int CreateWeapon(string fileName, string itemID, string setupMethod)
        {
            string path = $"{ITEMS_PATH}/Weapons/{fileName}.asset";
            if (AssetExists(path))
                return 0;
            
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.ItemID = itemID;
            AssetDatabase.CreateAsset(weapon, path);
            
            // Setup via static helper from Editor class
            if (setupMethod == "SetupDefaultRifleStats")
            {
                WeaponDefinitionEditor.SetupHelpers.SetupDefaultRifleStats(weapon);
            }
            else if (setupMethod == "SetupDefaultPistolStats")
            {
                WeaponDefinitionEditor.SetupHelpers.SetupDefaultPistolStats(weapon);
            }
            EditorUtility.SetDirty(weapon);
            
            Debug.Log($"Created: {fileName}");
            return 1;
        }
        
        private int CreateArmor()
        {
            EnsureDirectory($"{ITEMS_PATH}/Armor");
            int created = 0;
            
            // Helmet
            created += CreateArmorItem("Armor_Helmet", "armor_helmet", "SetupDefaultHelmetStats");
            
            // Vest
            created += CreateArmorItem("Armor_Vest", "armor_vest", "SetupDefaultVestStats");
            
            // Backpack
            created += CreateArmorItem("Armor_Backpack", "armor_backpack", "SetupDefaultBackpackStats");
            
            // Belt (custom)
            if (!AssetExists($"{ITEMS_PATH}/Armor/Armor_Belt.asset"))
            {
                var belt = ScriptableObject.CreateInstance<EquipmentDefinition>();
                belt.ItemID = "armor_belt";
                belt.DisplayName = "Tactical Belt";
                belt.Description = "Belt with extra pouches";
                
                AssetDatabase.CreateAsset(belt, $"{ITEMS_PATH}/Armor/Armor_Belt.asset");
                
                // Setup via Editor helper
                EquipmentDefinitionEditor.SetupHelpers.SetupDefaultBeltStats(belt);
                EditorUtility.SetDirty(belt);
                
                created++;
                Debug.Log("Created: Belt");
            }
            
            // Gloves (custom)
            if (!AssetExists($"{ITEMS_PATH}/Armor/Armor_Gloves.asset"))
            {
                var gloves = ScriptableObject.CreateInstance<EquipmentDefinition>();
                gloves.ItemID = "armor_gloves";
                gloves.DisplayName = "Tactical Gloves";
                gloves.Description = "Improves grip and handling";
                
                AssetDatabase.CreateAsset(gloves, $"{ITEMS_PATH}/Armor/Armor_Gloves.asset");
                
                // Setup via Editor helper
                EquipmentDefinitionEditor.SetupHelpers.SetupDefaultGlovesStats(gloves);
                EditorUtility.SetDirty(gloves);
                
                created++;
                Debug.Log("Created: Gloves");
            }
            
            return created;
        }
        
        private int CreateArmorItem(string fileName, string itemID, string setupMethod)
        {
            string path = $"{ITEMS_PATH}/Armor/{fileName}.asset";
            if (AssetExists(path))
                return 0;
            
            var armor = ScriptableObject.CreateInstance<EquipmentDefinition>();
            armor.ItemID = itemID;
            AssetDatabase.CreateAsset(armor, path);
            
            // Setup via static helper from Editor class
            if (setupMethod == "SetupDefaultVestStats")
            {
                EquipmentDefinitionEditor.SetupHelpers.SetupDefaultVestStats(armor);
            }
            else if (setupMethod == "SetupDefaultBackpackStats")
            {
                EquipmentDefinitionEditor.SetupHelpers.SetupDefaultBackpackStats(armor);
            }
            else if (setupMethod == "SetupDefaultHelmetStats")
            {
                EquipmentDefinitionEditor.SetupHelpers.SetupDefaultHelmetStats(armor);
            }
            EditorUtility.SetDirty(armor);
            
            Debug.Log($"Created: {fileName}");
            return 1;
        }
        
        private int CreateConsumables()
        {
            EnsureDirectory($"{ITEMS_PATH}/Consumables");
            int created = 0;
            
            // Medkit
            if (!AssetExists($"{ITEMS_PATH}/Consumables/Consumable_Medkit.asset"))
            {
                var medkit = ScriptableObject.CreateInstance<ConsumableDefinition>();
                medkit.ItemID = "consumable_medkit";
                
                AssetDatabase.CreateAsset(medkit, $"{ITEMS_PATH}/Consumables/Consumable_Medkit.asset");
                
                ConsumableDefinitionEditor.SetupHelpers.SetupDefaultMedkit(medkit);
                
                created++;
                Debug.Log("Created: Medkit");
            }
            
            // Energy Drink
            if (!AssetExists($"{ITEMS_PATH}/Consumables/Consumable_EnergyDrink.asset"))
            {
                var drink = ScriptableObject.CreateInstance<ConsumableDefinition>();
                drink.ItemID = "consumable_energydrink";
                
                AssetDatabase.CreateAsset(drink, $"{ITEMS_PATH}/Consumables/Consumable_EnergyDrink.asset");
                
                ConsumableDefinitionEditor.SetupHelpers.SetupDefaultEnergyDrink(drink);
                
                created++;
                Debug.Log("Created: Energy Drink");
            }
            
            return created;
        }
        
        private int CreateAttachments()
        {
            EnsureDirectory($"{ITEMS_PATH}/Attachments");
            int created = 0;
            
            var attachments = new (string fileName, string itemID, string setupMethod)[]
            {
                ("Attachment_RedDot", "attachment_reddot", "SetupRedDotScope"),
                ("Attachment_Suppressor", "attachment_suppressor", "SetupSuppressor"),
                ("Attachment_Grip", "attachment_grip", "SetupVerticalGrip"),
                ("Attachment_ExtMag", "attachment_extmag", "SetupExtendedMagazine"),
                ("Attachment_Flashlight", "attachment_flashlight", "SetupTacticalFlashlight"),
                ("Attachment_Pouch", "attachment_pouch", "SetupStoragePouch")
            };
            
            foreach (var attachment in attachments)
            {
                string path = $"{ITEMS_PATH}/Attachments/{attachment.fileName}.asset";
                if (AssetExists(path))
                    continue;
                
                var attachmentDef = ScriptableObject.CreateInstance<AttachmentDefinition>();
                attachmentDef.ItemID = attachment.itemID;
                AssetDatabase.CreateAsset(attachmentDef, path);
                
                // Setup via static helper from Editor class
                switch (attachment.setupMethod)
                {
                    case "SetupRedDotScope":
                        AttachmentDefinitionEditor.SetupHelpers.SetupRedDotScope(attachmentDef);
                        break;
                    case "SetupSuppressor":
                        AttachmentDefinitionEditor.SetupHelpers.SetupSuppressor(attachmentDef);
                        break;
                    case "SetupVerticalGrip":
                        AttachmentDefinitionEditor.SetupHelpers.SetupVerticalGrip(attachmentDef);
                        break;
                    case "SetupExtendedMagazine":
                        AttachmentDefinitionEditor.SetupHelpers.SetupExtendedMagazine(attachmentDef);
                        break;
                    case "SetupTacticalFlashlight":
                        AttachmentDefinitionEditor.SetupHelpers.SetupTacticalFlashlight(attachmentDef);
                        break;
                    case "SetupStoragePouch":
                        AttachmentDefinitionEditor.SetupHelpers.SetupStoragePouch(attachmentDef);
                        break;
                }
                EditorUtility.SetDirty(attachmentDef);
                
                created++;
                Debug.Log($"Created: {attachment.fileName}");
            }
            
            return created;
        }
        
        private int CreateThrowables()
        {
            EnsureDirectory($"{ITEMS_PATH}/Throwables");
            int created = 0;
            
            // Frag Grenade
            if (!AssetExists($"{ITEMS_PATH}/Throwables/Throwable_FragGrenade.asset"))
            {
                var frag = ScriptableObject.CreateInstance<ThrowableDefinition>();
                frag.ItemID = "throwable_fraggrenade";
                
                AssetDatabase.CreateAsset(frag, $"{ITEMS_PATH}/Throwables/Throwable_FragGrenade.asset");
                
                ThrowableDefinitionEditor.SetupHelpers.SetupDefaultFragGrenade(frag);
                
                created++;
                Debug.Log("Created: Frag Grenade");
            }
            
            // Smoke Grenade
            if (!AssetExists($"{ITEMS_PATH}/Throwables/Throwable_SmokeGrenade.asset"))
            {
                var smoke = ScriptableObject.CreateInstance<ThrowableDefinition>();
                smoke.ItemID = "throwable_smokegrenade";
                
                AssetDatabase.CreateAsset(smoke, $"{ITEMS_PATH}/Throwables/Throwable_SmokeGrenade.asset");
                
                ThrowableDefinitionEditor.SetupHelpers.SetupDefaultSmokeGrenade(smoke);
                
                created++;
                Debug.Log("Created: Smoke Grenade");
            }
            
            return created;
        }
        
        #endregion
        
        #region Helpers
        
        private void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path);
                string folder = Path.GetFileName(path);
                
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectory(parent);
                }
                
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
        
        private bool AssetExists(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Object>(path) != null;
        }
        
        private void OpenFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogWarning($"Folder does not exist: {path}");
            }
        }
        
        #endregion
    }
}
#endif