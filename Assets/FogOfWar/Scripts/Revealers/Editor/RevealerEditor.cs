#if UNITY_EDITOR
using FOW;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FOW
{
    [CustomEditor(typeof(FogOfWarRevealer), true)]
    public class RevealerEditor : Editor
    {
        const string undoName = "Change Revealer Properties";

        SerializedProperty QualityPreset;

        private void OnEnable()
        {
            QualityPreset = serializedObject.FindProperty("QualityPreset");
        }

        void DrawPropertiesUpTo(SerializedObject serializedObject, string stopPropertyName = null)
        {
            SerializedProperty property = serializedObject.GetIterator();

            // Must call NextVisible(true) to enter the serialized property
            if (property.NextVisible(true))
            {
                do
                {
                    if (property.name == stopPropertyName)
                        break;

                    EditorGUILayout.PropertyField(property, true);
                }
                while (property.NextVisible(false));
            }
        }



        public override void OnInspectorGUI()
        {
            /// <summary>
            /// Draws serialized properties starting *after* startProperty (exclusive),
            /// and stopping *before* stopPropertyName (exclusive).
            /// If startProperty is null, starts from the beginning.
            /// If stopPropertyName is null, draws to the end.
            /// </summary>
            void DrawPropertiesBetween(SerializedObject serializedObject, SerializedProperty startProperty = null, string stopPropertyName = null)
            {
                SerializedProperty currentProperty = startProperty ?? serializedObject.GetIterator();
                bool skipFirst = startProperty != null;

                // First move to the first visible property
                if (!skipFirst && !currentProperty.NextVisible(true))
                    return;
                //Debug.Log(currentProperty.name);
                do
                {
                    if (skipFirst)
                    {
                        skipFirst = false;
                        continue; // skip the actual startProperty
                    }

                    if (!string.IsNullOrEmpty(stopPropertyName) && currentProperty.name == stopPropertyName)
                        break;

                    EditorGUILayout.PropertyField(currentProperty, true);
                }
                while (currentProperty.NextVisible(false));
            }

            FogOfWarRevealer revealer = (FogOfWarRevealer)target;

            //void DrawFloatFieldWithUndo(string label, ref float property, Action onChanged = null, float minValue = float.MinValue)
            //{
            //    float newValue = EditorGUILayout.FloatField(label, property);
            //    if (!Mathf.Approximately(newValue, property))
            //    {
            //        Undo.RegisterCompleteObjectUndo(revealer, undoName);
            //        property = Mathf.Max(minValue, newValue);
            //        onChanged?.Invoke();
            //        EditorUtility.SetDirty(revealer);
            //    }
            //}

            //void DrawSliderWithUndo(string label, ref float property, float min, float max, string undoName, Action onChanged = null)
            //{
            //    float newValue = EditorGUILayout.Slider(label, property, min, max);
            //    if (!Mathf.Approximately(newValue, property))
            //    {
            //        Undo.RegisterCompleteObjectUndo(revealer, undoName);
            //        property = newValue;
            //        onChanged?.Invoke();
            //        EditorUtility.SetDirty(revealer);
            //    }
            //}

            serializedObject.Update();
            //DrawPropertiesUpTo(serializedObject, "QualityPreset");
            DrawPropertiesBetween(serializedObject, null, "QualityPreset");


            QualityPreset = serializedObject.FindProperty("QualityPreset");
            EditorGUILayout.PropertyField(QualityPreset);

            if (serializedObject.ApplyModifiedProperties()) //if we changed the quality mode
            {
                SetRevealerQuality(revealer);
                serializedObject.ApplyModifiedProperties();
            }

            if (revealer.QualityPreset == FogOfWarRevealer.RevealerQualityPreset.Custom)
            {
                QualityPreset = serializedObject.FindProperty("QualityPreset");
                DrawPropertiesBetween(serializedObject, QualityPreset, "DebugMode");
                if (revealer.RaycastResolution < .05f)
                    revealer.RaycastResolution = .05f;
                if (revealer.DoubleHitMaxAngleDelta < 1)
                    revealer.DoubleHitMaxAngleDelta = 1;
                serializedObject.ApplyModifiedProperties();
            }


            //EditorGUILayout.PropertyField(ViewRadius);

            if (revealer.TryGetComponent<RevealerDebug>(out RevealerDebug debug))
            {
                SerializedProperty DebugProperty = serializedObject.FindProperty("DebugMode");
                EditorGUILayout.PropertyField(DebugProperty, true);
                DrawPropertiesBetween(serializedObject, DebugProperty);
                serializedObject.ApplyModifiedProperties();
            }
            else if (revealer.DebugMode)
            {
                revealer.DebugMode = false;
                serializedObject.ApplyModifiedProperties();
            }

            //SerializedProperty LastDebugProperty = serializedObject.FindProperty("DebugMode");
        }

        void SetRevealerQuality(FogOfWarRevealer revealer)
        {
            switch (revealer.QualityPreset)
            {
                case FogOfWarRevealer.RevealerQualityPreset.ExtraLargeScaleRTS:
                    revealer.RaycastResolution = 1;
                    revealer.NumExtraIterations = 0;
                    revealer.NumExtraRaysOnIteration = 1;
                    revealer.ResolveEdge = false;
                    revealer.MaxEdgeResolveIterations = 1;
                    revealer.EdgeDstThreshold = .5f;
                    break;

                case FogOfWarRevealer.RevealerQualityPreset.LargeScaleRTS:
                    revealer.RaycastResolution = 2;
                    revealer.NumExtraIterations = 0;
                    revealer.NumExtraRaysOnIteration = 1;
                    revealer.ResolveEdge = false;
                    revealer.MaxEdgeResolveIterations = 1;
                    revealer.EdgeDstThreshold = .5f;
                    break;

                case FogOfWarRevealer.RevealerQualityPreset.MediumScaleRTS:
                    revealer.RaycastResolution = .5f;
                    revealer.NumExtraIterations = 1;
                    revealer.NumExtraRaysOnIteration = 3;
                    revealer.ResolveEdge = false;
                    revealer.MaxEdgeResolveIterations = 1;
                    revealer.EdgeDstThreshold = .3f;
                    break;

                case FogOfWarRevealer.RevealerQualityPreset.SmallScaleRTS:
                    revealer.RaycastResolution = .5f;
                    revealer.NumExtraIterations = 1;
                    revealer.NumExtraRaysOnIteration = 3;
                    revealer.ResolveEdge = true;
                    revealer.MaxEdgeResolveIterations = 1;
                    revealer.EdgeDstThreshold = .2f;
                    break;
                case FogOfWarRevealer.RevealerQualityPreset.HighResolution:
                    revealer.RaycastResolution = .5f;
                    revealer.NumExtraIterations = 3;
                    revealer.NumExtraRaysOnIteration = 3;
                    revealer.ResolveEdge = true;
                    revealer.MaxEdgeResolveIterations = 3;
                    revealer.EdgeDstThreshold = .15f;
                    break;
                case FogOfWarRevealer.RevealerQualityPreset.OverkillResolution:
                    revealer.RaycastResolution = 1f;
                    revealer.NumExtraIterations = 4;
                    revealer.NumExtraRaysOnIteration = 3;
                    revealer.ResolveEdge = true;
                    revealer.MaxEdgeResolveIterations = 7;
                    revealer.EdgeDstThreshold = .1f;
                    break;
            }
        }
    }
}
#endif