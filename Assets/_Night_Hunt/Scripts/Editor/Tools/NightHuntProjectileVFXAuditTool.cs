using System.Collections.Generic;
using System.IO;
using System.Text;
using NightHunt.Gameplay.Character.Combat.Weapons;
using UnityEditor;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    public static class NightHuntProjectileVFXAuditTool
    {
        private const string ReportPath = "Assets/_Night_Hunt/Reports/ProjectileVFXAuditReport.md";
        private const string RunRequestPath = "Assets/_Night_Hunt/EditorRunRequests/FixProjectileVFX.request";
        private const string WeaponProjectileFolder = "Assets/_Night_Hunt/Prefabs/Items/Projectile";
        private const string ThrowableFolder = "Assets/_Night_Hunt/Prefabs/Items/Runtime/Throwable";

        private static readonly ProjectileVfxSetup[] Setups =
        {
            new("Projectile_Bullet_Pistol.prefab",
                muzzle: "GunFireYellow", visual: "Projectile 1", detonation: "Hit 1",
                source: ProjectileSource.WeaponProjectile, visualScale: new Vector3(0.35f, 0.35f, 0.55f), vfxScale: 0.55f),

            new("Projectile_Bullet_SMG.prefab",
                muzzle: "GunFireBlue", visual: "Projectile 2", detonation: "Hit 2",
                source: ProjectileSource.WeaponProjectile, visualScale: new Vector3(0.32f, 0.32f, 0.75f), vfxScale: 0.55f),

            new("Projectile_Bullet_Rifle.prefab",
                muzzle: "GunFireYellow", visual: "Projectile 3", detonation: "Hit 3",
                source: ProjectileSource.WeaponProjectile, visualScale: new Vector3(0.35f, 0.35f, 0.9f), vfxScale: 0.65f),

            new("Projectile_Bullet_Sniper.prefab",
                muzzle: "BulletMuzzleBlue", visual: "Projectile 4", detonation: "Hit 4",
                source: ProjectileSource.WeaponProjectile, visualScale: new Vector3(0.3f, 0.3f, 1.25f), vfxScale: 0.75f),

            new("Projectile_Buckshot_Shotgun.prefab",
                muzzle: "BulletMuzzleFire", visual: "Projectile 5", detonation: "Hit 5",
                source: ProjectileSource.WeaponProjectile, visualScale: new Vector3(0.55f, 0.55f, 0.7f), vfxScale: 0.85f),

            new("Projectile_Rocket.prefab",
                muzzle: "RocketMuzzleFire", visual: "RocketMissileFire", detonation: "ExplosionRoundFire",
                source: ProjectileSource.WeaponProjectile, visualScale: Vector3.one, vfxScale: 1.4f),

            new("NetworkProjectile_FragGrenade.prefab",
                muzzle: null, visual: null, detonation: "GrenadeExplosionFire",
                source: ProjectileSource.Throwable, visualScale: Vector3.one, vfxScale: 1.6f),

            new("NetworkProjectile_SmokeGrenade.prefab",
                muzzle: null, visual: null, detonation: "SmokeExplosionWhite",
                source: ProjectileSource.Throwable, visualScale: Vector3.one, vfxScale: 1.8f),
        };

        [MenuItem("NightHunt/Tools/VFX/Audit And Fix Projectile VFX")]
        public static void AuditAndFixProjectileVFX()
        {
            EnsureFolder(Path.GetDirectoryName(ReportPath)?.Replace("\\", "/"));

            var log = new List<string>
            {
                "# Projectile VFX Audit",
                "",
                "| Prefab | Muzzle | Main Visual | Detonation | Result |",
                "|---|---|---|---|---|"
            };

            foreach (var setup in Setups)
            {
                string prefabPath = setup.Source == ProjectileSource.WeaponProjectile
                    ? $"{WeaponProjectileFolder}/{setup.PrefabName}"
                    : $"{ThrowableFolder}/{setup.PrefabName}";

                log.Add(FixPrefab(prefabPath, setup));
            }

            File.WriteAllText(ReportPath, string.Join("\n", log), Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NightHuntProjectileVFXAuditTool] Completed. Report: {ReportPath}");
        }

        [InitializeOnLoadMethod]
        private static void RunOnceIfRequested()
        {
            if (!File.Exists(RunRequestPath))
                return;

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RunRequestPath))
                    return;

                File.Delete(RunRequestPath);
                string metaPath = RunRequestPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);

                AuditAndFixProjectileVFX();
            };
        }

        private static string FixPrefab(string prefabPath, ProjectileVfxSetup setup)
        {
            if (!File.Exists(prefabPath))
                return Row(setup.PrefabName, setup, "MISSING PREFAB");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var projectileBase = root.GetComponent<ProjectileBase>();
                if (projectileBase == null)
                    projectileBase = root.AddComponent<ProjectileBase>();

                Transform muzzle = EnsureChild(root.transform, "[MuzzleFlash]", active: false);
                Transform visual = EnsureChild(root.transform, "[MainVisual]", active: true);
                Transform detonation = EnsureChild(root.transform, "[DetonationVFX]", active: false);

                string muzzleResult = ApplyEffect(muzzle, setup.MuzzleEffectName, setup.VfxScale, active: false);
                string visualResult = ApplyEffect(visual, setup.MainVisualEffectName, setup.VisualScale, active: true);
                string detonationResult = ApplyEffect(detonation, setup.DetonationEffectName, setup.VfxScale, active: false);

                var so = new SerializedObject(projectileBase);
                so.FindProperty("muzzleFlashChild")?.SetValue(muzzle.gameObject);
                so.FindProperty("mainVisualChild")?.SetValue(visual.gameObject);
                so.FindProperty("detonationVFXChild")?.SetValue(detonation.gameObject);
                so.FindProperty("muzzleFlashDuration")?.SetValue(setup.PrefabName.Contains("Rocket") ? 0.08f : 0.05f);
                so.FindProperty("lifetimeAfterImpact")?.SetValue(setup.Source == ProjectileSource.Throwable ? 3.5f : 1.2f);
                so.FindProperty("hideTrailOnImpact")?.SetValue(true);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return Row(setup.PrefabName, setup, $"{muzzleResult}; {visualResult}; {detonationResult}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ApplyEffect(Transform parent, string effectName, Vector3 scale, bool active)
        {
            if (string.IsNullOrWhiteSpace(effectName))
                return $"{parent.name}=kept";

            ClearEffectContainer(parent);

            GameObject effectPrefab = FindPrefab(effectName);
            if (effectPrefab == null)
                return $"{parent.name}=missing '{effectName}'";

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(effectPrefab, parent);
            instance.name = $"FX_{effectPrefab.name}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = scale;
            instance.SetActive(true);
            parent.gameObject.SetActive(active);

            return $"{parent.name}={effectPrefab.name}";
        }

        private static void ClearEffectContainer(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);

            foreach (var component in parent.GetComponents<Component>())
            {
                if (component is Transform)
                    continue;

                if (component is ParticleSystem
                    || component is TrailRenderer
                    || component is LineRenderer
                    || component is MeshRenderer
                    || component is MeshFilter
                    || component is Light)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static GameObject FindPrefab(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:Prefab", new[]
            {
                "Assets/Epic Toon FX",
                "Assets/Hovl Studio",
                "Assets/SciFi_Space_Soldier_Complete/Resources/Prefabs/VFX"
            });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static Transform EnsureChild(Transform parent, string name, bool active)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                child = go.transform;
                child.SetParent(parent, false);
            }

            child.gameObject.SetActive(active);
            return child;
        }

        private static string Row(string prefabName, ProjectileVfxSetup setup, string result)
        {
            return $"| {prefabName} | {setup.MuzzleEffectName ?? "-"} | {setup.MainVisualEffectName ?? "-"} | {setup.DetonationEffectName ?? "-"} | {result} |";
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private enum ProjectileSource
        {
            WeaponProjectile,
            Throwable,
        }

        private readonly struct ProjectileVfxSetup
        {
            public readonly string PrefabName;
            public readonly string MuzzleEffectName;
            public readonly string MainVisualEffectName;
            public readonly string DetonationEffectName;
            public readonly ProjectileSource Source;
            public readonly Vector3 VisualScale;
            public readonly Vector3 VfxScale;

            public ProjectileVfxSetup(string prefabName, string muzzle, string visual, string detonation,
                ProjectileSource source, Vector3 visualScale, float vfxScale)
            {
                PrefabName = prefabName;
                MuzzleEffectName = muzzle;
                MainVisualEffectName = visual;
                DetonationEffectName = detonation;
                Source = source;
                VisualScale = visualScale;
                VfxScale = Vector3.one * vfxScale;
            }
        }
    }
}
