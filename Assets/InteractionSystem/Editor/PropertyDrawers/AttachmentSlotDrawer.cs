#if UNITY_EDITOR
using NightHunt.InteractionSystem.Core;
using UnityEditor;
using UnityEngine;

namespace NightHunt.InteractionSystem.Editor
{
    [CustomPropertyDrawer(typeof(AttachmentSlotDefinition))]
    public class AttachmentSlotDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                float y = position.y + lineHeight + spacing;

                // Slot Type
                SerializedProperty slotType = property.FindPropertyRelative("slotType");
                Rect slotTypeRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(slotTypeRect, slotType);
                y += lineHeight + spacing;

                // Accepted Types
                SerializedProperty acceptedTypes = property.FindPropertyRelative("acceptedTypes");
                float arrayHeight = EditorGUI.GetPropertyHeight(acceptedTypes);
                Rect acceptedTypesRect = new Rect(position.x, y, position.width, arrayHeight);
                EditorGUI.PropertyField(acceptedTypesRect, acceptedTypes, true);
                y += arrayHeight + spacing;

                // Attachment Point Offset
                SerializedProperty offset = property.FindPropertyRelative("attachmentPointOffset");
                Rect offsetRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(offsetRect, offset);
                y += lineHeight + spacing;
                // Attachment Point Rotation
                SerializedProperty rotation = property.FindPropertyRelative("attachmentPointRotation");
                Rect rotationRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(rotationRect, rotation);
                y += lineHeight + spacing;

                // Is Required
                SerializedProperty isRequired = property.FindPropertyRelative("isRequired");
                Rect isRequiredRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.PropertyField(isRequiredRect, isRequired);

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
            float height = lineHeight + spacing; // Foldout

            height += lineHeight + spacing; // slotType

            SerializedProperty acceptedTypes = property.FindPropertyRelative("acceptedTypes");
            height += EditorGUI.GetPropertyHeight(acceptedTypes) + spacing;

            height += lineHeight + spacing; // offset
            height += lineHeight + spacing; // rotation
            height += lineHeight + spacing; // isRequired

            return height;
        }
    }
}
#endif