using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Editor.Inspectors
{
    /// <summary>
    /// Custom inspector for InteractableBase with test buttons.
    /// </summary>
    [CustomEditor(typeof(InteractableBase), true)]
    public class InteractableEditor : UnityEditor.Editor
    {
        private InteractionDebugger debugger;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            InteractableBase interactable = (InteractableBase)target;
            if (interactable == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

            // Get or create debugger
            if (debugger == null)
            {
                debugger = interactable.GetComponent<InteractionDebugger>();
                if (debugger == null)
                {
                    debugger = interactable.gameObject.AddComponent<InteractionDebugger>();
                }
            }

            // Test buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Interact"))
            {
                debugger.TestInteract();
            }

            if (GUILayout.Button("Test Can Interact"))
            {
                bool canInteract = debugger.TestCanInteract();
                EditorUtility.DisplayDialog("Test Result", canInteract ? "Can Interact" : "Cannot Interact", "OK");
            }
            EditorGUILayout.EndHorizontal();

            // Display current state
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Interaction Type: {interactable.GetInteractionType()}");
            EditorGUILayout.LabelField($"Required Hold Time: {interactable.GetRequiredHoldTime()}s");
            EditorGUILayout.LabelField($"Interaction Range: {interactable.GetInteractionRange()}m");
        }
    }
}
