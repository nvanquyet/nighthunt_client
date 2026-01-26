#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using NightHunt.InteractionSystem.Items.Data;
using NightHunt.InteractionSystem.Pickup.Handlers;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Editor.Windows
{
    /// <summary>
    /// Tool to create Interaction System data assets with default test values.
    /// </summary>
    public class DataCreationTool : EditorWindow
    {
        private const string DATA_FOLDER_PATH = "Assets/_Night_Hunt/Data/Interaction";
        
        private bool createWeapon = true;
        private bool createArmor = true;
        private bool createHelmet = true;
        private bool createBackpack = true;
        private bool createAttachment = true;
        private bool createPickupSettings = true;

        [MenuItem("NightHunt/InteractionSystem/Create Test Data")]
        public static void ShowWindow()
        {
            GetWindow<DataCreationTool>("Create Test Data");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Interaction System Data Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"Data will be created in: {DATA_FOLDER_PATH}", MessageType.Info);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Select Data Types to Create:", EditorStyles.boldLabel);
            createWeapon = EditorGUILayout.Toggle("Create Weapon Data", createWeapon);
            createArmor = EditorGUILayout.Toggle("Create Armor Data", createArmor);
            createHelmet = EditorGUILayout.Toggle("Create Helmet Data", createHelmet);
            createBackpack = EditorGUILayout.Toggle("Create Backpack Data", createBackpack);
            createAttachment = EditorGUILayout.Toggle("Create Attachment Data", createAttachment);
            createPickupSettings = EditorGUILayout.Toggle("Create Pickup Settings", createPickupSettings);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUILayout.Button("Create All Selected Data", GUILayout.Height(30)))
            {
                CreateAllData();
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Weapon Only"))
            {
                CreateWeaponData();
            }
            if (GUILayout.Button("Create Armor Only"))
            {
                CreateArmorData();
            }
            if (GUILayout.Button("Create Helmet Only"))
            {
                CreateHelmetData();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Backpack Only"))
            {
                CreateBackpackData();
            }
            if (GUILayout.Button("Create Attachment Only"))
            {
                CreateAttachmentData();
            }
            if (GUILayout.Button("Create Pickup Settings Only"))
            {
                CreatePickupSettings();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateAllData()
        {
            // Ensure folder exists
            if (!Directory.Exists(DATA_FOLDER_PATH))
            {
                Directory.CreateDirectory(DATA_FOLDER_PATH);
                AssetDatabase.Refresh();
            }

            int created = 0;

            if (createWeapon)
            {
                CreateWeaponData();
                created++;
            }

            if (createArmor)
            {
                CreateArmorData();
                created++;
            }

            if (createHelmet)
            {
                CreateHelmetData();
                created++;
            }

            if (createBackpack)
            {
                CreateBackpackData();
                created++;
            }

            if (createAttachment)
            {
                CreateAttachmentData();
                created++;
            }

            if (createPickupSettings)
            {
                CreatePickupSettings();
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Data Creation Complete", 
                $"Successfully created {created} data asset(s) in:\n{DATA_FOLDER_PATH}", "OK");
        }

        private void CreateWeaponData()
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            
            // Set base item properties
            SetBaseItemData(weapon, "weapon_ak47", "AK-47 Assault Rifle", 
                "A reliable assault rifle with good damage and fire rate.", 3.5f, ItemCategory.Weapon);

            // Set weapon-specific stats using reflection (since fields are private)
            var weaponType = typeof(WeaponData);
            var damageField = weaponType.GetField("damage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fireRateField = weaponType.GetField("fireRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var accuracyField = weaponType.GetField("accuracy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var recoilField = weaponType.GetField("recoil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rangeField = weaponType.GetField("range", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reloadSpeedField = weaponType.GetField("reloadSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (damageField != null) damageField.SetValue(weapon, 45f);
            if (fireRateField != null) fireRateField.SetValue(weapon, 600f);
            if (accuracyField != null) accuracyField.SetValue(weapon, 75f);
            if (recoilField != null) recoilField.SetValue(weapon, 50f);
            if (rangeField != null) rangeField.SetValue(weapon, 100f);
            if (reloadSpeedField != null) reloadSpeedField.SetValue(weapon, 2.5f);

            // Equipment slot is set in OnEnable, but we can force it
            var equipmentSlotField = typeof(EquipmentDataBase).GetField("equipmentSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (equipmentSlotField != null)
                equipmentSlotField.SetValue(weapon, EquipmentSlot.PrimaryWeapon);

            SaveAsset(weapon, "WeaponData_AK47.asset");
        }

        private void CreateArmorData()
        {
            ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();
            
            SetBaseItemData(armor, "armor_tactical_vest", "Tactical Vest", 
                "A tactical vest providing moderate protection.", 2.5f, ItemCategory.Armor);

            var armorType = typeof(ArmorData);
            var armorValueField = armorType.GetField("armorValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var damageReductionField = armorType.GetField("damageReduction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var movementSpeedField = armorType.GetField("movementSpeedModifier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (armorValueField != null) armorValueField.SetValue(armor, 50f);
            if (damageReductionField != null) damageReductionField.SetValue(armor, 0.3f);
            if (movementSpeedField != null) movementSpeedField.SetValue(armor, 1f);

            var equipmentSlotField = typeof(EquipmentDataBase).GetField("equipmentSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (equipmentSlotField != null)
                equipmentSlotField.SetValue(armor, EquipmentSlot.Body);

            SaveAsset(armor, "ArmorData_TacticalVest.asset");
        }

        private void CreateHelmetData()
        {
            HelmetData helmet = ScriptableObject.CreateInstance<HelmetData>();
            
            SetBaseItemData(helmet, "helmet_tactical", "Tactical Helmet", 
                "A tactical helmet providing head protection.", 1.2f, ItemCategory.Helmet);

            var helmetType = typeof(HelmetData);
            var headshotProtectionField = helmetType.GetField("headshotProtection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var visionRangeField = helmetType.GetField("visionRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var detectionRangeField = helmetType.GetField("detectionRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (headshotProtectionField != null) headshotProtectionField.SetValue(helmet, 0.5f);
            if (visionRangeField != null) visionRangeField.SetValue(helmet, 20f);
            if (detectionRangeField != null) detectionRangeField.SetValue(helmet, 15f);

            var equipmentSlotField = typeof(EquipmentDataBase).GetField("equipmentSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (equipmentSlotField != null)
                equipmentSlotField.SetValue(helmet, EquipmentSlot.Head);

            SaveAsset(helmet, "HelmetData_Tactical.asset");
        }

        private void CreateBackpackData()
        {
            BackpackData backpack = ScriptableObject.CreateInstance<BackpackData>();
            
            SetBaseItemData(backpack, "backpack_medium", "Medium Backpack", 
                "A medium-sized backpack with additional storage capacity.", 1.5f, ItemCategory.Backpack);

            var backpackType = typeof(BackpackData);
            var additionalSlotsField = backpackType.GetField("additionalSlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var additionalWeightField = backpackType.GetField("additionalWeightCapacity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (additionalSlotsField != null) additionalSlotsField.SetValue(backpack, 12);
            if (additionalWeightField != null) additionalWeightField.SetValue(backpack, 10f);

            var equipmentSlotField = typeof(EquipmentDataBase).GetField("equipmentSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (equipmentSlotField != null)
                equipmentSlotField.SetValue(backpack, EquipmentSlot.Backpack);

            SaveAsset(backpack, "BackpackData_Medium.asset");
        }

        private void CreateAttachmentData()
        {
            AttachmentData attachment = ScriptableObject.CreateInstance<AttachmentData>();
            
            SetBaseItemData(attachment, "attachment_red_dot_scope", "Red Dot Scope", 
                "A red dot sight attachment that improves accuracy.", 0.3f, ItemCategory.Attachment);

            var attachmentType = typeof(AttachmentData);
            var attachmentTypeField = attachmentType.GetField("attachmentType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var compatibleSlotsField = attachmentType.GetField("compatibleSlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var statModifiersField = attachmentType.GetField("statModifiers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (attachmentTypeField != null) attachmentTypeField.SetValue(attachment, "Optic");
            if (compatibleSlotsField != null) 
            {
                compatibleSlotsField.SetValue(attachment, new AttachmentSlotType[] { AttachmentSlotType.Scope });
            }
            if (statModifiersField != null)
            {
                // Create a stat modifier for accuracy improvement
                StatModifier accuracyModifier = new StatModifier(StatType.Accuracy, ModifierType.Additive, 10f, "Red Dot Scope");
                statModifiersField.SetValue(attachment, new StatModifier[] { accuracyModifier });
            }

            SaveAsset(attachment, "AttachmentData_RedDotScope.asset");
        }

        private void CreatePickupSettings()
        {
            PickupSettings settings = ScriptableObject.CreateInstance<PickupSettings>();
            
            var settingsType = typeof(PickupSettings);
            var autoPickupEnabledField = settingsType.GetField("autoPickupEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var autoPickupRadiusField = settingsType.GetField("autoPickupRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var autoPickupCategoriesField = settingsType.GetField("autoPickupCategories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var showPickupPromptField = settingsType.GetField("showPickupPrompt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pickupPromptRangeField = settingsType.GetField("pickupPromptRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (autoPickupEnabledField != null) autoPickupEnabledField.SetValue(settings, true);
            if (autoPickupRadiusField != null) autoPickupRadiusField.SetValue(settings, 2f);
            if (autoPickupCategoriesField != null) 
            {
                autoPickupCategoriesField.SetValue(settings, new ItemCategory[] { ItemCategory.Ammo, ItemCategory.Consumable });
            }
            if (showPickupPromptField != null) showPickupPromptField.SetValue(settings, true);
            if (pickupPromptRangeField != null) pickupPromptRangeField.SetValue(settings, 5f);

            SaveAsset(settings, "PickupSettings_Default.asset");
        }

        private void SetBaseItemData(ItemDataBase item, string itemId, string displayName, string description, float weight, ItemCategory category)
        {
            var itemType = typeof(ItemDataBase);
            var itemIdField = itemType.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var displayNameField = itemType.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descriptionField = itemType.GetField("description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var weightField = itemType.GetField("weight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var categoryField = itemType.GetField("category", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxStackField = itemType.GetField("maxStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isStackableField = itemType.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (itemIdField != null) itemIdField.SetValue(item, itemId);
            if (displayNameField != null) displayNameField.SetValue(item, displayName);
            if (descriptionField != null) descriptionField.SetValue(item, description);
            if (weightField != null) weightField.SetValue(item, weight);
            if (categoryField != null) categoryField.SetValue(item, category);
            if (maxStackField != null) maxStackField.SetValue(item, 1);
            if (isStackableField != null) isStackableField.SetValue(item, false);
        }

        private void SaveAsset(ScriptableObject asset, string fileName)
        {
            string path = Path.Combine(DATA_FOLDER_PATH, fileName);
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create asset
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[DataCreationTool] Created: {path}");
        }
    }
}
#endif
