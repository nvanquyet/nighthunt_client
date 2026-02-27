#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.StatSystem.Configs;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Editor;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Editor
{
    [CustomEditor(typeof(AttachmentDefinition))]
    public class AttachmentDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Sample Setup", EditorStyles.boldLabel);
            
            var attachment = (AttachmentDefinition)target;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Red Dot")) SetupRedDot(attachment);
            if (GUILayout.Button("Suppressor")) SetupSuppressor(attachment);
            if (GUILayout.Button("Grip")) SetupGrip(attachment);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ext Magazine")) SetupExtMag(attachment);
            if (GUILayout.Button("Flashlight")) SetupFlashlight(attachment);
            if (GUILayout.Button("Pouch")) SetupPouch(attachment);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("4x Scope")) Setup4xScope(attachment);
            if (GUILayout.Button("8x Scope")) Setup8xScope(attachment);
            EditorGUILayout.EndHorizontal();
        }
        
        private void SetupRedDot(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_RedDot");
            ItemStatConfigSetup.SetupRedDot(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "Red Dot Sight";
            attachment.Description = "Improves accuracy and aim speed at close-medium range";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Optic };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void SetupSuppressor(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Suppressor");
            ItemStatConfigSetup.SetupSuppressor(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "Suppressor";
            attachment.Description = "Reduces weapon noise and recoil but decreases range";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Barrel };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void SetupGrip(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Grip");
            ItemStatConfigSetup.SetupVerticalGrip(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "Vertical Grip";
            attachment.Description = "Improves recoil control and stability";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Grip };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void SetupExtMag(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_ExtMag");
            ItemStatConfigSetup.SetupExtendedMagazine(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "Extended Magazine";
            attachment.Description = "Increases magazine capacity but adds weight";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Magazine };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void SetupFlashlight(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Flashlight");
            ItemStatConfigSetup.SetupFlashlight(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "Tactical Flashlight";
            attachment.Description = "Provides illumination in dark areas";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.UnderBarrel, AttachmentSlotType.Light };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void SetupPouch(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Pouch");
            ItemStatConfigSetup.SetupStoragePouch(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "Storage Pouch";
            attachment.Description = "Increases weight capacity when attached";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Pouch };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void Setup4xScope(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_4xScope");
            ItemStatConfigSetup.Setup4xScope(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "4x ACOG Scope";
            attachment.Description = "Medium range scope";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Optic };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        private void Setup8xScope(AttachmentDefinition attachment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_8xScope");
            ItemStatConfigSetup.Setup8xScope(config);
            attachment.StatConfig = config;
            attachment.DisplayName = "8x Sniper Scope";
            attachment.Description = "Long range scope for snipers";
            attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Optic };
            attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(attachment);
        }
        
        public static class SetupHelpers
        {
            public static void SetupRedDotScope(AttachmentDefinition attachment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_RedDot");
                ItemStatConfigSetup.SetupRedDot(config);
                attachment.StatConfig = config;
                attachment.DisplayName = "Red Dot Sight";
                attachment.Description = "Improves accuracy and aim speed";
                attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Optic };
                attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(attachment);
            }
            public static void SetupSuppressor(AttachmentDefinition attachment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Suppressor");
                ItemStatConfigSetup.SetupSuppressor(config);
                attachment.StatConfig = config;
                attachment.DisplayName = "Suppressor";
                attachment.Description = "Reduces recoil, decreases range";
                attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Barrel };
                attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(attachment);
            }
            public static void SetupVerticalGrip(AttachmentDefinition attachment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Grip");
                ItemStatConfigSetup.SetupVerticalGrip(config);
                attachment.StatConfig = config;
                attachment.DisplayName = "Vertical Grip";
                attachment.Description = "Improves recoil control";
                attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Grip };
                attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(attachment);
            }
            public static void SetupExtendedMagazine(AttachmentDefinition attachment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_ExtMag");
                ItemStatConfigSetup.SetupExtendedMagazine(config);
                attachment.StatConfig = config;
                attachment.DisplayName = "Extended Magazine";
                attachment.Description = "+50% magazine capacity";
                attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Magazine };
                attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(attachment);
            }
            public static void SetupTacticalFlashlight(AttachmentDefinition attachment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Flashlight");
                ItemStatConfigSetup.SetupFlashlight(config);
                attachment.StatConfig = config;
                attachment.DisplayName = "Tactical Flashlight";
                attachment.Description = "Provides illumination";
                attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.UnderBarrel, AttachmentSlotType.Light };
                attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(attachment);
            }
            public static void SetupStoragePouch(AttachmentDefinition attachment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<AttachmentStatConfig>("AttachmentStatConfig_Pouch");
                ItemStatConfigSetup.SetupStoragePouch(config);
                attachment.StatConfig = config;
                attachment.DisplayName = "Storage Pouch";
                attachment.Description = "Increases weight capacity";
                attachment.CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Pouch };
                attachment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(attachment);
            }
        }
    }
}
#endif
