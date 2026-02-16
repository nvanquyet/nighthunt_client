// using UnityEngine;
// using GameplaySystems.Stat;
//
// namespace GameplaySystems.Core.Configs
// {
//     /// <summary>
//     /// Configuration for all stat definitions
//     /// Defines default values, min/max, and UI display settings
//     /// Create via: Assets → Create → GameplaySystems/Config/Stat Config
//     /// </summary>
//     [CreateAssetMenu(fileName = "StatConfig", menuName = "GameplaySystems/Config/Stat Config")]
//     public class StatConfig : ScriptableObject
//     {
//         [Header("Player Stats")]
//         [Tooltip("Định nghĩa tất cả stats của player")]
//         public PlayerStatDefinition[] PlayerStats;
//
//         [Header("Item Stats")]
//         [Tooltip("Định nghĩa tất cả stats của items")]
//         public ItemStatDefinition[] ItemStats;
//
//         #region Player Stat Helpers
//
//         /// <summary>
//         /// Get player stat definition by type
//         /// </summary>
//         public PlayerStatDefinition GetPlayerStatDefinition(PlayerStatType type)
//         {
//             if (PlayerStats == null)
//                 return default;
//
//             foreach (var stat in PlayerStats)
//             {
//                 if (stat.Type == type)
//                     return stat;
//             }
//
//             Debug.LogWarning($"[StatConfig] Player stat definition not found for type: {type}");
//             return default;
//         }
//
//         /// <summary>
//         /// Check if player stat exists in config
//         /// </summary>
//         public bool HasPlayerStat(PlayerStatType type)
//         {
//             if (PlayerStats == null)
//                 return false;
//
//             foreach (var stat in PlayerStats)
//             {
//                 if (stat.Type == type)
//                     return true;
//             }
//
//             return false;
//         }
//
//         #endregion
//
//         #region Item Stat Helpers
//
//         /// <summary>
//         /// Get item stat definition by type
//         /// </summary>
//         public ItemStatDefinition GetItemStatDefinition(ItemStatType type)
//         {
//             if (ItemStats == null)
//                 return default;
//
//             foreach (var stat in ItemStats)
//             {
//                 if (stat.Type == type)
//                     return stat;
//             }
//
//             Debug.LogWarning($"[StatConfig] Item stat definition not found for type: {type}");
//             return default;
//         }
//
//         /// <summary>
//         /// Check if item stat exists in config
//         /// </summary>
//         public bool HasItemStat(ItemStatType type)
//         {
//             if (ItemStats == null)
//                 return false;
//
//             foreach (var stat in ItemStats)
//             {
//                 if (stat.Type == type)
//                     return true;
//             }
//
//             return false;
//         }
//
//         #endregion
//
//         #region Validation
//
//         private void OnValidate()
//         {
//             // Check for duplicate player stats
//             if (PlayerStats != null)
//             {
//                 for (int i = 0; i < PlayerStats.Length; i++)
//                 {
//                     for (int j = i + 1; j < PlayerStats.Length; j++)
//                     {
//                         if (PlayerStats[i].Type == PlayerStats[j].Type)
//                         {
//                             Debug.LogError($"[StatConfig] Duplicate player stat definition: {PlayerStats[i].Type}");
//                         }
//                     }
//                 }
//             }
//
//             // Check for duplicate item stats
//             if (ItemStats != null)
//             {
//                 for (int i = 0; i < ItemStats.Length; i++)
//                 {
//                     for (int j = i + 1; j < ItemStats.Length; j++)
//                     {
//                         if (ItemStats[i].Type == ItemStats[j].Type)
//                         {
//                             Debug.LogError($"[StatConfig] Duplicate item stat definition: {ItemStats[i].Type}");
//                         }
//                     }
//                 }
//             }
//         }
//
//         #endregion
//
//         #region Editor Setup
//
// #if UNITY_EDITOR
//         [ContextMenu("Setup Default Player Stats")]
//         private void SetupDefaultPlayerStats()
//         {
//             PlayerStats = new PlayerStatDefinition[]
//             {
//                 // Core Vitals
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.Health,
//                     DisplayName = "Health",
//                     DefaultValue = 100f,
//                     MinValue = 0f,
//                     MaxValue = 1000f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.8f, 0.2f, 0.2f), // Red
//                     DisplayFormat = "0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.MaxHealth,
//                     DisplayName = "Max Health",
//                     DefaultValue = 100f,
//                     MinValue = 1f,
//                     MaxValue = 1000f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.8f, 0.2f, 0.2f),
//                     DisplayFormat = "0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.Stamina,
//                     DisplayName = "Stamina",
//                     DefaultValue = 100f,
//                     MinValue = 0f,
//                     MaxValue = 500f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.2f, 0.8f, 0.2f), // Green
//                     DisplayFormat = "0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.MaxStamina,
//                     DisplayName = "Max Stamina",
//                     DefaultValue = 100f,
//                     MinValue = 1f,
//                     MaxValue = 500f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.2f, 0.8f, 0.2f),
//                     DisplayFormat = "0"
//                 },
//
//                 // Movement
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.MovementSpeed,
//                     DisplayName = "Movement Speed",
//                     DefaultValue = 5f,
//                     MinValue = 0f,
//                     MaxValue = 20f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.2f, 0.6f, 1f), // Blue
//                     DisplayFormat = "0.0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.SprintSpeed,
//                     DisplayName = "Sprint Speed",
//                     DefaultValue = 8f,
//                     MinValue = 0f,
//                     MaxValue = 30f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.2f, 0.6f, 1f),
//                     DisplayFormat = "0.0"
//                 },
//
//                 // Weight System
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.WeightCapacity,
//                     DisplayName = "Weight Capacity",
//                     DefaultValue = 100f,
//                     MinValue = 0f,
//                     MaxValue = 500f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(1f, 0.8f, 0.2f), // Yellow
//                     DisplayFormat = "0.0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.CurrentWeight,
//                     DisplayName = "Current Weight",
//                     DefaultValue = 0f,
//                     MinValue = 0f,
//                     MaxValue = 1000f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(1f, 0.8f, 0.2f),
//                     DisplayFormat = "0.0"
//                 },
//
//                 // Combat - Defense
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.Armor,
//                     DisplayName = "Armor",
//                     DefaultValue = 0f,
//                     MinValue = 0f,
//                     MaxValue = 500f,
//                     ShowInUI = true,
//                     DisplayColor = new Color(0.6f, 0.6f, 0.6f), // Gray
//                     DisplayFormat = "0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.MagicResist,
//                     DisplayName = "Magic Resist",
//                     DefaultValue = 0f,
//                     MinValue = 0f,
//                     MaxValue = 500f,
//                     ShowInUI = false,
//                     DisplayColor = new Color(0.8f, 0.4f, 1f), // Purple
//                     DisplayFormat = "0"
//                 },
//
//                 // Vision
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.VisionRange,
//                     DisplayName = "Vision Range",
//                     DefaultValue = 50f,
//                     MinValue = 0f,
//                     MaxValue = 200f,
//                     ShowInUI = false,
//                     DisplayColor = Color.white,
//                     DisplayFormat = "0.0"
//                 },
//                 new PlayerStatDefinition
//                 {
//                     Type = PlayerStatType.NightVision,
//                     DisplayName = "Night Vision",
//                     DefaultValue = 0f,
//                     MinValue = 0f,
//                     MaxValue = 100f,
//                     ShowInUI = false,
//                     DisplayColor = new Color(0.2f, 1f, 0.2f), // Bright green
//                     DisplayFormat = "0"
//                 }
//             };
//
//             UnityEditor.EditorUtility.SetDirty(this);
//             Debug.Log("[StatConfig] Setup default player stats complete!");
//         }
//
//         [ContextMenu("Setup Default Item Stats")]
//         private void SetupDefaultItemStats()
//         {
//             ItemStats = new ItemStatDefinition[]
//             {
//                 // Weapon Stats
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.Damage,
//                     DisplayName = "Damage",
//                     DisplayColor = new Color(1f, 0.3f, 0.3f),
//                     DisplayFormat = "0",
//                     IsPositiveStat = true
//                 },
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.FireRate,
//                     DisplayName = "Fire Rate",
//                     DisplayColor = new Color(1f, 0.6f, 0.2f),
//                     DisplayFormat = "0",
//                     IsPositiveStat = true
//                 },
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.Accuracy,
//                     DisplayName = "Accuracy",
//                     DisplayColor = new Color(0.3f, 1f, 0.3f),
//                     DisplayFormat = "0",
//                     IsPositiveStat = true
//                 },
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.Recoil,
//                     DisplayName = "Recoil",
//                     DisplayColor = new Color(1f, 0.4f, 0.4f),
//                     DisplayFormat = "0",
//                     IsPositiveStat = false // Lower is better
//                 },
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.Spread,
//                     DisplayName = "Spread",
//                     DisplayColor = new Color(1f, 0.5f, 0.5f),
//                     DisplayFormat = "0.0",
//                     IsPositiveStat = false
//                 },
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.Range,
//                     DisplayName = "Range",
//                     DisplayColor = new Color(0.4f, 0.8f, 1f),
//                     DisplayFormat = "0",
//                     IsPositiveStat = true
//                 },
//
//                 // Armor Stats
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.ArmorValue,
//                     DisplayName = "Armor Value",
//                     DisplayColor = new Color(0.6f, 0.6f, 0.6f),
//                     DisplayFormat = "0",
//                     IsPositiveStat = true
//                 },
//
//                 // Weight
//                 new ItemStatDefinition
//                 {
//                     Type = ItemStatType.Weight,
//                     DisplayName = "Weight",
//                     DisplayColor = new Color(0.8f, 0.8f, 0.5f),
//                     DisplayFormat = "0.0",
//                     IsPositiveStat = false
//                 }
//             };
//
//             UnityEditor.EditorUtility.SetDirty(this);
//             Debug.Log("[StatConfig] Setup default item stats complete!");
//         }
// #endif
//
//         #endregion
//     }
//
//     #region Stat Definitions
//
//     [System.Serializable]
//     public struct ItemStatDefinition
//     {
//         [Tooltip("Loại stat")]
//         public ItemStatType Type;
//
//         [Tooltip("Tên hiển thị")]
//         public string DisplayName;
//
//         [Tooltip("Icon")]
//         public Sprite Icon;
//
//         [Tooltip("Màu sắc")]
//         public Color DisplayColor;
//
//         [Tooltip("Format string")]
//         public string DisplayFormat;
//
//         [Tooltip("Stat này có phải là positive stat không (tăng = tốt)")]
//         public bool IsPositiveStat;
//     }
//
//     #endregion
// }