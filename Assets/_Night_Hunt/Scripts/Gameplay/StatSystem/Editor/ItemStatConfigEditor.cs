#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.Gameplay.StatSystem.Editor
{
    [CustomEditor(typeof(ItemStatConfig))]
    public class ItemStatConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
#endif
