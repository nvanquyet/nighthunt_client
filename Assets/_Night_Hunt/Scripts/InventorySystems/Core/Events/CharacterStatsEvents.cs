using System;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for CharacterStats system.
    /// Used by EquipmentSystem and AttachmentSystem to apply stat modifiers.
    /// </summary>
    public static class CharacterStatsEvents
    {
        // === Modifier Management ===
        
        /// <summary>
        /// Fired to add a stat modifier.
        /// Args: (statType, calculationType, value, sourceId)
        /// </summary>
        public static event Action<CharacterStatType, ModifierCalculationType, float, string> OnAddModifier;
        
        /// <summary>
        /// Fired to remove all modifiers from a specific source.
        /// Args: (sourceId)
        /// </summary>
        public static event Action<string> OnRemoveModifier;
        
        /// <summary>
        /// Fired to remove all modifiers from all sources.
        /// </summary>
        public static event Action OnRemoveAllModifiers;
        
        // === Stat Changes ===
        
        /// <summary>
        /// Fired when any stat changes (after recalculation).
        /// </summary>
        public static event Action OnStatsChanged;
        
        /// <summary>
        /// Fired when HP changes.
        /// Args: (currentHP, maxHP)
        /// </summary>
        public static event Action<float, float> OnHPChanged;
        
        /// <summary>
        /// Fired when stamina changes.
        /// Args: (currentStamina, maxStamina)
        /// </summary>
        public static event Action<float, float> OnStaminaChanged;
        
        // === Invoke Methods ===
        
        public static void InvokeAddModifier(CharacterStatType statType, ModifierCalculationType calcType, float value, string sourceId)
            => OnAddModifier?.Invoke(statType, calcType, value, sourceId);
        
        public static void InvokeRemoveModifier(string sourceId)
            => OnRemoveModifier?.Invoke(sourceId);
        
        public static void InvokeRemoveAllModifiers()
            => OnRemoveAllModifiers?.Invoke();
        
        public static void InvokeStatsChanged()
            => OnStatsChanged?.Invoke();
        
        public static void InvokeHPChanged(float current, float max)
            => OnHPChanged?.Invoke(current, max);
        
        public static void InvokeStaminaChanged(float current, float max)
            => OnStaminaChanged?.Invoke(current, max);
    }
}