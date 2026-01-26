#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NightHunt.InteractionSystem.Core;

namespace NightHunt.InteractionSystem.Editor
{
    [CustomEditor(typeof(InteractableBase), true)]
    public class InteractableEditor : UnityEditor.Editor
    {
        private bool showDebugTools = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            showDebugTools = EditorGUILayout.Foldout(showDebugTools, "Debug Tools", true);

            if (!showDebugTools) return;

            InteractableBase interactable = (InteractableBase)target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Current State
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Type:", interactable.InteractionType.ToString());
            EditorGUILayout.LabelField("Distance:", $"{interactable.InteractionDistance}m");
            EditorGUILayout.LabelField("Prompt:", interactable.InteractionPrompt);

            EditorGUILayout.Space(5);

            // Test Buttons
            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

            if (GUILayout.Button("Test Can Interact"))
            {
                bool canInteract = interactable.CanInteract(null);
                Debug.Log($"Can Interact: {canInteract}");
            }

            if (GUILayout.Button("Test Interact (Simulated)"))
            {
                if (Application.isPlaying)
                {
                    interactable.OnInteract(null);
                    Debug.Log("Interaction triggered");
                }
                else
                {
                    Debug.LogWarning("Must be in Play mode");
                }
            }

            // Hold Interaction Testing
            if (interactable.InteractionType == InteractionType.Hold)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Hold Interaction Testing", EditorStyles.boldLabel);

                if (GUILayout.Button("Simulate Hold Start"))
                {
                    if (Application.isPlaying)
                    {
                        interactable.OnInteractStart(null);
                    }
                }

                if (GUILayout.Button("Simulate Hold Complete"))
                {
                    if (Application.isPlaying)
                    {
                        interactable.OnInteract(null);
                    }
                }

                if (GUILayout.Button("Simulate Hold Cancel"))
                {
                    if (Application.isPlaying)
                    {
                        interactable.OnInteractCancel(null);
                    }
                }
            }

            EditorGUILayout.EndVertical();

            // Visualize interaction range
            if (GUILayout.Button("Visualize Range in Scene"))
            {
                SceneView.lastActiveSceneView.Frame(
                    new Bounds(interactable.transform.position, Vector3.one * interactable.InteractionDistance * 2),
                    false);
            }
        }

        private void OnSceneGUI()
        {
            InteractableBase interactable = (InteractableBase)target;

            // Draw interaction range
            Handles.color = new Color(0, 1, 0, 0.1f);
            Handles.DrawSolidDisc(interactable.transform.position, Vector3.up, interactable.InteractionDistance);

            Handles.color = Color.green;
            Handles.DrawWireDisc(interactable.transform.position, Vector3.up, interactable.InteractionDistance);

            // Draw label
            Handles.Label(
                interactable.transform.position + Vector3.up * 2f,
                $"{interactable.InteractionPrompt}\nRange: {interactable.InteractionDistance}m",
                EditorStyles.whiteLargeLabel
            );
        }
    }
}
#endif
