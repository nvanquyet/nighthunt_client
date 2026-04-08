using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Rebuilds the entire canonical Item Database from the NightHunt_Full_GameDesign_Config_v3.json spec.
    ///
    /// Menu: NightHunt / JSON Item Rebuild / ◆ Full Rebuild (All Steps)
    ///   Step 1 — Remove stale items that no longer match the JSON config IDs
    ///   Step 2 — Create / overwrite all item definitions + stat configs from JSON data
    ///   Step 3 — Scan Resources folder and populate ItemDatabase._itemDefinitions
    ///
    /// After running, assign Icons & Prefabs via Inspector on each generated asset.
    /// </summary>
    public static class ItemDatabaseJsonRebuildTool
    {
        // ── Output paths ──────────────────────────────────────────────────────
        const string BASE    = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items";
        const string CONFIGS = "Assets/_Night_Hunt/Data/Configs/StatSystem Config/Items";
        const string DB_PATH = "Assets/_Night_Hunt/Data/Resources/Database/Items/ItemDatabase.asset";

        // ── Stale assets to remove (wrong IDs / wrong type / not in JSON config) ─
        static readonly string[] StaleAssets = new[]
        {
            // Weapons not matching JSON IDs
            $"{BASE}/Weapons/Weapon_AK47.asset",
            $"{BASE}/Weapons/Weapon_M4.asset",
            $"{BASE}/Weapons/Weapon_Pistol.asset",
            // Consumables wrong ID / wrong type
            $"{BASE}/Consumables/BeaconDefinition.asset",
            $"{BASE}/Consumables/Consumable_EnergyDrink.asset",
            $"{BASE}/Consumables/Consumable_Medkit.asset",
            // Old StatConfigs that accompanied the stale items (flat Items/ level)
            $"{CONFIGS}/ConsumableStatConfig_EnergyDrink.asset",
            $"{CONFIGS}/ConsumableStatConfig_Medkit.asset",
            // Throwables whose ItemID doesn't match JSON UTIL_SMOKE / UTIL_EMP
            $"{BASE}/Throwable/Throwable_SmokeGrenade.asset",
        };

        // ═════════════════════════════════════════════════════════════════════
        //  MENU ITEMS
        // ═════════════════════════════════════════════════════════════════════

        [MenuItem("NightHunt/JSON Item Rebuild/◆ Full Rebuild (All Steps)", priority = 5)]
        static void FullRebuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "JSON Item Rebuild — Full",
                    "This will:\n" +
                    "  1. Delete stale / mismatched item assets\n" +
                    "  2. Create/overwrite all items from JSON config\n" +
                    "  3. Populate ItemDatabase._itemDefinitions\n\n" +
                    "Prefabs and Icons must be re-assigned in Inspector.",
                    "◆ Rebuild", "Cancel")) return;

            int deleted  = Step1_CleanStale();
            int created  = Step2_GenerateFromJson();
            int registered = Step3_PopulateDatabase();

            EditorUtility.DisplayDialog(
                "Rebuild Complete",
                $"Removed: {deleted}  |  Created/updated: {created}  |  Registered: {registered}\n\n" +
                "Re-assign Icons & Prefabs in the Inspector on each new asset.\n" +
                "Equipment & Attachment assets were not touched.",
                "OK");
        }

        [MenuItem("NightHunt/JSON Item Rebuild/Step 1 — Clean Stale Items", priority = 11)]
        static void MenuStep1() { int n = Step1_CleanStale(); Debug.Log($"[JsonRebuild] Step 1 done — removed {n} stale asset(s)."); }

        [MenuItem("NightHunt/JSON Item Rebuild/Step 2 — Generate All Items from JSON", priority = 12)]
        static void MenuStep2() { int n = Step2_GenerateFromJson(); Debug.Log($"[JsonRebuild] Step 2 done — {n} asset(s) created/updated."); }

        [MenuItem("NightHunt/JSON Item Rebuild/Step 3 — Populate ItemDatabase", priority = 13)]
        static void MenuStep3() { int n = Step3_PopulateDatabase(); Debug.Log($"[JsonRebuild] Step 3 done — {n} definitions registered."); }

        // ═════════════════════════════════════════════════════════════════════
        //  STEP 1 — Delete stale assets
        // ═════════════════════════════════════════════════════════════════════

        static int Step1_CleanStale()
        {
            int deleted = 0;
            foreach (var path in StaleAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        Debug.Log($"[JsonRebuild] Deleted: {path}");
                        deleted++;
                    }
                }
            }
            AssetDatabase.Refresh();
            return deleted;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  STEP 2 — Generate all items from JSON spec
        // ═════════════════════════════════════════════════════════════════════

        static int Step2_GenerateFromJson()
        {
            EnsureDirs();
            int n = 0;
            n += BuildWeapons();
            n += BuildConsumables();
            n += BuildDeployables();
            n += BuildThrowables();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return n;
        }

        // ── Weapons ── (source: WeaponConfig in NightHunt_Full_GameDesign_Config_v3.json) ─

        static int BuildWeapons()
        {
            int n = 0;

            // PISTOL_9MM — DamageBody:18, FireRate:4/s(240RPM), Mag:15, Reserve:45, Reload:1.8s
            //              SpreadBase:0.1→3°, SpreadMoveMul:1.2, MoveSpeedMul:1.0, Weight:1.2, Common
            n += MakeWeapon("PISTOL_9MM", "9mm Pistol", WeaponClass.Pistol, ItemRarity.Common,
                damage:18f, fireRPM:240f, mag:15, reserve:45, reload:1.8f,
                spread:3f, spreadPenalty:1.2f, weight:1.2f, moveSpeedPct:0f);

            // SMG_MP5 — DamageBody:15, FireRate:10/s(600RPM), Mag:30, Reserve:90, Reload:2.2s
            //           SpreadBase:0.2→6°, SpreadMoveMul:1.3, MoveSpeedMul:0.92(-8%), Weight:2.8, Common
            n += MakeWeapon("SMG_MP5", "MP5", WeaponClass.SMG, ItemRarity.Common,
                damage:15f, fireRPM:600f, mag:30, reserve:90, reload:2.2f,
                spread:6f, spreadPenalty:1.3f, weight:2.8f, moveSpeedPct:-0.08f);

            // RIFLE_AK — DamageBody:25, FireRate:8/s(480RPM), Mag:30, Reserve:120, Reload:2.4s
            //            SpreadBase:0.25→7.5°, SpreadMoveMul:1.4, MoveSpeedMul:0.87(-13%), Weight:4, Rare
            n += MakeWeapon("RIFLE_AK", "AK Rifle", WeaponClass.Rifle, ItemRarity.Rare,
                damage:25f, fireRPM:480f, mag:30, reserve:120, reload:2.4f,
                spread:7.5f, spreadPenalty:1.4f, weight:4.0f, moveSpeedPct:-0.13f);

            // SHOT_PUMP — DamageBody:10(×pellets), FireRate:1.2/s(72RPM), Mag:6, Reserve:24, Reload:2.8s
            //             SpreadBase:0.3→9°, SpreadMoveMul:1.5, MoveSpeedMul:0.75(-25%), Weight:4.5, Rare
            n += MakeWeapon("SHOT_PUMP", "Pump Shotgun", WeaponClass.Shotgun, ItemRarity.Rare,
                damage:10f, fireRPM:72f, mag:6, reserve:24, reload:2.8f,
                spread:9f, spreadPenalty:1.5f, weight:4.5f, moveSpeedPct:-0.25f);

            // SNIPER_M24 — DamageBody:80, FireRate:0.8/s(48RPM), Mag:5, Reserve:15, Reload:3.0s
            //              SpreadBase:0.18→0.5°, SpreadMoveMul:1.6, MoveSpeedMul:0.7(-30%), Weight:5.2, Epic
            n += MakeWeapon("SNIPER_M24", "M24 Sniper", WeaponClass.Sniper, ItemRarity.Epic,
                damage:80f, fireRPM:48f, mag:5, reserve:15, reload:3.0f,
                spread:0.5f, spreadPenalty:1.6f, weight:5.2f, moveSpeedPct:-0.30f);

            // HEAVY_RPG — DamageBody:140, FireRate:0.5/s(30RPM), Mag:1, Reserve:1, Reload:4.5s
            //             SpreadBase:0.15→4.5°, SpreadMoveMul:1.8, MoveSpeedMul:0.6(-40%), Weight:8, Legendary
            n += MakeWeapon("HEAVY_RPG", "RPG Launcher", WeaponClass.Launcher, ItemRarity.Legendary,
                damage:140f, fireRPM:30f, mag:1, reserve:1, reload:4.5f,
                spread:4.5f, spreadPenalty:1.8f, weight:8.0f, moveSpeedPct:-0.40f);

            return n;
        }

        // ── Consumables ── (Instant / Channel UseType from JSON ItemConfig) ──

        static int BuildConsumables()
        {
            int n = 0;

            // HEAL_BANDAGE — Instant HealHP 25, CastTime 2s, Stack 5, Weight 0.2, Common
            n += MakeConsumable("HEAL_BANDAGE", "Bandage", ItemRarity.Common,
                weight:0.2f, castTime:2f, maxStack:5,
                effects: new[] { Eff(ConsumableEffectType.RestoreHealth, PlayerStatType.Health,
                    25f, 0f, true, "Restore 25 HP") });

            // HEAL_MEDKIT — Channel HealHP 70, CastTime 4s, Stack 2, Weight 0.8, Rare
            n += MakeConsumable("HEAL_MEDKIT", "Medkit", ItemRarity.Rare,
                weight:0.8f, castTime:4f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.RestoreHealth, PlayerStatType.Health,
                    70f, 0f, false, "Restore 70 HP") });

            // STAM_DRINK — Instant StaminaOverTime 60/10s, CastTime 1.5s, Stack 3, Weight 0.3, Common
            n += MakeConsumable("STAM_DRINK", "Energy Drink", ItemRarity.Common,
                weight:0.3f, castTime:1.5f, maxStack:3,
                effects: new[] { Eff(ConsumableEffectType.StaminaOverTime, PlayerStatType.Stamina,
                    60f, 10f, false, "+60 Stamina over 10s") });

            // BUFF_SPEED — Instant SpeedBuff +15%/8s, CastTime 1s, Stack 3, Weight 0.4, Rare
            n += MakeConsumable("BUFF_SPEED", "Speed Boost", ItemRarity.Rare,
                weight:0.4f, castTime:1f, maxStack:3,
                effects: new[] { Eff(ConsumableEffectType.SpeedBoost, PlayerStatType.MovementSpeed,
                    0.15f, 8f, false, "+15% speed for 8s", buffId:"buff_speed") });

            // BUFF_SILENT — Instant NoiseReduce 50%/10s, CastTime 1s, Stack 2, Weight 0.4, Rare
            n += MakeConsumable("BUFF_SILENT", "Silent Step", ItemRarity.Rare,
                weight:0.4f, castTime:1f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.NoiseReduce, PlayerStatType.Health,
                    0.5f, 10f, false, "-50% noise radius for 10s", buffId:"buff_silent") });

            // CLEANSE_ANTIDOTE — Instant Cleanse all debuffs, CastTime 1s, Stack 2, Weight 0.3, Common
            n += MakeConsumable("CLEANSE_ANTIDOTE", "Antidote", ItemRarity.Common,
                weight:0.3f, castTime:1f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.Cure, PlayerStatType.Health,
                    1f, 0f, true, "Cleanse all debuffs") });

            // VISION_EYE — Instant VisionIncrease +5/10s, CastTime 0.5s, Stack 2, Weight 0.2, Common
            n += MakeConsumable("VISION_EYE", "Eye Vision", ItemRarity.Common,
                weight:0.2f, castTime:0.5f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.VisionIncrease, PlayerStatType.VisionRange,
                    5f, 10f, false, "+5 vision range for 10s") });

            // KEY_CORE — Instant, UnlockObjective, Stack 1, Weight 0.1, Legendary, not droppable
            n += MakeConsumable("KEY_CORE", "Energy Core Key", ItemRarity.Legendary,
                weight:0.1f, castTime:0f, maxStack:1,
                effects: new[] { Eff(ConsumableEffectType.UnlockObjective, PlayerStatType.Health,
                    1f, 0f, true, "Unlock energy core objective") });

            return n;
        }

        // ── Deployables ── (PlaceOnGround UseType from JSON ItemConfig) ───────

        static int BuildDeployables()
        {
            int n = 0;

            // DEPLOY_BEACON — PlaceOnGround, CastTime 5s, Stack 1, Weight 2.5, Uncommon
            n += MakeConsumable("DEPLOY_BEACON", "Respawn Beacon", ItemRarity.Uncommon,
                weight:2.5f, castTime:5f, maxStack:1,
                effects: new[] { Eff(ConsumableEffectType.DeployBeacon, PlayerStatType.Health,
                    1f, 0f, true, "Place respawn beacon") },
                prefix: "Deployable");

            // VISION_LIGHTPOINT — PlaceOnGround, LightArea 12/25s, CastTime 1s, Stack 2, Weight 0.6, Rare
            n += MakeConsumable("VISION_LIGHTPOINT", "Light Point", ItemRarity.Rare,
                weight:0.6f, castTime:1f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.PlaceVisionNode, PlayerStatType.VisionRange,
                    12f, 25f, false, "Place light beacon for 25s") },
                prefix: "Deployable");

            // VISION_NODE — PlaceOnGround, PlaceVisionNode 15/30s, CastTime 2s, Stack 2, Weight 0.7, Epic
            n += MakeConsumable("VISION_NODE", "Vision Scanner Node", ItemRarity.Epic,
                weight:0.7f, castTime:2f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.PlaceVisionNode, PlayerStatType.VisionRange,
                    15f, 30f, false, "Place vision scanner for 30s") },
                prefix: "Deployable");

            // TRAP_MINE — PlaceOnGround, ExplosiveTrap damage 60, CastTime 2.5s, Stack 3, Weight 1.0, Rare
            n += MakeConsumable("TRAP_MINE", "Explosive Mine", ItemRarity.Rare,
                weight:1.0f, castTime:2.5f, maxStack:3,
                effects: new[] { Eff(ConsumableEffectType.PlaceExplosiveTrap, PlayerStatType.Health,
                    60f, 0f, true, "Place proximity explosive mine") },
                prefix: "Deployable");

            // TRAP_SHOCK — PlaceOnGround, SlowField 40%/12s, CastTime 2s, Stack 2, Weight 0.7, Rare
            n += MakeConsumable("TRAP_SHOCK", "Shock Field", ItemRarity.Rare,
                weight:0.7f, castTime:2f, maxStack:2,
                effects: new[] { Eff(ConsumableEffectType.PlaceSlowField, PlayerStatType.MovementSpeed,
                    0.4f, 12f, false, "Place 40% slow field for 12s") },
                prefix: "Deployable");

            return n;
        }

        // ── Throwables ── (Throw UseType from JSON ItemConfig) ────────────────

        static int BuildThrowables()
        {
            int n = 0;

            // UTIL_SMOKE — SmokeScreen, Throw, Stack 3, Weight 0.6, Common
            //              BlastRadius 8, FuseTime 2s, Duration 6s
            n += MakeThrowable("UTIL_SMOKE", "Smoke Grenade", ItemRarity.Common,
                weight:0.6f, throwForce:12f, fuseTime:2f, blastRadius:8f, damage:0f,
                type:ThrowableType.Smoke, maxStack:3);

            // UTIL_EMP — DisableBeacon/EMP, Throw, Stack 1, Weight 0.9, Epic
            //            BlastRadius 12, FuseTime 2s, Duration 4s
            n += MakeThrowable("UTIL_EMP", "EMP Grenade", ItemRarity.Epic,
                weight:0.9f, throwForce:10f, fuseTime:2f, blastRadius:12f, damage:0f,
                type:ThrowableType.Impact, maxStack:1);

            return n;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  STEP 3 — Populate ItemDatabase._itemDefinitions
        // ═════════════════════════════════════════════════════════════════════

        static int Step3_PopulateDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DB_PATH);
            if (db == null)
            {
                Debug.LogError($"[JsonRebuild] ItemDatabase not found at: {DB_PATH}");
                return 0;
            }

            var guids = AssetDatabase.FindAssets("t:ItemDefinition",
                new[] { "Assets/_Night_Hunt/Data/Resources" });

            var defs = new System.Collections.Generic.List<ItemDefinition>();
            foreach (var g in guids)
            {
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    AssetDatabase.GUIDToAssetPath(g));
                if (def != null && !string.IsNullOrEmpty(def.ItemID))
                    defs.Add(def);
            }

            var so   = new SerializedObject(db);
            var prop = so.FindProperty("_itemDefinitions");
            prop.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            foreach (var d in defs)
                Debug.Log($"[JsonRebuild]  ► {d.ItemID} ({d.Type}) [{d.name}]");

            return defs.Count;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ASSET MAKERS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates WeaponStatConfig + WeaponDefinition, both named after the JSON WeaponId.
        /// Returns 2 (one config + one definition).
        /// </summary>
        static int MakeWeapon(
            string id, string displayName, WeaponClass weapClass, ItemRarity rarity,
            float damage, float fireRPM, int mag, int reserve, float reload,
            float spread, float spreadPenalty, float weight, float moveSpeedPct)
        {
            string cfgPath = $"{CONFIGS}/WeaponStatConfig_{id}.asset";
            string defPath = $"{BASE}/Weapons/Weapon_{id}.asset";

            // WeaponStatConfig
            var cfg = CreateOrLoad<WeaponStatConfig>(cfgPath);
            cfg.Stats = new[]
            {
                Stat(ItemStatType.Damage,        damage,       0f,    9999f),
                Stat(ItemStatType.FireRate,       fireRPM,      1f,    6000f),
                Stat(ItemStatType.MagazineSize,   mag,          0f,    999f),
                Stat(ItemStatType.MaxAmmo,        reserve,      0f,    9999f),
                Stat(ItemStatType.ReloadSpeed,    reload,       0.1f,  15f),
                Stat(ItemStatType.SpreadBase,     spread,       0f,    90f),
                Stat(ItemStatType.SpreadPenalty,  spreadPenalty,0f,    10f),
            };
            cfg.PlayerModifiers = moveSpeedPct != 0f
                ? new[] { Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, moveSpeedPct, $"{id} move penalty") }
                : Array.Empty<PlayerStatModifier>();
            cfg.ItemModifiers = Array.Empty<ItemStatModifier>();
            EditorUtility.SetDirty(cfg);

            // WeaponDefinition
            var def = CreateOrLoad<WeaponDefinition>(defPath);
            def.ItemID       = id;
            def.DisplayName  = displayName;
            def.Description  = string.Empty;
            def.Rarity       = rarity;
            def.IsStackable  = false;
            def.MaxStackSize = 1;
            def.Weight       = weight;
            def.StatConfig   = cfg;
            def.WeaponClass  = weapClass;
            def.ValidSlots   = new[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
            EditorUtility.SetDirty(def);

            Debug.Log($"[JsonRebuild] Weapon  {id} ({displayName})");
            return 2;
        }

        /// <summary>
        /// Creates ConsumableStatConfig + ConsumableDefinition.
        /// prefix controls the asset filename prefix ("Consumable" or "Deployable").
        /// Returns 2 (config + definition).
        /// </summary>
        static int MakeConsumable(
            string id, string displayName, ItemRarity rarity,
            float weight, float castTime, int maxStack,
            ConsumableEffect[] effects,
            string prefix = "Consumable")
        {
            string cfgPath = $"{CONFIGS}/ConsumableStatConfig_{id}.asset";
            string defPath = $"{BASE}/Consumables/{prefix}_{id}.asset";

            // ConsumableStatConfig
            var cfg = CreateOrLoad<ConsumableStatConfig>(cfgPath);
            cfg.Stats           = Array.Empty<ItemStatConfig.ItemStatDefinition>();
            cfg.PlayerModifiers = Array.Empty<PlayerStatModifier>();
            cfg.ItemModifiers   = Array.Empty<ItemStatModifier>();
            cfg.Effects         = effects;
            EditorUtility.SetDirty(cfg);

            // ConsumableDefinition
            var def = CreateOrLoad<ConsumableDefinition>(defPath);
            def.ItemID         = id;
            def.DisplayName    = displayName;
            def.Description    = string.Empty;
            def.Rarity         = rarity;
            def.IsStackable    = maxStack > 1;
            def.MaxStackSize   = Mathf.Max(1, maxStack);
            def.Weight         = weight;
            def.StatConfig     = cfg;
            def.UsageDuration  = castTime;
            def.CanCancelUsage = true;
            def.ValidSlots     = new[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(def);

            Debug.Log($"[JsonRebuild] {prefix} {id} ({displayName})");
            return 2;
        }

        /// <summary>
        /// Creates a ThrowableDefinition (no separate stat config — stats are inlined).
        /// Returns 1 (definition only).
        /// </summary>
        static int MakeThrowable(
            string id, string displayName, ItemRarity rarity,
            float weight, float throwForce, float fuseTime,
            float blastRadius, float damage,
            ThrowableType type, int maxStack)
        {
            string defPath = $"{BASE}/Throwable/Throwable_{id}.asset";

            var def = CreateOrLoad<ThrowableDefinition>(defPath);
            def.ItemID             = id;
            def.DisplayName        = displayName;
            def.Description        = string.Empty;
            def.Rarity             = rarity;
            def.IsStackable        = maxStack > 1;
            def.MaxStackSize       = Mathf.Max(1, maxStack);
            def.Weight             = weight;
            def.ThrowForce         = Mathf.Max(1f, throwForce);
            def.LaunchAngleDeg     = 45f;
            def.FuseTime           = fuseTime;
            def.ExplosionRadius    = blastRadius;
            def.Damage             = damage;
            def.ThrowableType      = type;
            def.CanBounce          = false;
            def.UsageDuration      = 0.5f;  // prepare/cook time
            def.CanCancelUsage     = true;
            def.ValidSlots         = new[] { SlotLocationType.Inventory };
            EditorUtility.SetDirty(def);

            Debug.Log($"[JsonRebuild] Throwable {id} ({displayName})");
            return 1;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SMALL DSL HELPERS  (match ItemStatSystemGenerator patterns exactly)
        // ═════════════════════════════════════════════════════════════════════

        static ItemStatConfig.ItemStatDefinition Stat(ItemStatType t, float val, float min, float max)
            => new() { Type = t, DefaultValue = val, MinValue = min, MaxValue = max };

        static PlayerStatModifier Mod(PlayerStatType t, ModifierType mt, float v, string desc)
            => new() { StatType = t, ModifierType = mt, Value = v, Description = desc };

        static ConsumableEffect Eff(
            ConsumableEffectType et, PlayerStatType st,
            float v, float dur, bool instant, string desc, string buffId = null)
            => new() { EffectType = et, StatType = st, Value = v, Duration = dur,
                       IsInstant = instant, Description = desc, BuffID = buffId ?? string.Empty };

        // ── Asset utilities ───────────────────────────────────────────────────

        static T CreateOrLoad<T>(string assetPath) where T : ScriptableObject
        {
            EnsureDir(Path.GetDirectoryName(assetPath)!);
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        static void EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        static void EnsureDirs()
        {
            var dirs = new[]
            {
                $"{BASE}/Weapons",
                $"{BASE}/Consumables",
                $"{BASE}/Throwable",
                $"{CONFIGS}",
            };
            foreach (var d in dirs) EnsureDir(d);
        }
    }
}
