using System.Collections.Generic;
using System.IO;
using System.Text;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.GameplaySystems.Core.Data;
using UnityEditor;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    public static class NightHuntWeaponPrefabAuditTool
    {
        private const string WeaponFolder = "Assets/_Night_Hunt/Prefabs/Items/Weapon";
        private const string ProjectileFolder = "Assets/_Night_Hunt/Prefabs/Items/Projectile";
        private const string ReportPath = "Assets/_Night_Hunt/Reports/WeaponPrefabAuditReport.md";
        private const string RunRequestPath = "Assets/_Night_Hunt/EditorRunRequests/FixWeaponPrefabs.request";

        private static readonly WeaponSetup[] WeaponSetups =
        {
            new("Weapon_Pistol_1.prefab", "Projectile_Bullet_Pistol.prefab", "Weapon_Pistol.asset", typeof(HitscanWeapon),
                maxRange: 90f, speed: 360f, gravity: 0f, spreadBase: 1.2f, spreadPenalty: 0.45f,
                spreadRecovery: 4.5f, defaultFireMode: FireMode.Single, allowToggle: true,
                tacticalReload: true, headMultiplier: 2f, pellets: 1, pelletBonus: 2f,
                firePoint: new Vector3(0f, 0f, 0.48f), ik: new Vector3(-0.08f, -0.04f, 0.12f),
                projectileColor: new Color(1f, 0.82f, 0.18f, 1f), hitboxRadius: 0.04f,
                projectileLifetime: 0.35f, usePhysicsProjectile: false, visualScale: new Vector3(0.12f, 0.18f, 0.55f)),

            new("Weapon_SMG_1.prefab", "Projectile_Bullet_SMG.prefab", "Weapon_SMG.asset", typeof(HitscanWeapon),
                maxRange: 110f, speed: 420f, gravity: 0f, spreadBase: 1.35f, spreadPenalty: 0.5f,
                spreadRecovery: 5.5f, defaultFireMode: FireMode.Auto, allowToggle: true,
                tacticalReload: true, headMultiplier: 1.85f, pellets: 1, pelletBonus: 2.5f,
                firePoint: new Vector3(0f, 0f, 0.58f), ik: new Vector3(-0.1f, -0.05f, 0.18f),
                projectileColor: new Color(0.45f, 0.9f, 1f, 1f), hitboxRadius: 0.04f,
                projectileLifetime: 0.28f, usePhysicsProjectile: false, visualScale: new Vector3(0.14f, 0.18f, 0.75f)),

            new("Weapon_Rifle_1.prefab", "Projectile_Bullet_Rifle.prefab", "Weapon_AR.asset", typeof(HitscanWeapon),
                maxRange: 150f, speed: 450f, gravity: 0f, spreadBase: 0.9f, spreadPenalty: 0.35f,
                spreadRecovery: 4f, defaultFireMode: FireMode.Auto, allowToggle: true,
                tacticalReload: true, headMultiplier: 2f, pellets: 1, pelletBonus: 2.5f,
                firePoint: new Vector3(0f, 0f, 0.72f), ik: new Vector3(-0.12f, -0.05f, 0.22f),
                projectileColor: new Color(1f, 0.45f, 0.08f, 1f), hitboxRadius: 0.045f,
                projectileLifetime: 0.35f, usePhysicsProjectile: false, visualScale: new Vector3(0.14f, 0.2f, 1.05f)),

            new("Weapon_Sniper_1.prefab", "Projectile_Bullet_Sniper.prefab", "Weapon_Sniper.asset", typeof(HitscanWeapon),
                maxRange: 260f, speed: 650f, gravity: 0f, spreadBase: 0.18f, spreadPenalty: 0.08f,
                spreadRecovery: 3.5f, defaultFireMode: FireMode.Single, allowToggle: false,
                tacticalReload: true, headMultiplier: 2.5f, pellets: 1, pelletBonus: 0.5f,
                firePoint: new Vector3(0f, 0f, 1.05f), ik: new Vector3(-0.15f, -0.05f, 0.32f),
                projectileColor: new Color(0.95f, 0.95f, 1f, 1f), hitboxRadius: 0.035f,
                projectileLifetime: 0.4f, usePhysicsProjectile: false, visualScale: new Vector3(0.12f, 0.18f, 1.35f)),

            new("Weapon_Shotgun_1.prefab", "Projectile_Buckshot_Shotgun.prefab", "Weapon_Shotgun.asset", typeof(HitscanWeapon),
                maxRange: 55f, speed: 340f, gravity: 0f, spreadBase: 2f, spreadPenalty: 0.75f,
                spreadRecovery: 2.5f, defaultFireMode: FireMode.Single, allowToggle: false,
                tacticalReload: true, headMultiplier: 1.5f, pellets: 8, pelletBonus: 4.5f,
                firePoint: new Vector3(0f, 0f, 0.65f), ik: new Vector3(-0.14f, -0.05f, 0.24f),
                projectileColor: new Color(1f, 0.62f, 0.14f, 1f), hitboxRadius: 0.035f,
                projectileLifetime: 0.3f, usePhysicsProjectile: false, visualScale: new Vector3(0.18f, 0.22f, 0.9f)),

            new("Weapon_Melee_1.prefab", null, "Weapon_Melee.asset", typeof(MeleeWeapon),
                maxRange: 2f, speed: 1f, gravity: 0f, spreadBase: 0f, spreadPenalty: 0f,
                spreadRecovery: 1f, defaultFireMode: FireMode.Single, allowToggle: false,
                tacticalReload: false, headMultiplier: 1f, pellets: 1, pelletBonus: 0f,
                firePoint: new Vector3(0f, 0f, 0.75f), ik: new Vector3(-0.08f, -0.03f, 0.2f),
                projectileColor: Color.white, hitboxRadius: 0f,
                projectileLifetime: 0f, usePhysicsProjectile: false, visualScale: new Vector3(0.08f, 0.08f, 1.1f)),

            new("Weapon_RocketLauncher_1.prefab", "Projectile_Rocket.prefab", "Weapon_RocketLauncher.asset", typeof(ProjectileWeapon),
                maxRange: 105f, speed: 65f, gravity: 0.15f, spreadBase: 0.45f, spreadPenalty: 0.1f,
                spreadRecovery: 5f, defaultFireMode: FireMode.Single, allowToggle: false,
                tacticalReload: false, headMultiplier: 1f, pellets: 1, pelletBonus: 0f,
                firePoint: new Vector3(0f, 0f, 0.88f), ik: new Vector3(-0.16f, -0.05f, 0.28f),
                projectileColor: new Color(0.95f, 0.18f, 0.08f, 1f), hitboxRadius: 0.18f,
                projectileLifetime: 3f, usePhysicsProjectile: true, visualScale: new Vector3(0.26f, 0.26f, 1.1f)),
        };

        [MenuItem("NightHunt/Tools/Weapons/Audit And Fix Weapon Prefabs")]
        public static void AuditAndFixWeaponPrefabs()
        {
            EnsureFolder(ProjectileFolder);
            EnsureFolder(Path.GetDirectoryName(ReportPath)?.Replace("\\", "/"));

            var log = new List<string>
            {
                "# Weapon Prefab Audit",
                "",
                "| Weapon | Component | Projectile | FirePoint | IK | Result |",
                "|---|---|---|---|---|---|"
            };

            foreach (var setup in WeaponSetups)
            {
                GameObject projectilePrefab = null;
                if (!string.IsNullOrWhiteSpace(setup.ProjectileName))
                {
                    string projectilePath = $"{ProjectileFolder}/{setup.ProjectileName}";
                    projectilePrefab = BuildOrUpdateProjectilePrefab(projectilePath, setup);
                }

                string weaponPath = $"{WeaponFolder}/{setup.WeaponName}";
                var weaponPrefab = FixWeaponPrefab(weaponPath, setup, projectilePrefab, out string row);
                AssignWeaponDefinitionVisual(setup, weaponPrefab);
                log.Add(row);
            }

            File.WriteAllText(ReportPath, string.Join("\n", log), Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NightHuntWeaponPrefabAuditTool] Completed. Report: {ReportPath}");
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

                AuditAndFixWeaponPrefabs();
            };
        }

        private static GameObject BuildOrUpdateProjectilePrefab(string path, WeaponSetup setup)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = root != null
                ? PrefabUtility.LoadPrefabContents(path)
                : new GameObject(Path.GetFileNameWithoutExtension(path));

            instance.name = Path.GetFileNameWithoutExtension(path);
            instance.layer = LayerMask.NameToLayer("Projectile");

            var projectile = instance.GetComponent<ProjectileComponent>();
            if (projectile == null)
                projectile = instance.AddComponent<ProjectileComponent>();

            var collider = instance.GetComponent<SphereCollider>();
            if (collider == null)
                collider = instance.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = setup.ProjectileHitboxRadius;

            var muzzle = EnsureChild(instance.transform, "[MuzzleFlash]");
            var visual = EnsureChild(instance.transform, "[MainVisual]");
            var detonation = EnsureChild(instance.transform, "[DetonationVFX]");

            EnsureTrail(visual, setup.ProjectileColor, setup.UsePhysicsProjectile);
            if (setup.UsePhysicsProjectile)
                EnsureRocketBody(visual, setup.ProjectileColor);

            var so = new SerializedObject(projectile);
            so.FindProperty("isImpact")?.SetValue(true);
            so.FindProperty("fuseTime")?.SetValue(0f);
            so.FindProperty("lifetimeAfterImpact")?.SetValue(setup.ProjectileLifetimeAfterImpact);
            so.FindProperty("hideTrailOnImpact")?.SetValue(true);
            so.FindProperty("muzzleFlashDuration")?.SetValue(setup.UsePhysicsProjectile ? 0.08f : 0.05f);
            so.FindProperty("muzzleFlashChild")?.SetValue(muzzle.gameObject);
            so.FindProperty("mainVisualChild")?.SetValue(visual.gameObject);
            so.FindProperty("detonationVFXChild")?.SetValue(detonation.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            if (root != null)
                PrefabUtility.UnloadPrefabContents(instance);
            else
                Object.DestroyImmediate(instance);

            return saved != null ? saved : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject FixWeaponPrefab(string weaponPath, WeaponSetup setup, GameObject projectilePrefab, out string reportRow)
        {
            bool existed = File.Exists(weaponPath);
            var root = existed
                ? PrefabUtility.LoadPrefabContents(weaponPath)
                : new GameObject(Path.GetFileNameWithoutExtension(weaponPath));

            string componentStatus;

            var weapon = root.GetComponentInChildren<WeaponBase>(true);
            GameObject componentHost = weapon != null ? weapon.gameObject : root;

            if (setup.WeaponComponentType == typeof(HitscanWeapon))
            {
                if (weapon != null && weapon.GetType() != typeof(HitscanWeapon))
                    Object.DestroyImmediate(weapon);

                weapon = componentHost.GetComponent<HitscanWeapon>();
                if (weapon == null)
                    weapon = componentHost.AddComponent<HitscanWeapon>();
                componentStatus = "HitscanWeapon";
            }
            else if (setup.WeaponComponentType == typeof(ProjectileWeapon))
            {
                if (weapon != null && weapon.GetType() != typeof(ProjectileWeapon))
                    Object.DestroyImmediate(weapon);

                weapon = componentHost.GetComponent<ProjectileWeapon>();
                if (weapon == null)
                    weapon = componentHost.AddComponent<ProjectileWeapon>();
                componentStatus = "ProjectileWeapon";
            }
            else
            {
                if (weapon != null && weapon.GetType() != typeof(MeleeWeapon))
                    Object.DestroyImmediate(weapon);

                weapon = componentHost.GetComponent<MeleeWeapon>();
                if (weapon == null)
                    weapon = componentHost.AddComponent<MeleeWeapon>();
                componentStatus = "MeleeWeapon";
            }

            var firePoint = FindOrCreateChild(componentHost.transform, "Fire Point", setup.FirePointLocalPosition);
            var ik = FindOrCreateChild(componentHost.transform, "ArmIK", setup.LeftHandIkLocalPosition);
            EnsureVisualPlaceholder(componentHost.transform, setup);

            var so = new SerializedObject(weapon);
            so.FindProperty("firePoint")?.SetValue(firePoint);
            so.FindProperty("leftHandIKTarget")?.SetValue(ik);
            so.FindProperty("projectilePrefab")?.SetValue(projectilePrefab);
            so.FindProperty("maxRange")?.SetValue(setup.MaxRange);
            so.FindProperty("projectileSpeed")?.SetValue(setup.ProjectileSpeed);
            so.FindProperty("gravityScale")?.SetValue(setup.GravityScale);
            so.FindProperty("spreadBase")?.SetValue(setup.SpreadBase);
            so.FindProperty("spreadPenaltyPerShot")?.SetValue(setup.SpreadPenaltyPerShot);
            so.FindProperty("spreadRecoveryRate")?.SetValue(setup.SpreadRecoveryRate);
            so.FindProperty("spreadMax")?.SetValue(setup.SpreadBase * 4f);
            so.FindProperty("defaultFireMode")?.SetValue(setup.DefaultFireMode);
            so.FindProperty("allowFireModeToggle")?.SetValue(setup.AllowFireModeToggle);
            so.FindProperty("canTacticalReload")?.SetValue(setup.CanTacticalReload);
            so.FindProperty("damageHeadMultiplier")?.SetValue(setup.DamageHeadMultiplier);
            so.FindProperty("pelletCount")?.SetValue(setup.PelletCount);
            so.FindProperty("pelletSpreadBonus")?.SetValue(setup.PelletSpreadBonus);
            so.ApplyModifiedPropertiesWithoutUndo();

            string firePointName = firePoint != null ? firePoint.name : "-";
            string ikName = ik != null ? ik.name : "-";

            var saved = PrefabUtility.SaveAsPrefabAsset(root, weaponPath);
            if (existed)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            string projectileLabel = string.IsNullOrWhiteSpace(setup.ProjectileName) ? "-" : $"`{setup.ProjectileName}`";
            reportRow = $"| `{setup.WeaponName}` | {componentStatus} | {projectileLabel} | `{firePointName}` | `{ikName}` | {(existed ? "fixed" : "created")} |";
            return saved != null ? saved : AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath);
        }

        private static Transform FindOrCreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                child = go.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            return child;
        }

        private static void EnsureVisualPlaceholder(Transform parent, WeaponSetup setup)
        {
            if (parent.GetComponentInChildren<Renderer>(true) != null)
                return;

            var model = parent.Find("[GeneratedModel]");
            if (model == null)
            {
                var primitiveType = setup.WeaponComponentType == typeof(MeleeWeapon)
                    ? PrimitiveType.Capsule
                    : PrimitiveType.Cube;

                var go = GameObject.CreatePrimitive(primitiveType);
                go.name = "[GeneratedModel]";
                model = go.transform;
                model.SetParent(parent, false);

                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }

            model.localPosition = new Vector3(0f, 0f, setup.VisualScale.z * 0.35f);
            model.localRotation = setup.WeaponComponentType == typeof(MeleeWeapon)
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            model.localScale = setup.VisualScale;
        }

        private static void AssignWeaponDefinitionVisual(WeaponSetup setup, GameObject weaponPrefab)
        {
            if (weaponPrefab == null || string.IsNullOrWhiteSpace(setup.DefinitionAssetName))
                return;

            string definitionPath = $"Assets/_Night_Hunt/Data/Resources/Database/Items/List Items/Weapons/{setup.DefinitionAssetName}";
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(definitionPath);
            if (definition == null)
            {
                Debug.LogWarning($"[NightHuntWeaponPrefabAuditTool] WeaponDefinition missing: {definitionPath}");
                return;
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_visualPrefab")?.SetValue(weaponPrefab);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                return child;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsureTrail(Transform visual, Color color, bool thick)
        {
            var trail = visual.GetComponent<TrailRenderer>();
            if (trail == null)
                trail = visual.gameObject.AddComponent<TrailRenderer>();

            trail.time = thick ? 0.35f : 0.08f;
            trail.startWidth = thick ? 0.08f : 0.025f;
            trail.endWidth = 0.002f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private static void EnsureRocketBody(Transform visual, Color color)
        {
            var filter = visual.GetComponent<MeshFilter>();
            if (filter == null)
                filter = visual.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = visual.gameObject.AddComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.sharedMaterial = mat;

            visual.localScale = new Vector3(0.18f, 0.18f, 0.42f);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string name = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct WeaponSetup
        {
            public readonly string WeaponName;
            public readonly string ProjectileName;
            public readonly string DefinitionAssetName;
            public readonly System.Type WeaponComponentType;
            public readonly float MaxRange;
            public readonly float ProjectileSpeed;
            public readonly float GravityScale;
            public readonly float SpreadBase;
            public readonly float SpreadPenaltyPerShot;
            public readonly float SpreadRecoveryRate;
            public readonly FireMode DefaultFireMode;
            public readonly bool AllowFireModeToggle;
            public readonly bool CanTacticalReload;
            public readonly float DamageHeadMultiplier;
            public readonly int PelletCount;
            public readonly float PelletSpreadBonus;
            public readonly Vector3 FirePointLocalPosition;
            public readonly Vector3 LeftHandIkLocalPosition;
            public readonly Color ProjectileColor;
            public readonly float ProjectileHitboxRadius;
            public readonly float ProjectileLifetimeAfterImpact;
            public readonly bool UsePhysicsProjectile;
            public readonly Vector3 VisualScale;

            public WeaponSetup(
                string weaponName, string projectileName, string definitionAssetName, System.Type componentType,
                float maxRange, float speed, float gravity, float spreadBase, float spreadPenalty,
                float spreadRecovery, FireMode defaultFireMode, bool allowToggle,
                bool tacticalReload, float headMultiplier, int pellets, float pelletBonus,
                Vector3 firePoint, Vector3 ik, Color projectileColor, float hitboxRadius,
                float projectileLifetime, bool usePhysicsProjectile, Vector3 visualScale)
            {
                WeaponName = weaponName;
                ProjectileName = projectileName;
                DefinitionAssetName = definitionAssetName;
                WeaponComponentType = componentType;
                MaxRange = maxRange;
                ProjectileSpeed = speed;
                GravityScale = gravity;
                SpreadBase = spreadBase;
                SpreadPenaltyPerShot = spreadPenalty;
                SpreadRecoveryRate = spreadRecovery;
                DefaultFireMode = defaultFireMode;
                AllowFireModeToggle = allowToggle;
                CanTacticalReload = tacticalReload;
                DamageHeadMultiplier = headMultiplier;
                PelletCount = pellets;
                PelletSpreadBonus = pelletBonus;
                FirePointLocalPosition = firePoint;
                LeftHandIkLocalPosition = ik;
                ProjectileColor = projectileColor;
                ProjectileHitboxRadius = hitboxRadius;
                ProjectileLifetimeAfterImpact = projectileLifetime;
                UsePhysicsProjectile = usePhysicsProjectile;
                VisualScale = visualScale;
            }
        }
    }

}
