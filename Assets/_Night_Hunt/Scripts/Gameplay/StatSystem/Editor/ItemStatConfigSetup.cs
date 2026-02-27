#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.StatSystem.Configs;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.StatSystem.Editor
{
    /// <summary>
    /// Static helpers to setup ItemStatConfig values. Used by definition editors.
    /// </summary>
    public static class ItemStatConfigSetup
    {
        private const string CONFIG_PATH = "Assets/_Night_Hunt/Resources/Configs/StatConfigs";

        #region Equipment

        public static void SetupVest(EquipmentStatConfig config)
        {
            // ArmorValue  : inspectable + buffable by Plate attachment.
            // MaxDurability: armor HP; instance.CurrentResource = current durability.
            // PlayerModifiers.Armor: pushed to PlayerStatType when equipped.
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.ArmorValue,    DefaultValue = 80,  MinValue = 0, MaxValue = 500  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MaxDurability, DefaultValue = 150, MinValue = 0, MaxValue = 1000 },
            };
            config.PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier { StatType = PlayerStatType.Armor, Value = 80, ModifierType = ModifierType.Flat, Description = "Vest armor" },
                new PlayerStatModifier { StatType = PlayerStatType.MovementSpeed, Value = -10f, ModifierType = ModifierType.Percentage, Description = "Heavy vest penalty" }
            };
        }

        public static void SetupBackpack(EquipmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier { StatType = PlayerStatType.MovementSpeed, Value = -2f, ModifierType = ModifierType.Percentage, Description = "Backpack penalty" }
            };
        }

        public static void SetupHelmet(EquipmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.ArmorValue,    DefaultValue = 40,  MinValue = 0, MaxValue = 200 },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MaxDurability, DefaultValue = 100, MinValue = 0, MaxValue = 500 },
            };
            config.PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier { StatType = PlayerStatType.Armor, Value = 40, ModifierType = ModifierType.Flat, Description = "Helmet protection" }
            };
        }

        public static void SetupBelt(EquipmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.PlayerModifiers = new PlayerStatModifier[0];
        }

        public static void SetupGloves(EquipmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.PlayerModifiers = new PlayerStatModifier[0];
        }

        #endregion

        #region Weapon

        public static void SetupRifle(WeaponStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.Damage,        DefaultValue = 30,   MinValue = 0,    MaxValue = 200  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.FireRate,       DefaultValue = 600,  MinValue = 0,    MaxValue = 1200 },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.Accuracy,       DefaultValue = 70,   MinValue = 0,    MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.SpreadPenalty,  DefaultValue = 25,   MinValue = 0,    MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.SpreadBase,     DefaultValue = 0.5f, MinValue = 0,    MaxValue = 10   },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.DrawSpeed,      DefaultValue = 1.2f, MinValue = 0,    MaxValue = 5    },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.ReloadSpeed,    DefaultValue = 2.5f, MinValue = 0,    MaxValue = 10   },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MagazineSize,   DefaultValue = 30,   MinValue = 1,    MaxValue = 200  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MaxAmmo,        DefaultValue = 300,  MinValue = 0,    MaxValue = 2000 },
            };
            config.PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier { StatType = PlayerStatType.MovementSpeed, Value = -5f, ModifierType = ModifierType.Percentage, Description = "Rifle penalty" }
            };
        }

        public static void SetupPistol(WeaponStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.Damage,        DefaultValue = 20,   MinValue = 0,    MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.FireRate,       DefaultValue = 300,  MinValue = 0,    MaxValue = 600  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.Accuracy,       DefaultValue = 60,   MinValue = 0,    MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.SpreadPenalty,  DefaultValue = 15,   MinValue = 0,    MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.DrawSpeed,      DefaultValue = 0.8f, MinValue = 0,    MaxValue = 5    },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.ReloadSpeed,    DefaultValue = 1.8f, MinValue = 0,    MaxValue = 10   },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MagazineSize,   DefaultValue = 12,   MinValue = 1,    MaxValue = 100  },
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.MaxAmmo,        DefaultValue = 120,  MinValue = 0,    MaxValue = 600  },
            };
            config.PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier { StatType = PlayerStatType.MovementSpeed, Value = -2f, ModifierType = ModifierType.Percentage, Description = "Pistol penalty" }
            };
        }

        #endregion

        #region Attachment

        public static void SetupRedDot(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreateFlat(ItemStatType.Accuracy, 10f, "Red dot accuracy"),
                ItemStatModifier.CreatePercentage(ItemStatType.DrawSpeed, 20f, "Faster aim"),
                ItemStatModifier.CreateFlat(ItemStatType.SpreadPenalty, -3f, "Recoil control")
            };
        }

        public static void SetupSuppressor(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreatePercentage(ItemStatType.SpreadPenalty, -30f, "Suppressor recoil"),
                ItemStatModifier.CreateFlat(ItemStatType.Damage, -2f, "Minor damage reduction")
            };
        }

        public static void SetupVerticalGrip(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreatePercentage(ItemStatType.SpreadPenalty, -25f, "Recoil control"),
                ItemStatModifier.CreateFlat(ItemStatType.Accuracy, 5f, "Stability"),
                ItemStatModifier.CreatePercentage(ItemStatType.SpreadBase, -15f, "Tighter spread")
            };
        }

        public static void SetupExtendedMagazine(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreatePercentage(ItemStatType.MagazineSize, 50f, "+50% magazine"),
                ItemStatModifier.CreatePercentage(ItemStatType.ReloadSpeed, -10f, "Slower reload")
            };
        }

        public static void SetupFlashlight(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
                // BatteryCapacity: 1 hour at 1 unit/s. instance.CurrentResource = remaining battery.
                new ItemStatConfig.ItemStatDefinition { Type = ItemStatType.BatteryCapacity, DefaultValue = 3600, MinValue = 0, MaxValue = 36000 },
            };
            config.PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier { StatType = PlayerStatType.VisionRange, Value = 15f, ModifierType = ModifierType.Flat, Description = "Illumination range" }
            };
        }

        public static void SetupStoragePouch(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
            };
        }

        public static void Setup4xScope(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreateFlat(ItemStatType.Accuracy, 25f, "Long-range accuracy"),
                ItemStatModifier.CreatePercentage(ItemStatType.DrawSpeed, -15f, "Slower aim"),
                ItemStatModifier.CreateFlat(ItemStatType.SpreadPenalty, -5f, "Recoil control")
            };
        }

        public static void Setup8xScope(AttachmentStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            config.ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreateFlat(ItemStatType.Accuracy, 40f, "Sniper accuracy"),
                ItemStatModifier.CreatePercentage(ItemStatType.DrawSpeed, -30f, "Much slower aim"),
                ItemStatModifier.CreateFlat(ItemStatType.SpreadPenalty, -8f, "Recoil reduction")
            };
        }

        #endregion

        #region Consumable

        public static void SetupMedkit(ConsumableStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            
            config.Effects = new GameplaySystems.Core.Data.ConsumableEffect[]
            {
                new GameplaySystems.Core.Data.ConsumableEffect 
                { 
                    EffectType = GameplaySystems.Core.Data.ConsumableEffectType.RestoreHealth,  
                    StatType = PlayerStatType.Health, 
                    Value = 50f, 
                    IsInstant = false, 
                    Description = "Restore 50 HP" 
                },
                new GameplaySystems.Core.Data.ConsumableEffect 
                { 
                    EffectType = GameplaySystems.Core.Data.ConsumableEffectType.HealOverTime,   
                    StatType = PlayerStatType.Health, 
                    Value = 20f, 
                    Duration = 10f,   
                    Description = "Heal 2 HP/s for 10 s" 
                },
            };
        }

        public static void SetupEnergyDrink(ConsumableStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };
            
            config.Effects = new GameplaySystems.Core.Data.ConsumableEffect[]
            {
                new GameplaySystems.Core.Data.ConsumableEffect 
                { 
                    EffectType = GameplaySystems.Core.Data.ConsumableEffectType.RestoreStamina, 
                    StatType = PlayerStatType.Stamina, 
                    Value = 100f, 
                    IsInstant = true, 
                    Description = "Restore 100 Stamina" 
                },
            };
        }

        #endregion

        #region Throwable (Stats only)

        public static void SetupFragGrenade(ThrowableStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };

            // Stats-only config. Intentionally leave modifiers empty.
            config.PlayerModifiers = new PlayerStatModifier[0];
            config.ItemModifiers = new ItemStatModifier[0];
        }

        public static void SetupSmokeGrenade(ThrowableStatConfig config)
        {
            config.Stats = new ItemStatConfig.ItemStatDefinition[]
            {
            };

            // Stats-only config. Intentionally leave modifiers empty.
            config.PlayerModifiers = new PlayerStatModifier[0];
            config.ItemModifiers = new ItemStatModifier[0];
        }

        #endregion

        #region Asset Helpers

        public static T GetOrCreateConfig<T>(string assetName) where T : ItemStatConfig
        {
            var path = $"{CONFIG_PATH}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/_Night_Hunt/Resources"))
                AssetDatabase.CreateFolder("Assets/_Night_Hunt", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/_Night_Hunt/Resources/Configs"))
                AssetDatabase.CreateFolder("Assets/_Night_Hunt/Resources", "Configs");
            if (!AssetDatabase.IsValidFolder("Assets/_Night_Hunt/Resources/Configs/StatConfigs"))
                AssetDatabase.CreateFolder("Assets/_Night_Hunt/Resources/Configs", "StatConfigs");

            var config = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        #endregion
    }
}
#endif
