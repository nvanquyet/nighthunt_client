using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightHunt.GameplaySystems.World;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// World Spawn Scene Setup Tool
    ///
    /// Menu: NightHunt / Tools / Scene Spawn Setup
    ///
    /// Scans the currently open scene for WorldItemSpawnPoint components and lets you:
    ///   • See every spawn point and its current SpawnConfig assignment
    ///   • Assign configs via drag-and-drop or quick presets
    ///   • Save scene changes after assignment
    ///
    /// Quick-Preset "Map01 Test Layout" assigns:
    ///   LootSpawn_1  → Ground_Rifle_Random       (weapon scatter)
    ///   LootSpawn_2  → Crate_Medical              (medical container)
    ///   LootSpawn_3  → Ground_Medkit_Common       (consumable ground)
    ///   LootSpawn_4  → Crate_Weapons_Light        (weapon crate)
    ///   LootSpawn_5  → Chest_Basic                (lootable chest)
    ///   LootSpawn_6  → Ground_Attachment_Any      (attachment scatter)
    ///   LootSpawn_7  → BossDrop_Tier2_FullKit     (boss drop container)
    ///   LootSpawn_8  → Zone_Phase2_Reward         (zone reward container)
    ///   (Any extra spawn points → Crate_General_Common)
    /// </summary>
    public class WorldSpawnSceneSetupTool : EditorWindow
    {
        // ── Canonical config locations ─────────────────────────────────────
        private const string SPAWN_ITEMS = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/WorldSpawnConfigs/Items";
        private const string SPAWN_BOSS  = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/WorldSpawnConfigs/Boss";

        // ── Map01 preset — spawn name → config asset name ──────────────────
        private static readonly (string spawnName, string configName, string folder)[] Map01Preset = new[]
        {
            ("LootSpawn_1", "WorldSpawnConfig_Ground_Rifle_Random",    SPAWN_ITEMS),
            ("LootSpawn_2", "WorldSpawnConfig_Crate_Medical",          SPAWN_ITEMS),
            ("LootSpawn_3", "WorldSpawnConfig_Ground_Medkit_Common",   SPAWN_ITEMS),
            ("LootSpawn_4", "WorldSpawnConfig_Crate_Weapons_Light",    SPAWN_ITEMS),
            ("LootSpawn_5", "WorldSpawnConfig_Chest_Basic",            SPAWN_ITEMS),
            ("LootSpawn_6", "WorldSpawnConfig_Ground_Attachment_Any",  SPAWN_ITEMS),
            ("LootSpawn_7", "WorldSpawnConfig_BossDrop_Tier2_FullKit", SPAWN_BOSS),
            ("LootSpawn_8", "WorldSpawnConfig_Zone_Phase2_Reward",     SPAWN_BOSS),
        };

        // ── State ──────────────────────────────────────────────────────────
        private List<WorldItemSpawnPoint> _points = new List<WorldItemSpawnPoint>();
        private Vector2 _scroll;
        private readonly List<string> _log = new List<string>();
        private Vector2 _logScroll;

        // ── All known configs grouped ──────────────────────────────────────
        private WorldSpawnConfig[] _allItemConfigs;
        private WorldSpawnConfig[] _allBossConfigs;
        private string[] _itemConfigLabels;
        private string[] _bossConfigLabels;

        [MenuItem("NightHunt/Tools/Scene Spawn Setup")]
        public static void Open()
        {
            var w = GetWindow<WorldSpawnSceneSetupTool>("Scene Spawn Setup");
            w.minSize = new Vector2(500, 550);
            w.RefreshAll();
        }

        private void OnFocus() => RefreshAll();

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void RefreshAll()
        {
            ScanScene();
            LoadAllConfigs();
            Repaint();
        }

        private void ScanScene()
        {
            // Works with the currently loaded scene(s)
            _points.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    _points.AddRange(root.GetComponentsInChildren<WorldItemSpawnPoint>(true));
                }
            }
            _points = _points.OrderBy(p => p.gameObject.name).ToList();
        }

        private void LoadAllConfigs()
        {
            _allItemConfigs = LoadConfigsFromFolder(SPAWN_ITEMS);
            _allBossConfigs = LoadConfigsFromFolder(SPAWN_BOSS);
            _itemConfigLabels = BuildLabels(_allItemConfigs);
            _bossConfigLabels = BuildLabels(_allBossConfigs);
        }

        private static WorldSpawnConfig[] LoadConfigsFromFolder(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldSpawnConfig", new[] { folder });
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<WorldSpawnConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null)
                .OrderBy(c => c.name)
                .ToArray();
        }

        private static string[] BuildLabels(WorldSpawnConfig[] configs)
        {
            var labels = new string[configs.Length + 1];
            labels[0] = "— None —";
            for (int i = 0; i < configs.Length; i++)
                labels[i + 1] = configs[i].name;
            return labels;
        }

        // ── GUI ────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.LabelField("World Spawn Scene Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Found {_points.Count} WorldItemSpawnPoint(s) in the open scene.\n" +
                "Use the quick preset or drag-drop configs to each spawn point.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh scene scan", GUILayout.Height(28))) RefreshAll();
            var presetStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Apply Map01 Test Preset", presetStyle, GUILayout.Height(28)))
                ApplyMap01Preset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (_points.Count == 0)
            {
                EditorGUILayout.HelpBox("No WorldItemSpawnPoint found. Make sure scene 02_Map_01 is open.", MessageType.Warning);
            }
            else
            {
                DrawSpawnPointTable();
            }

            EditorGUILayout.Space(4);
            DrawLog();
        }

        private void DrawSpawnPointTable()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(320));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Spawn Point", EditorStyles.toolbarButton, GUILayout.Width(130));
            EditorGUILayout.LabelField("Type", EditorStyles.toolbarButton, GUILayout.Width(60));
            EditorGUILayout.LabelField("Current Config", EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Assign", EditorStyles.toolbarButton, GUILayout.Width(26));
            EditorGUILayout.EndHorizontal();

            foreach (var pt in _points)
            {
                if (pt == null) continue;

                var so = new SerializedObject(pt);
                so.Update();
                var cfgProp = so.FindProperty("spawnConfig");
                var currentCfg = cfgProp.objectReferenceValue as WorldSpawnConfig;

                EditorGUILayout.BeginHorizontal(GUILayout.Height(22));

                // Name — click to ping
                if (GUILayout.Button(pt.gameObject.name,
                    EditorStyles.miniLabel,
                    GUILayout.Width(130)))
                {
                    EditorGUIUtility.PingObject(pt.gameObject);
                    Selection.activeGameObject = pt.gameObject;
                }

                // Type badge
                string typeLabel = currentCfg != null
                    ? (currentCfg.SpawnType == WorldSpawnType.Item ? "Item" : "Crate")
                    : "?";
                var typeColor = currentCfg == null ? Color.gray
                    : currentCfg.SpawnType == WorldSpawnType.Item ? new Color(0.4f, 0.8f, 0.4f)
                    : new Color(0.8f, 0.6f, 0.3f);

                var prevColor = GUI.color;
                GUI.color = typeColor;
                GUILayout.Label(typeLabel, EditorStyles.miniLabel, GUILayout.Width(60));
                GUI.color = prevColor;

                // Config object field
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(cfgProp, GUIContent.none, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(pt.gameObject.scene);
                    Log($"Assigned {cfgProp.objectReferenceValue?.name ?? "null"} → {pt.gameObject.name}");
                }

                // Select in Inspector button
                if (GUILayout.Button("▶", GUILayout.Width(26)))
                    Selection.activeGameObject = pt.gameObject;

                EditorGUILayout.EndHorizontal();
                so.ApplyModifiedProperties();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Force Save Scene", GUILayout.Height(26)))
                SaveScene();
        }

        private void DrawLog()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(50))) _log.Clear();
            EditorGUILayout.EndHorizontal();

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(100));
            foreach (var line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ── Preset application ─────────────────────────────────────────────

        private void ApplyMap01Preset()
        {
            if (_points.Count == 0)
            {
                Log("⚠ No spawn points in scene. Open 02_Map_01 first.");
                return;
            }

            Log("── Applying Map01 Test Preset ──");
            int applied = 0;

            // Build lookup by name (case-insensitive)
            var byName = _points.ToDictionary(p => p.gameObject.name, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in Map01Preset)
            {
                if (!byName.TryGetValue(entry.spawnName, out var pt))
                {
                    Log($"  ⚠ Not found: {entry.spawnName}");
                    continue;
                }
                var cfg = AssetDatabase.LoadAssetAtPath<WorldSpawnConfig>($"{entry.folder}/{entry.configName}.asset");
                if (cfg == null)
                {
                    Log($"  ⚠ Config not found: {entry.configName}  (run Bulk Spawn Generator first)");
                    continue;
                }
                AssignConfig(pt, cfg);
                applied++;
            }

            // Fallback: assign a generic config to any extra spawn points not in the preset
            var presetNames = new HashSet<string>(Map01Preset.Select(e => e.spawnName), StringComparer.OrdinalIgnoreCase);
            var fallbackCfg = AssetDatabase.LoadAssetAtPath<WorldSpawnConfig>(SPAWN_ITEMS + "/WorldSpawnConfig_Crate_General_Common.asset");

            foreach (var pt in _points.Where(p => !presetNames.Contains(p.gameObject.name)))
            {
                if (fallbackCfg != null)
                    AssignConfig(pt, fallbackCfg);
                Log($"  Fallback → {pt.gameObject.name}: Crate_General_Common");
            }

            SaveScene();
            Log($"✅ Preset applied — {applied} of {Map01Preset.Length} named points, scene saved.");
            Repaint();
        }

        private void AssignConfig(WorldItemSpawnPoint pt, WorldSpawnConfig cfg)
        {
            var so = new SerializedObject(pt);
            so.Update();
            var prop = so.FindProperty("spawnConfig");
            if (prop == null)
            {
                Log($"  ⚠ Could not find 'spawnConfig' field on {pt.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = cfg;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(pt.gameObject.scene);
            Log($"  ✔ {pt.gameObject.name} → {cfg.name}");
        }

        private void SaveScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
        }

        private void Log(string msg)
        {
            _log.Add($"[{DateTime.Now:HH:mm:ss}]  {msg}");
            Debug.Log($"[SceneSpawnSetup] {msg}");
            Repaint();
        }
    }
}
