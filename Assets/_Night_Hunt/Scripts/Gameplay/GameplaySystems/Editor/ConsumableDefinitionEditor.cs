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
    [CustomEditor(typeof(ConsumableDefinition))]
    public class ConsumableDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Sample Setup", EditorStyles.boldLabel);
            
            var consumable = (ConsumableDefinition)target;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Setup Medkit")) SetupMedkit(consumable);
            if (GUILayout.Button("Setup Energy Drink")) SetupEnergyDrink(consumable);
            EditorGUILayout.EndHorizontal();
        }
        
        private void SetupMedkit(ConsumableDefinition consumable)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<ConsumableStatConfig>("ConsumableStatConfig_Medkit");
            ItemStatConfigSetup.SetupMedkit(config);
            consumable.StatConfig = config;
            consumable.DisplayName = "Medkit";
            consumable.Description = "Restores 50 HP instantly + 20 HP over 10 s.";
            consumable.IsStackable = true;
            consumable.MaxStackSize = 5;
            consumable.Weight = config.GetStatValue(ItemStatType.Weight);
            consumable.UsageDuration = 3.5f;
            consumable.CanCancelUsage = true;
            consumable.CanUseWhileMoving = false;
            consumable.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(consumable);
            Debug.Log("[ConsumableDefinitionEditor] Medkit setup complete.");
        }
        
        private void SetupEnergyDrink(ConsumableDefinition consumable)
        {
            var config = ItemStatConfigSetup.GetOrCreateConfig<ConsumableStatConfig>("ConsumableStatConfig_EnergyDrink");
            ItemStatConfigSetup.SetupEnergyDrink(config);
            consumable.StatConfig = config;
            consumable.DisplayName = "Energy Drink";
            consumable.Description = "Restores 100 stamina instantly.";
            consumable.IsStackable = true;
            consumable.MaxStackSize = 3;
            consumable.Weight = config.GetStatValue(ItemStatType.Weight);
            consumable.UsageDuration = 2f;
            consumable.CanCancelUsage = true;
            consumable.CanUseWhileMoving = true;
            consumable.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(consumable);
            Debug.Log("[ConsumableDefinitionEditor] Energy Drink setup complete.");
        }
        
        public static class SetupHelpers
        {
            public static void SetupDefaultMedkit(ConsumableDefinition consumable)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<ConsumableStatConfig>("ConsumableStatConfig_Medkit");
                ItemStatConfigSetup.SetupMedkit(config);
                consumable.StatConfig = config;
                consumable.DisplayName = "Medkit";
                consumable.Description = "Restores 50 HP instantly + 20 HP over 10 s.";
                consumable.IsStackable = true;
                consumable.MaxStackSize = 5;
                consumable.Weight = config.GetStatValue(ItemStatType.Weight);
                consumable.UsageDuration = 3.5f;
                consumable.CanCancelUsage = true;
                consumable.CanUseWhileMoving = false;
                consumable.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(consumable);
            }
            
            public static void SetupDefaultEnergyDrink(ConsumableDefinition consumable)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<ConsumableStatConfig>("ConsumableStatConfig_EnergyDrink");
                ItemStatConfigSetup.SetupEnergyDrink(config);
                consumable.StatConfig = config;
                consumable.DisplayName = "Energy Drink";
                consumable.Description = "Restores 100 stamina instantly.";
                consumable.IsStackable = true;
                consumable.MaxStackSize = 3;
                consumable.Weight = config.GetStatValue(ItemStatType.Weight);
                consumable.UsageDuration = 2f;
                consumable.CanCancelUsage = true;
                consumable.CanUseWhileMoving = true;
                consumable.ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(consumable);
            }
        }
    }
}
#endif
