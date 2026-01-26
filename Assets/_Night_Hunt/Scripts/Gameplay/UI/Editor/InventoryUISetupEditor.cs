using UnityEngine;
using UnityEditor;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Editor script for InventoryUISetup
    /// Provides button to setup UI structure in editor
    /// </summary>
    [CustomEditor(typeof(InventoryUISetup))]
    public class InventoryUISetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            InventoryUISetup setup = (InventoryUISetup)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Setup Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Setup UI Structure", GUILayout.Height(30)))
            {
                setup.SetupUIStructure();
                EditorUtility.SetDirty(setup);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Detect Platform", GUILayout.Height(25)))
            {
                // Force platform detection
                #if UNITY_ANDROID || UNITY_IOS
                Debug.Log("Platform: Mobile");
                #else
                Debug.Log("Platform: PC");
                #endif
            }

            EditorGUILayout.HelpBox(
                "Click 'Setup UI Structure' to automatically create and configure all UI components. " +
                "Make sure you have a Canvas in the scene first!",
                MessageType.Info);
        }
    }
}
