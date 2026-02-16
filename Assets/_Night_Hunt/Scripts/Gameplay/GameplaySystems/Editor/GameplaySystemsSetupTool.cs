#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using GameplaySystems.Core.Configs;
using GameplaySystems.Core.Data;

namespace GameplaySystems.Editor
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
            
            // PlayerStatConfig
            if (!AssetExists($"{CONFIG_PATH}/PlayerStatConfig.asset"))
            {
                var config = ScriptableObject.CreateInstance<PlayerStatConfig>();
                AssetDatabase.CreateAsset(config, $"{CONFIG_PATH}/PlayerStatConfig.asset");
                
                // Setup default stats via SerializedObject to call the method
                var so = new SerializedObject(config);
                config.GetType().GetMethod("SetupDefaultStats", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(config, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                
                created++;
                Debug.Log("Created: PlayerStatConfig");
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
                
                // Setup via reflection
                var so = new SerializedObject(m4);
                m4.GetType().GetMethod("SetupDefaultRifleStats", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(m4, null);
                so.ApplyModifiedProperties();
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
            
            var so = new SerializedObject(weapon);
            weapon.GetType().GetMethod(setupMethod, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(weapon, null);
            so.ApplyModifiedProperties();
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
                var belt = ScriptableObject.CreateInstance<ArmorDefinition>();
                belt.ItemID = "armor_belt";
                belt.DisplayName = "Tactical Belt";
                belt.Description = "Belt with extra pouches";
                belt.Weight = 1f;
                
                AssetDatabase.CreateAsset(belt, $"{ITEMS_PATH}/Armor/Armor_Belt.asset");
                EditorUtility.SetDirty(belt);
                created++;
                Debug.Log("Created: Belt");
            }
            
            // Gloves (custom)
            if (!AssetExists($"{ITEMS_PATH}/Armor/Armor_Gloves.asset"))
            {
                var gloves = ScriptableObject.CreateInstance<ArmorDefinition>();
                gloves.ItemID = "armor_gloves";
                gloves.DisplayName = "Tactical Gloves";
                gloves.Description = "Improves grip and handling";
                gloves.Weight = 0.2f;
                
                AssetDatabase.CreateAsset(gloves, $"{ITEMS_PATH}/Armor/Armor_Gloves.asset");
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
            
            var armor = ScriptableObject.CreateInstance<ArmorDefinition>();
            armor.ItemID = itemID;
            AssetDatabase.CreateAsset(armor, path);
            
            var so = new SerializedObject(armor);
            armor.GetType().GetMethod(setupMethod, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(armor, null);
            so.ApplyModifiedProperties();
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
                
                var so = new SerializedObject(medkit);
                medkit.GetType().GetMethod("SetupDefaultMedkit", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(medkit, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(medkit);
                
                created++;
                Debug.Log("Created: Medkit");
            }
            
            // Energy Drink
            if (!AssetExists($"{ITEMS_PATH}/Consumables/Consumable_EnergyDrink.asset"))
            {
                var drink = ScriptableObject.CreateInstance<ConsumableDefinition>();
                drink.ItemID = "consumable_energydrink";
                drink.DisplayName = "Energy Drink";
                drink.Description = "Restores stamina";
                drink.IsStackable = true;
                drink.MaxStackSize = 5;
                drink.Weight = 0.3f;
                
                AssetDatabase.CreateAsset(drink, $"{ITEMS_PATH}/Consumables/Consumable_EnergyDrink.asset");
                EditorUtility.SetDirty(drink);
                
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
                
                var so = new SerializedObject(attachmentDef);
                attachmentDef.GetType().GetMethod(attachment.setupMethod, 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(attachmentDef, null);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(attachmentDef);
                
                created++;
                Debug.Log($"Created: {attachment.fileName}");
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