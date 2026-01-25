#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Editor
{
    public static class InteractionSystemFolderGenerator
    {
        private const string ROOT = "Assets/InteractionSystem";

        [MenuItem("Tools/Night Hunt/Generate Interaction System Folders")]
        public static void Generate()
        {
            CreateStructure();
            AssetDatabase.Refresh();
            Debug.Log("✅ NightHunt Interaction System structure generated.");
        }

        static void CreateStructure()
        {
            // Runtime Core
            Dir("_Core/Abstractions",
                "InteractableBase.cs",
                "ItemDataBase.cs",
                "EquipmentDataBase.cs",
                "HealthComponentBase.cs",
                "InventoryComponentBase.cs"
            );

            Dir("_Core/Interfaces",
                "IInteractable.cs",
                "IPickupable.cs",
                "ILootContainer.cs",
                "IDamageable.cs",
                "IStackable.cs",
                "IAttachmentSlot.cs"
            );

            Dir("_Core/Data/Items",
                "WeaponData.cs",
                "ArmorData.cs",
                "HelmetData.cs",
                "BackpackData.cs",
                "ConsumableData.cs",
                "AmmoData.cs",
                "AttachmentData.cs"
            );

            Dir("_Core/Data/Configuration",
                "PickupSystemConfig.cs",
                "InteractionSystemConfig.cs",
                "ItemSystemConfig.cs"
            );

            Dir("_Core/Structs",
                "ItemInstance.cs",
                "DamagePayload.cs",
                "StatModifier.cs",
                "AttachmentSlotDefinition.cs",
                "InteractionProgress.cs"
            );

            // Input
            Dir("Input",
                "InputRouter.cs",
                "InputContextSwitcher.cs"
            );

            // Pickup
            Dir("Pickup/Detection",
                "PickupDetector.cs",
                "AutoPickupTrigger.cs"
            );

            Dir("Pickup/Handlers",
                "PickupHandler.cs",
                "PickupSettings.cs"
            );

            Dir("Pickup/Animation",
                "PickupAnimator.cs"
            );

            // Interaction
            Dir("Interaction/Detection",
                "InteractionDetector.cs"
            );

            Dir("Interaction/Handlers",
                "InteractionHandler.cs",
                "HoldInteractionHandler.cs",
                "InteractionUIController.cs"
            );

            Dir("Interaction/Implementations",
                "DoorInteractable.cs",
                "ContainerInteractable.cs",
                "CorpseInteractable.cs",
                "NPCInteractable.cs"
            );

            // Loot
            Dir("Loot",
                "LootContainer.cs",
                "LootContainerUI.cs",
                "LootTransferHandler.cs",
                "PlayerCorpseLoot.cs"
            );

            // Items
            Dir("Items/Runtime",
                "NetworkLootItem.cs",
                "ItemInstanceFactory.cs",
                "ItemDatabaseManager.cs"
            );

            Dir("Items/Attachments",
                "AttachmentManager.cs",
                "AttachmentSlot.cs",
                "StatCalculator.cs",
                "AttachmentVisualController.cs"
            );

            Dir("Items/Services",
                "ItemStackingService.cs",
                "ItemDropService.cs"
            );

            // Inventory
            Dir("Inventory/Components",
                "GridInventoryComponent.cs",
                "ListInventoryComponent.cs"
            );

            Dir("Inventory/UI",
                "InventoryUIController.cs",
                "ItemSlotUI.cs",
                "DragDropHandler.cs"
            );

            Dir("Inventory/Extensions",
                "InventoryDropExtension.cs"
            );

            // Equipment
            Dir("Equipment",
                "EquipmentManager.cs",
                "EquipmentVisualController.cs",
                "WeaponEquipHandler.cs"
            );

            // Combat
            Dir("Combat/Damage",
                "PlayerHealthComponent.cs",
                "EnemyHealthComponent.cs",
                "DamageCalculator.cs",
                "ArmorComponent.cs"
            );

            Dir("Combat/Weapons",
                "WeaponController.cs",
                "ProjectileController.cs"
            );

            // Utilities
            Dir("Utilities",
                "RaycastVisualizer.cs",
                "InteractionDebugger.cs",
                "GizmoDrawer.cs"
            );

            // Editor
            Dir("Editor/Inspectors",
                "ItemDataEditor.cs",
                "EquipmentDataEditor.cs",
                "InteractableEditor.cs"
            );

            Dir("Editor/PropertyDrawers",
                "AttachmentSlotDrawer.cs",
                "StatModifierDrawer.cs"
            );

            Dir("Editor/Windows",
                "InteractionSystemSetupWizard.cs",
                "AttachmentDebugWindow.cs",
                "InventoryGridVisualizer.cs"
            );

            Dir("Editor/Gizmos",
                "RaycastGizmoDrawer.cs",
                "InteractionRangeGizmo.cs",
                "AttachmentPointGizmo.cs"
            );
        }

        static void Dir(string relativePath, params string[] files)
        {
            string fullPath = Path.Combine(ROOT, relativePath);

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            foreach (var file in files)
            {
                string filePath = Path.Combine(fullPath, file);
                if (!File.Exists(filePath))
                    File.WriteAllText(filePath, "// Auto-generated by InteractionSystemFolderGenerator\n");
            }
        }
    }
}
#endif
