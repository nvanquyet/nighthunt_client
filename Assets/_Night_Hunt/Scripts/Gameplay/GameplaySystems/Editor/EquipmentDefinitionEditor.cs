#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Editor;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Editor
{
    [CustomEditor(typeof(EquipmentDefinition))]
    public class EquipmentDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
        private void SetupVest(EquipmentDefinition equipment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Vest");
            ItemStatConfigSetup.SetupVest(config);
            ApplyVestFields(equipment, config);
            equipment.StatConfig = config;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(equipment);
            Debug.Log("[EquipmentDefinitionEditor] Vest setup complete.");
        }
        
        private void SetupBackpack(EquipmentDefinition equipment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Backpack");
            ItemStatConfigSetup.SetupBackpack(config);
            ApplyBackpackFields(equipment, config);
            equipment.StatConfig = config;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(equipment);
            Debug.Log("[EquipmentDefinitionEditor] Backpack setup complete.");
        }
        
        private void SetupHelmet(EquipmentDefinition equipment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Helmet");
            ItemStatConfigSetup.SetupHelmet(config);
            ApplyHelmetFields(equipment, config);
            equipment.StatConfig = config;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(equipment);
            Debug.Log("[EquipmentDefinitionEditor] Helmet setup complete.");
        }
        
        private void SetupBelt(EquipmentDefinition equipment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Belt");
            ItemStatConfigSetup.SetupBelt(config);
            ApplyBeltFields(equipment, config);
            equipment.StatConfig = config;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(equipment);
            Debug.Log("[EquipmentDefinitionEditor] Belt setup complete.");
        }
        
        private void SetupGloves(EquipmentDefinition equipment)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Gloves");
            ItemStatConfigSetup.SetupGloves(config);
            ApplyGlovesFields(equipment, config);
            equipment.StatConfig = config;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(equipment);
            Debug.Log("[EquipmentDefinitionEditor] Gloves setup complete.");
        }
        
        private void ApplyVestFields(EquipmentDefinition equipment, EquipmentStatConfig config)
        {
            equipment.EquipmentSlot = EquipmentSlotType.Chest;
            equipment.DurabilityLossRate = 1f;
            equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            equipment.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Light, AttachmentSlotType.Pouch, AttachmentSlotType.Pouch, AttachmentSlotType.Plate };
        }
        
        private void ApplyBackpackFields(EquipmentDefinition equipment, EquipmentStatConfig config)
        {
            equipment.EquipmentSlot = EquipmentSlotType.Back;
            equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            equipment.AttachmentSlots = new AttachmentSlotType[0];
        }
        
        private void ApplyHelmetFields(EquipmentDefinition equipment, EquipmentStatConfig config)
        {
            equipment.EquipmentSlot = EquipmentSlotType.Head;
            equipment.DurabilityLossRate = 1f;
            equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            equipment.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Light };
        }
        
        private void ApplyBeltFields(EquipmentDefinition equipment, EquipmentStatConfig config)
        {
            equipment.EquipmentSlot = EquipmentSlotType.Belt;
            equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            equipment.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Pouch, AttachmentSlotType.Pouch };
        }
        
        private void ApplyGlovesFields(EquipmentDefinition equipment, EquipmentStatConfig config)
        {
            equipment.EquipmentSlot = EquipmentSlotType.Hands;
            equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
            equipment.AttachmentSlots = new AttachmentSlotType[0];
        }
        
        public static class SetupHelpers
        {
            public static void SetupDefaultVestStats(EquipmentDefinition equipment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Vest");
                ItemStatConfigSetup.SetupVest(config);
                equipment.StatConfig = config;
                equipment.EquipmentSlot = EquipmentSlotType.Chest;
                equipment.DurabilityLossRate = 1f;
                equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
                equipment.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Light, AttachmentSlotType.Pouch, AttachmentSlotType.Pouch, AttachmentSlotType.Plate };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(equipment);
            }
            public static void SetupDefaultBackpackStats(EquipmentDefinition equipment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Backpack");
                ItemStatConfigSetup.SetupBackpack(config);
                equipment.StatConfig = config;
                equipment.EquipmentSlot = EquipmentSlotType.Back;
                equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
                equipment.AttachmentSlots = new AttachmentSlotType[0];
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(equipment);
            }
            public static void SetupDefaultHelmetStats(EquipmentDefinition equipment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Helmet");
                ItemStatConfigSetup.SetupHelmet(config);
                equipment.StatConfig = config;
                equipment.EquipmentSlot = EquipmentSlotType.Head;
                equipment.DurabilityLossRate = 1f;
                equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
                equipment.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Light };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(equipment);
            }
            public static void SetupDefaultBeltStats(EquipmentDefinition equipment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Belt");
                ItemStatConfigSetup.SetupBelt(config);
                equipment.StatConfig = config;
                equipment.EquipmentSlot = EquipmentSlotType.Belt;
                equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
                equipment.AttachmentSlots = new AttachmentSlotType[] { AttachmentSlotType.Pouch, AttachmentSlotType.Pouch };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(equipment);
            }
            public static void SetupDefaultGlovesStats(EquipmentDefinition equipment)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<EquipmentStatConfig>("EquipmentStatConfig_Gloves");
                ItemStatConfigSetup.SetupGloves(config);
                equipment.StatConfig = config;
                equipment.EquipmentSlot = EquipmentSlotType.Hands;
                equipment.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Equipment };
                equipment.AttachmentSlots = new AttachmentSlotType[0];
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(equipment);
            }
        }
    }
}
#endif
