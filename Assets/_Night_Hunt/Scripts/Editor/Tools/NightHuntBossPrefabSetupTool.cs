#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.FogOfWar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Generates production boss prefabs from reusable turret module prefabs.
    /// BossController stays an area/orchestrator. TurretGun prefabs own HP, weapon,
    /// fire points, projectile prefab, collider, and bullet target config.
    /// </summary>
    public static class NightHuntBossPrefabSetupTool
    {
        private const string TurretRoot = "Assets/_Night_Hunt/Prefabs/Boss/Turrets";
        private const string FullBossRoot = "Assets/_Night_Hunt/Prefabs/Boss/Full";
        private const string ModelRoot = "Assets/_Night_Hunt/Prefabs/Boss/Models";
        private const string ProjectileRoot = "Assets/_Night_Hunt/Prefabs/Items/Projectile";
        private const string RewardRoot = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/WorldSpawnConfigs/Boss";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Night_Hunt/Scenes/02_Map_01.unity",
            "Assets/_Night_Hunt/Scenes/02_Map_02.unity",
        };

        [MenuItem("NightHunt/Boss/Generate Full Boss Prefabs", priority = 220)]
        public static void GenerateOnly()
        {
            GeneratePrefabs(patchScenes: false);
        }

        [MenuItem("NightHunt/Boss/Generate Full Boss Prefabs And Patch Scenes", priority = 221)]
        public static void GenerateAndPatchScenes()
        {
            GeneratePrefabs(patchScenes: true);
        }

        [MenuItem("NightHunt/Boss/Repair FishNet Boss Prefab Registry", priority = 222)]
        public static void RepairFishNetBossPrefabRegistry()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(DefaultPrefabObjectsPath);
            if (asset == null)
            {
                Debug.LogWarning($"[NightHuntBossPrefabSetup] Missing {DefaultPrefabObjectsPath}; cannot repair registry.");
                return;
            }

            var so = new SerializedObject(asset);
            var prefabs = so.FindProperty("_prefabs");
            if (prefabs == null)
            {
                Debug.LogWarning("[NightHuntBossPrefabSetup] DefaultPrefabObjects has no _prefabs property.");
                return;
            }

            NormalizeNetworkPrefabRegistry(prefabs);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NightHuntBossPrefabSetup] Repaired FishNet boss prefab registry.");
        }

        private static void GeneratePrefabs(bool patchScenes)
        {
            EnsureFolder(TurretRoot);
            EnsureFolder(FullBossRoot);

            var lightTurret = CreateTurretPrefab(new TurretSpec
            {
                Name = "BossTurret_Light_Hitscan",
                Path = $"{TurretRoot}/BossTurret_Light_Hitscan.prefab",
                ModelPath = $"{ModelRoot}/Base_Turret_Lt_Lvl1.prefab",
                ProjectilePath = $"{ProjectileRoot}/Projectile_Bullet_Rifle.prefab",
                FireMode = BossTurretFireMode.Hitscan,
                MaxHp = 260f,
                Damage = 22f,
                Cooldown = 0.45f,
                ProjectileSpeed = 70f,
                TrackSpeed = 210f,
                IdleSweepSpeed = 24f,
                IdleSweepAngle = 50f,
                ColliderRadius = 0.8f,
                ColliderHeight = 2.0f,
                AcquireRadius = 1.1f,
                FirePoints = new[] { new Vector3(0f, 0.18f, 1.05f) },
            });

            var heavyTurret = CreateTurretPrefab(new TurretSpec
            {
                Name = "BossTurret_Heavy_Hitscan",
                Path = $"{TurretRoot}/BossTurret_Heavy_Hitscan.prefab",
                ModelPath = $"{ModelRoot}/Base_Turret_Lvl3.prefab",
                ProjectilePath = $"{ProjectileRoot}/Projectile_Bullet_Sniper.prefab",
                FireMode = BossTurretFireMode.Hitscan,
                MaxHp = 420f,
                Damage = 38f,
                Cooldown = 0.75f,
                ProjectileSpeed = 90f,
                TrackSpeed = 165f,
                IdleSweepSpeed = 18f,
                IdleSweepAngle = 42f,
                ColliderRadius = 1.0f,
                ColliderHeight = 2.4f,
                AcquireRadius = 1.35f,
                FirePoints = new[]
                {
                    new Vector3(-0.18f, 0.22f, 1.25f),
                    new Vector3(0.18f, 0.22f, 1.25f),
                },
                FirePointsPerVolley = 1,
            });

            var rocketTurret = CreateTurretPrefab(new TurretSpec
            {
                Name = "BossTurret_Rocket_Artillery",
                Path = $"{TurretRoot}/BossTurret_Rocket_Artillery.prefab",
                ModelPath = $"{ModelRoot}/Base_Turret_Lvl5.prefab",
                ProjectilePath = $"{ProjectileRoot}/Projectile_Rocket.prefab",
                FireMode = BossTurretFireMode.Projectile,
                MaxHp = 520f,
                Damage = 72f,
                Cooldown = 2.4f,
                ProjectileSpeed = 42f,
                GravityScale = 0f,
                PreferHighArc = false,
                TrackSpeed = 115f,
                IdleSweepSpeed = 14f,
                IdleSweepAngle = 38f,
                ColliderRadius = 1.15f,
                ColliderHeight = 2.7f,
                AcquireRadius = 1.5f,
                FirePoints = new[] { new Vector3(0f, 0.34f, 1.45f) },
            });

            var aegis = CreateBossPrefab(new BossSpec
            {
                Name = "Boss_AegisOutpost_T1",
                Path = $"{FullBossRoot}/Boss_AegisOutpost_T1.prefab",
                BossId = "boss_aegis_outpost_t1",
                CoreModelPath = $"{ModelRoot}/Base_Tower_Lvl1.prefab",
                AggroRadius = 28f,
                AttackRadius = 26f,
                BossKillScore = 450,
                RewardPath = $"{RewardRoot}/WorldSpawnConfig_BossDrop_Tier1_CommonLoot.asset",
                Turrets = new[]
                {
                    new TurretPlacement(lightTurret, "Turret_FrontLeft_Light", new Vector3(-2.0f, 0f, 1.3f), 25f),
                    new TurretPlacement(lightTurret, "Turret_FrontRight_Light", new Vector3(2.0f, 0f, 1.3f), -25f),
                },
            });

            var bastion = CreateBossPrefab(new BossSpec
            {
                Name = "Boss_BastionBattery_T2",
                Path = $"{FullBossRoot}/Boss_BastionBattery_T2.prefab",
                BossId = "boss_bastion_battery_t2",
                CoreModelPath = $"{ModelRoot}/Base_Tower_Lvl2.prefab",
                AggroRadius = 32f,
                AttackRadius = 30f,
                BossKillScore = 650,
                RewardPath = $"{RewardRoot}/WorldSpawnConfig_BossDrop_Tier2_Rifle.asset",
                Turrets = new[]
                {
                    new TurretPlacement(lightTurret, "Turret_Left_Light", new Vector3(-2.7f, 0f, 0.8f), 45f),
                    new TurretPlacement(heavyTurret, "Turret_Center_Heavy", new Vector3(0f, 0f, 1.7f), 0f),
                    new TurretPlacement(lightTurret, "Turret_Right_Light", new Vector3(2.7f, 0f, 0.8f), -45f),
                },
            });

            var citadel = CreateBossPrefab(new BossSpec
            {
                Name = "Boss_CitadelCore_T3",
                Path = $"{FullBossRoot}/Boss_CitadelCore_T3.prefab",
                BossId = "boss_citadel_core_t3",
                CoreModelPath = $"{ModelRoot}/Base_Tower_Lvl3.prefab",
                AggroRadius = 38f,
                AttackRadius = 34f,
                BossKillScore = 900,
                RewardPath = $"{RewardRoot}/WorldSpawnConfig_BossDrop_Tier3_Elite.asset",
                Turrets = new[]
                {
                    new TurretPlacement(heavyTurret, "Turret_North_Heavy", new Vector3(0f, 0f, 2.8f), 0f),
                    new TurretPlacement(lightTurret, "Turret_West_Light", new Vector3(-3.0f, 0f, 0f), 90f),
                    new TurretPlacement(lightTurret, "Turret_East_Light", new Vector3(3.0f, 0f, 0f), -90f),
                    new TurretPlacement(rocketTurret, "Turret_South_Rocket", new Vector3(0f, 0f, -2.4f), 180f),
                },
            });

            RegisterNetworkPrefabs(aegis, bastion, citadel);

            if (patchScenes)
                PatchDemoSceneBosses(aegis);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NightHuntBossPrefabSetup] Generated full boss prefabs and reusable turret prefabs.");
        }

        private static GameObject CreateTurretPrefab(TurretSpec spec)
        {
            var root = new GameObject(spec.Name);
            SetLayerRecursively(root, LayerMask.NameToLayer("Interactable"));

            root.AddComponent<FogTeamVisibilityBinder>();
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = spec.ColliderRadius;
            collider.height = spec.ColliderHeight;
            collider.center = new Vector3(0f, spec.ColliderHeight * 0.5f, 0f);
            collider.direction = 1;
            collider.isTrigger = false;

            var turret = root.AddComponent<TurretGun>();
            var head = new GameObject("TurretHead").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, spec.ColliderHeight * 0.62f, 0f);

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            if (modelPrefab != null)
            {
                var model = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                if (model != null)
                {
                    model.name = "[Model]";
                    model.transform.SetParent(head, false);
                    PrepareVisualModelInstance(model);
                }
            }
            else
            {
                Debug.LogWarning($"[NightHuntBossPrefabSetup] Missing turret model: {spec.ModelPath}");
            }

            var firePointTransforms = new Transform[Mathf.Max(1, spec.FirePoints.Length)];
            for (int i = 0; i < firePointTransforms.Length; i++)
            {
                var fp = new GameObject(i == 0 ? "FirePoint" : $"FirePoint_{i + 1}").transform;
                fp.SetParent(head, false);
                fp.localPosition = spec.FirePoints[Mathf.Min(i, spec.FirePoints.Length - 1)];
                firePointTransforms[i] = fp;
            }

            var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ProjectilePath);
            ConfigureTurret(turret, spec, head, firePointTransforms, projectilePrefab);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.Path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBossPrefab(BossSpec spec)
        {
            var root = new GameObject(spec.Name);
            SetLayerRecursively(root, LayerMask.NameToLayer("Interactable"));

            var networkObject = root.AddComponent<NetworkObject>();
            var boss = root.AddComponent<BossController>();
            root.AddComponent<FogTeamVisibilityBinder>();

            var core = new GameObject("BossCore_Visual");
            core.transform.SetParent(root.transform, false);
            var coreModel = AssetDatabase.LoadAssetAtPath<GameObject>(spec.CoreModelPath);
            if (coreModel != null)
            {
                var model = PrefabUtility.InstantiatePrefab(coreModel) as GameObject;
                if (model != null)
                {
                    model.name = "[CoreModel]";
                    model.transform.SetParent(core.transform, false);
                    PrepareVisualModelInstance(model);
                }
            }

            var turretRoot = new GameObject("Turrets");
            turretRoot.transform.SetParent(root.transform, false);

            var turretBehaviours = new List<TurretGun>();
            foreach (var placement in spec.Turrets)
            {
                if (placement.Prefab == null)
                    continue;

                var instance = PrefabUtility.InstantiatePrefab(placement.Prefab) as GameObject;
                if (instance == null)
                    continue;

                instance.name = placement.Name;
                instance.transform.SetParent(turretRoot.transform, false);
                instance.transform.localPosition = placement.LocalPosition;
                instance.transform.localRotation = Quaternion.Euler(0f, placement.LocalYawDegrees, 0f);
                SetLayerRecursively(instance, LayerMask.NameToLayer("Interactable"));

                var turret = instance.GetComponent<TurretGun>();
                if (turret != null)
                    turretBehaviours.Add(turret);
            }

            ConfigureBoss(boss, spec, turretBehaviours);
            WireNetworkBehaviours(networkObject, root.GetComponentsInChildren<NetworkBehaviour>(true));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.Path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureBoss(BossController boss, BossSpec spec, IReadOnlyList<TurretGun> turrets)
        {
            var so = new SerializedObject(boss);
            SetString(so, "_bossId", spec.BossId);
            SetFloat(so, "_aggroRadius", spec.AggroRadius);
            SetFloat(so, "_attackRadius", spec.AttackRadius);
            SetLayerMask(so, "_playerLayerMask", LayerMask.GetMask("Player"));
            SetLayerMask(so, "_obstacleLayerMask", LayerMask.GetMask("Default", "Wall", "Ground", "MapObstacle", "MapStatic"));
            SetFloat(so, "_scanInterval", 0.25f);
            SetObject(so, "_bossRewardConfig", AssetDatabase.LoadAssetAtPath<Object>(spec.RewardPath));
            SetInt(so, "_bossKillScore", spec.BossKillScore);
            SetInt(so, "_fogTeamId", 999);
            SetBool(so, "_fogAlwaysVisible", false);
            SetBool(so, "_showDebug", false);
            SetFloat(so, "_despawnDelay", 3f);

            var turretList = so.FindProperty("_turretGuns");
            if (turretList != null)
            {
                turretList.arraySize = turrets.Count;
                for (int i = 0; i < turrets.Count; i++)
                    turretList.GetArrayElementAtIndex(i).objectReferenceValue = turrets[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTurret(TurretGun turret, TurretSpec spec, Transform head, Transform[] firePoints, GameObject projectilePrefab)
        {
            var so = new SerializedObject(turret);
            SetObject(so, "_turretHead", head);
            SetObject(so, "_firePoint", firePoints[0]);

            var points = so.FindProperty("_firePoints");
            if (points != null)
            {
                points.arraySize = firePoints.Length;
                for (int i = 0; i < firePoints.Length; i++)
                    points.GetArrayElementAtIndex(i).objectReferenceValue = firePoints[i];
            }

            SetFloat(so, "_trackSpeed", spec.TrackSpeed);
            SetFloat(so, "_idleSweepSpeed", spec.IdleSweepSpeed);
            SetFloat(so, "_idleSweepAngle", spec.IdleSweepAngle);
            SetFloat(so, "_maxHp", spec.MaxHp);
            SetVector3(so, "_bulletAcquirePointOffset", new Vector3(0f, spec.ColliderHeight * 0.5f, 0f));
            SetFloat(so, "_bulletAcquireRadius", spec.AcquireRadius);
            SetInt(so, "_fireMode", (int)spec.FireMode);
            SetFloat(so, "_damage", spec.Damage);
            SetFloat(so, "_cooldown", spec.Cooldown);
            SetInt(so, "_firePointsPerVolley", Mathf.Max(1, spec.FirePointsPerVolley));
            SetObject(so, "_projectilePrefab", projectilePrefab);
            SetFloat(so, "_projectileSpeed", spec.ProjectileSpeed);
            SetFloat(so, "_gravityScale", spec.GravityScale);
            SetBool(so, "_preferHighArc", spec.PreferHighArc);
            SetInt(so, "_fogTeamId", 999);
            SetBool(so, "_fogAlwaysVisible", false);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireNetworkBehaviours(NetworkObject networkObject, NetworkBehaviour[] behaviours)
        {
            var no = new SerializedObject(networkObject);
            var list = no.FindProperty("NetworkBehaviours");
            if (list != null)
            {
                list.arraySize = behaviours.Length;
                for (int i = 0; i < behaviours.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];
            }
            no.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < behaviours.Length; i++)
            {
                var so = new SerializedObject(behaviours[i]);
                SetInt(so, "_componentIndexCache", i);
                SetObject(so, "_addedNetworkObject", networkObject);
                SetObject(so, "_networkObjectCache", networkObject);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RegisterNetworkPrefabs(params GameObject[] bossPrefabs)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(DefaultPrefabObjectsPath);
            if (asset == null)
            {
                Debug.LogWarning($"[NightHuntBossPrefabSetup] Missing {DefaultPrefabObjectsPath}; generated boss prefabs are not registered.");
                return;
            }

            var so = new SerializedObject(asset);
            var prefabs = so.FindProperty("_prefabs");
            if (prefabs == null)
            {
                Debug.LogWarning("[NightHuntBossPrefabSetup] DefaultPrefabObjects has no _prefabs property.");
                return;
            }

            foreach (var bossPrefab in bossPrefabs)
            {
                var nob = bossPrefab != null ? bossPrefab.GetComponent<NetworkObject>() : null;
                if (nob == null)
                    continue;

                EnsureAssetPathHash(nob);
                if (ContainsObject(prefabs, nob))
                    continue;

                prefabs.InsertArrayElementAtIndex(prefabs.arraySize);
                prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = nob;
            }

            NormalizeNetworkPrefabRegistry(prefabs);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void PrepareVisualModelInstance(GameObject model)
        {
            if (model == null)
                return;

            if (PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            StripNetworkComponents(model);
            DisableColliders(model);
            SetLayerRecursively(model, LayerMask.NameToLayer("Interactable"));
        }

        private static void StripNetworkComponents(GameObject root)
        {
            if (root == null)
                return;

            var behaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = behaviours.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(behaviours[i], true);

            var objects = root.GetComponentsInChildren<NetworkObject>(true);
            for (int i = objects.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(objects[i], true);
        }

        private static void NormalizeNetworkPrefabRegistry(SerializedProperty prefabs)
        {
            if (prefabs == null)
                return;

            int removed = 0;
            for (int i = prefabs.arraySize - 1; i >= 0; i--)
            {
                var element = prefabs.GetArrayElementAtIndex(i);
                var nob = element.objectReferenceValue as NetworkObject;
                if (nob == null)
                {
                    DeleteArrayElement(prefabs, i);
                    removed++;
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(nob);
                if (IsBossModulePrefabPath(path))
                {
                    DeleteArrayElement(prefabs, i);
                    removed++;
                    continue;
                }

                EnsureAssetPathHash(nob);
            }

            if (removed > 0)
                Debug.Log($"[NightHuntBossPrefabSetup] Removed {removed} invalid boss module prefab reference(s) from DefaultPrefabObjects.");
        }

        private static bool IsBossModulePrefabPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (path.StartsWith($"{TurretRoot}/") || path.StartsWith($"{ModelRoot}/"));
        }

        private static void DeleteArrayElement(SerializedProperty array, int index)
        {
            int before = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == before)
                array.DeleteArrayElementAtIndex(index);
        }

        private static void EnsureAssetPathHash(NetworkObject nob)
        {
            if (nob == null || nob.gameObject == null)
                return;

            string path = AssetDatabase.GetAssetPath(nob.gameObject);
            if (string.IsNullOrEmpty(path))
                return;

            ulong hash = Hashing.GetStableHashU64($"{path}{nob.gameObject.name}");
            if (nob.AssetPathHash == hash)
                return;

            nob.SetAssetPathHash(hash);
            EditorUtility.SetDirty(nob);
        }

        private static void PatchDemoSceneBosses(GameObject replacementBoss)
        {
            if (replacementBoss == null)
                return;

            var previousScene = SceneManager.GetActiveScene().path;

            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;

                foreach (var root in scene.GetRootGameObjects())
                {
                    var managers = root.GetComponentsInChildren<BossSpawnManager>(true);
                    foreach (var manager in managers)
                    {
                        var so = new SerializedObject(manager);
                        var spawns = so.FindProperty("_bossSpawns");
                        if (spawns == null)
                            continue;

                        for (int i = 0; i < spawns.arraySize; i++)
                        {
                            var entry = spawns.GetArrayElementAtIndex(i);
                            var prefab = entry.FindPropertyRelative("BossPrefab");
                            var current = prefab?.objectReferenceValue as GameObject;
                            if (prefab == null || current == null || current.name != "Boss Prefab")
                                continue;

                            prefab.objectReferenceValue = replacementBoss;
                            changed = true;
                        }

                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[NightHuntBossPrefabSetup] Patched scene boss prefab: {path} -> {replacementBoss.name}");
                }
            }

            if (!string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }

        private static bool ContainsObject(SerializedProperty array, Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
                    return true;

            return false;
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0)
                return;

            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
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

        private static void SetObject(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject so, string name, string value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.stringValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.boolValue = value;
        }

        private static void SetVector3(SerializedObject so, string name, Vector3 value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.vector3Value = value;
        }

        private static void SetLayerMask(SerializedObject so, string name, LayerMask value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.intValue = value.value;
        }

        private struct TurretSpec
        {
            public string Name;
            public string Path;
            public string ModelPath;
            public string ProjectilePath;
            public BossTurretFireMode FireMode;
            public float MaxHp;
            public float Damage;
            public float Cooldown;
            public float ProjectileSpeed;
            public float GravityScale;
            public bool PreferHighArc;
            public float TrackSpeed;
            public float IdleSweepSpeed;
            public float IdleSweepAngle;
            public float ColliderRadius;
            public float ColliderHeight;
            public float AcquireRadius;
            public Vector3[] FirePoints;
            public int FirePointsPerVolley;
        }

        private struct BossSpec
        {
            public string Name;
            public string Path;
            public string BossId;
            public string CoreModelPath;
            public float AggroRadius;
            public float AttackRadius;
            public int BossKillScore;
            public string RewardPath;
            public TurretPlacement[] Turrets;
        }

        private readonly struct TurretPlacement
        {
            public readonly GameObject Prefab;
            public readonly string Name;
            public readonly Vector3 LocalPosition;
            public readonly float LocalYawDegrees;

            public TurretPlacement(GameObject prefab, string name, Vector3 localPosition, float localYawDegrees)
            {
                Prefab = prefab;
                Name = name;
                LocalPosition = localPosition;
                LocalYawDegrees = localYawDegrees;
            }
        }
    }
}
#endif
