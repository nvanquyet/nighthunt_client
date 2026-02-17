using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameplaySystems.Inventory;
using GameplaySystems.Core.Data;
using GameplaySystems.Stat;

namespace GameplaySystems.Core
{
    /// <summary>
    /// Item stat calculation system
    /// Calculates final item stats from base definition + attachment modifiers
    /// 
    /// Design:
    /// - Singleton pattern (static methods)
    /// - NO NetworkBehaviour (pure calculation logic)
    /// - Cache results for performance
    /// - Call from UI for tooltips, comparisons
    /// 
    /// Usage:
    /// float finalDamage = ItemStatSystem.CalculateItemStat(weaponInstance, ItemStatType.Damage);
    /// </summary>
    public class ItemStatSystem
    {
        #region Singleton
        
        private static ItemStatSystem _instance;
        public static ItemStatSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ItemStatSystem();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Cache
        
        /// <summary>
        /// Cache of calculated item stats
        /// Key: ItemInstanceID, Value: Dictionary<StatType, Value>
        /// Cleared when attachments change
        /// </summary>
        private Dictionary<string, Dictionary<ItemStatType, float>> _statCache = 
            new Dictionary<string, Dictionary<ItemStatType, float>>();
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Calculate final stat value for an item
        /// Takes into account base stats + all attachment modifiers
        /// </summary>
        public static float CalculateItemStat(ItemInstance item, ItemStatType statType)
        {
            return Instance.CalculateItemStatInternal(item, statType);
        }
        
        /// <summary>
        /// Get base stat value (without modifiers)
        /// </summary>
        public static float GetBaseItemStat(string itemDefinitionID, ItemStatType statType)
        {
            return Instance.GetBaseItemStatInternal(itemDefinitionID, statType);
        }
        
        /// <summary>
        /// Get all calculated stats for an item
        /// Returns dictionary of all stats with final values
        /// </summary>
        public static Dictionary<ItemStatType, float> GetAllItemStats(ItemInstance item)
        {
            return Instance.GetAllItemStatsInternal(item);
        }
        
        /// <summary>
        /// Check if item has specific stat
        /// </summary>
        public static bool HasStat(string itemDefinitionID, ItemStatType statType)
        {
            return Instance.HasStatInternal(itemDefinitionID, statType);
        }
        
        /// <summary>
        /// Clear cached stats for an item
        /// Call when attachments change
        /// </summary>
        public static void InvalidateCache(string itemInstanceID)
        {
            Instance.InvalidateCacheInternal(itemInstanceID);
        }
        
        /// <summary>
        /// Clear all cached stats
        /// </summary>
        public static void ClearCache()
        {
            Instance.ClearCacheInternal();
        }
        
        #endregion
        
        #region Internal Implementation
        
        private float CalculateItemStatInternal(ItemInstance item, ItemStatType statType)
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
        
        private float GetBaseItemStatInternal(string itemDefinitionID, ItemStatType statType)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return 0f;
            
            return GetBaseStatFromDefinition(itemDef, statType);
        }
        
        private Dictionary<ItemStatType, float> GetAllItemStatsInternal(ItemInstance item)
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
                float value = CalculateItemStatInternal(item, statType);
                if (value != 0f) // Only include non-zero stats
                {
                    result[statType] = value;
                }
            }
            
            return result;
        }
        
        private bool HasStatInternal(string itemDefinitionID, ItemStatType statType)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return false;
            
            float baseValue = GetBaseStatFromDefinition(itemDef, statType);
            return baseValue != 0f;
        }
        
        #endregion
        
        #region Stat Calculation
        
        /// <summary>
        /// Get base stat value from item definition
        /// </summary>
        private float GetBaseStatFromDefinition(ItemDefinition itemDef, ItemStatType statType)
        {
            // Check weapon stats
            if (itemDef is WeaponDefinition weaponDef)
            {
                return weaponDef.GetStatValue(statType);
            }
            
            // Check armor stats
            if (itemDef is EquipmentDefinition armorDef)
            {
                return armorDef.GetStatValue(statType);
            }
            
            // Check if it's a common stat (Weight, Durability, etc.)
            return GetCommonStatValue(itemDef, statType);
        }
        
        /// <summary>
        /// Get common stat values (Weight, Durability, etc.)
        /// </summary>
        private float GetCommonStatValue(ItemDefinition itemDef, ItemStatType statType)
        {
            switch (statType)
            {
                case ItemStatType.Weight:
                    return itemDef.Weight;
                
                case ItemStatType.Durability:
                case ItemStatType.MaxDurability:
                    if (itemDef.ResourceType == ItemResourceType.Durability)
                        return itemDef.MaxResource;
                    return 0f;
                
                default:
                    return 0f;
            }
        }
        
        /// <summary>
        /// Apply attachment modifiers to base stat value
        /// </summary>
        private float ApplyAttachmentModifiers(ItemInstance item, ItemDefinition itemDef, ItemStatType statType, float baseValue)
        {
            if (item.AttachedItems == null || item.AttachedItems.Length == 0)
                return baseValue;
            
            float value = baseValue;
            var modifiers = new List<ItemStatModifier>();
            
            // Collect all modifiers from attachments
            for (int i = 0; i < item.AttachedItems.Length; i++)
            {
                string attachmentID = item.AttachedItems[i];
                if (string.IsNullOrEmpty(attachmentID))
                    continue;
                
                // Get attachment definition
                var attachmentDef = ItemDatabase.GetDefinition(attachmentID);
                if (attachmentDef is AttachmentDefinition attachDef)
                {
                    // Find modifiers for this stat type
                    if (attachDef.ItemModifiers != null)
                    {
                        foreach (var mod in attachDef.ItemModifiers)
                        {
                            if (mod.StatType == statType)
                            {
                                modifiers.Add(mod);
                            }
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
        private List<ItemStatType> GetDefinedStatTypes(ItemDefinition itemDef)
        {
            var statTypes = new List<ItemStatType>();
            
            if (itemDef is WeaponDefinition weaponDef && weaponDef.Stats != null)
            {
                foreach (var stat in weaponDef.Stats)
                {
                    if (!statTypes.Contains(stat.StatType))
                        statTypes.Add(stat.StatType);
                }
            }
            
            if (itemDef is EquipmentDefinition armorDef && armorDef.Stats != null)
            {
                foreach (var stat in armorDef.Stats)
                {
                    if (!statTypes.Contains(stat.StatType))
                        statTypes.Add(stat.StatType);
                }
            }
            
            // Add common stats
            if (itemDef.Weight > 0 && !statTypes.Contains(ItemStatType.Weight))
                statTypes.Add(ItemStatType.Weight);
            
            if (itemDef.ResourceType == ItemResourceType.Durability && !statTypes.Contains(ItemStatType.Durability))
            {
                statTypes.Add(ItemStatType.Durability);
                statTypes.Add(ItemStatType.MaxDurability);
            }
            
            return statTypes;
        }
        
        #endregion
        
        #region Cache Management
        
        private void CacheStatValue(string itemInstanceID, ItemStatType statType, float value)
        {
            if (!_statCache.TryGetValue(itemInstanceID, out var statDict))
            {
                statDict = new Dictionary<ItemStatType, float>();
                _statCache[itemInstanceID] = statDict;
            }
            
            statDict[statType] = value;
        }
        
        private void InvalidateCacheInternal(string itemInstanceID)
        {
            _statCache.Remove(itemInstanceID);
        }
        
        private void ClearCacheInternal()
        {
            _statCache.Clear();
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
            Debug.Log($"[ItemStatSystem] Cache contains {Instance._statCache.Count} items");
            
            int totalStats = 0;
            foreach (var kvp in Instance._statCache)
            {
                totalStats += kvp.Value.Count;
            }
            
            Debug.Log($"[ItemStatSystem] Total cached stat values: {totalStats}");
        }
        
        #endregion
    }
}