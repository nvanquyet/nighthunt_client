using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Items.Attachments;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Editor.Windows
{
    /// <summary>
    /// Editor window for debugging attachment system.
    /// </summary>
    public class AttachmentDebugWindow : EditorWindow
    {
        private AttachmentManager targetManager;
        private EquipmentDataBase equipmentData;

        [MenuItem("NightHunt/InteractionSystem/Attachment Debug Window")]
        public static void ShowWindow()
        {
            GetWindow<AttachmentDebugWindow>("Attachment Debug");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Attachment Debug Window", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetManager = (AttachmentManager)EditorGUILayout.ObjectField("Target Manager", targetManager, typeof(AttachmentManager), true);
            equipmentData = (EquipmentDataBase)EditorGUILayout.ObjectField("Equipment Data", equipmentData, typeof(EquipmentDataBase), false);

            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Select an AttachmentManager to debug.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Attachments", EditorStyles.boldLabel);

            var attachments = targetManager.GetAllAttachments();
            EditorGUILayout.LabelField($"Attached: {attachments.Count}");

            foreach (var kvp in attachments)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value.name}");
                if (GUILayout.Button("Detach", GUILayout.Width(60)))
                {
                    targetManager.DetachAttachment(kvp.Key);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Attach Random (Test)"))
            {
                // Test attachment functionality
                Debug.Log("[AttachmentDebugWindow] Random attach test - implement based on available attachments");
            }

            // Show calculated stats
            var statCalculator = targetManager.GetComponent<StatCalculator>();
            if (statCalculator != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Calculated Stats", EditorStyles.boldLabel);
                var stats = statCalculator.GetAllFinalStats();
                foreach (var kvp in stats)
                {
                    EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value:F2}");
                }
            }
        }
    }
}
