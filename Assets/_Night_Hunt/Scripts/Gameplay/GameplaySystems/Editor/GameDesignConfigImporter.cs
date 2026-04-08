#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;

namespace NightHunt.GameplaySystems.Editor
{
    /// <summary>
    /// One-shot bulk generator: reads NightHunt_Full_GameDesign_Config_v3.json
    /// and produces ScriptableObject assets for every item, weapon, and consumable entry.
    ///
    /// Menu: NightHunt / Tools / Game Design Config Importer
    ///
    /// WORKFLOW
    ///   1. Open via menu above.
    ///   2. Optionally adjust the JSON path and output folders.
    ///   3. Press "Import All" — assets created under the output folders.
    ///   4. Open the ItemDatabase asset and press "Refresh from project" if needed.
    ///
    /// RUNTIME LOADING
    ///   NO JSON is loaded at runtime. Generated SOs are referenced from the
    ///   ItemDatabase asset and from MonoBehaviour Inspector fields directly.
    /// </summary>
    public class GameDesignConfigImporter : EditorWindow
    {
        // ── Paths ─────────────────────────────────────────────────────────────

        private const string JSON_PATH      = "Assets/_Night_Hunt/Resources/Data/NightHunt_Full_GameDesign_Config_v3.json";
        // Canonical stat-config output — under Data/Configs (not legacy Resources/Configs).
        private const string STAT_CONFIGS   = "Assets/_Night_Hunt/Data/Configs/StatSystem Config/Items";
        // Item Definition output — matches Data/Resources/Database structure (Resources.LoadAll path).
        private const string OUT_WEAPONS    = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items/Weapons";
        private const string OUT_CONSUMABLE = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items/Consumables";
        private const string OUT_THROWABLE  = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items/Throwable";
        private const string OUT_DEPLOYABLE = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items/Deployables";
        private const string ITEM_DB_PATH   = "Assets/_Night_Hunt/Data/Resources/Database/Items/ItemDatabase.asset";

        // ── Window ────────────────────────────────────────────────────────────

        [MenuItem("NightHunt/Tools/Game Design Config Importer")]
        public static void Open() => GetWindow<GameDesignConfigImporter>("GD Config Importer");

        private Vector2 _scroll;
        private string  _log = "";
        private bool    _overwriteExisting = false;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Game Design Config Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reads " + System.IO.Path.GetFileName(JSON_PATH) + " and generates ScriptableObject assets.\n" +
                "No JSON is loaded at runtime — SOs are the runtime data source.",
                MessageType.Info);

            GUILayout.Space(4);
            _overwriteExisting = EditorGUILayout.Toggle("Overwrite existing assets", _overwriteExisting);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Output folders", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Weapons     → {OUT_WEAPONS}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Consumables → {OUT_CONSUMABLE}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Throwables  → {OUT_THROWABLE}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Deployables → {OUT_DEPLOYABLE}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  StatConfigs → {STAT_CONFIGS}", EditorStyles.miniLabel);

            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import All", GUILayout.Height(32)))
                    Run();
                if (GUILayout.Button("Import Weapons Only", GUILayout.Height(32)))
                    RunWeapons(LoadConfig());
                if (GUILayout.Button("Import Items Only", GUILayout.Height(32)))
                    RunItems(LoadConfig());
            }

            GUILayout.Space(4);
            if (GUILayout.Button("Clear Log"))
                _log = "";

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ── Import entry points ───────────────────────────────────────────────

        private void Run()
        {
            _log = "";
            GameConfigData cfg = LoadConfig();
            if (cfg == null) return;

            EnsureFolders();
            RunWeapons(cfg);
            RunItems(cfg);
            RegisterAllInItemDatabase();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("✅ Import complete.");
        }

        private GameConfigData LoadConfig()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(JSON_PATH);
            if (ta == null)
            {
                Log($"❌ JSON not found at: {JSON_PATH}");
                return null;
            }
            var cfg = JsonUtility.FromJson<GameConfigData>(ta.text);
            if (cfg == null) { Log("❌ Failed to parse JSON."); return null; }
            Log($"✅ Loaded JSON: {ta.name}");
            return cfg;
        }

        // ── Weapons ──────────────────────────────────────────────────────────

        private void RunWeapons(GameConfigData cfg)
        {
            if (cfg?.WeaponConfig == null) return;

            int created = 0, skipped = 0;
            foreach (var w in cfg.WeaponConfig)
            {
                if (string.IsNullOrEmpty(w.WeaponId)) { Log($"⚠ Skipping weapon with empty WeaponId"); continue; }

                // ── StatConfig ────────────────────────────────────────────────
                string statPath = $"{STAT_CONFIGS}/WeaponStat_{w.WeaponId}.asset";
                WeaponStatConfig stat = LoadOrCreate<WeaponStatConfig>(statPath);
                stat.Stats = BuildWeaponStats(w);
                stat.PlayerModifiers = new PlayerStatModifier[]
                {
                    new PlayerStatModifier
                    {
                        StatType     = PlayerStatType.MovementSpeed,
                        Value        = Mathf.RoundToInt((w.MoveSpeedMul - 1f) * 100f),
                        ModifierType = ModifierType.Percentage,
                        Description  = $"{w.DisplayName} move-speed modifier"
                    }
                };
                EditorUtility.SetDirty(stat);

                // ── WeaponDefinition ──────────────────────────────────────────
                string defPath = $"{OUT_WEAPONS}/Weapon_{w.WeaponId}.asset";
                WeaponDefinition def = LoadOrCreate<WeaponDefinition>(defPath);
                def.ItemID      = w.WeaponId;
                def.DisplayName = w.DisplayName;
                def.Rarity      = ParseRarity(w.Rarity);
                def.IsStackable  = false;
                def.MaxStackSize = 1;
                def.Weight       = w.Weight;
                def.WeaponClass  = ParseWeaponClass(w.Category);
                def.StatConfig   = stat;
                def.ValidSlots   = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
                def.AttachmentSlots = DefaultAttachmentSlots(w.Category);
                EditorUtility.SetDirty(def);

                created++;
                Log($"  ✔ {w.WeaponId} → {defPath}");
            }
            Log($"Weapons: {created} created/updated, {skipped} skipped.");
        }

        private static ItemStatConfig.ItemStatDefinition[] BuildWeaponStats(WeaponConfigData w)
        {
            // Accuracy derived from SpreadBase: lower spread = higher accuracy (capped 0–100)
            float accuracy = Mathf.Clamp(100f - w.SpreadBase * 100f, 0f, 100f);
            return new ItemStatConfig.ItemStatDefinition[]
            {
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.Damage,       DefaultValue = w.DamageBody,          MinValue = 0,   MaxValue = 500  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.FireRate,      DefaultValue = w.FireRate * 60f,      MinValue = 0,   MaxValue = 1500 },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.Accuracy,      DefaultValue = accuracy,              MinValue = 0,   MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.SpreadBase,    DefaultValue = w.SpreadBase,          MinValue = 0,   MaxValue = 10   },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.SpreadPenalty, DefaultValue = w.SpreadMoveMul * 10f, MinValue = 0,   MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.ReloadSpeed,   DefaultValue = w.ReloadTime,          MinValue = 0,   MaxValue = 20   },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MagazineSize,  DefaultValue = w.MagazineSize,        MinValue = 1,   MaxValue = 300  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MaxAmmo,       DefaultValue = w.ReserveAmmo,         MinValue = 0,   MaxValue = 2000 },
            };
        }

        // ── Consumable / Throwable / Deployable Items ─────────────────────────

        private void RunItems(GameConfigData cfg)
        {
            if (cfg?.ItemConfig == null) return;

            int created = 0;
            foreach (var item in cfg.ItemConfig)
            {
                if (string.IsNullOrEmpty(item.ItemId)) { Log($"⚠ Skipping item with empty ItemId"); continue; }

                switch (item.UseType)
                {
                    case "Throw":         CreateThrowable(item); break;
                    case "PlaceOnGround": CreateDeployable(item); break;
                    default:              CreateConsumable(item); break;
                }
                created++;
            }
            Log($"Items: {created} processed.");
        }

        private void CreateConsumable(ItemConfigData item)
        {
            // ── StatConfig ────────────────────────────────────────────────────
            string statPath = $"{STAT_CONFIGS}/ConsumableStat_{item.ItemId}.asset";
            ConsumableStatConfig stat = LoadOrCreate<ConsumableStatConfig>(statPath);
            stat.Effects = new ConsumableEffect[]
            {
                new ConsumableEffect
                {
                    EffectType  = ParseConsumableEffectType(item.EffectType),
                    Value       = item.EffectValue,
                    Duration    = item.EffectDuration,
                    IsInstant   = item.CastTime <= 0f,
                    Description = item.DisplayName
                }
            };
            EditorUtility.SetDirty(stat);

            // ── ConsumableDefinition ──────────────────────────────────────────
            string defPath = $"{OUT_CONSUMABLE}/Consumable_{item.ItemId}.asset";
            ConsumableDefinition def = LoadOrCreate<ConsumableDefinition>(defPath);
            ApplyBaseItemFields(def, item);
            def.UsageDuration  = item.CastTime;
            def.CanCancelUsage = true;
            def.StatConfig     = stat;
            EditorUtility.SetDirty(def);
            Log($"  ✔ {item.ItemId} → {defPath}");
        }

        private void CreateThrowable(ItemConfigData item)
        {
            string statPath = $"{STAT_CONFIGS}/ThrowableStat_{item.ItemId}.asset";
            ThrowableStatConfig stat = LoadOrCreate<ThrowableStatConfig>(statPath);
            EditorUtility.SetDirty(stat);

            string defPath = $"{OUT_THROWABLE}/Throwable_{item.ItemId}.asset";
            ThrowableDefinition def = LoadOrCreate<ThrowableDefinition>(defPath);
            ApplyBaseItemFields(def, item);
            def.UsageDuration  = item.CastTime;
            def.CanCancelUsage = true;
            def.StatConfig     = stat;
            def.Damage         = item.EffectValue;
            def.ExplosionRadius = item.EffectType == "SmokeScreen" ? item.EffectValue : 5f;
            def.FuseTime       = item.EffectDuration > 0 ? item.EffectDuration : 3f;
            def.ThrowForce     = 15f;
            def.ThrowableType  = ParseThrowableType(item.EffectType);
            def.CanBounce      = true;
            EditorUtility.SetDirty(def);
            Log($"  ✔ {item.ItemId} (Throwable) → {defPath}");
        }

        private void CreateDeployable(ItemConfigData item)
        {
            // Deployable items (PlaceOnGround) are represented as ConsumableDefinitions
            // with a "deploy" ConsumableEffect. After UsageDuration the server spawns
            // the appropriate NetworkObject (Beacon, VisionNode, Trap…).

            string statPath = $"{STAT_CONFIGS}/DeployableStat_{item.ItemId}.asset";
            ConsumableStatConfig stat = LoadOrCreate<ConsumableStatConfig>(statPath);
            stat.Effects = new ConsumableEffect[]
            {
                new ConsumableEffect
                {
                    EffectType  = ParseConsumableEffectType(item.EffectType),
                    Value       = item.EffectValue,
                    Duration    = item.EffectDuration,
                    IsInstant   = false,
                    Description = item.DisplayName,
                    BuffID      = item.ExtraParamsJson  // carry extra params (e.g. BeaconConfigId)
                }
            };
            EditorUtility.SetDirty(stat);

            string defPath = $"{OUT_DEPLOYABLE}/Deployable_{item.ItemId}.asset";
            ConsumableDefinition def = LoadOrCreate<ConsumableDefinition>(defPath);
            ApplyBaseItemFields(def, item);
            def.UsageDuration  = item.CastTime;           // cast = place time
            def.CanCancelUsage = true;
            def.StatConfig     = stat;
            EditorUtility.SetDirty(def);
            Log($"  ✔ {item.ItemId} (Deployable/PlaceOnGround) → {defPath}");
        }

        // ── ItemDatabase registration ─────────────────────────────────────────

        private void RegisterAllInItemDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ITEM_DB_PATH);
            if (db == null)
            {
                Log($"⚠ ItemDatabase not found at {ITEM_DB_PATH}. Create it first, then re-run Import.");
                return;
            }

            // Find all generated ItemDefinition assets under Data/Items
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[]
            {
                OUT_WEAPONS, OUT_CONSUMABLE, OUT_THROWABLE, OUT_DEPLOYABLE
            });

            var defs = new List<ItemDefinition>(guids.Length);
            foreach (var g in guids)
            {
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g));
                if (def != null) defs.Add(def);
            }

            // Use serialized property to overwrite the _itemDefinitions array
            var so = new SerializedObject(db);
            var prop = so.FindProperty("_itemDefinitions");
            if (prop == null)
            {
                Log("⚠ Could not find '_itemDefinitions' field on ItemDatabase.");
                return;
            }
            prop.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            Log($"✅ ItemDatabase updated — {defs.Count} definitions registered.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null && !_overwriteExisting) return existing;
            if (existing != null) return existing;  // overwrite = just dirty it, not recreate

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ApplyBaseItemFields(PhysicalItemDefinition def, ItemConfigData item)
        {
            def.ItemID       = item.ItemId;
            def.DisplayName  = item.DisplayName;
            def.Rarity       = ParseRarity(item.Rarity);
            def.IsStackable  = item.MaxStack > 1;
            def.MaxStackSize = Mathf.Max(1, item.MaxStack);
            def.Weight       = item.Weight;
            def.ValidSlots   = new SlotLocationType[] { SlotLocationType.Inventory };
        }

        private static ItemRarity ParseRarity(string s) => s switch
        {
            "Common"    => ItemRarity.Common,
            "Uncommon"  => ItemRarity.Uncommon,
            "Rare"      => ItemRarity.Rare,
            "Epic"      => ItemRarity.Epic,
            "Legendary" => ItemRarity.Legendary,
            _           => ItemRarity.Common
        };

        private static WeaponClass ParseWeaponClass(string category) => category switch
        {
            "Pistol"  => WeaponClass.Pistol,
            "SMG"     => WeaponClass.SMG,
            "Rifle"   => WeaponClass.Rifle,
            "Shotgun" => WeaponClass.Shotgun,
            "Sniper"  => WeaponClass.Sniper,
            "Heavy"   => WeaponClass.Launcher,
            _         => WeaponClass.Rifle
        };

        private static AttachmentSlotType[] DefaultAttachmentSlots(string category) => category switch
        {
            "Pistol"  => new[] { AttachmentSlotType.Barrel, AttachmentSlotType.Magazine },
            "SMG"     => new[] { AttachmentSlotType.Barrel, AttachmentSlotType.Magazine, AttachmentSlotType.Grip },
            "Rifle"   => new[] { AttachmentSlotType.Optic,  AttachmentSlotType.Barrel, AttachmentSlotType.Grip, AttachmentSlotType.Magazine, AttachmentSlotType.UnderBarrel },
            "Shotgun" => new[] { AttachmentSlotType.Barrel, AttachmentSlotType.Magazine },
            "Sniper"  => new[] { AttachmentSlotType.Optic,  AttachmentSlotType.Barrel },
            _         => new AttachmentSlotType[0]
        };

        private static ConsumableEffectType ParseConsumableEffectType(string effectType) => effectType switch
        {
            "HealHP"             => ConsumableEffectType.RestoreHealth,
            "HealStaminaOverTime"=> ConsumableEffectType.StaminaOverTime,
            "SpeedBuff"          => ConsumableEffectType.SpeedBoost,
            "NoiseReduce"        => ConsumableEffectType.NoiseReduce,
            "VisionIncrease"     => ConsumableEffectType.VisionIncrease,
            "Cleanse"            => ConsumableEffectType.Cure,
            "UnlockObjective"    => ConsumableEffectType.UnlockObjective,
            "DeployBeacon"       => ConsumableEffectType.DeployBeacon,
            "PlaceVisionNode"    => ConsumableEffectType.PlaceVisionNode,
            "LightArea"          => ConsumableEffectType.PlaceVisionNode,  // vision node variant
            "ExplosiveTrap"      => ConsumableEffectType.PlaceExplosiveTrap,
            "SlowField"          => ConsumableEffectType.PlaceSlowField,
            "SmokeScreen"        => ConsumableEffectType.ApplyDebuff,      // handled as Throwable
            "DisableBeacon"      => ConsumableEffectType.DisableBeacon,    // handled as Throwable
            _                    => ConsumableEffectType.ApplyBuff
        };

        private static ThrowableType ParseThrowableType(string effectType) => effectType switch
        {
            "SmokeScreen"    => ThrowableType.Smoke,
            "DisableBeacon"  => ThrowableType.Impact,   // EMP → impact detonation
            "Flashbang"      => ThrowableType.Flashbang,
            "ExplosiveTrap"  => ThrowableType.Grenade,
            _                => ThrowableType.Grenade
        };

        private static void EnsureFolders()
        {
            // Item definitions — canonical Database structure
            EnsureFolder("Assets/_Night_Hunt/Data",                                               "Resources");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources",                                    "Database");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources/Database",                           "Items");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources/Database/Items",                     "List Items");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources/Database/Items/List Items",          "Weapons");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources/Database/Items/List Items",          "Consumables");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources/Database/Items/List Items",          "Throwable");
            EnsureFolder("Assets/_Night_Hunt/Data/Resources/Database/Items/List Items",          "Deployables");
            // StatConfigs — canonical Data/Configs location
            EnsureFolder("Assets/_Night_Hunt/Data",                                              "Configs");
            EnsureFolder("Assets/_Night_Hunt/Data/Configs",                                      "StatSystem Config");
            EnsureFolder("Assets/_Night_Hunt/Data/Configs/StatSystem Config",                    "Items");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }

        private void Log(string msg)
        {
            _log += msg + "\n";
            Debug.Log($"[GDConfigImporter] {msg}");
            Repaint();
        }
    }
}
#endif
