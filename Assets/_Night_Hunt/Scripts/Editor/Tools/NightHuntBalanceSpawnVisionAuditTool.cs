using System.Collections.Generic;
using System.IO;
using System.Linq;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.World;
using UnityEditor;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Applies the current NightHunt balance baseline and restores spawn/reward tables
    /// against the real item asset names in the project.
    /// </summary>
    public static class NightHuntBalanceSpawnVisionAuditTool
    {
        private const string RequestPath = "Assets/_Night_Hunt/EditorRunRequests/ApplyBalanceSpawnVision.request";
        private const string ReportPath = "Assets/_Night_Hunt/Reports/BalanceSpawnVisionAuditReport.md";
        private const string ItemRoot = "Assets/_Night_Hunt/Data/Resources/Database/Items";
        private const string SpawnTableRoot = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/SpawnTables";
        private const string WorldConfigRoot = "Assets/_Night_Hunt/Data/Resources/Database/Spawn/WorldSpawnConfigs";
        private const string PlayerStatConfigPath = "Assets/_Night_Hunt/Data/Configs/StatSystem Config/Characters/PlayerStatConfig.asset";
        private const string WeightPenaltyConfigPath = "Assets/_Night_Hunt/Data/Configs/Gameplay Config/WeightPenaltyConfig.asset";

        private static readonly Dictionary<string, TableSpec> TableSpecs = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Ground
            ["SpawnTable_ItemScatter"] = Spec(null,
                "Weapon_Pistol", "Weapon_SMG", "Weapon_AR", "Consumable_HEAL_BANDAGE",
                "Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Armor_Helmet",
                "Armor_Vest", "Throwable_FragGrenade", "Throwable_UTIL_SMOKE",
                "Attachment_ExtMag", "Attachment_Grip", "Attachment_RedDot"),
            ["SpawnTable_Ground_Pistol"] = Spec(null, "Weapon_Pistol"),
            ["SpawnTable_Ground_AR_Common"] = Spec(null, "Weapon_AR"),
            ["SpawnTable_Ground_AR_Mixed"] = Spec(null, "Weapon_AR", "Weapon_SMG"),
            ["SpawnTable_Ground_AR_Rare"] = Spec("Weapon_AR", "Weapon_Sniper", "Weapon_Shotgun"),
            ["SpawnTable_Ground_Medkit_Common"] = Spec(null, "Consumable_HEAL_BANDAGE", "Consumable_HEAL_MEDKIT"),
            ["SpawnTable_Ground_Medkit_Rare"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_CLEANSE_ANTIDOTE"),
            ["SpawnTable_Ground_EnergyDrink_Common"] = Spec(null, "Consumable_STAM_DRINK", "Consumable_BUFF_SPEED"),
            ["SpawnTable_Ground_Beacon"] = Spec(null, "Deployable_DEPLOY_BEACON", "Deployable_VISION_NODE"),
            ["SpawnTable_Ground_Throwable_Mixed"] = Spec(null, "Throwable_FragGrenade", "Throwable_UTIL_SMOKE", "Throwable_UTIL_EMP"),
            ["SpawnTable_Ground_FragGrenade"] = Spec(null, "Throwable_FragGrenade"),
            ["SpawnTable_Ground_SmokeGrenade"] = Spec(null, "Throwable_UTIL_SMOKE"),
            ["SpawnTable_Ground_Attachment_Any"] = Spec(null, "Attachment_ExtMag", "Attachment_Flashlight", "Attachment_Grip", "Attachment_Pouch", "Attachment_RedDot", "Attachment_Suppressor"),
            ["SpawnTable_Ground_Attachment_Optic"] = Spec(null, "Attachment_RedDot", "Attachment_Suppressor"),
            ["SpawnTable_Ground_Attachment_Support"] = Spec(null, "Attachment_ExtMag", "Attachment_Flashlight", "Attachment_Grip", "Attachment_Pouch"),
            ["SpawnTable_Ground_Helmet"] = Spec(null, "Armor_Helmet"),
            ["SpawnTable_Ground_Vest"] = Spec(null, "Armor_Vest"),
            ["SpawnTable_Ground_Gloves"] = Spec(null, "Armor_Gloves"),
            ["SpawnTable_Ground_Belt"] = Spec(null, "Armor_Belt"),
            ["SpawnTable_Ground_Backpack"] = Spec(null, "Armor_Backpack"),

            // Containers / chests
            ["SpawnTable_Crate_Medical"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_HEAL_BANDAGE", "Consumable_STAM_DRINK", "Consumable_CLEANSE_ANTIDOTE"),
            ["SpawnTable_Crate_Weapons_Light"] = Spec("Weapon_Pistol", "Weapon_SMG", "Attachment_RedDot", "Attachment_ExtMag", "Attachment_Suppressor"),
            ["SpawnTable_Crate_Weapons_Heavy"] = Spec("Weapon_AR", "Weapon_Shotgun", "Weapon_Sniper", "Attachment_Grip", "Attachment_ExtMag", "Attachment_Suppressor"),
            ["SpawnTable_Crate_Equipment_Basic"] = Spec("Armor_Helmet", "Armor_Vest", "Armor_Gloves", "Armor_Belt"),
            ["SpawnTable_Crate_Equipment_Full"] = Spec("Armor_Helmet", "Armor_Vest", "Armor_Backpack", "Armor_Gloves", "Armor_Belt"),
            ["SpawnTable_Crate_Utility"] = Spec("Throwable_FragGrenade", "Throwable_UTIL_SMOKE", "Throwable_UTIL_EMP", "Deployable_VISION_NODE"),
            ["SpawnTable_Crate_General_Common"] = Spec(null, "Consumable_HEAL_BANDAGE", "Consumable_STAM_DRINK", "Weapon_Pistol", "Armor_Helmet", "Throwable_UTIL_SMOKE", "Attachment_RedDot"),
            ["SpawnTable_Crate_General_Rare"] = Spec(null, "Weapon_AR", "Weapon_SMG", "Weapon_Shotgun", "Armor_Vest", "Armor_Backpack", "Attachment_Suppressor", "Consumable_KEY_CORE"),
            ["SpawnTable_Chest_Basic"] = Spec(null, "Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Weapon_Pistol", "Armor_Gloves", "Throwable_FragGrenade", "Attachment_ExtMag"),
            ["SpawnTable_Chest_Military"] = Spec("Weapon_AR", "Attachment_Grip", "Attachment_RedDot", "Attachment_Suppressor", "Consumable_HEAL_MEDKIT"),
            ["SpawnTable_Chest_Locked_Basic"] = Spec("Weapon_AR", "Armor_Helmet", "Armor_Vest", "Weapon_SMG", "Attachment_Grip", "Attachment_RedDot"),
            ["SpawnTable_Chest_Locked_Elite"] = Spec("Weapon_AR", "Weapon_Sniper", "Armor_Helmet", "Armor_Vest", "Armor_Backpack", "Weapon_RocketLauncher", "Attachment_Suppressor", "Consumable_KEY_CORE"),
            ["SpawnTable_LockedChest"] = Spec("Weapon_AR", "Armor_Vest", "Consumable_KEY_CORE", "Weapon_Sniper", "Weapon_Shotgun", "Attachment_Suppressor"),
            ["SpawnTable_Container"] = Spec(null, "Consumable_HEAL_BANDAGE", "Consumable_STAM_DRINK", "Weapon_Pistol", "Armor_Helmet", "Attachment_ExtMag"),
            ["SpawnTable_Cluster_Weapon_Scattered"] = Spec(null, "Weapon_Pistol", "Weapon_SMG", "Weapon_AR", "Weapon_Shotgun"),
            ["SpawnTable_Cluster_Consumable_Mixed"] = Spec(null, "Consumable_HEAL_BANDAGE", "Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Consumable_BUFF_SPEED"),
            ["SpawnTable_Cluster_Attachment_Mixed"] = Spec(null, "Attachment_ExtMag", "Attachment_Flashlight", "Attachment_Grip", "Attachment_Pouch", "Attachment_RedDot", "Attachment_Suppressor"),

            // Boss / zone rewards
            ["SpawnTable_BossDrop_Tier1_CommonLoot"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Weapon_Pistol", "Weapon_SMG", "Attachment_ExtMag"),
            ["SpawnTable_BossDrop_Tier1_Medical"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Consumable_CLEANSE_ANTIDOTE"),
            ["SpawnTable_BossDrop_Tier2_Rifle"] = Spec("Weapon_AR", "Consumable_HEAL_MEDKIT", "Weapon_SMG", "Attachment_Grip", "Attachment_RedDot", "Attachment_Suppressor"),
            ["SpawnTable_BossDrop_Tier2_Armor"] = Spec("Armor_Helmet", "Armor_Vest", "Armor_Gloves", "Armor_Backpack", "Armor_Belt"),
            ["SpawnTable_BossDrop_Tier2_FullKit"] = Spec("Weapon_AR", "Armor_Helmet", "Armor_Vest", "Attachment_Grip", "Armor_Backpack", "Consumable_HEAL_MEDKIT"),
            ["SpawnTable_BossDrop_Tier3_Elite"] = Spec("Weapon_Sniper", "Weapon_AR", "Armor_Helmet", "Armor_Vest", "Armor_Backpack", "Weapon_RocketLauncher", "Attachment_Suppressor", "Consumable_KEY_CORE"),
            ["SpawnTable_BossDrop_Tier3_AllWeapons"] = Spec("Weapon_Pistol", "Weapon_SMG", "Weapon_AR", "Weapon_Shotgun", "Weapon_Sniper", "Weapon_RocketLauncher", "Attachment_ExtMag", "Attachment_Grip", "Attachment_RedDot"),
            ["SpawnTable_Zone_Phase1_Reward"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Armor_Helmet", "Attachment_RedDot"),
            ["SpawnTable_Zone_Phase2_Reward"] = Spec("Consumable_HEAL_MEDKIT", "Armor_Vest", "Weapon_SMG", "Weapon_AR", "Attachment_Grip", "Armor_Backpack"),
            ["SpawnTable_Zone_Phase3_Reward"] = Spec("Weapon_AR", "Consumable_HEAL_MEDKIT", "Armor_Helmet", "Armor_Vest", "Weapon_Sniper", "Attachment_Suppressor", "Armor_Backpack"),
            ["SpawnTable_Zone_Capture_Standard"] = Spec("Armor_Helmet", "Armor_Vest", "Consumable_STAM_DRINK", "Consumable_HEAL_MEDKIT"),
            ["SpawnTable_Zone_Capture_Elite"] = Spec("Armor_Helmet", "Armor_Vest", "Armor_Backpack", "Armor_Gloves", "Weapon_AR", "Attachment_RedDot"),
            ["SpawnTable_Zone_Clear_Beacon"] = Spec("Deployable_DEPLOY_BEACON", "Consumable_HEAL_MEDKIT", "Deployable_VISION_NODE", "Consumable_STAM_DRINK"),
            ["SpawnTable_Zone_BeaconGuardian_Drop"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Weapon_Pistol", "Weapon_AR", "Armor_Helmet", "Armor_Vest"),
            ["SpawnTable_SupplyDrop_Common"] = Spec("Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK", "Weapon_SMG", "Weapon_AR", "Armor_Helmet", "Armor_Vest"),
            ["SpawnTable_SupplyDrop_Rare"] = Spec("Weapon_Sniper", "Consumable_HEAL_MEDKIT", "Weapon_AR", "Attachment_Grip", "Attachment_Suppressor", "Armor_Backpack"),
            ["SpawnTable_Cache_Hidden_A"] = Spec("Attachment_ExtMag", "Attachment_Grip", "Attachment_RedDot", "Attachment_Suppressor", "Consumable_HEAL_MEDKIT", "Consumable_STAM_DRINK"),
            ["SpawnTable_Cache_Hidden_B"] = Spec("Weapon_AR", "Consumable_HEAL_MEDKIT", "Armor_Helmet", "Armor_Vest", "Armor_Backpack", "Attachment_Grip"),
            ["SpawnTable_Event_Special_Crate"] = Spec("Consumable_HEAL_MEDKIT", "Deployable_DEPLOY_BEACON", "Weapon_AR", "Weapon_Sniper", "Armor_Vest", "Attachment_Suppressor"),
            ["SpawnTable_FinalZone_Jackpot"] = Spec("Weapon_AR", "Weapon_SMG", "Weapon_Shotgun", "Weapon_Sniper", "Weapon_RocketLauncher", "Armor_Helmet", "Armor_Vest", "Armor_Backpack", "Consumable_HEAL_MEDKIT", "Consumable_KEY_CORE"),
            ["SpawnTable_PhaseTest_AllItems"] = Spec("Weapon_Pistol", "Weapon_SMG", "Weapon_AR", "Weapon_Shotgun", "Weapon_Sniper", "Weapon_RocketLauncher", "Weapon_Melee", "Consumable_HEAL_MEDKIT", "Armor_Vest", "Throwable_FragGrenade", "Deployable_VISION_NODE"),
        };

        [MenuItem("NightHunt/Tools/Apply Balance + Spawn + Vision Baseline")]
        public static void ApplyBaseline()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));

            var log = new List<string>
            {
                "# NightHunt Balance / Spawn / Vision Audit",
                "",
                $"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                ""
            };

            var items = BuildItemLookup(log);
            ApplyPlayerVisionBaseline(log);
            ApplyWeightPenaltyBaseline(log);
            ApplySpawnTableBaseline(items, log);
            ApplyWorldSpawnConfigBaseline(log);
            AuditSpawnTables(log);

            File.WriteAllLines(ReportPath, log);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NightHuntBalanceSpawnVisionAuditTool] Done. Report: {ReportPath}");
        }

        [MenuItem("NightHunt/Tools/Request Apply Balance + Spawn + Vision Baseline")]
        public static void RequestApplyBaseline()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
            File.WriteAllText(RequestPath, System.DateTime.Now.ToString("O"));
            AssetDatabase.Refresh();
            Debug.Log($"[NightHuntBalanceSpawnVisionAuditTool] Request created: {RequestPath}");
        }

        [InitializeOnLoadMethod]
        private static void RunOnceIfRequested()
        {
            if (!File.Exists(RequestPath))
                return;

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestPath))
                    return;

                File.Delete(RequestPath);
                ApplyBaseline();
            };
        }

        private static void ApplyPlayerVisionBaseline(List<string> log)
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerStatConfig>(PlayerStatConfigPath);
            if (config?.Stats == null)
            {
                log.Add("## Player Stats");
                log.Add($"- WARN: missing PlayerStatConfig at `{PlayerStatConfigPath}`.");
                log.Add("");
                return;
            }

            for (int i = 0; i < config.Stats.Length; i++)
            {
                if (config.Stats[i].Type != PlayerStatType.VisionRange)
                    continue;

                var stat = config.Stats[i];
                stat.DefaultValue = 18f;
                stat.MinValue = 0f;
                stat.MaxValue = 80f;
                config.Stats[i] = stat;
                EditorUtility.SetDirty(config);
                log.Add("## Player Stats");
                log.Add("- `VisionRange` baseline set to 18m, max 80m.");
                log.Add("");
                return;
            }
        }

        private static void ApplyWeightPenaltyBaseline(List<string> log)
        {
            var config = AssetDatabase.LoadAssetAtPath<WeightPenaltyConfig>(WeightPenaltyConfigPath);
            if (config == null)
            {
                log.Add("## Weight Penalty");
                log.Add($"- WARN: missing WeightPenaltyConfig at `{WeightPenaltyConfigPath}`.");
                log.Add("");
                return;
            }

            config.NormalBarColor = new Color(0.2f, 0.8f, 0.3f, 1f);
            config.Tiers = new[]
            {
                Tier(0.80f, "Heavy", new Color(0.95f, 0.75f, 0.25f, 1f), -8f),
                Tier(1.00f, "Overweight", new Color(0.95f, 0.35f, 0.18f, 1f), -22f),
                Tier(1.30f, "Overloaded", new Color(0.80f, 0.12f, 0.12f, 1f), -45f),
            };
            EditorUtility.SetDirty(config);

            log.Add("## Weight Penalty");
            log.Add("- Rebuilt tiers: Heavy 80% (-8% speed), Overweight 100% (-22% speed), Overloaded 130% (-45% speed).");
            log.Add("");
        }

        private static WeightPenaltyConfig.PenaltyTier Tier(float threshold, string label, Color color, float speedPercent)
        {
            return new WeightPenaltyConfig.PenaltyTier
            {
                ThresholdRatio = threshold,
                Label = label,
                BarColor = color,
                Modifiers = new[]
                {
                    new PlayerStatModifier
                    {
                        StatType = PlayerStatType.MovementSpeed,
                        Value = speedPercent,
                        ModifierType = ModifierType.Percentage,
                        Description = label + " weight penalty"
                    }
                }
            };
        }

        private static void ApplySpawnTableBaseline(Dictionary<string, ItemDefinition> items, List<string> log)
        {
            log.Add("## Spawn Tables");

            foreach (var pair in TableSpecs)
            {
                var table = FindSpawnTable(pair.Key);
                if (table == null)
                {
                    log.Add($"- WARN: `{pair.Key}` not found.");
                    continue;
                }

                bool force = pair.Key == "SpawnTable_ItemScatter" || pair.Key == "SpawnTable_PhaseTest_AllItems";
                if (!force && table.FixedEntries.Count + table.RandomEntries.Count > 0)
                {
                    log.Add($"- Kept `{pair.Key}`: already configured ({table.FixedEntries.Count} fixed, {table.RandomEntries.Count} random).");
                    continue;
                }

                FillTable(table, pair.Value, items, force, log);
            }

            log.Add("");
        }

        private static void FillTable(SpawnTable table, TableSpec spec, Dictionary<string, ItemDefinition> items, bool force, List<string> log)
        {
            if (force)
            {
                table.FixedEntries.Clear();
                table.RandomEntries.Clear();
            }

            table.Mode = spec.Fixed.Length > 0 && spec.Random.Length > 0
                ? SpawnTableMode.FixedPlusRandom
                : spec.Fixed.Length > 0
                    ? SpawnTableMode.FixedOnly
                    : SpawnTableMode.RandomOnly;
            table.MinRandomCount = spec.Random.Length > 0 ? 1 : 0;
            table.MaxRandomCount = table.name.Contains("BossDrop") || table.name.Contains("FinalZone") || table.name.Contains("Phase3")
                ? 4
                : table.name.Contains("Crate") || table.name.Contains("Chest") || table.name.Contains("Zone_")
                    ? 3
                    : 2;
            table.MinTotalItems = Mathf.Clamp(spec.Fixed.Length > 0 ? spec.Fixed.Length : 1, 1, 8);
            table.MaxTotalItems = Mathf.Clamp(table.MinTotalItems + table.MaxRandomCount, table.MinTotalItems, 12);

            AddEntries(table.FixedEntries, spec.Fixed, items, 1f, log, table.name, "fixed");
            AddEntries(table.RandomEntries, spec.Random, items, 1f, log, table.name, "random");

            EditorUtility.SetDirty(table);
            log.Add($"- Filled `{table.name}`: {table.FixedEntries.Count} fixed, {table.RandomEntries.Count} random.");
        }

        private static void AddEntries(List<LootItemEntry> list, string[] keys, Dictionary<string, ItemDefinition> items, float chance, List<string> log, string tableName, string bucket)
        {
            foreach (string key in keys)
            {
                if (!items.TryGetValue(key, out var item))
                {
                    log.Add($"  - WARN: `{tableName}` {bucket} item `{key}` not found.");
                    continue;
                }

                list.Add(new LootItemEntry
                {
                    Item = item,
                    MinQuantity = 1,
                    MaxQuantity = IsConsumableOrThrowable(key) ? 2 : 1,
                    Chance = chance
                });
            }
        }

        private static void ApplyWorldSpawnConfigBaseline(List<string> log)
        {
            log.Add("## World Spawn Configs");

            EnsureWorldConfig("Items/WorldSpawnConfig_ItemScatter.asset", "SpawnTable_ItemScatter", WorldSpawnType.Item, true, 75f, 3, 2.5f, false, log);
            EnsureWorldConfig("Items/WorldSpawnConfig_Crate_General_Common.asset", "SpawnTable_Crate_General_Common", WorldSpawnType.Container, true, 120f, 1, 0f, false, log);
            EnsureWorldConfig("Items/WorldSpawnConfig_Chest_Locked_Elite.asset", "SpawnTable_Chest_Locked_Elite", WorldSpawnType.Container, false, 180f, 1, 0f, true, log);
            EnsureWorldConfig("Boss/WorldSpawnConfig_Zone_Phase1_Reward.asset", "SpawnTable_Zone_Phase1_Reward", WorldSpawnType.Item, false, 90f, 4, 2f, false, log);
            EnsureWorldConfig("Boss/WorldSpawnConfig_Zone_Phase2_Reward.asset", "SpawnTable_Zone_Phase2_Reward", WorldSpawnType.Item, false, 90f, 5, 2.5f, false, log);
            EnsureWorldConfig("Boss/WorldSpawnConfig_Zone_Phase3_Reward.asset", "SpawnTable_Zone_Phase3_Reward", WorldSpawnType.Item, false, 90f, 6, 3f, false, log);
            EnsureWorldConfig("Boss/WorldSpawnConfig_BossDrop_Tier3_Elite.asset", "SpawnTable_BossDrop_Tier3_Elite", WorldSpawnType.Item, false, 90f, 6, 3f, false, log);

            log.Add("");
        }

        private static void EnsureWorldConfig(string relativePath, string tableName, WorldSpawnType spawnType, bool canRespawn, float respawnTime, int maxActive, float scatterRadius, bool locked, List<string> log)
        {
            string path = $"{WorldConfigRoot}/{relativePath}";
            var config = AssetDatabase.LoadAssetAtPath<WorldSpawnConfig>(path);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                config = ScriptableObject.CreateInstance<WorldSpawnConfig>();
                AssetDatabase.CreateAsset(config, path);
                log.Add($"- Created `{path}`.");
            }

            config.SpawnType = spawnType;
            config.SpawnTable = FindSpawnTable(tableName);
            config.CanRespawn = canRespawn;
            config.RespawnTime = respawnTime;
            config.MaxActive = Mathf.Max(1, maxActive);
            config.ScatterRadius = scatterRadius;
            config.SpawnLocked = locked;
            config.ContainerAutoReset = false;
            EditorUtility.SetDirty(config);
            log.Add($"- Wired `{config.name}` -> `{tableName}` ({spawnType}, maxActive={config.MaxActive}).");
        }

        private static void AuditSpawnTables(List<string> log)
        {
            log.Add("## Audit");

            int empty = 0;
            int missingRefs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SpawnTable", new[] { SpawnTableRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var table = AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
                if (table == null) continue;

                int entries = table.FixedEntries.Count + table.RandomEntries.Count;
                int nulls = table.FixedEntries.Count(e => e.Item == null) + table.RandomEntries.Count(e => e.Item == null);
                if (entries == 0)
                {
                    empty++;
                    log.Add($"- EMPTY: `{path}`.");
                }
                if (nulls > 0)
                {
                    missingRefs += nulls;
                    log.Add($"- MISSING REF: `{path}` has {nulls} null item entries.");
                }
            }

            log.Add($"- Empty tables: {empty}");
            log.Add($"- Null item refs: {missingRefs}");
        }

        private static Dictionary<string, ItemDefinition> BuildItemLookup(List<string> log)
        {
            var lookup = new Dictionary<string, ItemDefinition>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;
                lookup[Path.GetFileNameWithoutExtension(path)] = item;
            }

            log.Add("## Item Database");
            log.Add($"- ItemDefinitions found: {lookup.Count}");
            log.Add("");
            return lookup;
        }

        private static SpawnTable FindSpawnTable(string tableName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{tableName} t:SpawnTable", new[] { SpawnTableRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == tableName)
                    return AssetDatabase.LoadAssetAtPath<SpawnTable>(path);
            }
            return null;
        }

        private static bool IsConsumableOrThrowable(string key) =>
            key.StartsWith("Consumable_", System.StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("Throwable_", System.StringComparison.OrdinalIgnoreCase);

        private static TableSpec Spec(string fixedCsv, params string[] random)
        {
            string[] fixedItems = string.IsNullOrEmpty(fixedCsv)
                ? System.Array.Empty<string>()
                : fixedCsv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            return new TableSpec(fixedItems, random ?? System.Array.Empty<string>());
        }

        private readonly struct TableSpec
        {
            public readonly string[] Fixed;
            public readonly string[] Random;

            public TableSpec(string[] fixedItems, string[] randomItems)
            {
                Fixed = fixedItems;
                Random = randomItems;
            }
        }
    }
}
