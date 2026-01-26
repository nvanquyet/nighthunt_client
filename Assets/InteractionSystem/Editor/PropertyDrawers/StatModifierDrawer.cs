#if UNITY_EDITOR
using NightHunt.InteractionSystem.Core;
using UnityEditor;
using UnityEngine;

namespace NightHunt.InteractionSystem.Editor
{
    [CustomPropertyDrawer(typeof(StatModifier))]
    public class StatModifierDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
        
            // Calculate rects
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
        
            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
            
                float y = position.y + lineHeight + spacing;
            
                // Stat Type
                SerializedProperty statType = property.FindPropertyRelative("statType");
                Rect statTypeRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(statTypeRect, statType);
                y += lineHeight + spacing;
            
                // Modifier Type
                SerializedProperty modifierType = property.FindPropertyRelative("modifierType");
                Rect modifierTypeRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(modifierTypeRect, modifierType);
                y += lineHeight + spacing;
            
                // Value
                SerializedProperty value = property.FindPropertyRelative("value");
                Rect valueRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(valueRect, value);
                y += lineHeight + spacing;
            
                // Priority
                SerializedProperty priority = property.FindPropertyRelative("priority");
                Rect priorityRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(priorityRect, priority);
                y += lineHeight + spacing;
            
                // Source ID
                SerializedProperty sourceId = property.FindPropertyRelative("sourceId");
                Rect sourceIdRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(sourceIdRect, sourceId);
            
                EditorGUI.indentLevel--;
            }
        
            EditorGUI.EndProperty();
        }
    
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }
        
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
        
            return lineHeight * 6 + spacing * 5;
        }
    }
}
#endif