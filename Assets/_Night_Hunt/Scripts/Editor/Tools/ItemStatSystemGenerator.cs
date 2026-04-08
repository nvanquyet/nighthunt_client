using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Item Stat System Generator — creates ALL ScriptableObject assets for items.
    ///
    /// Menu: NightHunt / Tools / Generate All Item Assets
    ///
    /// Creates:
    ///   Weapons     — 6 weapon definitions + stat configs
    ///   Equipment   — 6 armor/gear definitions + stat configs
    ///   Consumables — 5 consumable definitions + stat configs
    ///   Throwables  — 4 throwable definitions + stat configs
    ///   Attachments — 5 attachment definitions + stat configs
    ///
    /// Output paths:
    ///   Assets/_Night_Hunt/Data/Resources/Database/Items/
    ///
    /// After generation:
    ///   1. Verify assets in Project window
    ///   2. Assign Icons / Prefabs via Inspector
    ///   3. Add to ItemDatabase (automatic if ItemDatabase scans Resources/)
    ///   4. Assign StatConfigs to PlayerPrefab → PlayerStatConfig
    /// </summary>
    public static class ItemStatSystemGenerator
    {
        private const string BasePath   = "Assets/_Night_Hunt/Data/Resources/Database/Items/List Items";
        private const string ConfigPath = "Assets/_Night_Hunt/Data/Configs/StatSystem Config/Items";

        // ── Menu ─────────────────────────────────────────────────────────────────

        [MenuItem("NightHunt/Tools/Generate All Item Assets", priority = 10)]
        public static void GenerateAllItems()
        {
            if (!EditorUtility.DisplayDialog(
                    "Generate Item Assets",
                    "This will CREATE ScriptableObject assets for all items.\n" +
                    "Existing assets at the same path will be OVERWRITTEN.",
                    "Generate", "Cancel")) return;

            EnsureDirectories();

            int created = 0;
            created += GenerateWeapons();
            created += GenerateEquipment();
            created += GenerateConsumables();
            created += GenerateThrowables();
            created += GenerateAttachments();
            GeneratePlayerStatConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Done",
                $"Generated {created} item assets.\n\n" +
                "Assign icons and prefabs via Inspector.\n" +
                "Check Console for any errors.",
                "OK");
        }

        [MenuItem("NightHunt/Tools/Generate Weapons Only", priority = 11)]
        public static void GenerateWeaponsOnly()
        {
            EnsureDirectories();
            int n = GenerateWeapons();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[ItemGen] Generated {n} weapon assets.");
        }

        [MenuItem("NightHunt/Tools/Generate Equipment Only", priority = 12)]
        public static void GenerateEquipmentOnly()
        {
            EnsureDirectories();
            int n = GenerateEquipment();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[ItemGen] Generated {n} equipment assets.");
        }

        // ── Weapons ───────────────────────────────────────────────────────────────

        private static int GenerateWeapons()
        {
            int count = 0;
            count += MakeWeapon("weapon_pistol_m1911",  "M1911 Pistol",
                WeaponClass.Pistol,
                damage: 35f, fireRate: 350f, accuracy: 0.75f, spreadBase: 3f, spreadPenalty: 1.5f, spreadRecovery: 8f,
                mag: 7, maxAmmo: 70, drawSpeed: 0.4f, reloadSpeed: 1.8f,
                armor: 0f, weight: 0.8f,
                playerMods: new[]{ Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, -0.02f, "Pistol draw") });

            count += MakeWeapon("weapon_smg_mp5",       "MP5 SMG",
                WeaponClass.SMG,
                damage: 22f, fireRate: 700f, accuracy: 0.62f, spreadBase: 4f, spreadPenalty: 1.2f, spreadRecovery: 10f,
                mag: 30, maxAmmo: 300, drawSpeed: 0.5f, reloadSpeed: 2.2f,
                armor: 0f, weight: 2.5f,
                playerMods: new[]{ Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, -0.05f, "SMG carry") });

            count += MakeWeapon("weapon_rifle_ak47",    "AK-47",
                WeaponClass.Rifle,
                damage: 45f, fireRate: 550f, accuracy: 0.68f, spreadBase: 5f, spreadPenalty: 1.8f, spreadRecovery: 7f,
                mag: 30, maxAmmo: 300, drawSpeed: 0.6f, reloadSpeed: 2.5f,
                armor: 0f, weight: 3.5f,
                playerMods: new[]{ Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, -0.08f, "Rifle carry") });

            count += MakeWeapon("weapon_shotgun_870",   "M870 Shotgun",
                WeaponClass.Shotgun,
                damage: 120f, fireRate: 60f, accuracy: 0.40f, spreadBase: 15f, spreadPenalty: 3f, spreadRecovery: 4f,
                mag: 8, maxAmmo: 80, drawSpeed: 0.7f, reloadSpeed: 3.5f,
                armor: 0f, weight: 3.8f,
                playerMods: new[]{ Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, -0.08f, "Shotgun carry") });

            count += MakeWeapon("weapon_sniper_awm",    "AWM Sniper",
                WeaponClass.Sniper,
                damage: 110f, fireRate: 40f, accuracy: 0.95f, spreadBase: 0.5f, spreadPenalty: 4f, spreadRecovery: 3f,
                mag: 5, maxAmmo: 50, drawSpeed: 0.8f, reloadSpeed: 4.0f,
                armor: 0f, weight: 4.5f,
                playerMods: new[]{ Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, -0.12f, "Sniper carry") });

            count += MakeWeapon("weapon_melee_knife",   "Combat Knife",
                WeaponClass.Melee,
                damage: 60f, fireRate: 120f, accuracy: 1.0f, spreadBase: 0f, spreadPenalty: 0f, spreadRecovery: 0f,
                mag: 0, maxAmmo: 0, drawSpeed: 0.25f, reloadSpeed: 0f,
                armor: 0f, weight: 0.3f,
                playerMods: Array.Empty<PlayerStatModifier>());

            return count;
        }

        private static int MakeWeapon(
            string id, string displayName,
            WeaponClass weapClass,
            float damage, float fireRate, float accuracy,
            float spreadBase, float spreadPenalty, float spreadRecovery,
            int mag, int maxAmmo, float drawSpeed, float reloadSpeed,
            float armor, float weight,
            PlayerStatModifier[] playerMods)
        {
            string configAssetPath = $"{ConfigPath}/Weapons/Stat_{id}.asset";
            string defAssetPath    = $"{BasePath}/Weapons/Def_{id}.asset";

            // WeaponStatConfig
            var cfg = CreateOrLoad<WeaponStatConfig>(configAssetPath);
            cfg.Stats = new[]
            {
                Stat(ItemStatType.Damage,         damage,         0, 9999),
                Stat(ItemStatType.FireRate,        fireRate,       1, 6000),
                Stat(ItemStatType.Accuracy,        accuracy,       0, 1),
                Stat(ItemStatType.SpreadBase,      spreadBase,     0, 90),
                Stat(ItemStatType.SpreadPenalty,   spreadPenalty,  0, 30),
                Stat(ItemStatType.SpreadRecovery,  spreadRecovery, 0, 50),
                Stat(ItemStatType.MagazineSize,    mag,            0, 200),
                Stat(ItemStatType.MaxAmmo,         maxAmmo,        0, 9999),
                Stat(ItemStatType.DrawSpeed,       drawSpeed,      0.1f, 5),
                Stat(ItemStatType.ReloadSpeed,     reloadSpeed,    0.1f, 10),
            };
            cfg.PlayerModifiers = playerMods;
            cfg.ItemModifiers   = Array.Empty<ItemStatModifier>();
            EditorUtility.SetDirty(cfg);

            // WeaponDefinition
            var def = CreateOrLoad<WeaponDefinition>(defAssetPath);
            ApplyItemDefinitionBase(def, id, displayName, weight, isStackable: false, maxStack: 1);
            def.StatConfig  = cfg;
            def.WeaponClass = weapClass;
            // Note: BallisticType and FireMode now live on WeaponBase (the prefab component),
            // not on WeaponDefinition. Assign them on the HeldPrefab WeaponBase component.
            EditorUtility.SetDirty(def);

            Debug.Log($"[ItemGen] Weapon: {displayName} ({id})");
            return 2; // config + definition
        }

        // ── Equipment ─────────────────────────────────────────────────────────────

        private static int GenerateEquipment()
        {
            int count = 0;
            count += MakeEquipment("equip_helmet_l1", "Helmet Level 1", EquipmentSlotType.Head,
                armor: 20f, durability: 100f, weight: 1.5f,
                playerMods: new[]{ Mod(PlayerStatType.Armor, ModifierType.Flat, 20f, "Helmet Lv1") });

            count += MakeEquipment("equip_helmet_l2", "Helmet Level 2", EquipmentSlotType.Head,
                armor: 40f, durability: 120f, weight: 2f,
                playerMods: new[]{ Mod(PlayerStatType.Armor, ModifierType.Flat, 40f, "Helmet Lv2") });

            count += MakeEquipment("equip_vest_l1", "Tactical Vest Level 1", EquipmentSlotType.Chest,
                armor: 50f, durability: 150f, weight: 3f,
                playerMods: new[]
                {
                    Mod(PlayerStatType.Armor,         ModifierType.Flat,       50f, "Vest Lv1 Armor"),
                    Mod(PlayerStatType.WeightCapacity,ModifierType.Flat,       5f,  "Vest Lv1 Carry"),
                    Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage,-0.05f,"Vest Lv1 Speed"),
                });

            count += MakeEquipment("equip_vest_l2", "Tactical Vest Level 2", EquipmentSlotType.Chest,
                armor: 85f, durability: 200f, weight: 5f,
                playerMods: new[]
                {
                    Mod(PlayerStatType.Armor,         ModifierType.Flat,       85f, "Vest Lv2 Armor"),
                    Mod(PlayerStatType.WeightCapacity,ModifierType.Flat,       10f, "Vest Lv2 Carry"),
                    Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage,-0.10f,"Vest Lv2 Speed"),
                });

            count += MakeEquipment("equip_boots", "Combat Boots", EquipmentSlotType.Feet,
                armor: 0f, durability: 80f, weight: 1.2f,
                playerMods: new[]{ Mod(PlayerStatType.MovementSpeed, ModifierType.Percentage, 0.10f, "Boots Speed") });

            count += MakeEquipment("equip_backpack", "Tactical Backpack", EquipmentSlotType.Back,
                armor: 0f, durability: 100f, weight: 2f,
                playerMods: new[]{ Mod(PlayerStatType.WeightCapacity, ModifierType.Flat, 20f, "Backpack Carry") });

            return count;
        }

        private static int MakeEquipment(
            string id, string displayName, EquipmentSlotType slot,
            float armor, float durability, float weight,
            PlayerStatModifier[] playerMods)
        {
            string configAssetPath = $"{ConfigPath}/Equipment/Stat_{id}.asset";
            string defAssetPath    = $"{BasePath}/Equipment/Def_{id}.asset";

            var cfg = CreateOrLoad<EquipmentStatConfig>(configAssetPath);
            cfg.Stats = new[]
            {
                Stat(ItemStatType.ArmorValue,     armor,      0, 500),
                Stat(ItemStatType.MaxDurability,  durability, 0, 1000),
            };
            cfg.PlayerModifiers = playerMods;
            cfg.ItemModifiers   = Array.Empty<ItemStatModifier>();
            EditorUtility.SetDirty(cfg);

            var def = CreateOrLoad<EquipmentDefinition>(defAssetPath);
            ApplyItemDefinitionBase(def, id, displayName, weight, false, 1);
            def.StatConfig    = cfg;
            def.EquipmentSlot = slot;
            EditorUtility.SetDirty(def);

            Debug.Log($"[ItemGen] Equipment: {displayName} ({id})");
            return 2;
        }

        // ── Consumables ───────────────────────────────────────────────────────────

        private static int GenerateConsumables()
        {
            int count = 0;

            count += MakeConsumable("cons_medkit",   "Med Kit",       weight: 0.8f, useDuration: 5f,
                effects: new[]{ Effect(ConsumableEffectType.RestoreHealth, PlayerStatType.Health, 100f, 0f, true,  "Restore 100 HP") });

            count += MakeConsumable("cons_bandage",  "Bandage",       weight: 0.2f, useDuration: 2f,
                effects: new[]{ Effect(ConsumableEffectType.HealOverTime, PlayerStatType.Health, 40f, 6f, false, "Heal 40 HP over 6s") });

            count += MakeConsumable("cons_energy",   "Energy Drink",  weight: 0.3f, useDuration: 3f,
                effects: new[]
                {
                    Effect(ConsumableEffectType.RestoreHealth,  PlayerStatType.Health,       50f, 0f, true,  "+50 HP"),
                    Effect(ConsumableEffectType.SpeedBoost,     PlayerStatType.MovementSpeed, 0.20f, 15f, false, "+20% speed 15s", buffId: "buff_speed_energy"),
                });

            count += MakeConsumable("cons_painkiller", "Painkiller",  weight: 0.2f, useDuration: 2f,
                effects: new[]{ Effect(ConsumableEffectType.HealOverTime, PlayerStatType.Health, 80f, 20f, false, "Regen HP over 20s") });

            count += MakeConsumable("cons_eyedrop",  "Eye Drop",     weight: 0.1f, useDuration: 1.5f,
                effects: new[]{ Effect(ConsumableEffectType.ApplyBuff, PlayerStatType.VisionRange, 20f, 20f, false, "+20 vision 20s", buffId: "buff_vision_eyedrop") });

            return count;
        }

        private static int MakeConsumable(
            string id, string displayName, float weight, float useDuration,
            ConsumableEffect[] effects)
        {
            string configPath = $"{ConfigPath}/Consumables/Stat_{id}.asset";
            string defPath    = $"{BasePath}/Consumables/Def_{id}.asset";

            var cfg = CreateOrLoad<ConsumableStatConfig>(configPath);
            cfg.Stats           = Array.Empty<ItemStatConfig.ItemStatDefinition>();
            cfg.PlayerModifiers = Array.Empty<PlayerStatModifier>();
            cfg.ItemModifiers   = Array.Empty<ItemStatModifier>();
            cfg.Effects         = effects;
            EditorUtility.SetDirty(cfg);

            var def = CreateOrLoad<ConsumableDefinition>(defPath);
            ApplyItemDefinitionBase(def, id, displayName, weight, isStackable: true, maxStack: 5);
            def.StatConfig   = cfg;
            def.UsageDuration = useDuration;
            def.CanCancelUsage = true;
            EditorUtility.SetDirty(def);

            Debug.Log($"[ItemGen] Consumable: {displayName} ({id})");
            return 2;
        }

        // ── Throwables ────────────────────────────────────────────────────────────

        private static int GenerateThrowables()
        {
            int count = 0;
            count += MakeThrowable("throw_grenade",   "Frag Grenade",   weight: 0.4f, fuseTime: 3f,   blastRadius: 6f,  damage: 150f, throwableType: ThrowableType.Grenade);
            count += MakeThrowable("throw_smoke",     "Smoke Grenade",  weight: 0.3f, fuseTime: 1f,   blastRadius: 10f, damage: 0f,   throwableType: ThrowableType.Smoke);
            count += MakeThrowable("throw_flashbang", "Flashbang",      weight: 0.3f, fuseTime: 1.5f, blastRadius: 8f,  damage: 10f,  throwableType: ThrowableType.Flashbang);
            count += MakeThrowable("throw_beacon",    "Respawn Beacon", weight: 0.6f, fuseTime: 0f,   blastRadius: 3f,  damage: 0f,   throwableType: ThrowableType.Impact);
            return count;
        }

        private static int MakeThrowable(string id, string displayName, float weight, float fuseTime, float blastRadius, float damage, ThrowableType throwableType)
        {
            string configPath = $"{ConfigPath}/Throwables/Stat_{id}.asset";
            string defPath    = $"{BasePath}/Throwable/Def_{id}.asset";

            var cfg = CreateOrLoad<ThrowableStatConfig>(configPath);
            cfg.Stats           = new[]
            {
                Stat(ItemStatType.Damage, damage, 0, 9999),
            };
            cfg.PlayerModifiers = Array.Empty<PlayerStatModifier>();
            cfg.ItemModifiers   = Array.Empty<ItemStatModifier>();
            EditorUtility.SetDirty(cfg);

            var def = CreateOrLoad<ThrowableDefinition>(defPath);
            ApplyItemDefinitionBase(def, id, displayName, weight, isStackable: true, maxStack: 3);
            def.StatConfig      = cfg;
            def.ThrowableType   = throwableType;
            def.Damage          = damage;
            def.FuseTime        = fuseTime;
            def.ExplosionRadius = blastRadius;
            // NOTE: def.ProjectilePrefab must be assigned manually in the Inspector.
            //       It requires a physics prefab with Rigidbody + ProjectileComponent.
            //       ThrowableHandler.SpawnProjectile() will LogError and abort every throw if null.
            EditorUtility.SetDirty(def);

            Debug.Log($"[ItemGen] Throwable: {displayName} ({id}) type={throwableType} damage={damage}");
            return 2;
        }

        // ── Attachments ───────────────────────────────────────────────────────────

        private static int GenerateAttachments()
        {
            int count = 0;

            // Optics
            count += MakeAttachment("att_optic_redot",   "Red Dot Sight",     AttachmentSlotType.Optic,
                itemMods: new[]
                {
                    ItemMod(ItemStatType.Accuracy,    ModifierType.Percentage, 0.15f, "Red Dot accuracy"),
                    ItemMod(ItemStatType.SpreadBase,  ModifierType.Percentage,-0.10f, "Red Dot spread"),
                },
                weight: 0.2f);

            count += MakeAttachment("att_optic_4x",      "4x Scope",          AttachmentSlotType.Optic,
                itemMods: new[]
                {
                    ItemMod(ItemStatType.Accuracy,    ModifierType.Percentage, 0.30f, "4x Scope accuracy"),
                    ItemMod(ItemStatType.SpreadBase,  ModifierType.Percentage,-0.20f, "4x Scope spread"),
                    ItemMod(ItemStatType.FireRate,    ModifierType.Percentage,-0.10f, "4x Scope fire rate"),
                },
                weight: 0.3f);

            // Barrel
            count += MakeAttachment("att_barrel_silencer","Silencer",          AttachmentSlotType.Barrel,
                itemMods: new[]
                {
                    ItemMod(ItemStatType.Damage,      ModifierType.Flat,      -10f,  "Silencer damage"),
                    ItemMod(ItemStatType.SpreadBase,  ModifierType.Percentage,-0.05f,"Silencer spread"),
                },
                weight: 0.25f);

            // Grip
            count += MakeAttachment("att_grip_vertical",  "Vertical Grip",     AttachmentSlotType.Grip,
                itemMods: new[]
                {
                    ItemMod(ItemStatType.SpreadPenalty, ModifierType.Percentage,-0.10f,"Grip spread pen"),
                    ItemMod(ItemStatType.Accuracy,      ModifierType.Percentage, 0.05f,"Grip accuracy"),
                },
                weight: 0.15f);

            // Magazine
            count += MakeAttachment("att_mag_extended",   "Extended Magazine",  AttachmentSlotType.Magazine,
                itemMods: new[]
                {
                    ItemMod(ItemStatType.MagazineSize, ModifierType.Percentage, 0.50f,"Extended mag size"),
                    ItemMod(ItemStatType.ReloadSpeed,  ModifierType.Percentage, 0.15f,"Extended reload"),
                },
                weight: 0.3f);

            return count;
        }

        private static int MakeAttachment(
            string id, string displayName, AttachmentSlotType slot,
            ItemStatModifier[] itemMods, float weight)
        {
            string configPath = $"{ConfigPath}/Attachments/Stat_{id}.asset";
            string defPath    = $"{BasePath}/Attachments/Def_{id}.asset";

            var cfg = CreateOrLoad<AttachmentStatConfig>(configPath);
            cfg.Stats           = Array.Empty<ItemStatConfig.ItemStatDefinition>();
            cfg.PlayerModifiers = Array.Empty<PlayerStatModifier>();
            cfg.ItemModifiers   = itemMods;
            EditorUtility.SetDirty(cfg);

            var def = CreateOrLoad<AttachmentDefinition>(defPath);
            ApplyItemDefinitionBase(def, id, displayName, weight, false, 1);
            def.StatConfig    = cfg;
            // AttachmentDefinition.CanAttachTo[] from ItemDefinition base — set via SO
            var defSo = new SerializedObject(def);
            var canAttachProp = defSo.FindProperty("CanAttachTo");
            if (canAttachProp != null && canAttachProp.isArray)
            {
                canAttachProp.arraySize = 1;
                canAttachProp.GetArrayElementAtIndex(0).enumValueIndex = (int)slot;
                defSo.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(def);

            Debug.Log($"[ItemGen] Attachment: {displayName} ({id})");
            return 2;
        }

        // ── PlayerStatConfig ──────────────────────────────────────────────────────

        private static void GeneratePlayerStatConfig()
        {
            string path = $"Assets/_Night_Hunt/Data/Configs/StatSystem Config/Characters/PlayerStatConfig_Default.asset";
            EnsureDir(Path.GetDirectoryName(path)!);

            var cfg = CreateOrLoad<PlayerStatConfig>(path);
            // PlayerStatConfig structure - set via SerializedObject
            var so = new SerializedObject(cfg);
            var entries = so.FindProperty("statEntries");
            if (entries != null && entries.isArray)
            {
                SetPlayerStatEntries(entries);
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(cfg);
            Debug.Log("[ItemGen] PlayerStatConfig_Default created");
        }

        private static void SetPlayerStatEntries(SerializedProperty entries)
        {
            // Base stats for a default player
            var stats = new (PlayerStatType type, float baseVal, float min, float max)[]
            {
                (PlayerStatType.Health,         100f, 0f, 200f),
                (PlayerStatType.MaxHealth,       100f, 50f, 200f),
                (PlayerStatType.Stamina,         100f, 0f, 100f),
                (PlayerStatType.MaxStamina,      100f, 50f, 100f),
                (PlayerStatType.MovementSpeed,     5f, 0f, 12f),
                (PlayerStatType.WeightCapacity,   50f, 10f, 200f),
                (PlayerStatType.CurrentWeight,     0f, 0f, 200f),
                (PlayerStatType.Armor,             0f, 0f, 500f),
                (PlayerStatType.VisionRange,      15f, 3f, 50f),
            };

            entries.arraySize = stats.Length;
            for (int i = 0; i < stats.Length; i++)
            {
                var e     = entries.GetArrayElementAtIndex(i);
                var tProp = e.FindPropertyRelative("StatType");
                var bProp = e.FindPropertyRelative("BaseValue");
                var nProp = e.FindPropertyRelative("MinValue");
                var xProp = e.FindPropertyRelative("MaxValue");

                if (tProp != null) tProp.enumValueIndex = (int)stats[i].type;
                if (bProp != null) bProp.floatValue     = stats[i].baseVal;
                if (nProp != null) nProp.floatValue     = stats[i].min;
                if (xProp != null) xProp.floatValue     = stats[i].max;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void ApplyItemDefinitionBase(ItemDefinition def, string id, string name, float weight,
            bool isStackable, int maxStack)
        {
            var so = new SerializedObject(def);
            SetStr(so, "ItemID",      id);
            SetStr(so, "DisplayName", name);
            SetFlt(so, "Weight",      weight);
            SetBool(so, "IsStackable", isStackable);
            SetInt(so, "MaxStackSize", maxStack);
            so.ApplyModifiedProperties();
        }

        private static T CreateOrLoad<T>(string assetPath) where T : ScriptableObject
        {
            EnsureDir(Path.GetDirectoryName(assetPath)!);
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static void EnsureDirectories()
        {
            var dirs = new[]
            {
                $"{ConfigPath}/Weapons",
                $"{ConfigPath}/Equipment",
                $"{ConfigPath}/Consumables",
                $"{ConfigPath}/Throwables",
                $"{ConfigPath}/Attachments",
                $"{BasePath}/Weapons",
                $"{BasePath}/Equipment",
                $"{BasePath}/Consumables",
                $"{BasePath}/Throwable",
                $"{BasePath}/Attachments",
            };
            foreach (var d in dirs) EnsureDir(d);
        }

        // Small DSL for readability

        private static ItemStatConfig.ItemStatDefinition Stat(ItemStatType t, float def, float min, float max)
            => new() { Type = t, DefaultValue = def, MinValue = min, MaxValue = max };

        private static PlayerStatModifier Mod(PlayerStatType t, ModifierType mt, float v, string desc)
            => new() { StatType = t, ModifierType = mt, Value = v, Description = desc };

        private static ItemStatModifier ItemMod(ItemStatType t, ModifierType mt, float v, string desc)
            => new() { StatType = t, ModifierType = mt, Value = v, Description = desc };

        private static ConsumableEffect Effect(ConsumableEffectType et, PlayerStatType st, float v, float dur, bool instant, string desc, string buffId = null)
            => new() { EffectType = et, StatType = st, Value = v, Duration = dur, IsInstant = instant, Description = desc, BuffID = buffId };

        private static void SetStr(SerializedObject so, string prop, string val)
            { var p = so.FindProperty(prop); if (p != null) p.stringValue = val; }
        private static void SetFlt(SerializedObject so, string prop, float val)
            { var p = so.FindProperty(prop); if (p != null) p.floatValue = val; }
        private static void SetBool(SerializedObject so, string prop, bool val)
            { var p = so.FindProperty(prop); if (p != null) p.boolValue = val; }
        private static void SetInt(SerializedObject so, string prop, int val)
            { var p = so.FindProperty(prop); if (p != null) p.intValue = val; }
    }
}
