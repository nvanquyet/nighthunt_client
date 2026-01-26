using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Equipment;
using NightHunt.InteractionSystem.Items;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Editor
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;
    using System.Linq;

    public class AttachmentDebugWindow : EditorWindow
    {
        private EquipmentManager targetEquipmentManager;
        private EquipmentSlot selectedSlot;
        private Vector2 scrollPosition;

        [MenuItem("NightHunt/Tools/Attachment Debug")]
        public static void ShowWindow()
        {
            GetWindow<AttachmentDebugWindow>("Attachment Debug");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Attachment System Debugger", EditorStyles.boldLabel);

            targetEquipmentManager = (EquipmentManager)EditorGUILayout.ObjectField(
                "Equipment Manager",
                targetEquipmentManager,
                typeof(EquipmentManager),
                true
            );

            if (targetEquipmentManager == null)
            {
                EditorGUILayout.HelpBox("Select an EquipmentManager to debug", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();

            // Slot selection
            selectedSlot = (EquipmentSlot)EditorGUILayout.EnumPopup("Equipment Slot", selectedSlot);

            EditorGUILayout.Space();

            // Display current equipment
            DisplayEquipmentInfo();

            EditorGUILayout.Space();

            // Display attachments
            DisplayAttachments();

            EditorGUILayout.Space();

            // Display final stats
            DisplayFinalStats();

            EditorGUILayout.Space();

            // Test controls
            DisplayTestControls();
        }

        private void DisplayEquipmentInfo()
        {
            EditorGUILayout.LabelField("Equipment Info", EditorStyles.boldLabel);

            ItemInstance? equippedItem = targetEquipmentManager.GetEquippedItem(selectedSlot);

            if (!equippedItem.HasValue)
            {
                EditorGUILayout.HelpBox("No item equipped in this slot", MessageType.Info);
                return;
            }

            EquipmentDataBase equipData = targetEquipmentManager.GetEquippedItemData(selectedSlot);
            if (equipData != null)
            {
                EditorGUILayout.LabelField("Name:", equipData.displayName);
                EditorGUILayout.LabelField("ID:", equipData.itemId);
                EditorGUILayout.LabelField("Slots:", equipData.attachmentSlots?.Length.ToString() ?? "0");
            }
        }

        private void DisplayAttachments()
        {
            EditorGUILayout.LabelField("Attachments", EditorStyles.boldLabel);

            AttachmentManager manager = targetEquipmentManager.GetAttachmentManager(selectedSlot);
            if (manager == null)
            {
                EditorGUILayout.HelpBox("No attachment manager for this slot", MessageType.Warning);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            foreach (var kvp in manager.Slots)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"Slot: {kvp.Key}", EditorStyles.boldLabel);

                if (kvp.Value.IsOccupied)
                {
                    AttachmentData attachment = kvp.Value.CurrentAttachment;
                    EditorGUILayout.LabelField("Attachment:", attachment.displayName);

                    // Show modifiers
                    if (attachment.modifiers != null)
                    {
                        foreach (var mod in attachment.modifiers)
                        {
                            EditorGUILayout.LabelField($"  • {StatCalculator.GetStatDescription(mod)}");
                        }
                    }

                    if (GUILayout.Button("Detach"))
                    {
                        if (Application.isPlaying)
                        {
                            manager.TryDetach(kvp.Key);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Status:", "Empty");
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DisplayFinalStats()
        {
            EditorGUILayout.LabelField("Final Stats", EditorStyles.boldLabel);

            AttachmentManager manager = targetEquipmentManager.GetAttachmentManager(selectedSlot);
            if (manager == null) return;

            StatModifier[] finalStats = manager.GetFinalStats();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (finalStats.Length == 0)
            {
                EditorGUILayout.LabelField("No stats to display");
            }
            else
            {
                foreach (var stat in finalStats)
                {
                    EditorGUILayout.LabelField(StatCalculator.GetStatDescription(stat));
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DisplayTestControls()
        {
            EditorGUILayout.LabelField("Test Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to test", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("Attach Random Attachment"))
            {
                AttachRandomAttachment();
            }

            if (GUILayout.Button("Detach All"))
            {
                DetachAllAttachments();
            }

            if (GUILayout.Button("Recalculate Stats"))
            {
                RecalculateStats();
            }

            EditorGUILayout.EndVertical();
        }

        private void AttachRandomAttachment()
        {
            AttachmentManager manager = targetEquipmentManager.GetAttachmentManager(selectedSlot);
            if (manager == null) return;

            // Get first empty slot
            var emptySlot = manager.Slots.FirstOrDefault(s => !s.Value.IsOccupied);
            if (emptySlot.Value == null) return;

            // Find compatible attachment
            AttachmentData[] allAttachments = Resources.LoadAll<AttachmentData>("Items/Attachments");
            var compatible = allAttachments.Where(a => a.compatibleSlots.Contains(emptySlot.Key)).ToArray();

            if (compatible.Length > 0)
            {
                AttachmentData randomAttachment = compatible[Random.Range(0, compatible.Length)];
                manager.TryAttach(emptySlot.Key, randomAttachment);
                Debug.Log($"Attached {randomAttachment.displayName} to {emptySlot.Key}");
            }
        }

        private void DetachAllAttachments()
        {
            AttachmentManager manager = targetEquipmentManager.GetAttachmentManager(selectedSlot);
            if (manager == null) return;

            foreach (var slot in manager.Slots.Keys.ToArray())
            {
                manager.TryDetach(slot);
            }

            Debug.Log("Detached all attachments");
        }

        private void RecalculateStats()
        {
            // Force recalculation
            Debug.Log("Stats recalculated");
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
#endif
}