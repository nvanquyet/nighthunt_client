#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;
using NightHunt.StatSystem.Editor;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Editor
{
    [CustomEditor(typeof(WeaponDefinition))]
    public class WeaponDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Sample Setup", EditorStyles.boldLabel);
            
            var weapon = (WeaponDefinition)target;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Setup Rifle")) SetupRifle(weapon);
            if (GUILayout.Button("Setup Pistol")) SetupPistol(weapon);
            EditorGUILayout.EndHorizontal();
        }
        
        private void SetupRifle(WeaponDefinition weapon)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<WeaponStatConfig>("WeaponStatConfig_Rifle");
            ItemStatConfigSetup.SetupRifle(config);
            weapon.StatConfig = config;
            weapon.MagazineSize = 30;
            weapon.MaxAmmo = 300;
            weapon.DefaultAmmo = 300;
            weapon.ReloadTime = 2.5f;
            weapon.CanTacticalReload = true;
            weapon.ResourceType = ItemResourceType.Ammo;
            weapon.MaxResource = weapon.MaxAmmo;
            weapon.DefaultResource = weapon.DefaultAmmo;
            weapon.Weight = config.GetStatValue(ItemStatType.Weight);
            weapon.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
            weapon.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Optic, AttachmentSlotType.Grip, AttachmentSlotType.Magazine, AttachmentSlotType.Barrel, AttachmentSlotType.UnderBarrel };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(weapon);
            Debug.Log("[WeaponDefinitionEditor] Rifle setup complete.");
        }
        
        private void SetupPistol(WeaponDefinition weapon)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<WeaponStatConfig>("WeaponStatConfig_Pistol");
            ItemStatConfigSetup.SetupPistol(config);
            weapon.StatConfig = config;
            weapon.MagazineSize = 12;
            weapon.MaxAmmo = 120;
            weapon.DefaultAmmo = 120;
            weapon.ReloadTime = 1.8f;
            weapon.CanTacticalReload = true;
            weapon.ResourceType = ItemResourceType.Ammo;
            weapon.MaxResource = weapon.MaxAmmo;
            weapon.DefaultResource = weapon.DefaultAmmo;
            weapon.Weight = config.GetStatValue(ItemStatType.Weight);
            weapon.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
            weapon.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Barrel, AttachmentSlotType.Magazine };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(weapon);
            Debug.Log("[WeaponDefinitionEditor] Pistol setup complete.");
        }
        
        public static class SetupHelpers
        {
            public static void SetupDefaultRifleStats(WeaponDefinition weapon)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<WeaponStatConfig>("WeaponStatConfig_Rifle");
                ItemStatConfigSetup.SetupRifle(config);
                weapon.StatConfig = config;
                weapon.MagazineSize = 30;
                weapon.MaxAmmo = 300;
                weapon.DefaultAmmo = 300;
                weapon.ReloadTime = 2.5f;
                weapon.CanTacticalReload = true;
                weapon.ResourceType = ItemResourceType.Ammo;
                weapon.MaxResource = weapon.MaxAmmo;
                weapon.DefaultResource = weapon.DefaultAmmo;
                weapon.Weight = config.GetStatValue(ItemStatType.Weight);
                weapon.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
                weapon.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Optic, AttachmentSlotType.Grip, AttachmentSlotType.Magazine, AttachmentSlotType.Barrel, AttachmentSlotType.UnderBarrel };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(weapon);
            }
            public static void SetupDefaultPistolStats(WeaponDefinition weapon)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<WeaponStatConfig>("WeaponStatConfig_Pistol");
                ItemStatConfigSetup.SetupPistol(config);
                weapon.StatConfig = config;
                weapon.MagazineSize = 12;
                weapon.MaxAmmo = 120;
                weapon.DefaultAmmo = 120;
                weapon.ReloadTime = 1.8f;
                weapon.CanTacticalReload = true;
                weapon.ResourceType = ItemResourceType.Ammo;
                weapon.MaxResource = weapon.MaxAmmo;
                weapon.DefaultResource = weapon.DefaultAmmo;
                weapon.Weight = config.GetStatValue(ItemStatType.Weight);
                weapon.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
                weapon.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Barrel, AttachmentSlotType.Magazine };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(weapon);
            }
        }
    }
}
#endif
