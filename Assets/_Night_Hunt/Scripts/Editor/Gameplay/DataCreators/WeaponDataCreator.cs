using UnityEngine;
using UnityEditor;
using NightHunt.Data;

namespace NightHunt.Editor.Gameplay.DataCreators
{
    /// <summary>
    /// Tool to create weapon config data
    /// </summary>
    public class WeaponDataCreator : EditorWindow
    {
        private string weaponId = "";
        private string displayName = "";
        private string ballisticType = "Hitscan";
        private float damageBody = 30f;
        private float damageHeadMul = 2f;
        private float fireRate = 10f;
        private int magazineSize = 30;
        private int reserveAmmo = 90;
        private float reloadTime = 2f;
        private float maxRange = 100f;
        private float spreadBase = 0.1f;
        private float spreadMoveMul = 1.5f;
        private float recoilHorizontal = 1f;
        private float recoilVertical = 2f;
        private float projectileSpeed = 50f;
        private float gravityScale = 1f;

        [MenuItem("Tools/Night Hunt/Create Weapon Data")]
        public static void ShowWindow()
        {
            GetWindow<WeaponDataCreator>("Weapon Data Creator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Weapon Data Creator", EditorStyles.boldLabel);

            weaponId = EditorGUILayout.TextField("Weapon ID", weaponId);
            displayName = EditorGUILayout.TextField("Display Name", displayName);
            ballisticType = EditorGUILayout.TextField("Ballistic Type", ballisticType);
            damageBody = EditorGUILayout.FloatField("Damage Body", damageBody);
            damageHeadMul = EditorGUILayout.FloatField("Damage Head Multiplier", damageHeadMul);
            fireRate = EditorGUILayout.FloatField("Fire Rate", fireRate);
            magazineSize = EditorGUILayout.IntField("Magazine Size", magazineSize);
            reserveAmmo = EditorGUILayout.IntField("Reserve Ammo", reserveAmmo);
            reloadTime = EditorGUILayout.FloatField("Reload Time", reloadTime);
            maxRange = EditorGUILayout.FloatField("Max Range", maxRange);
            spreadBase = EditorGUILayout.FloatField("Spread Base", spreadBase);
            spreadMoveMul = EditorGUILayout.FloatField("Spread Move Multiplier", spreadMoveMul);
            recoilHorizontal = EditorGUILayout.FloatField("Recoil Horizontal", recoilHorizontal);
            recoilVertical = EditorGUILayout.FloatField("Recoil Vertical", recoilVertical);
            projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", projectileSpeed);
            gravityScale = EditorGUILayout.FloatField("Gravity Scale", gravityScale);

            if (GUILayout.Button("Create Weapon Data"))
            {
                CreateWeaponData();
            }
        }

        private void CreateWeaponData()
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                EditorUtility.DisplayDialog("Error", "Weapon ID cannot be empty!", "OK");
                return;
            }

            // TODO: Create weapon config data and add to config file
            Debug.Log($"[WeaponDataCreator] Created weapon data: {weaponId}");
            EditorUtility.DisplayDialog("Success", $"Weapon data created: {weaponId}", "OK");
        }
    }
}

