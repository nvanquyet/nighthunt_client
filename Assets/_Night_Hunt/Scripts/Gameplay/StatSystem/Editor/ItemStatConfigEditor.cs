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
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Sample Setup", EditorStyles.boldLabel);
            
            var config = (ItemStatConfig)target;
            
            if (config is WeaponStatConfig wc)
            {
                if (GUILayout.Button("Setup Rifle"))
                {
                    ItemStatConfigSetup.SetupRifle(wc);
                    EditorUtility.SetDirty(config);
                }
                if (GUILayout.Button("Setup Pistol"))
                {
                    ItemStatConfigSetup.SetupPistol(wc);
                    EditorUtility.SetDirty(config);
                }
            }
            else if (config is EquipmentStatConfig ec)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Vest")) { ItemStatConfigSetup.SetupVest(ec); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Backpack")) { ItemStatConfigSetup.SetupBackpack(ec); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Helmet")) { ItemStatConfigSetup.SetupHelmet(ec); EditorUtility.SetDirty(config); }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Belt")) { ItemStatConfigSetup.SetupBelt(ec); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Gloves")) { ItemStatConfigSetup.SetupGloves(ec); EditorUtility.SetDirty(config); }
                EditorGUILayout.EndHorizontal();
            }
            else if (config is AttachmentStatConfig ac)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Red Dot")) { ItemStatConfigSetup.SetupRedDot(ac); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Suppressor")) { ItemStatConfigSetup.SetupSuppressor(ac); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Grip")) { ItemStatConfigSetup.SetupVerticalGrip(ac); EditorUtility.SetDirty(config); }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Ext Mag")) { ItemStatConfigSetup.SetupExtendedMagazine(ac); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Flashlight")) { ItemStatConfigSetup.SetupFlashlight(ac); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Pouch")) { ItemStatConfigSetup.SetupStoragePouch(ac); EditorUtility.SetDirty(config); }
                EditorGUILayout.EndHorizontal();
            }
            else if (config is ConsumableStatConfig cc)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Medkit")) { ItemStatConfigSetup.SetupMedkit(cc); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Energy Drink")) { ItemStatConfigSetup.SetupEnergyDrink(cc); EditorUtility.SetDirty(config); }
                EditorGUILayout.EndHorizontal();
            }
            else if (config is ThrowableStatConfig tc)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Frag Grenade")) { ItemStatConfigSetup.SetupFragGrenade(tc); EditorUtility.SetDirty(config); }
                if (GUILayout.Button("Smoke Grenade")) { ItemStatConfigSetup.SetupSmokeGrenade(tc); EditorUtility.SetDirty(config); }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
