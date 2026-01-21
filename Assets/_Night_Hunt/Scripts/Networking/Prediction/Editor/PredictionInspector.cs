using UnityEngine;
using UnityEditor;
using NightHunt.Networking.Prediction.Core;

namespace NightHunt.Networking.Prediction.Editor
{
    /// <summary>
    /// Custom inspector cho PredictedObject.
    /// Hiển thị runtime stats và quick config shortcuts.
    /// </summary>
    [CustomEditor(typeof(PredictedObject<,>), true)]
    public class PredictionInspector : UnityEditor.Editor
    {
        private bool _showStats = true;
        private bool _showConfig = false;
        private bool _showDebug = false;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime stats chỉ hiển thị khi game đang chạy.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prediction Stats", EditorStyles.boldLabel);

            // Stats section
            _showStats = EditorGUILayout.Foldout(_showStats, "Runtime Stats", true);
            if (_showStats)
            {
                DrawStats();
            }

            // Config section
            _showConfig = EditorGUILayout.Foldout(_showConfig, "Quick Config", true);
            if (_showConfig)
            {
                DrawQuickConfig();
            }

            // Debug section
            _showDebug = EditorGUILayout.Foldout(_showDebug, "Debug Tools", true);
            if (_showDebug)
            {
                DrawDebugTools();
            }
        }

        private void DrawStats()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Try to get stats từ PredictedObject
            var predictedObject = target as IPredictedObject;
            if (predictedObject != null)
            {
                EditorGUILayout.LabelField("Prediction Enabled:", predictedObject.IsPredictionEnabled ? "Yes" : "No");
                EditorGUILayout.LabelField("Position:", predictedObject.Position.ToString());
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickConfig()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("Enable Prediction"))
            {
                // Enable prediction
            }

            if (GUILayout.Button("Disable Prediction"))
            {
                // Disable prediction
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDebugTools()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("Simulate Lag (50ms)"))
            {
                // Simulate lag
            }

            if (GUILayout.Button("Simulate Packet Loss (10%)"))
            {
                // Simulate packet loss
            }

            if (GUILayout.Button("Force Reconcile"))
            {
                // Force reconciliation
            }

            EditorGUILayout.EndVertical();
        }
    }
}

