#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Object;
using NightHunt.Gameplay.Beacon;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Respawn;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.ItemUse;
using NightHunt.GameplaySystems.Loot;
using UnityEditor;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Audits item definitions against the new split:
    /// - VisualPrefab = pure client-side item visual.
    /// - Runtime prefabs = concrete network/gameplay prefabs on throwable/beacon components.
    /// </summary>
    public static class NightHuntItemDefinitionAuditTool
    {
        private const string TemplateRoot = "Assets/_Night_Hunt/Prefabs/_Generated/Templates";
        private const string LootRoot = "Assets/_Night_Hunt/Prefabs/LootItem";
        private const string DeprecatedRoot = "Assets/_Night_Hunt/Prefabs/_Deprecated/LegacyItemPrefabFolders";

        [MenuItem("NightHunt/Validate/Item Definitions/Report Only", priority = 100)]
        public static void ReportOnly() => AuditDefinitions(autoFix: false);

        [MenuItem("NightHunt/Validate/Item Definitions/Auto Fix Missing Prefabs", priority = 101)]
        public static void AutoFixMissingPrefabsAndAssignments() => AuditDefinitions(autoFix: true);

        [MenuItem("NightHunt/Validate/Item Definitions/Move Legacy Item Prefab Folders To _Deprecated", priority = 102)]
        public static void MoveLegacyItemPrefabFoldersToDeprecated()
        {
            EnsureFolder(DeprecatedRoot);
            MoveFolderIfExists("Assets/_Night_Hunt/Prefabs/Items/Ground", $"{DeprecatedRoot}/Ground");
            MoveFolderIfExists("Assets/_Night_Hunt/Prefabs/Items/Hold", $"{DeprecatedRoot}/Hold");
            MoveFolderIfExists("Assets/_Night_Hunt/Prefabs/Items/Deploy", $"{DeprecatedRoot}/Deploy");
            MoveFolderIfExists("Assets/_Night_Hunt/Prefabs/Items/Projectile", $"{DeprecatedRoot}/Projectile");
            AssetDatabase.Refresh();
        }

        private static void AuditDefinitions(bool autoFix)
        {
            EnsureFolders();
            var templates = EnsureTemplates();
            var definitions = LoadAllItemDefinitions();

            int warnings = 0;
            int fixes = 0;
            var log = new List<string>
            {
                $"=== NightHunt Item Definition Audit ({(autoFix ? "auto-fix" : "report")}) ===",
                $"Definitions found: {definitions.Count}"
            };

            foreach (var def in definitions)
            {
                if (def == null)
                    continue;

                bool changed = false;

                if (!def.IsValid(out var validationError))
                {
                    warnings++;
                    log.Add($"WARN {AssetDatabase.GetAssetPath(def)}: {validationError}");
                }

                if (def is PhysicalItemDefinition physical)
                {
                    if (physical.VisualPrefab == null)
                    {
                        warnings++;
                        log.Add($"MISSING VisualPrefab: {def.ItemID} ({def.GetType().Name})");
                        if (autoFix)
                        {
                            physical.VisualPrefab = ResolveVisualTemplate(def, templates);
                            changed = true;
                            fixes++;
                        }
                    }
                    else if (ItemVisualResolver.IsNetworkedVisual(physical.VisualPrefab))
                    {
                        warnings++;
                        log.Add($"WARN VisualPrefab must be pure visual, but has NetworkObject: {def.ItemID} -> {physical.VisualPrefab.name}");
                    }
                }

                if (def is WeaponDefinition weapon)
                {
                    var visual = ItemVisualResolver.ResolveVisualPrefab(weapon);
                    if (visual == null || !HasComponentInPrefab<WeaponBase>(visual))
                    {
                        warnings++;
                        log.Add($"WARN Weapon VisualPrefab should contain WeaponBase: {def.ItemID}");
                    }
                }

                if (def is ThrowableDefinition throwable)
                {
                    if (throwable.ProjectilePrefab == null)
                    {
                        warnings++;
                        log.Add($"MISSING Throwable ProjectilePrefab: {def.ItemID}");
                        if (autoFix)
                        {
                            throwable.ProjectilePrefab = templates.ThrowableProjectile;
                            changed = true;
                            fixes++;
                        }
                    }
                    else
                    {
                        ValidateThrowableProjectile(throwable, log, ref warnings);
                    }
                }

                if (def is BeaconDefinition beacon)
                {
                    if (beacon.NetworkBeaconPrefab == null)
                    {
                        warnings++;
                        log.Add($"MISSING NetworkBeaconPrefab: {def.ItemID}");
                        if (autoFix)
                        {
                            beacon.NetworkBeaconPrefab = templates.RespawnBeacon;
                            changed = true;
                            fixes++;
                        }
                    }
                    else if (!HasComponentInPrefab<NetworkObject>(beacon.NetworkBeaconPrefab) || !HasComponentInPrefab<RespawnBeacon>(beacon.NetworkBeaconPrefab))
                    {
                        warnings++;
                        log.Add($"WARN NetworkBeaconPrefab must have NetworkObject + RespawnBeacon: {def.ItemID} -> {beacon.NetworkBeaconPrefab.name}");
                    }

                    if (beacon.PlacementPreviewPrefab == null && autoFix)
                    {
                        beacon.PlacementPreviewPrefab = templates.BeaconPreview;
                        changed = true;
                        fixes++;
                    }
                }

                if (def is DeployableDefinition deployable)
                {
                    if (deployable.NetworkDeployablePrefab == null)
                    {
                        warnings++;
                        log.Add($"MISSING NetworkDeployablePrefab: {def.ItemID}");
                        if (autoFix)
                        {
                            deployable.NetworkDeployablePrefab = ResolveDeployableTemplate(deployable, templates);
                            changed = true;
                            fixes++;
                        }
                    }
                    else if (!HasComponentInPrefab<NetworkObject>(deployable.NetworkDeployablePrefab) ||
                             !HasComponentInPrefab<BaseDeployable>(deployable.NetworkDeployablePrefab))
                    {
                        warnings++;
                        log.Add($"WARN NetworkDeployablePrefab must have NetworkObject + BaseDeployable: {def.ItemID} -> {deployable.NetworkDeployablePrefab.name}");
                    }
                    else
                    {
                        ValidateDeployablePrefabContract(deployable, log, ref warnings);
                    }

                    if (deployable.PlacementPreviewPrefab == null && autoFix)
                    {
                        deployable.PlacementPreviewPrefab = deployable.VisualPrefab != null
                            ? deployable.VisualPrefab
                            : templates.Visual;
                        changed = true;
                        fixes++;
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(def);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.Add($"Warnings: {warnings}");
            log.Add($"Fixes applied: {fixes}");
            Debug.Log(string.Join("\n", log));
        }

        private static GameObject ResolveVisualTemplate(ItemDefinition def, Templates templates)
        {
            if (def is WeaponDefinition weapon)
            {
                return weapon.WeaponClass switch
                {
                    WeaponClass.Launcher => templates.ProjectileWeapon,
                    WeaponClass.Melee => templates.MeleeWeapon,
                    _ => templates.HitscanWeapon,
                };
            }

            return templates.Visual;
        }

        private static void ValidateThrowableProjectile(ThrowableDefinition throwable, List<string> log, ref int warnings)
        {
            var prefab = throwable.ProjectilePrefab;
            if (!HasComponentInPrefab<NetworkObject>(prefab))
            {
                warnings++;
                log.Add($"WARN Throwable projectile missing NetworkObject: {throwable.ItemID} -> {prefab.name}");
            }
            if (!HasComponentInPrefab<Rigidbody>(prefab))
            {
                warnings++;
                log.Add($"WARN Throwable projectile missing Rigidbody: {throwable.ItemID} -> {prefab.name}");
            }
            if (!HasComponentInPrefab<ProjectileNetworked>(prefab))
            {
                warnings++;
                log.Add($"WARN Throwable projectile missing ProjectileNetworked: {throwable.ItemID} -> {prefab.name}");
            }
            if (!HasComponentInPrefab<ProjectileBase>(prefab))
            {
                warnings++;
                log.Add($"WARN Throwable projectile missing ProjectileBase VFX component: {throwable.ItemID} -> {prefab.name}");
            }
        }

        private static void ValidateDeployablePrefabContract(DeployableDefinition deployable, List<string> log, ref int warnings)
        {
            var prefab = deployable.NetworkDeployablePrefab;
            if (prefab == null)
                return;

            if (RequiresVisionWard(deployable.DeployableKind) && !HasComponentInPrefab<VisionWard>(prefab))
            {
                warnings++;
                log.Add($"WARN DeployableKind {deployable.DeployableKind} requires VisionWard: {deployable.ItemID} -> {prefab.name}");
                return;
            }

            if (RequiresTrapDeployable(deployable.DeployableKind) && !HasComponentInPrefab<TrapDeployable>(prefab))
            {
                warnings++;
                log.Add($"WARN DeployableKind {deployable.DeployableKind} requires TrapDeployable: {deployable.ItemID} -> {prefab.name}");
            }
        }

        private static bool RequiresVisionWard(DeployableKind kind)
            => kind == DeployableKind.VisionNode || kind == DeployableKind.LightPoint;

        private static bool RequiresTrapDeployable(DeployableKind kind)
            => kind == DeployableKind.ExplosiveMine || kind == DeployableKind.ShockField;

        private static List<ItemDefinition> LoadAllItemDefinitions()
        {
            var results = new List<ItemDefinition>();
            var seen = new HashSet<string>();
            string[] typeFilters =
            {
                "t:WeaponDefinition",
                "t:EquipmentDefinition",
                "t:AttachmentDefinition",
                "t:ConsumableDefinition",
                "t:ThrowableDefinition",
                "t:BeaconDefinition",
                "t:DeployableDefinition",
            };

            foreach (var filter in typeFilters)
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path))
                    continue;

                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (def != null)
                    results.Add(def);
            }

            return results;
        }

        private static Templates EnsureTemplates()
        {
            return new Templates
            {
                Visual = LoadOrCreate($"{TemplateRoot}/Visual_Item_Template.prefab", BuildVisual),
                HitscanWeapon = LoadOrCreate($"{TemplateRoot}/Weapon_Hitscan_Template.prefab", BuildHitscanWeapon),
                ProjectileWeapon = LoadOrCreate($"{TemplateRoot}/Weapon_Projectile_Template.prefab", BuildProjectileWeapon),
                MeleeWeapon = LoadOrCreate($"{TemplateRoot}/Weapon_Melee_Template.prefab", BuildMeleeWeapon),
                ThrowableProjectile = LoadOrCreate($"{TemplateRoot}/Projectile_ThrowableNetworked_Template.prefab", BuildThrowableProjectile),
                RespawnBeacon = LoadOrCreate($"{TemplateRoot}/Deployable_RespawnBeacon_Template.prefab", BuildRespawnBeacon),
                SimpleDeployable = LoadOrCreate($"{TemplateRoot}/Deployable_SimpleNetworked_Template.prefab", BuildSimpleDeployable),
                TrapDeployable = LoadOrCreate($"{TemplateRoot}/Deployable_TrapNetworked_Template.prefab", BuildTrapDeployable),
                VisionDeployable = LoadOrCreate($"{TemplateRoot}/Deployable_VisionWardNetworked_Template.prefab", BuildVisionDeployable),
                BeaconPreview = LoadOrCreate($"{TemplateRoot}/Deployable_RespawnBeaconPreview_Template.prefab", BuildBeaconPreview),
                WorldItem = LoadOrCreate($"{LootRoot}/Prefab_WorldItem.prefab", BuildWorldItemShell),
                WorldContainer = LoadOrCreate($"{LootRoot}/Prefab_WorldContainer.prefab", BuildWorldContainerShell),
            };
        }

        private static GameObject ResolveDeployableTemplate(DeployableDefinition deployable, Templates templates)
        {
            if (RequiresVisionWard(deployable.DeployableKind))
                return templates.VisionDeployable;

            if (RequiresTrapDeployable(deployable.DeployableKind))
                return templates.TrapDeployable;

            return templates.SimpleDeployable;
        }

        private static GameObject BuildVisual()
        {
            var root = new GameObject("Visual_Item_Template");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "[Visual]";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = new Vector3(0.12f, 0.12f, 0.28f);
            return root;
        }

        private static GameObject BuildHitscanWeapon()
        {
            var root = new GameObject("Weapon_Hitscan_Template");
            var weapon = root.AddComponent<HitscanWeapon>();
            BuildWeaponChildren(root, weapon, firePointZ: 0.45f);
            return root;
        }

        private static GameObject BuildProjectileWeapon()
        {
            var root = new GameObject("Weapon_Projectile_Template");
            var weapon = root.AddComponent<ProjectileWeapon>();
            BuildWeaponChildren(root, weapon, firePointZ: 0.75f);
            return root;
        }

        private static GameObject BuildMeleeWeapon()
        {
            var root = new GameObject("Weapon_Melee_Template");
            var weapon = root.AddComponent<MeleeWeapon>();
            BuildWeaponChildren(root, weapon, firePointZ: 0.55f);
            return root;
        }

        private static void BuildWeaponChildren(GameObject root, WeaponBase weapon, float firePointZ)
        {
            var model = new GameObject("[Model]");
            model.transform.SetParent(root.transform, false);

            var firePoint = new GameObject("[FirePoint]");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0f, firePointZ);

            var leftHand = new GameObject("[LeftHandIK]");
            leftHand.transform.SetParent(root.transform, false);
            leftHand.transform.localPosition = new Vector3(-0.12f, -0.05f, 0.2f);

            var so = new SerializedObject(weapon);
            so.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
            so.FindProperty("leftHandIKTarget").objectReferenceValue = leftHand.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildThrowableProjectile()
        {
            var root = new GameObject("Projectile_ThrowableNetworked_Template");
            root.AddComponent<NetworkObject>();
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<SphereCollider>().radius = 0.18f;
            root.AddComponent<ProjectileNetworked>();
            var projectileBase = root.AddComponent<ProjectileBase>();

            projectileBase.muzzleFlashChild = BuildChild(root, "[MuzzleFlash]", false);
            projectileBase.mainVisualChild = BuildChild(root, "[MainVisual]", true);
            projectileBase.detonationVFXChild = BuildChild(root, "[DetonationVFX]", false);
            projectileBase.lifetimeAfterImpact = 3f;
            projectileBase.hideTrailOnImpact = true;
            return root;
        }

        private static GameObject BuildRespawnBeacon()
        {
            var root = new GameObject("Deployable_RespawnBeacon_Template");
            root.AddComponent<NetworkObject>();
            root.AddComponent<CapsuleCollider>().height = 1.2f;
            root.AddComponent<RespawnBeacon>();
            BuildChild(root, "[Visual]", true);
            return root;
        }

        private static GameObject BuildSimpleDeployable()
        {
            var root = new GameObject("Deployable_SimpleNetworked_Template");
            root.AddComponent<NetworkObject>();
            root.AddComponent<CapsuleCollider>().height = 1.0f;
            root.AddComponent<SimpleDeployable>();
            BuildChild(root, "[Visual]", true);
            return root;
        }

        private static GameObject BuildTrapDeployable()
        {
            var root = new GameObject("Deployable_TrapNetworked_Template");
            root.AddComponent<NetworkObject>();
            root.AddComponent<SphereCollider>().radius = 0.45f;
            root.AddComponent<TrapDeployable>();
            BuildChild(root, "[Visual]", true);
            return root;
        }

        private static GameObject BuildVisionDeployable()
        {
            var root = new GameObject("Deployable_VisionWardNetworked_Template");
            root.AddComponent<NetworkObject>();
            root.AddComponent<CapsuleCollider>().height = 1.0f;
            root.AddComponent<VisionWard>();
            BuildChild(root, "[Visual]", true);
            return root;
        }

        private static GameObject BuildBeaconPreview()
        {
            var root = new GameObject("Deployable_RespawnBeaconPreview_Template");
            BuildChild(root, "[PreviewVisual]", true);
            return root;
        }

        private static GameObject BuildWorldItemShell()
        {
            var root = new GameObject("Prefab_WorldItem");
            root.AddComponent<NetworkObject>();
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.55f;
            collider.isTrigger = false;
            root.AddComponent<WorldItem>();
            return root;
        }

        private static GameObject BuildWorldContainerShell()
        {
            var root = new GameObject("Prefab_WorldContainer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<BoxCollider>().size = new Vector3(1.2f, 1.0f, 1.2f);
            root.AddComponent<WorldContainer>();
            return root;
        }

        private static GameObject BuildChild(GameObject parent, string name, bool active)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = name;
            Object.DestroyImmediate(child.GetComponent<Collider>());
            child.transform.SetParent(parent.transform, false);
            child.SetActive(active);
            return child;
        }

        private static GameObject LoadOrCreate(string path, System.Func<GameObject> builder)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/"));
            var sceneObject = builder();
            var prefab = PrefabUtility.SaveAsPrefabAsset(sceneObject, path);
            Object.DestroyImmediate(sceneObject);
            Debug.Log($"[ItemDefinitionAudit] Created template prefab: {path}");
            return prefab;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(TemplateRoot);
            EnsureFolder(LootRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void MoveFolderIfExists(string from, string to)
        {
            if (!AssetDatabase.IsValidFolder(from))
                return;

            EnsureFolder(System.IO.Path.GetDirectoryName(to)?.Replace("\\", "/"));
            if (AssetDatabase.IsValidFolder(to))
            {
                Debug.LogWarning($"[ItemDefinitionAudit] Skip move. Target already exists: {to}");
                return;
            }

            string error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error))
                Debug.Log($"[ItemDefinitionAudit] Moved legacy folder: {from} -> {to}");
            else
                Debug.LogWarning($"[ItemDefinitionAudit] Move failed: {from} -> {to}. {error}");
        }

        private static bool HasComponentInPrefab<T>(GameObject prefab) where T : Component
        {
            if (prefab == null)
                return false;

            return prefab.GetComponent<T>() != null || prefab.GetComponentInChildren<T>(true) != null;
        }

        private struct Templates
        {
            public GameObject Visual;
            public GameObject HitscanWeapon;
            public GameObject ProjectileWeapon;
            public GameObject MeleeWeapon;
            public GameObject ThrowableProjectile;
            public GameObject RespawnBeacon;
            public GameObject SimpleDeployable;
            public GameObject TrapDeployable;
            public GameObject VisionDeployable;
            public GameObject BeaconPreview;
            public GameObject WorldItem;
            public GameObject WorldContainer;
        }
    }
}
#endif
