using UnityEditor;
using UnityEngine;

namespace ShaderCrew.SeeThroughShader
{
    public class AbsoluteValueDrawer : MaterialPropertyDrawer
    {

        public AbsoluteValueDrawer()
        {
        }

        public override void OnGUI(Rect position, MaterialProperty property, string label, MaterialEditor editor)
        {
            if (property.propertyType == UnityEngine.Rendering.ShaderPropertyType.Float)
            {
                property.floatValue = Mathf.Max(EditorGUI.FloatField(position, label, property.floatValue), 0);
            }
            else
            {
                editor.DefaultShaderProperty(property, label);
            }
        }

    }
}