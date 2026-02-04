using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;

namespace NightHunt.Inventory.Domain.Stats
{
    /// <summary>
    /// Manages stat modifiers and calculates final stat values.
    /// Supports both flat addition and percentage multiplier.
    /// </summary>
    public class StatModifierStack
    {
        private ModifierCalculationType calcType;
        private Dictionary<CharacterStatType, List<StatModifier>> characterModifiers;
        private Dictionary<WeaponStatType, List<StatModifier>> weaponModifiers;

        /// <summary>
        /// Initializes the modifier stack with global calculation type.
        /// </summary>
        public void Initialize(ModifierSystemConfig config)
        {
            calcType = config.CalculationType;
            characterModifiers = new Dictionary<CharacterStatType, List<StatModifier>>();
            weaponModifiers = new Dictionary<WeaponStatType, List<StatModifier>>();
        }

        #region Character Stats

        /// <summary>
        /// Calculates final character stat value.
        /// </summary>
        public float CalculateFinalStat(CharacterStatType statType, float baseValue)
        {
            if (!characterModifiers.ContainsKey(statType))
                return baseValue;

            var mods = characterModifiers[statType];

            if (calcType == ModifierCalculationType.FlatAddition)
            {
                // Sum all flat additions
                float total = baseValue;
                foreach (var mod in mods)
                    total += mod.Value;
                return total;
            }
            else // PercentMultiplier
            {
                // Multiply by sum of percentages
                float totalPercent = 1f;
                foreach (var mod in mods)
                    totalPercent += mod.Value; // Value is 0.1 for +10%, 0.2 for +20%, etc.
                return baseValue * totalPercent;
            }
        }

        /// <summary>
        /// Adds a character stat modifier.
        /// </summary>
        public void AddModifier(CharacterStatType statType, float value, string sourceId)
        {
            if (!characterModifiers.ContainsKey(statType))
                characterModifiers[statType] = new List<StatModifier>();

            characterModifiers[statType].Add(new StatModifier { Value = value, SourceId = sourceId });
        }

        /// <summary>
        /// Removes a character stat modifier by source ID.
        /// </summary>
        public void RemoveModifier(CharacterStatType statType, string sourceId)
        {
            if (!characterModifiers.ContainsKey(statType))
                return;

            characterModifiers[statType].RemoveAll(m => m.SourceId == sourceId);
        }

        /// <summary>
        /// Removes all modifiers with a specific source ID prefix.
        /// </summary>
        public void RemoveAllWithSourcePrefix(string sourcePrefix)
        {
            foreach (var kvp in characterModifiers)
            {
                kvp.Value.RemoveAll(m => m.SourceId.StartsWith(sourcePrefix));
            }

            foreach (var kvp in weaponModifiers)
            {
                kvp.Value.RemoveAll(m => m.SourceId.StartsWith(sourcePrefix));
            }
        }

        #endregion

        #region Weapon Stats

        /// <summary>
        /// Calculates final weapon stat value.
        /// </summary>
        public float CalculateFinalStat(WeaponStatType statType, float baseValue)
        {
            if (!weaponModifiers.ContainsKey(statType))
                return baseValue;

            var mods = weaponModifiers[statType];

            if (calcType == ModifierCalculationType.FlatAddition)
            {
                float total = baseValue;
                foreach (var mod in mods)
                    total += mod.Value;
                return total;
            }
            else
            {
                float totalPercent = 1f;
                foreach (var mod in mods)
                    totalPercent += mod.Value;
                return baseValue * totalPercent;
            }
        }

        /// <summary>
        /// Adds a weapon stat modifier.
        /// </summary>
        public void AddWeaponModifier(WeaponStatType statType, float value, string sourceId)
        {
            if (!weaponModifiers.ContainsKey(statType))
                weaponModifiers[statType] = new List<StatModifier>();

            weaponModifiers[statType].Add(new StatModifier { Value = value, SourceId = sourceId });
        }

        /// <summary>
        /// Removes a weapon stat modifier by source ID.
        /// </summary>
        public void RemoveWeaponModifier(WeaponStatType statType, string sourceId)
        {
            if (!weaponModifiers.ContainsKey(statType))
                return;

            weaponModifiers[statType].RemoveAll(m => m.SourceId == sourceId);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Gets all active modifiers for a character stat.
        /// </summary>
        public List<StatModifier> GetModifiers(CharacterStatType statType)
        {
            if (characterModifiers.ContainsKey(statType))
                return new List<StatModifier>(characterModifiers[statType]);
            return new List<StatModifier>();
        }

        /// <summary>
        /// Gets all active modifiers for a weapon stat.
        /// </summary>
        public List<StatModifier> GetModifiers(WeaponStatType statType)
        {
            if (weaponModifiers.ContainsKey(statType))
                return new List<StatModifier>(weaponModifiers[statType]);
            return new List<StatModifier>();
        }

        /// <summary>
        /// Clears all modifiers.
        /// </summary>
        public void ClearAll()
        {
            characterModifiers.Clear();
            weaponModifiers.Clear();
        }

        #endregion
    }
}