using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.StatSystem.Systems
{
    /// <summary>
    /// Item stat calculation system
    /// 
    /// RESPONSIBILITIES:
    /// - Calculate final item stats from base definition + attachment modifiers
    /// - Cache calculated stats for performance
    /// - Invalidate cache when attachments change
    /// 
    /// DESIGN:
    /// - Static utility class (no NetworkBehaviour)
    /// - Pure calculation logic, no network sync
    /// - Used by UI for tooltips and comparisons
    /// 
    /// USAGE:
    /// float finalDamage = ItemStatSystem.CalculateItemStat(weaponInstance, ItemStatType.Damage);
    /// </summary>
    public static class ItemStatSystem
    {
        #region Cache
        
        /// <summary>
        /// Cache of calculated item stats
        /// Key: ItemInstanceID, Value: Dictionary<StatType, Value>
        /// Cleared when attachments change
        /// </summary>
        private static Dictionary<string, Dictionary<ItemStatType, float>> _statCache = 
            new Dictionary<string, Dictionary<ItemStatType, float>>();
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Calculate final stat value for an item
        /// 
        /// PARAMETERS:
        /// - item: Item instance to calculate stat for
        /// - statType: Type of stat to calculate
        /// 
        /// RETURNS:
        /// - Final calculated stat value (base + attachment modifiers)
        /// 
        /// NETWORK:
        /// - Client-side calculation only, no network sync
        /// </summary>
        public static float CalculateItemStat(ItemInstance item, ItemStatType statType)
        {
            if (item == null)
                return 0f;
            
            // Check cache first
            if (_statCache.TryGetValue(item.InstanceID, out var cachedStats))
            {
                if (cachedStats.TryGetValue(statType, out var cachedValue))
                    return cachedValue;
            }
            
            // Calculate from base + attachments
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (itemDef == null)
            {
                Debug.LogWarning($"[ItemStatSystem] Item definition not found: {item.DefinitionID}");
                return 0f;
            }
            
            // Get base value
            float baseValue = GetBaseStatFromDefinition(itemDef, statType);
            
            // Apply attachment modifiers
            float finalValue = ApplyAttachmentModifiers(item, itemDef, statType, baseValue);
            
            // Cache result
            CacheStatValue(item.InstanceID, statType, finalValue);
            
            return finalValue;
        }
        
        /// <summary>
        /// Get base stat value (without modifiers)
        /// </summary>
        public static float GetBaseItemStat(string itemDefinitionID, ItemStatType statType)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return 0f;
            
            return GetBaseStatFromDefinition(itemDef, statType);
        }
        
        /// <summary>
        /// Get all calculated stats for an item
        /// Returns dictionary of all stats with final values
        /// </summary>
        public static Dictionary<ItemStatType, float> GetAllItemStats(ItemInstance item)
        {
            if (item == null)
                return new Dictionary<ItemStatType, float>();
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (itemDef == null)
                return new Dictionary<ItemStatType, float>();
            
            var result = new Dictionary<ItemStatType, float>();
            
            // Get all stat types defined for this item
            var statTypes = GetDefinedStatTypes(itemDef);
            
            foreach (var statType in statTypes)
            {
                float value = CalculateItemStat(item, statType);
                if (value != 0f) // Only include non-zero stats
                {
                    result[statType] = value;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if item has specific stat
        /// </summary>
        public static bool HasStat(string itemDefinitionID, ItemStatType statType)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return false;
            
            float baseValue = GetBaseStatFromDefinition(itemDef, statType);
            return baseValue != 0f;
        }
        
        /// <summary>
        /// Clear cached stats for an item
        /// Call when attachments change
        /// </summary>
        public static void InvalidateCache(string itemInstanceID)
        {
            _statCache.Remove(itemInstanceID);
        }
        
        /// <summary>
        /// Clear all cached stats
        /// </summary>
        public static void ClearCache()
        {
            _statCache.Clear();
        }
        
        #endregion
        
        #region Stat Calculation
        
        /// <summary>
        /// Get base stat value from item definition
        /// </summary>
        private static float GetBaseStatFromDefinition(ItemDefinition itemDef, ItemStatType statType)
        {
            // Check weapon stats
            if (itemDef is WeaponDefinition weaponDef)
                return weaponDef.GetStatValue(statType);
            
            // Check equipment stats
            if (itemDef is EquipmentDefinition equipmentDef)
                return equipmentDef.GetStatValue(statType);
            
            // Check attachment stats
            if (itemDef is AttachmentDefinition attachmentDef)
                return attachmentDef.GetStatValue(statType);
            
            // Check consumable stats
            if (itemDef is ConsumableDefinition consumableDef)
                return consumableDef.GetStatValue(statType);
            
            // Check throwable stats
            if (itemDef is ThrowableDefinition throwableDef)
                return throwableDef.GetStatValue(statType);
            
            // Check if it's a common stat (Weight, Durability, etc.)
            return GetCommonStatValue(itemDef, statType);
        }
        
        /// <summary>
        /// Fallback for stat types not defined in StatConfig.Stats[].
        /// All resource-style stats (MaxAmmo, MaxDurability, BatteryCapacity) now live in
        /// StatConfig.Stats[] and are handled by the main CalculateItemStat path above.
        /// </summary>
        private static float GetCommonStatValue(ItemDefinition itemDef, ItemStatType statType)
        {
            return 0f;
        }
        
        /// <summary>
        /// Apply attachment modifiers to base stat value
        /// </summary>
        private static float ApplyAttachmentModifiers(ItemInstance item, ItemDefinition itemDef, ItemStatType statType, float baseValue)
        {
            if (item.AttachedItems == null || item.AttachedItems.Length == 0)
                return baseValue;
            
            float value = baseValue;
            var modifiers = new List<ItemStatModifier>();
            
            // Collect all modifiers from attachments (AttachedItems stores instance IDs)
            for (int i = 0; i < item.AttachedItems.Length; i++)
            {
                string attachmentInstanceID = item.AttachedItems[i];
                if (string.IsNullOrEmpty(attachmentInstanceID))
                    continue;
                
                var attachmentInstance = ItemDatabase.GetInstance(attachmentInstanceID);
                if (attachmentInstance == null) continue;
                
                var attachmentDef = ItemDatabase.GetDefinition(attachmentInstance.DefinitionID);
                if (attachmentDef is AttachmentDefinition attachDef)
                {
                    var itemMods = attachDef.GetItemModifiers();
                    if (itemMods != null)
                    {
                        foreach (var mod in itemMods)
                        {
                            if (mod.StatType == statType)
                                modifiers.Add(mod);
                        }
                    }
                }
            }
            
            // Apply modifiers in order: Flat → Percentage
            
            // Apply flat modifiers first
            foreach (var mod in modifiers.Where(m => m.ModifierType == ModifierType.Flat))
            {
                value += mod.Value;
            }
            
            // Apply percentage modifiers
            foreach (var mod in modifiers.Where(m => m.ModifierType == ModifierType.Percentage))
            {
                value *= (1f + mod.Value / 100f);
            }
            
            return value;
        }
        
        /// <summary>
        /// Get all stat types defined for an item
        /// </summary>
        private static List<ItemStatType> GetDefinedStatTypes(ItemDefinition itemDef)
        {
            var statTypes = new List<ItemStatType>();
            
            // Get StatConfig from all item types
            ItemStatConfig config = null;
            if (itemDef is WeaponDefinition weaponDef && weaponDef.StatConfig != null)
                config = weaponDef.StatConfig;
            else if (itemDef is EquipmentDefinition equipmentDef && equipmentDef.StatConfig != null)
                config = equipmentDef.StatConfig;
            else if (itemDef is AttachmentDefinition attachmentDef && attachmentDef.StatConfig != null)
                config = attachmentDef.StatConfig;
            else if (itemDef is ConsumableDefinition consumableDef && consumableDef.StatConfig != null)
                config = consumableDef.StatConfig;
            else if (itemDef is ThrowableDefinition throwableDef && throwableDef.StatConfig != null)
                config = throwableDef.StatConfig;
            
            // Add stats from StatConfig
            if (config?.Stats != null)
            {
                foreach (var stat in config.Stats)
                {
                    if (!statTypes.Contains(stat.Type))
                        statTypes.Add(stat.Type);
                }
            }
            
            // NOTE: MaxAmmo/MaxDurability/BatteryCapacity are in StatConfig.Stats[] now.
            // They are added automatically in the loop above when present in config.

            return statTypes;
        }
        
        #endregion
        
        #region Cache Management
        
        private static void CacheStatValue(string itemInstanceID, ItemStatType statType, float value)
        {
            if (!_statCache.TryGetValue(itemInstanceID, out var statDict))
            {
                statDict = new Dictionary<ItemStatType, float>();
                _statCache[itemInstanceID] = statDict;
            }
            
            statDict[statType] = value;
        }
        
        #endregion
        
        #region Debug
        
        /// <summary>
        /// Log all stats for an item
        /// </summary>
        public static void LogItemStats(ItemInstance item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ItemStatSystem] Cannot log stats for null item");
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (itemDef == null)
            {
                Debug.LogWarning($"[ItemStatSystem] Item definition not found: {item.DefinitionID}");
                return;
            }
            
            Debug.Log($"=== Item Stats: {itemDef.DisplayName} ===");
            
            var allStats = GetAllItemStats(item);
            
            foreach (var kvp in allStats.OrderBy(k => k.Key))
            {
                float baseValue = GetBaseItemStat(item.DefinitionID, kvp.Key);
                float modifier = kvp.Value - baseValue;
                
                string modStr = modifier != 0 ? $" ({modifier:+0.0;-0.0})" : "";
                Debug.Log($"  {kvp.Key}: {kvp.Value:F1}{modStr} (Base: {baseValue:F1})");
            }
            
            if (item.AttachedItems != null)
            {
                int attachmentCount = item.AttachedItems.Count(a => !string.IsNullOrEmpty(a));
                Debug.Log($"  Attachments: {attachmentCount}");
            }
        }
        
        /// <summary>
        /// Log cache statistics
        /// </summary>
        public static void LogCacheStats()
        {
            Debug.Log($"[ItemStatSystem] Cache contains {_statCache.Count} items");
            
            int totalStats = 0;
            foreach (var kvp in _statCache)
            {
                totalStats += kvp.Value.Count;
            }
            
            Debug.Log($"[ItemStatSystem] Total cached stat values: {totalStats}");
        }
        
        #endregion
    }
}
