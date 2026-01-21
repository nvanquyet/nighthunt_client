using UnityEngine;
using UnityEditor;
using NightHunt.Networking.Prediction.Utils;

namespace NightHunt.Networking.Prediction.Editor
{
    /// <summary>
    /// Custom editor cho PredictionConfig ScriptableObject.
    /// Cung cấp preset configurations và validation.
    /// </summary>
    [CustomEditor(typeof(PredictionConfig))]
    public class ConfigEditor : UnityEditor.Editor
    {
        private bool _showPresets = false;
        private bool _showValidation = true;

        public override void OnInspectorGUI()
        {
            PredictionConfig config = (PredictionConfig)target;

            EditorGUILayout.LabelField("Prediction Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Presets section
            _showPresets = EditorGUILayout.Foldout(_showPresets, "Presets", true);
            if (_showPresets)
            {
                DrawPresets(config);
            }

            EditorGUILayout.Space();

            // Default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // Validation section
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true);
            if (_showValidation)
            {
                DrawValidation(config);
            }

            // Apply changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }

        private void DrawPresets(PredictionConfig config)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Quick Presets:", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Low Latency"))
            {
                ApplyLowLatencyPreset(config);
            }

            if (GUILayout.Button("Balanced"))
            {
                ApplyBalancedPreset(config);
            }

            if (GUILayout.Button("Performance"))
            {
                ApplyPerformancePreset(config);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ApplyLowLatencyPreset(PredictionConfig config)
        {
            config.tickRate = 120;
            config.stateHistorySize = 64;
            config.reconciliationThreshold = 0.05f;
            config.snapThreshold = 0.5f;
            config.smoothTime = 0.05f;
            config.interpolationDelay = 0.05f;
            config.useExtrapolation = true;
            config.extrapolationLimit = 0.2f;
            config.useLOD = false;
            EditorUtility.SetDirty(config);
        }

        private void ApplyBalancedPreset(PredictionConfig config)
        {
            config.tickRate = 60;
            config.stateHistorySize = 32;
            config.reconciliationThreshold = 0.1f;
            config.snapThreshold = 1f;
            config.smoothTime = 0.1f;
            config.interpolationDelay = 0.1f;
            config.useExtrapolation = true;
            config.extrapolationLimit = 0.5f;
            config.useLOD = true;
            EditorUtility.SetDirty(config);
        }

        private void ApplyPerformancePreset(PredictionConfig config)
        {
            config.tickRate = 30;
            config.stateHistorySize = 16;
            config.reconciliationThreshold = 0.2f;
            config.snapThreshold = 2f;
            config.smoothTime = 0.2f;
            config.interpolationDelay = 0.2f;
            config.useExtrapolation = false;
            config.useLOD = true;
            config.enableObjectPooling = true;
            EditorUtility.SetDirty(config);
        }

        private void DrawValidation(PredictionConfig config)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool hasWarnings = false;

            // Validate tick rate
            if (config.tickRate < 30)
            {
                EditorGUILayout.HelpBox("Tick rate quá thấp có thể gây lag.", MessageType.Warning);
                hasWarnings = true;
            }

            if (config.tickRate > 120)
            {
                EditorGUILayout.HelpBox("Tick rate quá cao có thể gây performance issues.", MessageType.Warning);
                hasWarnings = true;
            }

            // Validate LOD distances
            if (config.lodDistance1 >= config.lodDistance2 || config.lodDistance2 >= config.lodDistance3)
            {
                EditorGUILayout.HelpBox("LOD distances phải tăng dần: distance1 < distance2 < distance3", MessageType.Error);
                hasWarnings = true;
            }

            // Validate reconciliation threshold
            if (config.reconciliationThreshold > config.snapThreshold)
            {
                EditorGUILayout.HelpBox("Reconciliation threshold nên nhỏ hơn snap threshold.", MessageType.Warning);
                hasWarnings = true;
            }

            if (!hasWarnings)
            {
                EditorGUILayout.HelpBox("Configuration hợp lệ.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }
}

