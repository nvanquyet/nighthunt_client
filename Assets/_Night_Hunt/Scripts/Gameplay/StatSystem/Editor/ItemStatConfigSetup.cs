#if UNITY_EDITOR
using UnityEditor;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.Gameplay.StatSystem.Editor
{
    /// <summary>
    /// Editor utility — GetOrCreateConfig helper used by the GameDesignConfigImporter.
    /// All hardcoded sample-setup methods have been removed.
    /// Use NightHunt/Tools/Game Design Config Importer to generate configs from JSON.
    /// </summary>
    public static class ItemStatConfigSetup
    {
        internal const string CONFIG_PATH = "Assets/_Night_Hunt/Resources/Configs/StatConfigs";

        /// <summary>
        /// Loads an existing ItemStatConfig asset by name, or creates a new one at CONFIG_PATH.
        /// Used by GameDesignConfigImporter.
        /// </summary>
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

            var config = UnityEngine.ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }
        
        // --- Backcompat setup stubs ---
        // The original project used hardcoded setup helpers. The Game Design
        // Config Importer replaced that, but editor scripts still call these
        // names. Provide lightweight no-op implementations to restore compile
        // compatibility. These may be extended to populate defaults if needed.

        public static void SetupFragGrenade(ThrowableStatConfig config) { }
        public static void SetupSmokeGrenade(ThrowableStatConfig config) { }

        public static void SetupRedDot(AttachmentStatConfig config) { }
        public static void SetupSuppressor(AttachmentStatConfig config) { }
        public static void SetupVerticalGrip(AttachmentStatConfig config) { }
        public static void SetupExtendedMagazine(AttachmentStatConfig config) { }
        public static void SetupFlashlight(AttachmentStatConfig config) { }
        public static void SetupStoragePouch(AttachmentStatConfig config) { }
        public static void Setup4xScope(AttachmentStatConfig config) { }
        public static void Setup8xScope(AttachmentStatConfig config) { }

        public static void SetupMedkit(ConsumableStatConfig config) { }
        public static void SetupEnergyDrink(ConsumableStatConfig config) { }

        public static void SetupVest(EquipmentStatConfig config) { }
        public static void SetupBackpack(EquipmentStatConfig config) { }
        public static void SetupHelmet(EquipmentStatConfig config) { }
        public static void SetupBelt(EquipmentStatConfig config) { }
        public static void SetupGloves(EquipmentStatConfig config) { }
    }
}
#endif
