using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NightHunt.GameplaySystems.World;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// NightHunt Data Migration Tool
    ///
    /// Menu: NightHunt / Tools / Data Migration &amp; Bulk Spawn Generator
    ///
    /// ──────────────────────────────────────────────────────────────────────────
    /// STEP 1 — Move stale legacy assets to _outdate (pending removal)
    ///   • Data/Items/**                     → Data/_outdate/Items_Legacy/
    ///   • Resources/Configs/StatConfigs/**  → Data/_outdate/StatConfigs_Legacy/
    ///   • Data/Resources/Database/Spawn/WorldSpawnConfig*.asset (unnamed) → _outdate/Spawn_Legacy/
    ///   • Data/Resources/Database/Spawn/Lootable/** → _outdate/Spawn_Legacy/Lootable/
    ///   • Data/Resources/Database/Spawn/SpawnTable/** → _outdate/Spawn_Legacy/SpawnTable/
    ///
    /// STEP 2 — Ensure canonical folder structure
    ///   Data/Resources/Database/Spawn/
    ///     LootableConfigs/
    ///     SpawnTables/Items/
    ///     SpawnTables/Boss/
    ///     WorldSpawnConfigs/Items/
    ///     WorldSpawnConfigs/Boss/
    ///
    /// STEP 3 — Generate shared LootableConfig presets
    ///   LootableConfig_Instant  — single E-press, auto-loot
    ///   LootableConfig_Hold     — hold E for 1.5 s
    ///
    /// STEP 4 — Generate 34 Item WorldSpawn configs
    ///   Ground scatter: consumables, weapons, attachments, equipment, throwables
    ///   Containers: medical, weapon, equipment, utility, general crates / locked chests
    ///   Clusters:   multi-active scatter groups
    ///
    /// STEP 5 — Generate 20 Boss / Zone drop configs
    ///   Boss tiers 1-3, zone phase rewards, capture/clear rewards,
    ///   supply drops, hidden caches, event crates, jackpot
    ///
    /// All new assets: Data/Resources/Database/Spawn/{LootableConfigs|SpawnTables|WorldSpawnConfigs}/
    /// Existing assets with matching names are SKIPPED (never overwritten).
    /// ──────────────────────────────────────────────────────────────────────────
    /// </summary>
    public class NightHuntDataMigrationTool : EditorWindow
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Path constants – canonical output
        // ──────────────────────────────────────────────────────────────────────

        private const string SPAWN_ROOT     = "Assets/_Night_Hunt/Data/Resources/Database/Spawn";
        private const string LOOTABLE_DIR   = SPAWN_ROOT + "/LootableConfigs";
        private const string TABLE_ITEMS    = SPAWN_ROOT + "/SpawnTables/Items";
        private const string TABLE_BOSS     = SPAWN_ROOT + "/SpawnTables/Boss";
        private const string CONFIG_ITEMS   = SPAWN_ROOT + "/WorldSpawnConfigs/Items";
        private const string CONFIG_BOSS    = SPAWN_ROOT + "/WorldSpawnConfigs/Boss";

        // Outdate root
        private const string OUTDATE_ROOT   = "Assets/_Night_Hunt/Data/_outdate";

        // Stale folders to migrate
        private const string STALE_ITEMS    = "Assets/_Night_Hunt/Data/Items";
        private const string STALE_STATCFG  = "Assets/_Night_Hunt/Resources/Configs/StatConfigs";

        // ──────────────────────────────────────────────────────────────────────
        //  Item definition paths (canonical)
        // ──────────────────────────────────────────────────────────────────────

        private const string ITEMS_BASE = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items";

        private static readonly string P_AK47     = ITEMS_BASE + "/Weapons/Weapon_AK47.asset";
        private static readonly string P_M4       = ITEMS_BASE + "/Weapons/Weapon_M4.asset";
        private static readonly string P_PISTOL   = ITEMS_BASE + "/Weapons/Weapon_Pistol.asset";

        private static readonly string P_MEDKIT   = ITEMS_BASE + "/Consumables/Consumable_Medkit.asset";
        private static readonly string P_ENERGY   = ITEMS_BASE + "/Consumables/Consumable_EnergyDrink.asset";
        private static readonly string P_BEACON   = ITEMS_BASE + "/Consumables/BeaconDefinition.asset";

        private static readonly string P_HELMET   = ITEMS_BASE + "/Equipment/Armor_Helmet.asset";
        private static readonly string P_VEST     = ITEMS_BASE + "/Equipment/Armor_Vest.asset";
        private static readonly string P_GLOVES   = ITEMS_BASE + "/Equipment/Armor_Gloves.asset";
        private static readonly string P_BELT     = ITEMS_BASE + "/Equipment/Armor_Belt.asset";
        private static readonly string P_BACKPACK = ITEMS_BASE + "/Equipment/Armor_Backpack.asset";

        private static readonly string P_FRAG     = ITEMS_BASE + "/Throwable/Throwable_FragGrenade.asset";
        private static readonly string P_SMOKE    = ITEMS_BASE + "/Throwable/Throwable_SmokeGrenade.asset";

        private static readonly string P_EXTMAG   = ITEMS_BASE + "/Attachments/Attachment_ExtMag.asset";
        private static readonly string P_FLASH    = ITEMS_BASE + "/Attachments/Attachment_Flashlight.asset";
        private static readonly string P_GRIP     = ITEMS_BASE + "/Attachments/Attachment_Grip.asset";
        private static readonly string P_POUCH    = ITEMS_BASE + "/Attachments/Attachment_Pouch.asset";
        private static readonly string P_REDDOT   = ITEMS_BASE + "/Attachments/Attachment_RedDot.asset";
        private static readonly string P_SUPPRESS = ITEMS_BASE + "/Attachments/Attachment_Suppressor.asset";

        // ──────────────────────────────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────────────────────────────

        private Vector2 _scroll;
        private Vector2 _logScroll;
        private readonly List<string> _log = new List<string>();

        // ──────────────────────────────────────────────────────────────────────
        //  Entry point
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem("NightHunt/Tools/Data Migration & Bulk Spawn Generator")]
        public static void Open()
        {
            var w = GetWindow<NightHuntDataMigrationTool>("NH Data Migration");
            w.minSize = new Vector2(420, 600);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("NightHunt Data Migration & Bulk Spawn Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "STEP 1  Move legacy assets to _outdate (won't delete — manual cleanup later).\n" +
                "STEP 2  Ensure canonical folder structure.\n" +
                "STEP 3  Create shared LootableConfig presets.\n" +
                "STEP 4  Generate 34 item WorldSpawn configs + SpawnTables.\n" +
                "STEP 5  Generate 20 boss/zone WorldSpawn configs + SpawnTables.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            var bigStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
            if (GUILayout.Button("Run Full Migration (Steps 1-5)", bigStyle, GUILayout.Height(38)))
                RunAll();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Or run individual steps:", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1  Move stale to _outdate"))  StepMoveStaleData();
            if (GUILayout.Button("2  Ensure folders"))           StepEnsureFolders();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("3  Lootable presets"))         StepLootableConfigs();
            if (GUILayout.Button("4  Item spawns (34)"))         StepGenerateItemSpawns();
            if (GUILayout.Button("5  Boss drops (20)"))          StepGenerateBossDrops();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _log.Clear();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(300));
            foreach (var line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Orchestrator
        // ──────────────────────────────────────────────────────────────────────

        private void RunAll()
        {
            StepMoveStaleData();
            StepEnsureFolders();
            StepLootableConfigs();
            StepGenerateItemSpawns();
            StepGenerateBossDrops();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("═══ Migration complete ═══");
        }

        // ──────────────────────────────────────────────────────────────────────
        //  STEP 1 — Move stale assets to _outdate
        // ──────────────────────────────────────────────────────────────────────

        private void StepMoveStaleData()
        {
            Log("── Step 1: Moving stale assets to _outdate ──");

            // Ensure _outdate root exists
            EnsureFolder("Assets/_Night_Hunt/Data", "_outdate");

            // 1a — Data/Items/ → _outdate/Items_Legacy/
            MoveFolderContents(STALE_ITEMS, OUTDATE_ROOT + "/Items_Legacy", "Items_Legacy");

            // 1b — Resources/Configs/StatConfigs/ → _outdate/StatConfigs_Legacy/
            MoveFolderContents(STALE_STATCFG, OUTDATE_ROOT + "/StatConfigs_Legacy", "StatConfigs_Legacy");

            // 1c — Unnamed Spawn garbage in Database/Spawn/
            MoveMatchingFiles(SPAWN_ROOT, OUTDATE_ROOT + "/Spawn_Legacy",
                "Spawn_Legacy", new[] { "WorldSpawnConfig.asset", "WorldSpawnConfig 1.asset" });

            // 1d — Spawn/Lootable/ folder
            MoveFolderContents(SPAWN_ROOT + "/Lootable",
                OUTDATE_ROOT + "/Spawn_Legacy/Lootable", "Spawn_Legacy/Lootable");

            // 1e — Spawn/SpawnTable/ folder
            MoveFolderContents(SPAWN_ROOT + "/SpawnTable",
                OUTDATE_ROOT + "/Spawn_Legacy/SpawnTable", "Spawn_Legacy/SpawnTable");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("  Step 1 done.");
        }

        /// <summary>Moves every non-.meta file from <paramref name="srcFolder"/> into <paramref name="dstFolder"/>.</summary>
        private void MoveFolderContents(string srcFolder, string dstFolder, string label)
        {
            if (!AssetDatabase.IsValidFolder(srcFolder))
            {
                Log($"  Skip (no folder): {srcFolder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("", new[] { srcFolder });
            if (guids.Length == 0)
            {
                Log($"  Empty folder: {srcFolder}");
                return;
            }

            EnsureFolderPath(dstFolder);

            int moved = 0;
            foreach (var guid in guids)
            {
                string src = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(src)) continue;  // skip sub-folders

                string fileName = System.IO.Path.GetFileName(src);
                string dst = dstFolder + "/" + fileName;

                if (AssetDatabase.LoadAssetAtPath<Object>(dst) != null)
                {
                    Log($"  Skip (dst exists): {fileName}");
                    continue;
                }

                string err = AssetDatabase.MoveAsset(src, dst);
                if (string.IsNullOrEmpty(err))
                    moved++;
                else
                    Log($"  ⚠ Move failed ({fileName}): {err}");
            }
            Log($"  Moved {moved} assets → {label}");
        }

        /// <summary>Moves a specific list of named files from <paramref name="srcFolder"/>.</summary>
        private void MoveMatchingFiles(string srcFolder, string dstFolder, string label, string[] fileNames)
        {
            if (!AssetDatabase.IsValidFolder(srcFolder)) return;

            bool any = false;
            foreach (var name in fileNames)
            {
                string src = srcFolder + "/" + name;
                if (AssetDatabase.LoadAssetAtPath<Object>(src) == null) continue;
                if (!any) { EnsureFolderPath(dstFolder); any = true; }
                string dst = dstFolder + "/" + name;
                string err = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(err))
                    Log($"  ⚠ Move failed ({name}): {err}");
                else
                    Log($"  Moved: {name} → {label}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        //  STEP 2 — Canonical folder structure
        // ──────────────────────────────────────────────────────────────────────

        private void StepEnsureFolders()
        {
            Log("── Step 2: Ensuring canonical folder structure ──");
            EnsureFolderPath(LOOTABLE_DIR);
            EnsureFolderPath(TABLE_ITEMS);
            EnsureFolderPath(TABLE_BOSS);
            EnsureFolderPath(CONFIG_ITEMS);
            EnsureFolderPath(CONFIG_BOSS);
            Log("  Folders ready.");
        }

        // ──────────────────────────────────────────────────────────────────────
        //  STEP 3 — Shared LootableConfig presets
        // ──────────────────────────────────────────────────────────────────────

        private void StepLootableConfigs()
        {
            Log("── Step 3: Lootable configs ──");

            var instant = GetOrCreate<LootableConfig>(LOOTABLE_DIR + "/LootableConfig_Instant.asset", out bool c1);
            if (c1)
            {
                instant.InteractionMode = LootInteractionMode.Instant;
                instant.HoldDuration    = 0.1f;
                instant.AllowAutoLoot   = true;
                instant.ShowPrompt      = true;
                instant.PromptText      = "Press E to Loot";
                instant.MaxInteractDistance = 3f;
                instant.DespawnTime     = 300f;
                EditorUtility.SetDirty(instant);
                Log("  Created: LootableConfig_Instant");
            }

            var hold = GetOrCreate<LootableConfig>(LOOTABLE_DIR + "/LootableConfig_Hold.asset", out bool c2);
            if (c2)
            {
                hold.InteractionMode = LootInteractionMode.Hold;
                hold.HoldDuration    = 1.5f;
                hold.AllowAutoLoot   = false;
                hold.ShowPrompt      = true;
                hold.PromptText      = "Hold E to Open";
                hold.MaxInteractDistance = 3f;
                hold.DespawnTime     = 300f;
                EditorUtility.SetDirty(hold);
                Log("  Created: LootableConfig_Hold");
            }

            AssetDatabase.SaveAssets();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  STEP 4 — 34 Item WorldSpawn configs
        // ──────────────────────────────────────────────────────────────────────

        private void StepGenerateItemSpawns()
        {
            Log("── Step 4: Generating 34 item spawn configs ──");

            // ── Consumable ground scatter ────────────────────────────────────

            Build("Ground_Medkit_Common",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 60, maxRespawnCount: 0,
                maxActive: 1, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_MEDKIT, 0.80f) });

            Build("Ground_EnergyDrink_Common",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 45, maxRespawnCount: 0,
                maxActive: 2, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_ENERGY, 0.90f) });

            Build("Ground_Medkit_Rare",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 1f, locked: false, hold: false,
                SpawnTableMode.FixedOnly, minR: 0, maxR: 0, minT: 2, maxT: 2,
                fixd: new[] { E(P_MEDKIT, 1f, 2, 2) },
                rand: None);

            Build("Ground_Beacon",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 180, maxRespawnCount: 0,
                maxActive: 1, scatter: 1f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_BEACON, 0.25f) });

            // ── Weapon ground scatter ────────────────────────────────────────

            Build("Ground_Pistol",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 1, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_PISTOL, 0.70f) });

            Build("Ground_Rifle_AK47",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_AK47, 0.50f) });

            Build("Ground_Rifle_M4",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_M4, 0.45f) });

            Build("Ground_Rifle_Random",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_AK47, 0.40f), E(P_M4, 0.40f) });

            // ── Attachment ground scatter ────────────────────────────────────

            Build("Ground_Attachment_Optic",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 2, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_REDDOT, 0.60f), E(P_SUPPRESS, 0.40f) });

            Build("Ground_Attachment_Support",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 75, maxRespawnCount: 0,
                maxActive: 2, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_GRIP, 0.75f), E(P_EXTMAG, 0.75f), E(P_FLASH, 0.55f) });

            Build("Ground_Attachment_Any",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 75, maxRespawnCount: 0,
                maxActive: 3, scatter: 2.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_EXTMAG, 0.60f), E(P_FLASH, 0.55f), E(P_GRIP, 0.65f),
                              E(P_POUCH, 0.60f), E(P_REDDOT, 0.60f), E(P_SUPPRESS, 0.50f) });

            // ── Equipment ground scatter ─────────────────────────────────────

            Build("Ground_Helmet",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 1, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_HELMET, 0.65f) });

            Build("Ground_Vest",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 1, scatter: 1.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_VEST, 0.60f) });

            Build("Ground_Gloves",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 75, maxRespawnCount: 0,
                maxActive: 2, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_GLOVES, 0.70f) });

            Build("Ground_Belt",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 75, maxRespawnCount: 0,
                maxActive: 2, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_BELT, 0.70f) });

            Build("Ground_Backpack",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 1f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_BACKPACK, 0.40f) });

            // ── Throwable ground scatter ─────────────────────────────────────

            Build("Ground_FragGrenade",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 2, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 2, minT: 1, maxT: 2,
                fixd: None,
                rand: new[] { E(P_FRAG, 0.55f, 1, 2) });

            Build("Ground_SmokeGrenade",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 75, maxRespawnCount: 0,
                maxActive: 2, scatter: 2f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 2, minT: 1, maxT: 2,
                fixd: None,
                rand: new[] { E(P_SMOKE, 0.60f, 1, 2) });

            Build("Ground_Throwable_Mixed",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 3, scatter: 2.5f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 2, minT: 1, maxT: 2,
                fixd: None,
                rand: new[] { E(P_FRAG, 0.50f, 1, 2), E(P_SMOKE, 0.60f, 1, 2) });

            // ── Container spawns ─────────────────────────────────────────────

            Build("Crate_Medical",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 0, maxR: 2, minT: 1, maxT: 3,
                fixd: new[] { E(P_MEDKIT, 1f) },
                rand: new[] { E(P_ENERGY, 0.80f), E(P_MEDKIT, 0.40f) });

            Build("Crate_Weapons_Light",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 2, maxT: 3,
                fixd: new[] { E(P_PISTOL, 1f) },
                rand: new[] { E(P_REDDOT, 0.60f), E(P_EXTMAG, 0.60f), E(P_SUPPRESS, 0.40f) });

            Build("Crate_Weapons_Heavy",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 3, minT: 2, maxT: 4,
                fixd: new[] { E(P_AK47, 0.60f), E(P_M4, 0.40f) },
                rand: new[] { E(P_GRIP, 0.70f), E(P_SUPPRESS, 0.60f), E(P_REDDOT, 0.50f) });

            Build("Crate_Equipment_Basic",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 120, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 0, maxR: 2, minT: 2, maxT: 4,
                fixd: new[] { E(P_HELMET, 1f), E(P_VEST, 1f) },
                rand: new[] { E(P_GLOVES, 0.60f), E(P_BELT, 0.60f) });

            Build("Crate_Equipment_Full",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 180, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 0, maxR: 2, minT: 3, maxT: 5,
                fixd: new[] { E(P_HELMET, 1f), E(P_VEST, 1f), E(P_BACKPACK, 1f) },
                rand: new[] { E(P_GLOVES, 0.80f), E(P_BELT, 0.80f) });

            Build("Crate_Utility",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 0, maxR: 2, minT: 2, maxT: 4,
                fixd: new[] { E(P_FRAG, 1f, 1, 2), E(P_SMOKE, 1f) },
                rand: new[] { E(P_FRAG, 0.60f, 1, 2), E(P_SMOKE, 0.60f) });

            Build("Crate_General_Common",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 60, maxRespawnCount: 0,
                maxActive: 2, scatter: 0f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 3, minT: 1, maxT: 3,
                fixd: None,
                rand: new[] { E(P_MEDKIT, 0.65f), E(P_ENERGY, 0.75f),
                              E(P_PISTOL, 0.30f), E(P_HELMET, 0.35f),
                              E(P_FRAG, 0.45f), E(P_REDDOT, 0.40f) });

            Build("Crate_General_Rare",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 180, maxRespawnCount: 0,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.RandomOnly, minR: 2, maxR: 4, minT: 2, maxT: 4,
                fixd: None,
                rand: new[] { E(P_AK47, 0.55f), E(P_M4, 0.50f), E(P_HELMET, 0.70f),
                              E(P_VEST, 0.70f), E(P_BACKPACK, 0.50f), E(P_SUPPRESS, 0.60f) });

            Build("Chest_Basic",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 120, maxRespawnCount: 3,
                maxActive: 1, scatter: 0f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 2, minT: 1, maxT: 2,
                fixd: None,
                rand: new[] { E(P_MEDKIT, 0.70f), E(P_ENERGY, 0.80f),
                              E(P_PISTOL, 0.35f), E(P_GLOVES, 0.40f),
                              E(P_FRAG, 0.50f), E(P_EXTMAG, 0.55f) });

            Build("Chest_Military",
                WorldSpawnType.Container, canRespawn: true, respawnTime: 240, maxRespawnCount: 2,
                maxActive: 1, scatter: 0f, locked: false, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 2, maxR: 4, minT: 3, maxT: 5,
                fixd: new[] { E(P_AK47, 1f) },
                rand: new[] { E(P_GRIP, 0.80f), E(P_REDDOT, 0.80f), E(P_SUPPRESS, 0.70f),
                              E(P_EXTMAG, 0.75f), E(P_MEDKIT, 0.60f) });

            Build("Chest_Locked_Basic",
                WorldSpawnType.Container, canRespawn: false, respawnTime: 120, maxRespawnCount: 1,
                maxActive: 1, scatter: 0f, locked: true, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 3, maxT: 4,
                fixd: new[] { E(P_AK47, 0.60f), E(P_M4, 0.40f), E(P_HELMET, 1f) },
                rand: new[] { E(P_VEST, 0.80f), E(P_GRIP, 0.70f), E(P_REDDOT, 0.70f) });

            Build("Chest_Locked_Elite",
                WorldSpawnType.Container, canRespawn: false, respawnTime: 120, maxRespawnCount: 1,
                maxActive: 1, scatter: 0f, locked: true, hold: true,
                SpawnTableMode.FixedPlusRandom, minR: 2, maxR: 4, minT: 5, maxT: 8,
                fixd: new[] { E(P_M4, 1f), E(P_HELMET, 1f), E(P_VEST, 1f), E(P_BACKPACK, 1f) },
                rand: new[] { E(P_GRIP, 0.90f), E(P_REDDOT, 0.90f), E(P_SUPPRESS, 0.80f),
                              E(P_EXTMAG, 0.85f), E(P_MEDKIT, 0.90f), E(P_GLOVES, 0.80f) });

            // ── Multi-active scatter clusters ────────────────────────────────

            Build("Cluster_Attachment_Mixed",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 60, maxRespawnCount: 0,
                maxActive: 4, scatter: 3f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 2, minT: 1, maxT: 2,
                fixd: None,
                rand: new[] { E(P_EXTMAG, 0.65f), E(P_GRIP, 0.65f), E(P_FLASH, 0.60f),
                              E(P_POUCH, 0.60f), E(P_REDDOT, 0.65f), E(P_SUPPRESS, 0.55f) });

            Build("Cluster_Consumable_Mixed",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 45, maxRespawnCount: 0,
                maxActive: 4, scatter: 3f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 2, minT: 1, maxT: 2,
                fixd: None,
                rand: new[] { E(P_MEDKIT, 0.70f), E(P_ENERGY, 0.80f) });

            Build("Cluster_Weapon_Scattered",
                WorldSpawnType.Item, canRespawn: true, respawnTime: 90, maxRespawnCount: 0,
                maxActive: 3, scatter: 4f, locked: false, hold: false,
                SpawnTableMode.RandomOnly, minR: 1, maxR: 1, minT: 1, maxT: 1,
                fixd: None,
                rand: new[] { E(P_PISTOL, 0.60f), E(P_AK47, 0.35f), E(P_M4, 0.30f) });

            AssetDatabase.SaveAssets();
            Log("  Step 4 done — 34 item spawns generated.");
        }

        // ──────────────────────────────────────────────────────────────────────
        //  STEP 5 — 20 Boss/Zone drop configs
        // ──────────────────────────────────────────────────────────────────────

        private void StepGenerateBossDrops()
        {
            Log("── Step 5: Generating 20 boss/zone drop configs ──");

            // ── Boss tier drops ──────────────────────────────────────────────

            BuildBoss("BossDrop_Tier1_CommonLoot",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 3, maxT: 5,
                fixd: new[] { E(P_MEDKIT, 1f, 2, 2), E(P_ENERGY, 1f) },
                rand: new[] { E(P_PISTOL, 0.70f), E(P_REDDOT, 0.60f), E(P_EXTMAG, 0.60f) });

            BuildBoss("BossDrop_Tier1_Medical",
                SpawnTableMode.FixedOnly, minR: 0, maxR: 0, minT: 5, maxT: 5,
                fixd: new[] { E(P_MEDKIT, 1f, 3, 3), E(P_ENERGY, 1f, 2, 2) },
                rand: None);

            BuildBoss("BossDrop_Tier2_Rifle",
                SpawnTableMode.FixedPlusRandom, minR: 2, maxR: 3, minT: 3, maxT: 5,
                fixd: new[] { E(P_AK47, 1f), E(P_MEDKIT, 1f) },
                rand: new[] { E(P_GRIP, 0.80f), E(P_REDDOT, 0.80f), E(P_SUPPRESS, 0.60f) });

            BuildBoss("BossDrop_Tier2_Armor",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 3, minT: 3, maxT: 5,
                fixd: new[] { E(P_HELMET, 1f), E(P_VEST, 1f) },
                rand: new[] { E(P_GLOVES, 0.80f), E(P_BACKPACK, 0.60f), E(P_BELT, 0.70f) });

            BuildBoss("BossDrop_Tier2_FullKit",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 4, maxT: 6,
                fixd: new[] { E(P_AK47, 1f), E(P_HELMET, 1f), E(P_VEST, 1f) },
                rand: new[] { E(P_GRIP, 0.80f), E(P_BACKPACK, 0.60f), E(P_MEDKIT, 0.80f) });

            BuildBoss("BossDrop_Tier3_Elite",
                SpawnTableMode.FixedPlusRandom, minR: 3, maxR: 5, minT: 6, maxT: 9,
                fixd: new[] { E(P_M4, 1f), E(P_HELMET, 1f), E(P_VEST, 1f), E(P_BACKPACK, 1f) },
                rand: new[] { E(P_GRIP, 0.90f), E(P_REDDOT, 0.90f), E(P_SUPPRESS, 0.85f),
                              E(P_EXTMAG, 0.85f), E(P_GLOVES, 0.80f), E(P_BELT, 0.75f) });

            BuildBoss("BossDrop_Tier3_AllWeapons",
                SpawnTableMode.FixedPlusRandom, minR: 3, maxR: 6, minT: 5, maxT: 9,
                fixd: new[] { E(P_AK47, 1f), E(P_M4, 1f), E(P_PISTOL, 1f) },
                rand: new[] { E(P_GRIP, 0.90f), E(P_REDDOT, 0.90f), E(P_SUPPRESS, 0.90f),
                              E(P_EXTMAG, 0.90f), E(P_FLASH, 0.80f), E(P_POUCH, 0.80f) });

            // ── Zone phase rewards ───────────────────────────────────────────

            BuildBoss("Zone_Phase1_Reward",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 3, minT: 2, maxT: 5,
                fixd: new[] { E(P_MEDKIT, 1f, 2, 2) },
                rand: new[] { E(P_REDDOT, 0.60f), E(P_ENERGY, 0.70f), E(P_HELMET, 0.50f) });

            BuildBoss("Zone_Phase2_Reward",
                SpawnTableMode.FixedPlusRandom, minR: 2, maxR: 3, minT: 3, maxT: 5,
                fixd: new[] { E(P_MEDKIT, 1f, 2, 2), E(P_VEST, 1f) },
                rand: new[] { E(P_AK47, 0.65f), E(P_M4, 0.60f), E(P_GRIP, 0.70f), E(P_BACKPACK, 0.50f) });

            BuildBoss("Zone_Phase3_Reward",
                SpawnTableMode.FixedPlusRandom, minR: 3, maxR: 6, minT: 5, maxT: 9,
                fixd: new[] { E(P_M4, 1f), E(P_MEDKIT, 1f, 3, 3), E(P_HELMET, 1f), E(P_VEST, 1f) },
                rand: new[] { E(P_GRIP, 0.90f), E(P_REDDOT, 0.90f), E(P_SUPPRESS, 0.85f),
                              E(P_EXTMAG, 0.90f), E(P_BACKPACK, 0.80f), E(P_GLOVES, 0.80f) });

            // ── Zone capture / clear rewards ─────────────────────────────────

            BuildBoss("Zone_Capture_Standard",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 2, maxT: 4,
                fixd: new[] { E(P_HELMET, 1f), E(P_VEST, 1f) },
                rand: new[] { E(P_ENERGY, 0.80f), E(P_MEDKIT, 0.70f) });

            BuildBoss("Zone_Capture_Elite",
                SpawnTableMode.FixedPlusRandom, minR: 2, maxR: 4, minT: 4, maxT: 7,
                fixd: new[] { E(P_HELMET, 1f), E(P_VEST, 1f), E(P_BACKPACK, 1f), E(P_GLOVES, 1f) },
                rand: new[] { E(P_AK47, 0.70f), E(P_M4, 0.65f), E(P_GRIP, 0.80f), E(P_REDDOT, 0.80f) });

            BuildBoss("Zone_Clear_Beacon",
                SpawnTableMode.FixedPlusRandom, minR: 0, maxR: 1, minT: 2, maxT: 3,
                fixd: new[] { E(P_BEACON, 1f), E(P_MEDKIT, 1f, 2, 2) },
                rand: new[] { E(P_ENERGY, 0.70f) });

            BuildBoss("Zone_BeaconGuardian_Drop",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 2, maxT: 4,
                fixd: new[] { E(P_MEDKIT, 1f), E(P_ENERGY, 1f) },
                rand: new[] { E(P_PISTOL, 0.50f), E(P_AK47, 0.30f), E(P_HELMET, 0.60f), E(P_VEST, 0.55f) });

            // ── Supply drops ─────────────────────────────────────────────────

            BuildBoss("SupplyDrop_Common",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 3, minT: 3, maxT: 5,
                fixd: new[] { E(P_MEDKIT, 1f, 2, 2), E(P_ENERGY, 1f) },
                rand: new[] { E(P_PISTOL, 0.60f), E(P_AK47, 0.40f), E(P_HELMET, 0.50f), E(P_VEST, 0.45f) });

            BuildBoss("SupplyDrop_Rare",
                SpawnTableMode.FixedPlusRandom, minR: 3, maxR: 5, minT: 4, maxT: 7,
                fixd: new[] { E(P_M4, 1f), E(P_MEDKIT, 1f, 3, 3) },
                rand: new[] { E(P_GRIP, 0.85f), E(P_REDDOT, 0.85f), E(P_SUPPRESS, 0.80f),
                              E(P_HELMET, 0.75f), E(P_VEST, 0.75f), E(P_BACKPACK, 0.65f) });

            // ── Hidden caches ────────────────────────────────────────────────

            BuildBoss("Cache_Hidden_A",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 2, minT: 4, maxT: 6,
                fixd: new[] { E(P_EXTMAG, 1f), E(P_GRIP, 1f), E(P_REDDOT, 1f), E(P_SUPPRESS, 1f) },
                rand: new[] { E(P_MEDKIT, 0.80f), E(P_ENERGY, 0.75f) });

            BuildBoss("Cache_Hidden_B",
                SpawnTableMode.FixedPlusRandom, minR: 1, maxR: 3, minT: 3, maxT: 5,
                fixd: new[] { E(P_AK47, 1f), E(P_MEDKIT, 1f, 2, 2) },
                rand: new[] { E(P_HELMET, 0.65f), E(P_VEST, 0.60f), E(P_BACKPACK, 0.50f), E(P_GRIP, 0.70f) });

            // ── Special / Event ──────────────────────────────────────────────

            BuildBoss("Event_Special_Crate",
                SpawnTableMode.FixedPlusRandom, minR: 3, maxR: 6, minT: 5, maxT: 9,
                fixd: new[] { E(P_MEDKIT, 1f, 3, 3), E(P_BEACON, 1f) },
                rand: new[] { E(P_AK47, 0.75f), E(P_M4, 0.70f), E(P_HELMET, 0.80f),
                              E(P_VEST, 0.80f), E(P_BACKPACK, 0.70f), E(P_GRIP, 0.85f),
                              E(P_REDDOT, 0.85f), E(P_SUPPRESS, 0.80f) });

            BuildBoss("FinalZone_Jackpot",
                SpawnTableMode.FixedOnly, minR: 0, maxR: 0, minT: 12, maxT: 12,
                fixd: new[] {
                    E(P_M4, 1f), E(P_AK47, 1f), E(P_PISTOL, 1f),
                    E(P_HELMET, 1f), E(P_VEST, 1f), E(P_BACKPACK, 1f), E(P_GLOVES, 1f), E(P_BELT, 1f),
                    E(P_MEDKIT, 1f, 3, 3),
                    E(P_EXTMAG, 1f), E(P_GRIP, 1f), E(P_REDDOT, 1f), E(P_SUPPRESS, 1f)
                },
                rand: None);

            AssetDatabase.SaveAssets();
            Log("  Step 5 done — 20 boss/zone configs generated.");
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Build helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Convenience helper returning a single LootItemEntry (callers wrap in arrays when needed).</summary>
        private LootItemEntry E(string path, float chance, int min = 1, int max = 1) =>
            Entry(path, chance, min, max);

        private static LootItemEntry[] None => System.Array.Empty<LootItemEntry>();

        /// <summary>Creates an item-spawn WorldSpawnConfig + SpawnTable pair in the Items subdirs.</summary>
        private void Build(
            string name,
            WorldSpawnType spawnType,
            bool   canRespawn,    float respawnTime,    int maxRespawnCount,
            int    maxActive,     float scatter,        bool locked,    bool hold,
            SpawnTableMode mode,  int minR, int maxR,   int minT, int maxT,
            LootItemEntry[] fixd, LootItemEntry[] rand)
        {
            var table = MakeSpawnTable(name, TABLE_ITEMS, mode, minR, maxR, minT, maxT, fixd, rand);
            MakeSpawnConfig(name, CONFIG_ITEMS, spawnType, canRespawn, respawnTime, maxRespawnCount,
                maxActive, scatter, locked, hold, table);
        }

        /// <summary>Creates a boss/zone Config + Table pair (Container, no respawn) in Boss subdirs.</summary>
        private void BuildBoss(
            string name,
            SpawnTableMode mode, int minR, int maxR, int minT, int maxT,
            LootItemEntry[] fixd, LootItemEntry[] rand)
        {
            var table = MakeSpawnTable(name, TABLE_BOSS, mode, minR, maxR, minT, maxT, fixd, rand);
            MakeSpawnConfig(name, CONFIG_BOSS,
                WorldSpawnType.Container,
                canRespawn: false, respawnTime: 120f, maxRespawnCount: 1,
                maxActive: 1, scatter: 0f, locked: false, hold: true, table);
        }

        private SpawnTable MakeSpawnTable(
            string name, string folder,
            SpawnTableMode mode, int minR, int maxR, int minT, int maxT,
            LootItemEntry[] fixd, LootItemEntry[] rand)
        {
            string path = folder + "/SpawnTable_" + name + ".asset";
            var tbl = GetOrCreate<SpawnTable>(path, out bool created);
            if (!created) return tbl;

            tbl.Mode            = mode;
            tbl.MinRandomCount  = minR;
            tbl.MaxRandomCount  = maxR;
            tbl.MinTotalItems   = minT;
            tbl.MaxTotalItems   = maxT;
            tbl.RollOnOpen      = true;
            tbl.DropToWorldOnOpen = false;

            tbl.FixedEntries  = new List<LootItemEntry>(fixd ?? System.Array.Empty<LootItemEntry>());
            tbl.RandomEntries = new List<LootItemEntry>(rand ?? System.Array.Empty<LootItemEntry>());

            EditorUtility.SetDirty(tbl);
            Log($"  + SpawnTable_{name}");
            return tbl;
        }

        private void MakeSpawnConfig(
            string name, string folder,
            WorldSpawnType spawnType,
            bool canRespawn, float respawnTime, int maxRespawnCount,
            int maxActive, float scatter, bool locked, bool hold,
            SpawnTable spawnTable)
        {
            string path = folder + "/WorldSpawnConfig_" + name + ".asset";
            var cfg = GetOrCreate<WorldSpawnConfig>(path, out bool created);
            if (!created) return;

            cfg.SpawnType           = spawnType;
            cfg.SpawnTable          = spawnTable;
            cfg.CanRespawn          = canRespawn;
            cfg.RespawnTime         = respawnTime;
            cfg.MaxRespawnCount     = maxRespawnCount;
            cfg.MaxActive           = maxActive;
            cfg.ScatterRadius       = scatter;
            cfg.SpawnLocked         = locked;
            cfg.ContainerAutoReset  = false;
            cfg.ContainerResetDelay = 60f;
            cfg.LootableConfig      = LoadLootable(hold);

            EditorUtility.SetDirty(cfg);
            Log($"  + WorldSpawnConfig_{name}");
        }

        private static LootItemEntry Entry(string itemPath, float chance, int min = 1, int max = 1)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (item == null)
                Debug.LogWarning($"[NightHuntDataMigration] Item not found at: {itemPath}");

            return new LootItemEntry { Item = item, MinQuantity = min, MaxQuantity = max, Chance = chance };
        }

        private LootableConfig LoadLootable(bool hold)
        {
            string path = hold
                ? LOOTABLE_DIR + "/LootableConfig_Hold.asset"
                : LOOTABLE_DIR + "/LootableConfig_Instant.asset";

            var cfg = AssetDatabase.LoadAssetAtPath<LootableConfig>(path);
            if (cfg == null)
                Log($"  ⚠ LootableConfig not found ({path}). Run Step 3 first.");
            return cfg;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Asset / folder utilities
        // ──────────────────────────────────────────────────────────────────────

        private T GetOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                created = false;
                return existing;
            }
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            created = true;
            return so;
        }

        /// <summary>Ensures every segment of <paramref name="fullPath"/> exists as an AssetDatabase folder.</summary>
        private static void EnsureFolderPath(string fullPath)
        {
            string[] parts = fullPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }

        private void Log(string msg)
        {
            string line = $"[{System.DateTime.Now:HH:mm:ss}]  {msg}";
            _log.Add(line);
            Debug.Log($"[NightHuntMigration] {msg}");
            Repaint();
        }
    }
}
