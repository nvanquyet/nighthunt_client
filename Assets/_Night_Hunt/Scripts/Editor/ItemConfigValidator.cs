using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Data;

namespace NightHunt.Editor
{
    /// <summary>
    /// Tool để validate item configs và check for errors
    /// </summary>
    public class ItemConfigValidator : EditorWindow
    {
        private Vector2 scrollPos;
        private List<string> errors = new List<string>();
        private List<string> warnings = new List<string>();

        [MenuItem("Night Hunt/Setup Tools/Validate Item Configs")]
        public static void ShowWindow()
        {
            GetWindow<ItemConfigValidator>("Item Config Validator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Item Config Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate All Configs", GUILayout.Height(30)))
            {
                ValidateConfigs();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Show errors
            if (errors.Count > 0)
            {
                EditorGUILayout.LabelField($"Errors ({errors.Count}):", EditorStyles.boldLabel);
                foreach (var error in errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            // Show warnings
            if (warnings.Count > 0)
            {
                EditorGUILayout.LabelField($"Warnings ({warnings.Count}):", EditorStyles.boldLabel);
                foreach (var warning in warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("All configs are valid!", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Validate all configs
        /// </summary>
        private void ValidateConfigs()
        {
            errors.Clear();
            warnings.Clear();

            if (GameConfigLoader.Instance == null)
            {
                errors.Add("GameConfigLoader.Instance is null. Please ensure config is loaded.");
                return;
            }

            var configData = GameConfigLoader.Instance.ConfigData;
            if (configData?.ItemConfig == null)
            {
                errors.Add("ItemConfig is null or empty.");
                return;
            }

            // Validate each item config
            HashSet<string> itemIds = new HashSet<string>();
            foreach (var itemConfig in configData.ItemConfig)
            {
                ValidateItemConfig(itemConfig, itemIds);
            }

            // Check for duplicate IDs
            var duplicates = configData.ItemConfig
                .GroupBy(i => i.ItemId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var dupId in duplicates)
            {
                errors.Add($"Duplicate ItemId found: {dupId}");
            }

            Debug.Log($"[ItemConfigValidator] Validation complete. Errors: {errors.Count}, Warnings: {warnings.Count}");
        }

        /// <summary>
        /// Validate single item config
        /// </summary>
        private void ValidateItemConfig(ItemConfigData config, HashSet<string> itemIds)
        {
            if (config == null)
            {
                errors.Add("Null item config found.");
                return;
            }

            // Check ItemId
            if (string.IsNullOrEmpty(config.ItemId))
            {
                errors.Add("Item config has empty ItemId.");
                return;
            }

            if (itemIds.Contains(config.ItemId))
            {
                errors.Add($"Duplicate ItemId: {config.ItemId}");
            }
            else
            {
                itemIds.Add(config.ItemId);
            }

            // Check DisplayName
            if (string.IsNullOrEmpty(config.DisplayName))
            {
                warnings.Add($"Item {config.ItemId} has no DisplayName.");
            }

            // Check Weight
            if (config.Weight < 0)
            {
                errors.Add($"Item {config.ItemId} has negative weight: {config.Weight}");
            }

            // Check MaxStack
            if (config.MaxStack < 1)
            {
                warnings.Add($"Item {config.ItemId} has MaxStack < 1: {config.MaxStack}");
            }

            // Check UseType enum
            if (!string.IsNullOrEmpty(config.UseType))
            {
                if (!System.Enum.TryParse<UseType>(config.UseType, out _))
                {
                    warnings.Add($"Item {config.ItemId} has invalid UseType: {config.UseType}");
                }
            }

            // Check EffectType enum
            if (!string.IsNullOrEmpty(config.EffectType))
            {
                if (!System.Enum.TryParse<EffectType>(config.EffectType, out _))
                {
                    warnings.Add($"Item {config.ItemId} has invalid EffectType: {config.EffectType}");
                }
            }

            // Try to convert to BaseItemConfig để validate structure
            try
            {
                var baseConfig = ItemConfigLoader.ConvertFromLegacy(config);
                if (baseConfig == null)
                {
                    warnings.Add($"Item {config.ItemId} could not be converted to BaseItemConfig.");
                }
            }
            catch (System.Exception e)
            {
                warnings.Add($"Item {config.ItemId} conversion error: {e.Message}");
            }
        }
    }
}

