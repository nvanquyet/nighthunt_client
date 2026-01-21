using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Helper class to deserialize item configs from JSON
    /// Handles conversion from old ItemConfigData to new BaseItemConfig derived classes
    /// </summary>
    public static class ItemConfigLoader
    {
        /// <summary>
        /// Convert old ItemConfigData to new BaseItemConfig structure
        /// </summary>
        public static BaseItemConfig ConvertFromLegacy(ItemConfigData legacy)
        {
            if (legacy == null) return null;

            // Determine item type from category or other fields
            ItemType itemType = DetermineItemType(legacy);

            BaseItemConfig config = null;

            switch (itemType)
            {
                case ItemType.Weapon:
                    config = new WeaponItemConfig();
                    break;
                case ItemType.Armor:
                    config = new ArmorItemConfig();
                    break;
                case ItemType.Consumable:
                    config = new ConsumableItemConfig();
                    break;
                case ItemType.Attachment:
                    config = new AttachmentItemConfig();
                    break;
                case ItemType.QuestItem:
                    config = new QuestItemConfig();
                    break;
                case ItemType.EventItem:
                    config = new EventItemConfig();
                    break;
                default:
                    // Default to consumable for backward compatibility
                    config = new ConsumableItemConfig();
                    break;
            }

            // Copy common fields
            config.ItemId = legacy.ItemId;
            config.DisplayName = legacy.DisplayName;
            config.Category = legacy.Category;
            config.Weight = legacy.Weight;
            config.Rarity = legacy.Rarity;
            config.CanDrop = legacy.CanDrop;
            config.AllowedPhaseMask = legacy.AllowedPhaseMask;
            config.MaxStack = legacy.MaxStack;
            config.IsStackable = legacy.IsConsumable && legacy.MaxStack > 1;

            // Parse UseType
            if (Enum.TryParse<UseType>(legacy.UseType, out UseType useType))
            {
                config.UseType = useType;
            }

            // Parse EffectType
            if (Enum.TryParse<EffectType>(legacy.EffectType, out EffectType effectType))
            {
                if (config is ConsumableItemConfig consumable)
                {
                    consumable.EffectType = effectType;
                    consumable.EffectValue = legacy.EffectValue;
                    consumable.EffectDuration = legacy.EffectDuration;
                    consumable.UseTime = legacy.CastTime;
                    consumable.IsInstantUse = legacy.CastTime <= 0f;
                }
            }

            // Parse tags from ExtraParamsJson if available
            if (!string.IsNullOrEmpty(legacy.ExtraParamsJson))
            {
                // Simple JSON parsing for tags (can be improved)
                if (legacy.ExtraParamsJson.Contains("Tags"))
                {
                    // Extract tags array (simplified)
                    config.Tags = new string[] { legacy.Category }; // Fallback to category
                }
            }
            else
            {
                config.Tags = new string[] { legacy.Category };
            }

            return config;
        }

        /// <summary>
        /// Determine item type from legacy config
        /// </summary>
        private static ItemType DetermineItemType(ItemConfigData legacy)
        {
            // Check category
            if (legacy.Category != null)
            {
                string category = legacy.Category.ToLower();
                if (category.Contains("weapon") || category.Contains("gun") || category.Contains("rifle"))
                    return ItemType.Weapon;
                if (category.Contains("armor") || category.Contains("helmet") || category.Contains("vest"))
                    return ItemType.Armor;
                if (category.Contains("attachment") || category.Contains("scope") || category.Contains("suppressor"))
                    return ItemType.Attachment;
                if (category.Contains("quest"))
                    return ItemType.QuestItem;
                if (category.Contains("event"))
                    return ItemType.EventItem;
            }

            // Check if consumable
            if (legacy.IsConsumable)
                return ItemType.Consumable;

            // Default to consumable
            return ItemType.Consumable;
        }

        /// <summary>
        /// Deserialize item config from JSON string
        /// </summary>
        public static BaseItemConfig DeserializeFromJson(string json)
        {
            try
            {
                // First try to deserialize as legacy ItemConfigData
                ItemConfigData legacy = JsonUtility.FromJson<ItemConfigData>(json);
                if (legacy != null)
                {
                    return ConvertFromLegacy(legacy);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemConfigLoader] Failed to deserialize as legacy format: {e.Message}");
            }

            // Try to deserialize as new format (would need custom JSON structure)
            // For now, return null and let caller handle
            return null;
        }
    }
}

