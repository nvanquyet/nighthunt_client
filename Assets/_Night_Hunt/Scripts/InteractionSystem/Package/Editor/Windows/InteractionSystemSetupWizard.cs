using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Pickup.Detection;
using NightHunt.InteractionSystem.Interaction.Detection;
using NightHunt.InteractionSystem.Interaction.Handlers;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Equipment;
using NightHunt.InteractionSystem.Utilities;
using System;

namespace NightHunt.InteractionSystem.Editor.Windows
{
    /// <summary>
    /// Setup wizard for Interaction System package.
    /// Helps users set up the system quickly.
    /// </summary>
    public class InteractionSystemSetupWizard : EditorWindow
    {
        private int currentStep = 0;
        private GameObject selectedPlayer;
        private bool setupPickup = true;
        private bool setupInteraction = true;
        private bool setupInventory = true;
        private bool setupEquipment = false;

        [MenuItem("NightHunt/InteractionSystem/Setup Wizard")]
        public static void ShowWindow()
        {
            GetWindow<InteractionSystemSetupWizard>("Interaction System Setup");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Interaction System Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Step 1: Select Player
            EditorGUILayout.LabelField($"Step {currentStep + 1}/4: Select Player GameObject", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            selectedPlayer = (GameObject)EditorGUILayout.ObjectField("Player GameObject", selectedPlayer, typeof(GameObject), true);

            if (selectedPlayer == null)
            {
                EditorGUILayout.HelpBox("Please select the player GameObject to set up.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components to Setup:", EditorStyles.boldLabel);
            setupPickup = EditorGUILayout.Toggle("Pickup System", setupPickup);
            setupInteraction = EditorGUILayout.Toggle("Interaction System", setupInteraction);
            setupInventory = EditorGUILayout.Toggle("Inventory System", setupInventory);
            setupEquipment = EditorGUILayout.Toggle("Equipment System", setupEquipment);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Navigation buttons
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = currentStep > 0;
            if (GUILayout.Button("Previous"))
            {
                currentStep--;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Setup All", GUILayout.Width(100)))
            {
                SetupAll();
            }

            GUI.enabled = currentStep < 3;
            if (GUILayout.Button("Next"))
            {
                currentStep++;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Step-specific UI
            switch (currentStep)
            {
                case 0:
                    DrawStep0();
                    break;
                case 1:
                    DrawStep1();
                    break;
                case 2:
                    DrawStep2();
                    break;
                case 3:
                    DrawStep3();
                    break;
            }
        }

        private void DrawStep0()
        {
            EditorGUILayout.LabelField("Step 1: Player Selection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select your player GameObject. Components will be organized into child objects for better hierarchy management:\n" +
                "• PickupSystem - All pickup-related components\n" +
                "• InteractionSystem - All interaction-related components\n" +
                "• InventorySystem - Inventory components\n" +
                "• EquipmentSystem - Equipment management components", MessageType.Info);
        }

        private void DrawStep1()
        {
            EditorGUILayout.LabelField("Step 2: Pickup System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The pickup system handles picking up items from the ground.", MessageType.Info);

            if (GUILayout.Button("Setup Pickup System"))
            {
                SetupPickupSystem();
            }
        }

        private void DrawStep2()
        {
            EditorGUILayout.LabelField("Step 3: Interaction System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The interaction system handles interacting with objects (doors, chests, etc.).", MessageType.Info);

            if (GUILayout.Button("Setup Interaction System"))
            {
                SetupInteractionSystem();
            }
        }

        private void DrawStep3()
        {
            EditorGUILayout.LabelField("Step 4: Inventory & Equipment", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Setup inventory and equipment systems.", MessageType.Info);

            if (GUILayout.Button("Setup Inventory System"))
            {
                SetupInventorySystem();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup Equipment System"))
            {
                SetupEquipmentSystem();
            }
        }

        /// <summary>
        /// Setup all systems at once.
        /// </summary>
        private void SetupAll()
        {
            if (selectedPlayer == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a player GameObject first.", "OK");
                return;
            }

            if (setupPickup)
                SetupPickupSystem();

            if (setupInteraction)
                SetupInteractionSystem();

            if (setupInventory)
                SetupInventorySystem();

            if (setupEquipment)
                SetupEquipmentSystem();

            EditorUtility.DisplayDialog("Setup Complete", "All selected systems have been set up!", "OK");
        }

        /// <summary>
        /// Setup pickup system components.
        /// </summary>
        private void SetupPickupSystem()
        {
            if (selectedPlayer == null)
                return;

            // Create or get PickupSystem child object
            GameObject pickupSystemObj = ComponentOrganizer.GetPickupSystemObject(selectedPlayer);

            // PickupDetector
            if (pickupSystemObj.GetComponent<PickupDetector>() == null)
            {
                var detector = pickupSystemObj.AddComponent<PickupDetector>();
                Debug.Log("[SetupWizard] Added PickupDetector to PickupSystem child");
            }

            // PickupHandler (NetworkBehaviour - cannot add in Editor, user must add manually)
            // Use reflection to check if component exists without direct reference
            Type pickupHandlerType = Type.GetType("NightHunt.InteractionSystem.Pickup.Handlers.PickupHandler, com.nighthunt.interactionsystem");
            if (pickupHandlerType != null && pickupSystemObj.GetComponent(pickupHandlerType) == null)
            {
                Debug.LogWarning("[SetupWizard] PickupHandler is a NetworkBehaviour and cannot be added in Editor. Please add it manually to PickupSystem child in Play mode or via prefab setup.");
            }

            // AutoPickupTrigger
            if (pickupSystemObj.GetComponent<AutoPickupTrigger>() == null)
            {
                var trigger = pickupSystemObj.AddComponent<AutoPickupTrigger>();
                Debug.Log("[SetupWizard] Added AutoPickupTrigger to PickupSystem child");
            }

            // PickupAnimator
            if (pickupSystemObj.GetComponent<Pickup.Animation.PickupAnimator>() == null)
            {
                var animator = pickupSystemObj.AddComponent<Pickup.Animation.PickupAnimator>();
                Debug.Log("[SetupWizard] Added PickupAnimator to PickupSystem child");
            }

            EditorUtility.SetDirty(selectedPlayer);
            EditorUtility.SetDirty(pickupSystemObj);
        }

        /// <summary>
        /// Setup interaction system components.
        /// </summary>
        private void SetupInteractionSystem()
        {
            if (selectedPlayer == null)
                return;

            // Create or get InteractionSystem child object
            GameObject interactionSystemObj = ComponentOrganizer.GetInteractionSystemObject(selectedPlayer);

            // InteractionDetector
            if (interactionSystemObj.GetComponent<InteractionDetector>() == null)
            {
                var detector = interactionSystemObj.AddComponent<InteractionDetector>();
                Debug.Log("[SetupWizard] Added InteractionDetector to InteractionSystem child");
            }

            // InteractionHandler
            if (interactionSystemObj.GetComponent<InteractionHandler>() == null)
            {
                var handler = interactionSystemObj.AddComponent<InteractionHandler>();
                Debug.Log("[SetupWizard] Added InteractionHandler to InteractionSystem child");
            }

            // HoldInteractionHandler
            if (interactionSystemObj.GetComponent<HoldInteractionHandler>() == null)
            {
                var holdHandler = interactionSystemObj.AddComponent<HoldInteractionHandler>();
                Debug.Log("[SetupWizard] Added HoldInteractionHandler to InteractionSystem child");
            }

            // Note: UI is handled in gameplay layer, not in package.
            // Subscribe to InteractionEvents in gameplay UI scripts.

            EditorUtility.SetDirty(selectedPlayer);
            EditorUtility.SetDirty(interactionSystemObj);
        }

        /// <summary>
        /// Setup inventory system components.
        /// </summary>
        private void SetupInventorySystem()
        {
            if (selectedPlayer == null)
                return;

            // Create or get InventorySystem child object
            GameObject inventorySystemObj = ComponentOrganizer.GetInventorySystemObject(selectedPlayer);

            // GridInventoryComponent (default)
            if (inventorySystemObj.GetComponent<GridInventoryComponent>() == null && 
                inventorySystemObj.GetComponent<ListInventoryComponent>() == null)
            {
                var inventory = inventorySystemObj.AddComponent<GridInventoryComponent>();
                Debug.Log("[SetupWizard] Added GridInventoryComponent to InventorySystem child");
            }

            // ItemDropHandler (NetworkBehaviour - cannot add in Editor, user must add manually)
            Type itemDropHandlerType = Type.GetType("NightHunt.InteractionSystem.Items.Drop.ItemDropHandler, com.nighthunt.interactionsystem");
            if (itemDropHandlerType != null && inventorySystemObj.GetComponent(itemDropHandlerType) == null)
            {
                Debug.LogWarning("[SetupWizard] ItemDropHandler is a NetworkBehaviour and cannot be added in Editor. Please add it manually to InventorySystem child in Play mode or via prefab setup.");
            }

            EditorUtility.SetDirty(selectedPlayer);
            EditorUtility.SetDirty(inventorySystemObj);
        }

        /// <summary>
        /// Setup equipment system components.
        /// </summary>
        private void SetupEquipmentSystem()
        {
            if (selectedPlayer == null)
                return;

            // Create or get EquipmentSystem child object
            GameObject equipmentSystemObj = ComponentOrganizer.GetEquipmentSystemObject(selectedPlayer);

            // EquipmentManager
            if (equipmentSystemObj.GetComponent<EquipmentManager>() == null)
            {
                var equipmentManager = equipmentSystemObj.AddComponent<EquipmentManager>();
                Debug.Log("[SetupWizard] Added EquipmentManager to EquipmentSystem child");
            }

            // EquipmentVisualController
            if (equipmentSystemObj.GetComponent<EquipmentVisualController>() == null)
            {
                var visualController = equipmentSystemObj.AddComponent<EquipmentVisualController>();
                Debug.Log("[SetupWizard] Added EquipmentVisualController to EquipmentSystem child");
            }

            EditorUtility.SetDirty(selectedPlayer);
            EditorUtility.SetDirty(equipmentSystemObj);
        }
    }
}
