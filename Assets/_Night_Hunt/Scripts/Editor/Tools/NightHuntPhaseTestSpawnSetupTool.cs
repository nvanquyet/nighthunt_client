using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NightHunt.Gameplay.Spawn;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Batch setup for the item/loot phase-test layout in Map01.
    /// </summary>
    public static class NightHuntPhaseTestSpawnSetupTool
    {
        private const string ScenePath = "Assets/_Night_Hunt/Scenes/02_Map_01.unity";
        private const string PhaseTestWorldConfigPath =
            "Assets/_Night_Hunt/Data/Resources/Database/Spawn/WorldSpawnConfigs/Items/WorldSpawnConfig_PhaseTest_AllItems.asset";
        private const string ReportPath =
            "Assets/_Night_Hunt/Data/Resources/Database/Spawn/PHASE_TEST_SPAWN_OVERVIEW.md";
        private const string RunRequestPath =
            "Assets/_Night_Hunt/EditorRunRequests/ApplyPhaseTestMap01.request";

        private static readonly (string name, Vector3 position, string configPath)[] LootSpawns =
        {
            ("PhaseLoot_AllItems_Main", new Vector3(-13.5f, 0.5f, 4.2f), PhaseTestWorldConfigPath),
            ("PhaseLoot_AllItems_North", new Vector3(-89.5f, 0.5f, 19f), PhaseTestWorldConfigPath),
            ("PhaseLoot_AllItems_South", new Vector3(-40.9f, 0.5f, -178.1f), PhaseTestWorldConfigPath),
            ("PhaseLoot_AllItems_West", new Vector3(-192.9f, 0.5f, -58.1f), PhaseTestWorldConfigPath),
        };

        private static readonly (string name, Vector3 position, int teamId)[] PlayerSpawns =
        {
            ("PhasePlayer_Team0_A", new Vector3(-24f, 0.5f, 12f), 0),
            ("PhasePlayer_Team0_B", new Vector3(-36f, 0.5f, 8f), 0),
            ("PhasePlayer_Team1_A", new Vector3(-168f, 0.5f, -46f), 1),
            ("PhasePlayer_Team1_B", new Vector3(-180f, 0.5f, -54f), 1),
            ("PhasePlayer_Neutral_A", new Vector3(-96f, 0.5f, -82f), -1),
        };

        [MenuItem("NightHunt/Tools/Phase Test/Apply Map01 Spawn Layout")]
        public static void ApplyPhaseTestMap01()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new FileNotFoundException($"Could not open scene: {ScenePath}");

            var lootRoot = GetOrCreateRoot("PhaseTest_LootSpawns");
            foreach (var spec in LootSpawns)
                CreateOrUpdateLootSpawn(lootRoot.transform, spec.name, spec.position, spec.configPath);

            var playerRoot = GetOrCreateRoot("PhaseTest_PlayerSpawns");
            var createdPlayerSpawns = new List<SpawnPoint>();
            foreach (var spec in PlayerSpawns)
                createdPlayerSpawns.Add(CreateOrUpdatePlayerSpawn(playerRoot.transform, spec.name, spec.position, spec.teamId));

            EnsureSpawnSystemReferences(createdPlayerSpawns);
            WriteOverviewReport();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PhaseTestSpawnSetup] Applied Map01 phase-test spawn layout and wrote overview report.");
        }

        [MenuItem("NightHunt/Tools/Phase Test/Request Apply Map01 Spawn Layout")]
        public static void RequestApplyPhaseTestMap01()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RunRequestPath));
            File.WriteAllText(RunRequestPath, System.DateTime.Now.ToString("O"));
            AssetDatabase.Refresh();
            Debug.Log($"[PhaseTestSpawnSetup] Request created: {RunRequestPath}");
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

                ApplyPhaseTestMap01();
            };
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;

            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            return root;
        }

        private static void CreateOrUpdateLootSpawn(Transform parent, string name, Vector3 position, string configPath)
        {
            var go = FindChild(parent, name) ?? new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;

            var point = go.GetComponent<WorldItemSpawnPoint>() ?? go.AddComponent<WorldItemSpawnPoint>();
            var config = AssetDatabase.LoadAssetAtPath<WorldSpawnConfig>(configPath);
            if (config == null)
                throw new FileNotFoundException($"Missing WorldSpawnConfig: {configPath}");

            var so = new SerializedObject(point);
            so.FindProperty("spawnConfig").objectReferenceValue = config;
            so.FindProperty("gizmoColor").colorValue = new Color(0.15f, 0.9f, 1f, 0.9f);
            so.FindProperty("showLabel").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SpawnPoint CreateOrUpdatePlayerSpawn(Transform parent, string name, Vector3 position, int teamId)
        {
            var go = FindChild(parent, name) ?? new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;

            var spawnPoint = go.GetComponent<SpawnPoint>() ?? go.AddComponent<SpawnPoint>();
            var so = new SerializedObject(spawnPoint);
            so.FindProperty("_teamId").intValue = teamId;
            so.FindProperty("_spawnRadius").floatValue = 2.5f;
            so.FindProperty("_randomizeRotation").boolValue = false;
            so.FindProperty("_gizmoSize").floatValue = 1f;
            so.FindProperty("_showTeamLabel").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawnPoint;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child.gameObject;
            }

            return null;
        }

        private static void EnsureSpawnSystemReferences(IReadOnlyCollection<SpawnPoint> phaseSpawns)
        {
            var spawnSystem = Object.FindFirstObjectByType<SpawnSystem>(FindObjectsInactive.Include);
            if (spawnSystem == null)
            {
                Debug.LogWarning("[PhaseTestSpawnSetup] SpawnSystem not found; player spawn points were created but not linked.");
                return;
            }

            var so = new SerializedObject(spawnSystem);
            var list = so.FindProperty("_spawnPoints");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[PhaseTestSpawnSetup] SpawnSystem._spawnPoints not found.");
                return;
            }

            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
                existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

            foreach (var spawnPoint in phaseSpawns)
            {
                if (spawnPoint == null || existing.Contains(spawnPoint))
                    continue;

                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = spawnPoint;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteOverviewReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Phase Test Spawn Overview");
            sb.AppendLine();
            sb.AppendLine("## Primary test table");
            sb.AppendLine("- `SpawnTable_PhaseTest_AllItems`: fixed table containing weapons, melee, throwables, deployables, consumables, and equipment for full-system loot testing.");
            sb.AppendLine("- `WorldSpawnConfig_PhaseTest_AllItems`: item scatter config using that table, `MaxActive=64`, `ScatterRadius=8`, `RespawnTime=30`.");
            sb.AppendLine();
            sb.AppendLine("## Map01 phase-test loot spawns");
            foreach (var spec in LootSpawns)
                sb.AppendLine($"- `{spec.name}` at `{spec.position}` -> `WorldSpawnConfig_PhaseTest_AllItems`");

            sb.AppendLine();
            sb.AppendLine("## Map01 phase-test player spawns");
            foreach (var spec in PlayerSpawns)
                sb.AppendLine($"- `{spec.name}` team `{spec.teamId}` at `{spec.position}`");

            sb.AppendLine();
            sb.AppendLine("## Existing spawn table groups");
            sb.AppendLine("- `Items/Ground_*`: single item or small ground scatter presets.");
            sb.AppendLine("- `Items/Crate_*`, `Items/Chest_*`: container/chest loot presets.");
            sb.AppendLine("- `Items/Cluster_*`: focused mixed clusters for weapon/consumable/attachment tests.");
            sb.AppendLine("- `Boss/*`, `Zone_*`, `SupplyDrop_*`: reward/drop tables for boss, zone, and event flows.");

            File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
