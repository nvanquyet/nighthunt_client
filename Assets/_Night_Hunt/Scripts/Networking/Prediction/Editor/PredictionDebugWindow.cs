using UnityEngine;
using UnityEditor;
using NightHunt.Networking.Prediction.Core;
using NightHunt.Networking.Prediction.Utils;
using System.Collections.Generic;

namespace NightHunt.Networking.Prediction.Editor
{
    /// <summary>
    /// Debug window cho prediction system.
    /// Hiển thị real-time graphs, stats, và state history.
    /// </summary>
    public class PredictionDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _showGraphs = true;
        private bool _showStats = true;
        private bool _showHistory = false;

        private readonly List<Vector2> _clientPositionHistory = new List<Vector2>();
        private readonly List<Vector2> _serverPositionHistory = new List<Vector2>();
        private readonly int _maxHistoryPoints = 100;

        [MenuItem("Window/FishNet/Prediction Debug")]
        public static void ShowWindow()
        {
            GetWindow<PredictionDebugWindow>("Prediction Debug");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FishNet Prediction Debug Window", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Graphs section
            _showGraphs = EditorGUILayout.Foldout(_showGraphs, "Position Graphs", true);
            if (_showGraphs)
            {
                DrawGraphs();
            }

            // Stats section
            _showStats = EditorGUILayout.Foldout(_showStats, "Network Stats", true);
            if (_showStats)
            {
                DrawStats();
            }

            // History section
            _showHistory = EditorGUILayout.Foldout(_showHistory, "State History", true);
            if (_showHistory)
            {
                DrawHistory();
            }

            EditorGUILayout.EndScrollView();

            // Update trong play mode
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawGraphs()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Client vs Server Position", EditorStyles.miniLabel);

            // Simple graph area
            Rect graphRect = GUILayoutUtility.GetRect(400, 200);
            DrawSimpleGraph(graphRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawSimpleGraph(Rect rect)
        {
            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            // Draw grid
            DrawGrid(rect);

            // Draw lines
            if (_clientPositionHistory.Count > 1)
            {
                DrawLine(_clientPositionHistory, Color.green, rect);
            }

            if (_serverPositionHistory.Count > 1)
            {
                DrawLine(_serverPositionHistory, Color.red, rect);
            }
        }

        private void DrawGrid(Rect rect)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f);

            // Vertical lines
            for (int i = 0; i <= 10; i++)
            {
                float x = rect.x + (rect.width / 10) * i;
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
            }

            // Horizontal lines
            for (int i = 0; i <= 10; i++)
            {
                float y = rect.y + (rect.height / 10) * i;
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.x + rect.width, y));
            }

            Handles.EndGUI();
        }

        private void DrawLine(List<Vector2> points, Color color, Rect rect)
        {
            if (points.Count < 2)
                return;

            Handles.BeginGUI();
            Handles.color = color;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];

                // Normalize to rect
                float x1 = rect.x + (p1.x / _maxHistoryPoints) * rect.width;
                float y1 = rect.y + rect.height - (p1.y / 100f) * rect.height;
                float x2 = rect.x + (p2.x / _maxHistoryPoints) * rect.width;
                float y2 = rect.y + rect.height - (p2.y / 100f) * rect.height;

                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }

            Handles.EndGUI();
        }

        private void DrawStats()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (NetworkStats.Instance != null)
            {
                EditorGUILayout.LabelField("RTT:", $"{NetworkStats.Instance.AverageRTT:F1} ms");
                EditorGUILayout.LabelField("Packet Loss:", $"{NetworkStats.Instance.PacketLossRate * 100f:F1}%");
                EditorGUILayout.LabelField("Reconciliation:", $"{NetworkStats.Instance.ReconciliationFrequency:F1}/s");
            }
            else
            {
                EditorGUILayout.HelpBox("NetworkStats not found. Add NetworkStats component to scene.", MessageType.Warning);
            }

            if (PredictionManager.Instance != null)
            {
                EditorGUILayout.LabelField("Tick Rate:", $"{PredictionManager.Instance.TickRate} Hz");
                EditorGUILayout.LabelField("Current Tick:", $"{PredictionManager.Instance.CurrentTick}");
            }
            else
            {
                EditorGUILayout.HelpBox("PredictionManager not found. Add PredictionManager component to scene.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHistory()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("State history sẽ được hiển thị ở đây.");
            EditorGUILayout.EndVertical();
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                // Update history
                // This would be called from actual prediction system
            }
        }
    }
}

