using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Manages active status effects
    /// </summary>
    public class StatusEffectManager : MonoBehaviour
    {
        private readonly List<IStatusEffect> activeEffects = new List<IStatusEffect>();
        private CharacterStats characterStats;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
        }

        private void Update()
        {
            // Update all effects
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = activeEffects[i];
                effect.OnUpdate(Time.deltaTime);

                // Remove expired effects
                if (effect.IsExpired)
                {
                    effect.OnRemove();
                    activeEffects.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Apply status effect
        /// </summary>
        public void ApplyEffect(IStatusEffect effect)
        {
            if (effect == null) return;

            // Check if already exists and if stackable
            var existing = activeEffects.FirstOrDefault(e => e.StatusId == effect.StatusId);
            if (existing != null)
            {
                // TODO: Check stackable rules from StatusEffectConfig ScriptableObject
                // For now, assume all effects are stackable
                bool isStackable = true; // Default to stackable
                /* OLD CODE - REMOVED (GameConfigLoader dependency)
                var config = NightHunt.Data.GameConfigLoader.Instance?.GetStatusEffectConfig(effect.StatusId);
                if (config != null && !config.Stackable)
                */
                if (!isStackable)
                {
                    // Refresh duration
                    if (existing is StatusEffect statusEffect)
                    {
                        statusEffect.RefreshDuration(effect.Duration);
                    }
                    return;
                }
            }

            // Add new effect
            activeEffects.Add(effect);
            effect.OnApply();
        }

        /// <summary>
        /// Remove status effect
        /// </summary>
        public void RemoveEffect(string statusId)
        {
            var effect = activeEffects.FirstOrDefault(e => e.StatusId == statusId);
            if (effect != null)
            {
                effect.OnRemove();
                activeEffects.Remove(effect);
            }
        }

        /// <summary>
        /// Check if has status effect
        /// </summary>
        public bool HasEffect(string statusId)
        {
            return activeEffects.Any(e => e.StatusId == statusId);
        }

        /// <summary>
        /// Get all active effects
        /// </summary>
        public List<IStatusEffect> GetActiveEffects()
        {
            return new List<IStatusEffect>(activeEffects);
        }

        /// <summary>
        /// Clear all effects
        /// </summary>
        public void ClearAll()
        {
            foreach (var effect in activeEffects)
            {
                effect.OnRemove();
            }
            activeEffects.Clear();
        }
    }
}

