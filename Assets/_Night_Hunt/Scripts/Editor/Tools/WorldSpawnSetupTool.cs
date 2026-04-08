using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NightHunt.GameplaySystems.World;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// World Spawn Setup Tool
    ///
    /// Menu: NightHunt / Tools / World Spawn Setup Tool
    ///
    /// Bulk-generates ScriptableObject presets for setting up world-spawn points:
    ///
    ///   LootableConfig presets  — interaction behaviour for lootable objects
    ///     LootableConfig_Instant  — single E-press, auto-loot enabled
    ///     LootableConfig_Hold     — hold E for 1.5 s, auto-loot disabled
    ///
    ///   SpawnTable presets      — item pools used by WorldSpawnConfig
    ///     SpawnTable_Default        — empty FixedPlusRandom table (fill items in Inspector)
    ///     SpawnTable_Container      — dedicated table for Container preset
    ///     SpawnTable_ItemScatter    — dedicated table for ItemScatter preset
    ///     SpawnTable_LockedChest    — dedicated table for LockedChest preset
    ///
    ///   WorldSpawnConfig presets — full spawn-point configuration
    ///     WorldSpawnConfig_Container    — respawning crate (Hold, 120 s)
    ///     WorldSpawnConfig_ItemScatter  — ground items scattered in 2 m radius (60 s, max 3 active)
    ///     WorldSpawnConfig_LockedChest  — one-time locked chest (SpawnLocked, no respawn)
    ///
    /// All assets are created in:  Assets/_Night_Hunt/Data/Resources/Database/Spawn/
    /// Existing assets with the same name are skipped (never overwritten).
    ///
    /// HOW TO USE:
    ///   1. Open via NightHunt → Tools → World Spawn Setup Tool
    ///   2. Click "Create All Presets" or individual section buttons
    ///   3. Assign the generated SpawnTable assets (fill FixedEntries / RandomEntries in Inspector)
    ///   4. Drag the WorldSpawnConfig asset onto a WorldSpawnPoint component in the scene
    /// </summary>
    public class WorldSpawnSetupTool : EditorWindow
    {
        // ──────────────────────────────────────────────────────────────────
        //  Constants
        // ──────────────────────────────────────────────────────────────────

        // Template presets go into the canonical LootableConfigs / WorldSpawnConfigs/Items subdirectories
        private const string OUTPUT_PATH    = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/WorldSpawnConfigs/Items";
        private const string LOOTABLE_PATH  = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/LootableConfigs";
        private const string TABLE_PATH     = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/SpawnTables/Items";

        // ──────────────────────────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────────────────────────

        private Vector2 _scroll;
        private Vector2 _logScroll;
        private readonly List<string> _log = new List<string>();

        // ──────────────────────────────────────────────────────────────────
        //  Item-map lookup (table name lower → fixed items[], random items[])
        //  Keys are asset file names lower-cased without extension.
        //  Derived directly from NightHuntDataMigrationTool Build/BuildBoss calls.
        // ──────────────────────────────────────────────────────────────────

        // Shorthand aliases used only inside this dictionary initialiser.
        private const string AK47      = "weapon_ak47";
        private const string M4        = "weapon_m4";
        private const string PISTOL    = "weapon_pistol";
        private const string MEDKIT    = "consumable_medkit";
        private const string ENERGY    = "consumable_energydrink";
        private const string BEACON    = "beacondefinition";
        private const string HELMET    = "armor_helmet";
        private const string VEST      = "armor_vest";
        private const string GLOVES    = "armor_gloves";
        private const string BELT      = "armor_belt";
        private const string BACKPACK  = "armor_backpack";
        private const string FRAG      = "throwable_fraggrenade";
        private const string SMOKE     = "throwable_smokegrenade";
        private const string EXTMAG    = "attachment_extmag";
        private const string FLASH     = "attachment_flashlight";
        private const string GRIP      = "attachment_grip";
        private const string POUCH     = "attachment_pouch";
        private const string REDDOT    = "attachment_reddot";
        private const string SUPPRESS  = "attachment_suppressor";

        // Each entry: (fixedItems[], randomItems[])  — null means that pool is empty.
        private static readonly Dictionary<string, (string[] F, string[] R)> s_itemMap =
            new Dictionary<string, (string[], string[])>(System.StringComparer.OrdinalIgnoreCase)
        {
            // ── Step 4: Ground scatter ─────────────────────────────────────────
            { "spawntable_ground_medkit_common",       (null,             new[]{ MEDKIT                               }) },
            { "spawntable_ground_energydrink_common",  (null,             new[]{ ENERGY                               }) },
            { "spawntable_ground_medkit_rare",         (new[]{ MEDKIT },  null                                        ) },
            { "spawntable_ground_beacon",               (null,             new[]{ BEACON                               }) },
            { "spawntable_ground_pistol",               (null,             new[]{ PISTOL                               }) },
            { "spawntable_ground_rifle_ak47",           (null,             new[]{ AK47                                 }) },
            { "spawntable_ground_rifle_m4",             (null,             new[]{ M4                                   }) },
            { "spawntable_ground_rifle_random",         (null,             new[]{ AK47, M4                             }) },
            { "spawntable_ground_attachment_optic",     (null,             new[]{ REDDOT, SUPPRESS                     }) },
            { "spawntable_ground_attachment_support",   (null,             new[]{ GRIP, EXTMAG, FLASH                  }) },
            { "spawntable_ground_attachment_any",       (null,             new[]{ EXTMAG, FLASH, GRIP, POUCH, REDDOT, SUPPRESS }) },
            { "spawntable_ground_helmet",               (null,             new[]{ HELMET                               }) },
            { "spawntable_ground_vest",                 (null,             new[]{ VEST                                 }) },
            { "spawntable_ground_gloves",               (null,             new[]{ GLOVES                               }) },
            { "spawntable_ground_belt",                 (null,             new[]{ BELT                                 }) },
            { "spawntable_ground_backpack",             (null,             new[]{ BACKPACK                             }) },
            { "spawntable_ground_fraggrenade",          (null,             new[]{ FRAG                                 }) },
            { "spawntable_ground_smokegrenade",         (null,             new[]{ SMOKE                                }) },
            { "spawntable_ground_throwable_mixed",      (null,             new[]{ FRAG, SMOKE                          }) },
            // ── Step 4: Containers ─────────────────────────────────────────────
            { "spawntable_crate_medical",               (new[]{ MEDKIT },                   new[]{ ENERGY, MEDKIT              }) },
            { "spawntable_crate_weapons_light",         (new[]{ PISTOL },                   new[]{ REDDOT, EXTMAG, SUPPRESS    }) },
            { "spawntable_crate_weapons_heavy",         (new[]{ AK47, M4 },                 new[]{ GRIP, SUPPRESS, REDDOT      }) },
            { "spawntable_crate_equipment_basic",       (new[]{ HELMET, VEST },             new[]{ GLOVES, BELT                }) },
            { "spawntable_crate_equipment_full",        (new[]{ HELMET, VEST, BACKPACK },   new[]{ GLOVES, BELT                }) },
            { "spawntable_crate_utility",               (new[]{ FRAG, SMOKE },              new[]{ FRAG, SMOKE                 }) },
            { "spawntable_crate_general_common",        (null,                              new[]{ MEDKIT, ENERGY, PISTOL, HELMET, FRAG, REDDOT     }) },
            { "spawntable_crate_general_rare",          (null,                              new[]{ AK47, M4, HELMET, VEST, BACKPACK, SUPPRESS       }) },
            { "spawntable_chest_basic",                 (null,                              new[]{ MEDKIT, ENERGY, PISTOL, GLOVES, FRAG, EXTMAG     }) },
            { "spawntable_chest_military",              (new[]{ AK47 },                     new[]{ GRIP, REDDOT, SUPPRESS, EXTMAG, MEDKIT           }) },
            { "spawntable_chest_locked_basic",          (new[]{ AK47, M4, HELMET },         new[]{ VEST, GRIP, REDDOT                               }) },
            { "spawntable_chest_locked_elite",          (new[]{ M4, HELMET, VEST, BACKPACK }, new[]{ GRIP, REDDOT, SUPPRESS, EXTMAG, MEDKIT, GLOVES }) },
            // ── Step 4: Clusters ───────────────────────────────────────────────
            { "spawntable_cluster_attachment_mixed",    (null, new[]{ EXTMAG, GRIP, FLASH, POUCH, REDDOT, SUPPRESS }) },
            { "spawntable_cluster_consumable_mixed",    (null, new[]{ MEDKIT, ENERGY                               }) },
            { "spawntable_cluster_weapon_scattered",    (null, new[]{ PISTOL, AK47, M4                             }) },
            // ── Step 5: Boss tier drops ────────────────────────────────────────
            { "spawntable_bossdrop_tier1_commonloot",   (new[]{ MEDKIT, ENERGY },           new[]{ PISTOL, REDDOT, EXTMAG                          }) },
            { "spawntable_bossdrop_tier1_medical",      (new[]{ MEDKIT, ENERGY },           null                                                    ) },
            { "spawntable_bossdrop_tier2_rifle",        (new[]{ AK47, MEDKIT },             new[]{ GRIP, REDDOT, SUPPRESS                          }) },
            { "spawntable_bossdrop_tier2_armor",        (new[]{ HELMET, VEST },             new[]{ GLOVES, BACKPACK, BELT                          }) },
            { "spawntable_bossdrop_tier2_fullkit",      (new[]{ AK47, HELMET, VEST },       new[]{ GRIP, BACKPACK, MEDKIT                          }) },
            { "spawntable_bossdrop_tier3_elite",        (new[]{ M4, HELMET, VEST, BACKPACK }, new[]{ GRIP, REDDOT, SUPPRESS, EXTMAG, GLOVES, BELT  }) },
            { "spawntable_bossdrop_tier3_allweapons",   (new[]{ AK47, M4, PISTOL },         new[]{ GRIP, REDDOT, SUPPRESS, EXTMAG, FLASH, POUCH    }) },
            // ── Step 5: Zone rewards ───────────────────────────────────────────
            { "spawntable_zone_phase1_reward",          (new[]{ MEDKIT },                   new[]{ REDDOT, ENERGY, HELMET                          }) },
            { "spawntable_zone_phase2_reward",          (new[]{ MEDKIT, VEST },             new[]{ AK47, M4, GRIP, BACKPACK                        }) },
            { "spawntable_zone_phase3_reward",          (new[]{ M4, MEDKIT, HELMET, VEST }, new[]{ GRIP, REDDOT, SUPPRESS, EXTMAG, BACKPACK, GLOVES }) },
            { "spawntable_zone_capture_standard",       (new[]{ HELMET, VEST },             new[]{ ENERGY, MEDKIT                                  }) },
            { "spawntable_zone_capture_elite",          (new[]{ HELMET, VEST, BACKPACK, GLOVES }, new[]{ AK47, M4, GRIP, REDDOT                   }) },
            { "spawntable_zone_clear_beacon",           (new[]{ BEACON, MEDKIT },           new[]{ ENERGY                                          }) },
            { "spawntable_zone_beaconguardian_drop",    (new[]{ MEDKIT, ENERGY },           new[]{ PISTOL, AK47, HELMET, VEST                      }) },
            // ── Step 5: Supply drops ───────────────────────────────────────────
            { "spawntable_supplydrop_common",           (new[]{ MEDKIT, ENERGY },           new[]{ PISTOL, AK47, HELMET, VEST                      }) },
            { "spawntable_supplydrop_rare",             (new[]{ M4, MEDKIT },               new[]{ GRIP, REDDOT, SUPPRESS, HELMET, VEST, BACKPACK   }) },
            // ── Step 5: Hidden caches ──────────────────────────────────────────
            { "spawntable_cache_hidden_a",              (new[]{ EXTMAG, GRIP, REDDOT, SUPPRESS }, new[]{ MEDKIT, ENERGY                           }) },
            { "spawntable_cache_hidden_b",              (new[]{ AK47, MEDKIT },             new[]{ HELMET, VEST, BACKPACK, GRIP                    }) },
            // ── Step 5: Special / Event ────────────────────────────────────────
            { "spawntable_event_special_crate",         (new[]{ MEDKIT, BEACON },           new[]{ AK47, M4, HELMET, VEST, BACKPACK, GRIP, REDDOT, SUPPRESS }) },
            { "spawntable_finalzone_jackpot",           (new[]{ M4, AK47, PISTOL, HELMET, VEST, BACKPACK, GLOVES, BELT, MEDKIT, EXTMAG, GRIP, REDDOT, SUPPRESS }, null) },
        };

        // ──────────────────────────────────────────────────────────────────
        //  Editor window lifecycle
        // ──────────────────────────────────────────────────────────────────

        [MenuItem("NightHunt/Tools/World Spawn Setup Tool")]
        public static void Open()
        {
            var win = GetWindow<WorldSpawnSetupTool>("World Spawn Setup");
            win.minSize = new Vector2(360, 520);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(4);
            DrawCreateAllButton();
            EditorGUILayout.Space(8);
            DrawLootableConfigSection();
            EditorGUILayout.Space(8);
            DrawSpawnTableSection();
            EditorGUILayout.Space(8);
            DrawWorldSpawnConfigSection();
            EditorGUILayout.Space(8);
            DrawLogSection();

            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────────────────────────
        //  GUI sections
        // ──────────────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("World Spawn Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates ScriptableObject presets for world spawn configuration.\n" +
                "LootableConfigs → " + LOOTABLE_PATH + "\n" +
                "SpawnTables     → " + TABLE_PATH + "\n" +
                "WorldSpawnConfigs → " + OUTPUT_PATH + "\n" +
                "Existing assets are skipped — assign SpawnTable entries manually in the Inspector.",
                MessageType.Info);
        }

        private void DrawCreateAllButton()
        {
            var style = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Create All Presets", style, GUILayout.Height(32)))
                CreateAll();
        }

        private void DrawLootableConfigSection()
        {
            EditorGUILayout.LabelField("LootableConfig Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Instant Loot")) CreateInstantLootableConfig();
            if (GUILayout.Button("Hold Loot (1.5 s)")) CreateHoldLootableConfig();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpawnTableSection()
        {
            EditorGUILayout.LabelField("SpawnTable Presets", EditorStyles.boldLabel);
            if (GUILayout.Button("Default SpawnTable template (empty, FixedPlusRandom)"))
                CreateNamedSpawnTable("SpawnTable_Template", SpawnTableMode.FixedPlusRandom, 0, 2, 1, 3);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("SpawnTable Diagnostics", EditorStyles.miniLabel);
            if (GUILayout.Button("Validate All SpawnTables"))
                ValidateAllSpawnTables();
            if (GUILayout.Button("Repair Missing Item References (Report Only)"))
                ReportMissingItemReferences();
            var repairStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Auto-Repair All Broken References", repairStyle))
            {
                if (EditorUtility.DisplayDialog(
                    "Auto-Repair Broken References",
                    "Re-links broken ItemDefinition references in ALL SpawnTables using the built-in item map.\n\nEntries that cannot be mapped are SKIPPED (left as-is, not deleted).\n\nProceed?",
                    "Repair", "Cancel"))
                    AutoRepairAllBrokenReferences();
            }
            var restoreStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            if (GUILayout.Button("Restore Empty SpawnTables from Map", restoreStyle))
            {
                if (EditorUtility.DisplayDialog(
                    "Restore Empty SpawnTables",
                    "Fills Fixed/Random entries for ALL SpawnTables that currently have zero entries, using the built-in item map.\n\nOnly tables with BOTH lists empty (size=0) are touched.\n\nProceed?",
                    "Restore", "Cancel"))
                    RestoreEmptySpawnTablesFromMap();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawWorldSpawnConfigSection()
        {
            EditorGUILayout.LabelField("WorldSpawnConfig Presets", EditorStyles.boldLabel);
            if (GUILayout.Button("Container  (Hold, 120 s respawn)")) CreateContainerSpawnConfig();
            if (GUILayout.Button("Item Scatter  (Instant, 60 s, max 3, radius 2 m)")) CreateItemSpawnConfig();
            if (GUILayout.Button("Locked Chest  (Hold, no respawn, SpawnLocked)")) CreateLockedChestSpawnConfig();
        }

        private void DrawLogSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _log.Clear();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(140));
            foreach (var line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────────────────────────
        //  Preset creators
        // ──────────────────────────────────────────────────────────────────

        private void CreateAll()
        {
            EnsureOutputFolder();
            CreateInstantLootableConfig();
            CreateHoldLootableConfig();
            CreateNamedSpawnTable("SpawnTable_Template", SpawnTableMode.FixedPlusRandom, 0, 2, 1, 3);
            CreateContainerSpawnConfig();
            CreateItemSpawnConfig();
            CreateLockedChestSpawnConfig();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("All presets done.");
        }

        private LootableConfig CreateInstantLootableConfig()
        {
            EnsureOutputFolder();
            var cfg = GetOrCreateSO<LootableConfig>("LootableConfig_Instant", LOOTABLE_PATH, out bool created);
            if (!created) return cfg;

            cfg.InteractionMode = LootInteractionMode.Instant;
            cfg.HoldDuration    = 0.1f;   // Min(0.1f) — near-instant
            cfg.AllowAutoLoot   = true;
            cfg.ShowPrompt      = true;
            cfg.PromptText      = "Press E to Loot";
            cfg.MaxInteractDistance = 3f;
            cfg.DespawnTime     = 300f;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Log("Created: LootableConfig_Instant");
            return cfg;
        }

        private LootableConfig CreateHoldLootableConfig()
        {
            EnsureOutputFolder();
            var cfg = GetOrCreateSO<LootableConfig>("LootableConfig_Hold", LOOTABLE_PATH, out bool created);
            if (!created) return cfg;

            cfg.InteractionMode = LootInteractionMode.Hold;
            cfg.HoldDuration    = 1.5f;
            cfg.AllowAutoLoot   = false;
            cfg.ShowPrompt      = true;
            cfg.PromptText      = "Hold E to Open";
            cfg.MaxInteractDistance = 3f;
            cfg.DespawnTime     = 300f;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Log("Created: LootableConfig_Hold");
            return cfg;
        }

        private SpawnTable CreateNamedSpawnTable(
            string name, SpawnTableMode mode,
            int minRandom, int maxRandom,
            int minTotal,  int maxTotal)
        {
            EnsureOutputFolder();
            var tbl = GetOrCreateSO<SpawnTable>(name, TABLE_PATH, out bool created);
            if (!created) return tbl;

            tbl.Mode            = mode;
            tbl.MinRandomCount  = minRandom;
            tbl.MaxRandomCount  = maxRandom;
            tbl.MinTotalItems   = minTotal;
            tbl.MaxTotalItems   = maxTotal;
            tbl.RollOnOpen      = true;
            tbl.DropToWorldOnOpen = false;
            EditorUtility.SetDirty(tbl);
            AssetDatabase.SaveAssets();
            Log($"Created: {name}  (assign items in Inspector)");
            return tbl;
        }

        private void CreateContainerSpawnConfig()
        {
            EnsureOutputFolder();
            var cfg = GetOrCreateSO<WorldSpawnConfig>("WorldSpawnConfig_Container", OUTPUT_PATH, out bool created);
            if (!created) return;

            cfg.SpawnType           = WorldSpawnType.Container;
            cfg.SpawnTable          = EnsureSpawnTable("SpawnTable_Container");
            cfg.CanRespawn          = true;
            cfg.RespawnTime         = 120f;
            cfg.MaxRespawnCount     = 0;
            cfg.MaxActive           = 1;
            cfg.ScatterRadius       = 0f;
            cfg.SpawnLocked         = false;
            cfg.ContainerAutoReset  = false;
            cfg.ContainerResetDelay = 60f;
            cfg.LootableConfig      = LoadOrCreate<LootableConfig>("LootableConfig_Hold", LOOTABLE_PATH, CreateHoldLootableConfig);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Log("Created: WorldSpawnConfig_Container");
        }

        private void CreateItemSpawnConfig()
        {
            EnsureOutputFolder();
            var cfg = GetOrCreateSO<WorldSpawnConfig>("WorldSpawnConfig_ItemScatter", OUTPUT_PATH, out bool created);
            if (!created) return;

            cfg.SpawnType           = WorldSpawnType.Item;
            cfg.SpawnTable          = EnsureSpawnTable("SpawnTable_ItemScatter");
            cfg.CanRespawn          = true;
            cfg.RespawnTime         = 60f;
            cfg.MaxRespawnCount     = 0;
            cfg.MaxActive           = 3;
            cfg.ScatterRadius       = 2f;
            cfg.SpawnLocked         = false;
            cfg.ContainerAutoReset  = false;
            cfg.ContainerResetDelay = 60f;
            cfg.LootableConfig      = LoadOrCreate<LootableConfig>("LootableConfig_Instant", LOOTABLE_PATH, CreateInstantLootableConfig);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Log("Created: WorldSpawnConfig_ItemScatter  (ScatterRadius=2, MaxActive=3, RespawnTime=60 s)");
        }

        private void CreateLockedChestSpawnConfig()
        {
            EnsureOutputFolder();
            var cfg = GetOrCreateSO<WorldSpawnConfig>("WorldSpawnConfig_LockedChest", OUTPUT_PATH, out bool created);
            if (!created) return;

            cfg.SpawnType           = WorldSpawnType.Container;
            cfg.SpawnTable          = EnsureSpawnTable("SpawnTable_LockedChest");
            cfg.CanRespawn          = false;
            cfg.RespawnTime         = 120f;
            cfg.MaxRespawnCount     = 1;
            cfg.MaxActive           = 1;
            cfg.ScatterRadius       = 0f;
            cfg.SpawnLocked         = true;
            cfg.ContainerAutoReset  = false;
            cfg.ContainerResetDelay = 0f;
            cfg.LootableConfig      = LoadOrCreate<LootableConfig>("LootableConfig_Hold", LOOTABLE_PATH, CreateHoldLootableConfig);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Log("Created: WorldSpawnConfig_LockedChest  (SpawnLocked=true, CanRespawn=false)");
        }

        // ──────────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────────

        /// <summary>Finds an existing SpawnTable by name or creates a blank FixedPlusRandom one.</summary>
        private SpawnTable EnsureSpawnTable(string name)
        {
            string path = AssetPath(name, TABLE_PATH);
            var existing = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
            if (existing != null) return existing;
            return CreateNamedSpawnTable(name, SpawnTableMode.FixedPlusRandom, 0, 2, 1, 3);
        }

        /// <summary>Loads an existing SO at <paramref name="folder"/>/<paramref name="name"/> or invokes <paramref name="factory"/> to create it.</summary>
        private T LoadOrCreate<T>(string name, string folder, System.Func<T> factory) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(AssetPath(name, folder));
            return existing != null ? existing : factory();
        }

        /// <summary>
        /// Loads an existing SO or creates a blank instance.
        /// <paramref name="created"/> is true only when a new asset was actually written.
        /// </summary>
        private T GetOrCreateSO<T>(string name, string folder, out bool created) where T : ScriptableObject
        {
            string path = AssetPath(name, folder);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Log($"Skip (exists): {name}");
                created = false;
                return existing;
            }

            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            created = true;
            return so;
        }

        private static string AssetPath(string name, string folder) => $"{folder}/{name}.asset";

        private void EnsureOutputFolder()
        {
            EnsurePath(OUTPUT_PATH);
            EnsurePath(LOOTABLE_PATH);
            EnsurePath(TABLE_PATH);
        }

        private static void EnsurePath(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void Log(string msg)
        {
            _log.Add($"[{System.DateTime.Now:HH:mm:ss}]  {msg}");
            Debug.Log($"[WorldSpawnSetupTool] {msg}");
            Repaint();
        }

        // ──────────────────────────────────────────────────────────────────
        //  SpawnTable diagnostics
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans every SpawnTable in the project and prints a summary:
        /// total count, valid/broken/empty entries, and paths of tables with issues.
        /// </summary>
        private void ValidateAllSpawnTables()
        {
            string[] guids = AssetDatabase.FindAssets("t:SpawnTable");
            int total        = guids.Length;
            int validTotal   = 0;
            int brokenTotal  = 0;  // null with non-zero instance ID  → "Missing"
            int emptyTotal   = 0;  // null with zero instance ID      → never assigned
            var problematic  = new List<string>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var table   = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
                if (table == null) continue;

                var so = new SerializedObject(table);
                int broken = 0, empty = 0;
                int valid  = ScanEntries(so.FindProperty("FixedEntries"),  ref broken, ref empty);
                valid     += ScanEntries(so.FindProperty("RandomEntries"), ref broken, ref empty);

                validTotal  += valid;
                brokenTotal += broken;
                emptyTotal  += empty;

                if (broken > 0 || empty > 0)
                    problematic.Add(path);
            }

            Log("=== Validate All SpawnTables ===");
            Log($"Total SpawnTables scanned : {total}");
            Log($"Valid item references      : {validTotal}");
            Log($"BROKEN references (Missing): {brokenTotal}");
            Log($"Empty entries (never filled): {emptyTotal}");

            if (problematic.Count > 0)
            {
                Log($"SpawnTables with issues ({problematic.Count}):" );
                foreach (var p in problematic)
                    Log($"  • {p}");
            }
            else
            {
                Log("All SpawnTables look healthy — no missing or empty entries found.");
            }
        }

        /// <summary>
        /// Iterates every SpawnTable and reports broken GUID references and empty entries
        /// in detail (table name, list type, entry index).  Does NOT modify any assets.
        /// </summary>
        private void ReportMissingItemReferences()
        {
            string[] guids = AssetDatabase.FindAssets("t:SpawnTable");
            int brokenFound = 0;
            int emptyFound  = 0;

            Log("=== Repair Missing Item References — Report ===");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var table   = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
                if (table == null) continue;

                var so = new SerializedObject(table);

                ReportEntries(so.FindProperty("FixedEntries"),  "Fixed",  table.name, path, ref brokenFound, ref emptyFound);
                ReportEntries(so.FindProperty("RandomEntries"), "Random", table.name, path, ref brokenFound, ref emptyFound);
            }

            Log($"--- Summary ---");
            Log($"BROKEN references found : {brokenFound}  (GUID lost — must re-assign manually in Inspector)");
            Log($"Empty entries found     : {emptyFound}   (never assigned — fill in Inspector or re-run migration tool)");

            if (brokenFound > 0)
            {
                Log("ACTION: Open each listed SpawnTable in the Inspector, locate the \"Missing\" slot, and drag the correct ItemDefinition onto it.");
                Log("HINT  : If the item asset was moved/renamed but not deleted, use AssetDatabase.FindAssets (or the search above) to locate it.");
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Private helpers
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Counts valid, broken (Missing), and empty entries in a serialized LootItemEntry array.
        /// A broken entry has <c>objectReferenceValue == null</c> but
        /// <c>objectReferenceInstanceIDValue != 0</c> — the GUID pointer still exists but
        /// Unity can no longer resolve it (asset moved / renamed / deleted).
        /// </summary>
        private static int ScanEntries(
            SerializedProperty arrayProp,
            ref int brokenCount,
            ref int emptyCount)
        {
            if (arrayProp == null) return 0;

            int valid = 0;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var itemProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("Item");
                if (itemProp == null) continue;

                if (itemProp.objectReferenceValue != null)
                {
                    valid++;
                }
                else if (itemProp.objectReferenceInstanceIDValue != 0)
                {
                    brokenCount++;  // GUID stored but asset missing
                }
                else
                {
                    emptyCount++;   // Never assigned
                }
            }
            return valid;
        }

        /// <summary>
        /// Detailed per-entry log for a single SpawnTable list (Fixed or Random).
        /// Increments <paramref name="brokenCount"/> and <paramref name="emptyCount"/> in place.
        /// </summary>
        private void ReportEntries(
            SerializedProperty arrayProp,
            string listLabel,
            string tableName,
            string tablePath,
            ref int brokenCount,
            ref int emptyCount)
        {
            if (arrayProp == null) return;

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element  = arrayProp.GetArrayElementAtIndex(i);
                var itemProp = element.FindPropertyRelative("Item");
                if (itemProp == null) continue;

                if (itemProp.objectReferenceValue != null) continue;  // healthy

                if (itemProp.objectReferenceInstanceIDValue != 0)
                {
                    brokenCount++;
                    var chanceProp = element.FindPropertyRelative("Chance");
                    var minProp    = element.FindPropertyRelative("MinQuantity");
                    var maxProp    = element.FindPropertyRelative("MaxQuantity");
                    Log($"  [BROKEN]  {tableName} → {listLabel}[{i}]  chance={chanceProp?.floatValue:F2}  qty={minProp?.intValue}-{maxProp?.intValue}");
                    Log($"            Path: {tablePath}");
                    Debug.LogWarning($"[WorldSpawnSetupTool] BROKEN reference in {tableName} / {listLabel}[{i}] ({tablePath})");
                }
                else
                {
                    emptyCount++;
                    Log($"  [EMPTY]   {tableName} → {listLabel}[{i}]  (never assigned)");
                }
            }
        }

        /// <summary>
        /// Finds a ScriptableObject asset whose file name (without extension) matches
        /// <paramref name="name"/> exactly.  Returns the first match, or <c>null</c>.
        /// Generic so it works for ItemDefinition, AttachmentDefinition, etc.
        /// </summary>
        private static T FindAssetByName<T>(string name) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Auto-repair
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a case-insensitive lookup of every ItemDefinition asset found under
        /// the Database/Items folder, keyed by lower-cased file name (no extension).
        /// </summary>
        private Dictionary<string, ItemDefinition> BuildItemLookup()
        {
            var lookup = new Dictionary<string, ItemDefinition>(System.StringComparer.OrdinalIgnoreCase);

            // t:ItemDefinition finds all concrete subclasses automatically.
            string[] guids = AssetDatabase.FindAssets(
                "t:ItemDefinition",
                new[] { "Assets/_Night_Hunt/Data/Resources/Database/Items" });

            if (guids.Length == 0)
            {
                // Fallback: filter ScriptableObjects if Unity can't resolve the abstract type.
                guids = AssetDatabase.FindAssets(
                    "t:ScriptableObject",
                    new[] { "Assets/_Night_Hunt/Data/Resources/Database/Items" });
            }

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;

                string key = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!lookup.ContainsKey(key))
                    lookup[key] = item;
            }

            Log($"Item lookup built: {lookup.Count} ItemDefinition assets found.");
            return lookup;
        }

        /// <summary>
        /// Returns the lower-cased asset file name for the item that should occupy
        /// <paramref name="entryIdx"/> in the Fixed (<paramref name="isFixed"/>=true) or
        /// Random pool of the given SpawnTable, based on the static item map.
        /// Returns <c>null</c> when no mapping is registered.
        /// </summary>
        private static string InferItemKey(string tableNameLower, bool isFixed, int entryIdx)
        {
            if (!s_itemMap.TryGetValue(tableNameLower, out var pair)) return null;
            string[] pool = isFixed ? pair.F : pair.R;
            if (pool == null || entryIdx < 0 || entryIdx >= pool.Length) return null;
            return pool[entryIdx];
        }

        /// <summary>
        /// Iterates all SpawnTable assets, finds broken GUID references, and either
        /// re-links them from <paramref name="itemLookup"/> (using the static item map)
        /// or deletes the entry if no mapping is available.
        /// Iterates entries backwards to keep indices stable during deletion.
        /// </summary>
        private void AutoRepairAllBrokenReferences()
        {
            var itemLookup = BuildItemLookup();
            if (itemLookup.Count == 0)
            {
                Log("ERROR: Item lookup is empty. Verify that ItemDefinition assets exist in Database/Items/.");
                return;
            }

            string[] tableGuids = AssetDatabase.FindAssets("t:SpawnTable");
            int repairedCount = 0;
            int deletedCount  = 0;

            Log($"=== Auto-Repair: scanning {tableGuids.Length} SpawnTables ===");

            foreach (var guid in tableGuids)
            {
                string path  = AssetDatabase.GUIDToAssetPath(guid);
                var    table = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
                if (table == null) continue;

                var  so    = new SerializedObject(table);
                bool dirty = false;

                dirty |= RepairEntryArray(so.FindProperty("FixedEntries"),  isFixed: true,  table.name, itemLookup, ref repairedCount, ref deletedCount);
                dirty |= RepairEntryArray(so.FindProperty("RandomEntries"), isFixed: false, table.name, itemLookup, ref repairedCount, ref deletedCount);

                if (dirty)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(table);
                }
            }

            AssetDatabase.SaveAssets();
            Log($"=== Auto-Repair done.  Repaired={repairedCount},  Deleted={deletedCount} ===");
            Debug.Log($"[WorldSpawnSetupTool] Auto-repair complete — repaired: {repairedCount}, deleted: {deletedCount}");
        }

        /// <summary>
        /// Repairs or deletes every broken entry in a single serialised
        /// LootItemEntry list (FixedEntries or RandomEntries).
        /// Returns <c>true</c> if any modification was made.
        /// </summary>
        private bool RepairEntryArray(
            SerializedProperty arrayProp,
            bool isFixed,
            string tableName,
            Dictionary<string, ItemDefinition> itemLookup,
            ref int repairedCount,
            ref int deletedCount)
        {
            if (arrayProp == null || !arrayProp.isArray) return false;

            // Strip spaces so "Spawn Table_Boss Drop" matches "spawntable_bossdrop"
            string tableKey = NormalizeTableName(tableName);
            bool   dirty    = false;

            // Iterate backwards so deletion does not shift remaining indices.
            for (int i = arrayProp.arraySize - 1; i >= 0; i--)
            {
                var entry    = arrayProp.GetArrayElementAtIndex(i);
                var itemProp = entry.FindPropertyRelative("Item");
                if (itemProp == null) continue;

                bool isBroken = itemProp.objectReferenceValue == null &&
                                itemProp.objectReferenceInstanceIDValue != 0;
                if (!isBroken) continue;

                string inferredKey = InferItemKey(tableKey, isFixed, i);
                itemLookup.TryGetValue(inferredKey ?? string.Empty, out var resolved);

                if (resolved != null)
                {
                    itemProp.objectReferenceValue = resolved;
                    repairedCount++;
                    dirty = true;
                    Log($"  [REPAIRED] {tableName} / {(isFixed ? "Fixed" : "Random")}[{i}] \u2192 {resolved.name}");
                }
                else
                {
                    // Cannot resolve — SKIP, do NOT delete.
                    // Possible cause: asset filename doesn't match map key, or item was deleted.
                    Log($"  [SKIP]  {tableName} / {(isFixed ? "Fixed" : "Random")}[{i}]  (no map entry; inferred key: {inferredKey ?? "none"})");
                    Debug.LogWarning($"[WorldSpawnSetupTool] Could not resolve broken ref in {tableName} / {(isFixed ? "Fixed" : "Random")}[{i}] — entry left unchanged.");
                }
            }

            return dirty;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Restore empty SpawnTables from map
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a SpawnTable asset name to the normalised dictionary key:
        /// lower-cased and all spaces removed so filenames like
        /// "Spawn Table_Boss Drop_Tier 1_Medical" map correctly to
        /// "spawntable_bossdrop_tier1_medical".
        /// </summary>
        private static string NormalizeTableName(string tableName) =>
            tableName.ToLowerInvariant().Replace(" ", "");

        /// <summary>
        /// Finds every SpawnTable whose FixedEntries AND RandomEntries are both empty
        /// (size == 0) and fills them from the static item map.
        /// Tables that already have at least one entry are not touched.
        /// </summary>
        private void RestoreEmptySpawnTablesFromMap()
        {
            var itemLookup = BuildItemLookup();
            if (itemLookup.Count == 0)
            {
                Log("ERROR: Item lookup is empty. Check Database/Items/ folder.");
                return;
            }

            string[] tableGuids     = AssetDatabase.FindAssets("t:SpawnTable");
            int      restoredTables = 0;
            int      addedEntries   = 0;
            int      skippedTables  = 0;

            Log($"=== Restore Empty SpawnTables: scanning {tableGuids.Length} assets ===");

            foreach (var guid in tableGuids)
            {
                string path  = AssetDatabase.GUIDToAssetPath(guid);
                var    table = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
                if (table == null) continue;

                string normalizedKey = NormalizeTableName(table.name);

                var so        = new SerializedObject(table);
                var fixedArr  = so.FindProperty("FixedEntries");
                var randomArr = so.FindProperty("RandomEntries");

                if (fixedArr == null || randomArr == null) continue;

                // Only restore when BOTH lists are completely empty.
                if (fixedArr.arraySize != 0 || randomArr.arraySize != 0)
                {
                    skippedTables++;
                    continue;
                }

                if (!s_itemMap.TryGetValue(normalizedKey, out var pair))
                {
                    Log($"  [NO MAP] {table.name}  \u2192 no entry in item map (key: {normalizedKey})");
                    continue;
                }

                bool dirty = false;
                dirty |= FillArrayFromKeys(fixedArr,  pair.F, itemLookup, ref addedEntries, table.name, "Fixed");
                dirty |= FillArrayFromKeys(randomArr, pair.R, itemLookup, ref addedEntries, table.name, "Random");

                if (dirty)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(table);
                    restoredTables++;
                }
            }

            AssetDatabase.SaveAssets();
            Log($"=== Restore done.  Tables restored={restoredTables},  Entries added={addedEntries},  Skipped (non-empty)={skippedTables} ===");
            Debug.Log($"[WorldSpawnSetupTool] Restore: {restoredTables} tables, {addedEntries} entries added.");
        }

        /// <summary>
        /// Appends one LootItemEntry per key in <paramref name="keys"/> to
        /// <paramref name="arrayProp"/>.  Missing items are logged and skipped.
        /// Returns <c>true</c> if at least one entry was added.
        /// </summary>
        private bool FillArrayFromKeys(
            SerializedProperty arrayProp,
            string[]           keys,
            Dictionary<string, ItemDefinition> itemLookup,
            ref int            addedCount,
            string             tableName,
            string             listLabel)
        {
            if (keys == null || keys.Length == 0) return false;

            bool dirty = false;

            foreach (var key in keys)
            {
                if (!itemLookup.TryGetValue(key, out var item))
                {
                    Log($"  [WARN] {tableName}/{listLabel}: no asset found for key '{key}'");
                    continue;
                }

                int idx = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(idx);
                var elem = arrayProp.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("Item").objectReferenceValue = item;
                elem.FindPropertyRelative("MinQuantity").intValue      = 1;
                elem.FindPropertyRelative("MaxQuantity").intValue      = 1;
                elem.FindPropertyRelative("Chance").floatValue         = 1f;

                addedCount++;
                dirty = true;
                Log($"  [ADDED] {tableName}/{listLabel}[{idx}] \u2192 {item.name}");
            }

            return dirty;
        }
    }
}
