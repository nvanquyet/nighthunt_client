using UnityEngine;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// UI display configuration for item stats (weapon stats, armor stats, etc.)
    ///
    /// RESPONSIBILITIES:
    /// - Display name, icon, accent color, text color, format string
    /// - IsPositiveStat flag (affects tooltip color coding)
    ///
    /// USAGE:
    /// Create via: Assets → Create → StatSystem/Config/Item Stat UI Config
    /// </summary>
    [CreateAssetMenu(fileName = "ItemStatUIConfig", menuName = "NightHunt/StatSystem/Item Stat UI Config")]
    public class ItemStatUIConfig : ScriptableObject
    {
        [Header("Item Stats")]
        [Tooltip("UI display settings for all item stats")]
        public ItemStatDefinition[] ItemStats;

        #region Helpers

        /// <summary>
        /// Get item stat definition by type. Returns default if not found.
        /// </summary>
        public ItemStatDefinition GetItemStatDefinition(ItemStatType type)
        {
            if (ItemStats == null) return default;

            foreach (var stat in ItemStats)
            {
                if (stat.Type == type)
                    return stat;
            }

            Debug.LogWarning($"[ItemStatUIConfig] Item stat definition not found: {type}");
            return default;
        }

        /// <summary>
        /// Returns true if a definition exists for this stat type.
        /// </summary>
        public bool HasItemStat(ItemStatType type)
        {
            if (ItemStats == null) return false;

            foreach (var stat in ItemStats)
            {
                if (stat.Type == type)
                    return true;
            }

            return false;
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            if (ItemStats == null) return;

            for (int i = 0; i < ItemStats.Length; i++)
            {
                for (int j = i + 1; j < ItemStats.Length; j++)
                {
                    if (ItemStats[i].Type == ItemStats[j].Type)
                    {
                        Debug.LogError($"[ItemStatUIConfig] Duplicate item stat definition: {ItemStats[i].Type}");
                    }
                }
            }
        }

        #endregion

        #region Editor Setup

#if UNITY_EDITOR
        [ContextMenu("Setup Default Item Stats")]
        private void SetupDefaultItemStats()
        {
            var types = (ItemStatType[])System.Enum.GetValues(typeof(ItemStatType));
            ItemStats = new ItemStatDefinition[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];

                var def = new ItemStatDefinition
                {
                    Type          = t,
                    DisplayName   = t.ToString(),
                    DisplayColor  = Color.white,
                    TextColor     = new Color(0.9f, 0.9f, 0.9f),
                    DisplayFormat = "0",
                    IsPositiveStat = true
                };

                switch (t)
                {
                    // Weapon Stats
                    case ItemStatType.Damage:
                        def.DisplayName = "Damage";
                        def.DisplayColor = new Color(1f, 0.3f, 0.3f);
                        def.TextColor = new Color(1f, 0.6f, 0.6f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.FireRate:
                        def.DisplayName = "Fire Rate";
                        def.DisplayColor = new Color(1f, 0.6f, 0.2f);
                        def.TextColor = new Color(1f, 0.78f, 0.55f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.Accuracy:
                        def.DisplayName = "Accuracy";
                        def.DisplayColor = new Color(0.3f, 1f, 0.3f);
                        def.TextColor = new Color(0.6f, 1f, 0.6f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.SpreadPenalty:
                        def.DisplayName = "Spread Penalty";
                        def.DisplayColor = new Color(1f, 0.4f, 0.4f);
                        def.TextColor = new Color(1f, 0.65f, 0.65f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = false;
                        break;

                    case ItemStatType.SpreadBase:
                        def.DisplayName = "Spread";
                        def.DisplayColor = new Color(1f, 0.4f, 0.4f);
                        def.TextColor = new Color(1f, 0.65f, 0.65f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = false;
                        break;

                    case ItemStatType.SpreadRecovery:
                        def.DisplayName = "Spread Recovery";
                        def.DisplayColor = new Color(0.4f, 0.8f, 1f);
                        def.TextColor = new Color(0.65f, 0.9f, 1f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.DrawSpeed:
                        def.DisplayName = "Draw Speed";
                        def.DisplayColor = new Color(0.5f, 0.8f, 1f);
                        def.TextColor = new Color(0.75f, 0.9f, 1f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.ReloadSpeed:
                        def.DisplayName = "Reload Speed";
                        def.DisplayColor = new Color(0.5f, 0.8f, 1f);
                        def.TextColor = new Color(0.75f, 0.9f, 1f);
                        def.DisplayFormat = "0.0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.MagazineSize:
                        def.DisplayName = "Magazine";
                        def.DisplayColor = new Color(0.7f, 0.5f, 1f);
                        def.TextColor = new Color(0.85f, 0.72f, 1f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.ArmorValue:
                        def.DisplayName = "Armor";
                        def.DisplayColor = new Color(0.4f, 0.8f, 0.6f);
                        def.TextColor = new Color(0.65f, 0.95f, 0.78f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.MaxDurability:
                        def.DisplayName = "Durability";
                        def.DisplayColor = new Color(0.4f, 0.85f, 0.65f);
                        def.TextColor = new Color(0.6f, 0.92f, 0.75f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.MaxAmmo:
                        def.DisplayName = "Max Ammo";
                        def.DisplayColor = new Color(0.9f, 0.6f, 0.2f);
                        def.TextColor = new Color(1f, 0.78f, 0.4f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;

                    case ItemStatType.BatteryCapacity:
                        def.DisplayName = "Battery";
                        def.DisplayColor = new Color(0.9f, 0.9f, 0.3f);
                        def.TextColor = new Color(1f, 1f, 0.55f);
                        def.DisplayFormat = "0";
                        def.IsPositiveStat = true;
                        break;
                }

                ItemStats[i] = def;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ItemStatUIConfig] Setup complete: {ItemStats.Length} item stat definitions.");
        }
#endif

        #endregion
    }

    #region Item Stat Definition Struct

    [System.Serializable]
    public struct ItemStatDefinition
    {
        [Tooltip("Item stat type")]
        public ItemStatType Type;

        [Tooltip("Label shown in tooltips")]
        public string DisplayName;

        [Tooltip("Optional icon sprite")]
        public Sprite Icon;

        [Tooltip("Accent color for icon")]
        public Color DisplayColor;

        [Tooltip("Text color for label and value. Falls back to DisplayColor if alpha < 0.01")]
        public Color TextColor;

        [Tooltip("Numeric format string (e.g. '0', '0.0')")]
        public string DisplayFormat;

        [Tooltip("True = giá trị cao hơn = tốt hơn (Damage, Accuracy). " +
                 "False = giá trị thấp hơn = tốt hơn (Recoil, Spread, Weight). " +
                 "Dùng để hiển thị màu trong tooltip: xanh (tốt) / đỏ (xấu)")]
        public bool IsPositiveStat;
    }

    #endregion
}
