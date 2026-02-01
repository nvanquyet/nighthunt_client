using System.Collections.Generic;
using UnityEngine;
using System;
using NightHunt.Gameplay.Core.Prediction;
using NightHunt.Gameplay.Character.Stats;
using NightHunt.Gameplay.Core;
using NightHunt.Networking;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Manages character stats (HP, Stamina, Speed, Vision, etc.)
    /// Uses enum-based stat system with ScriptableObject config support.
    /// Applies modifiers from equipment, status effects, zones, etc.
    /// Formula: FinalStat = BaseStat × Multipliers + Additions
    /// Implements IPredictable for client-side prediction
    /// </summary>
    public class CharacterStats : MonoBehaviour, IPredictable<CharacterStatsState>
    {
        [Header("Stats Config")]
        [SerializeField] private CharacterStatsConfig statsConfig;
        [SerializeField] private bool useConfigFile = false;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Base Stats (fallback if config not used)")]
        [SerializeField] private int baseHP = 100;
        [SerializeField] private float baseStamina = 100f;
        [SerializeField] private float baseMoveSpeed = 1f;
        [SerializeField] private float baseWeightCapacity = 20f;
        [SerializeField] private float baseVisionRadius = 12f;
        [SerializeField] private float baseNoiseLevel = 1f;

        // Current runtime stats
        private float currentHP;
        private float currentStamina;
        private float currentWeight = 0f;

        // Status effects
        private List<ActiveStatusEffect> activeStatusEffects = new List<ActiveStatusEffect>();

        // Prediction
        private GenericPredictionManager<CharacterStatsState> predictionManager;
        private CharacterStatsSync statsSync;

        // Modifier stack
        private StatModifierStack modifierStack = new StatModifierStack();

        private void Awake()
        {
            LoadBaseStats();
            InitializeStats();
            
            // Initialize prediction
            predictionManager = new GenericPredictionManager<CharacterStatsState>(this);
            
            statsSync = gameObject.FindInHierarchy<CharacterStatsSync>();
            RegisterInComponentRegistry();
        }

        /// <summary>
        /// Register this component in ComponentRegistry
        /// </summary>
        private void RegisterInComponentRegistry()
        {
            NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            
            if (networkPlayer != null)
            {
                ComponentRegistry.RegisterCharacterStats(networkPlayer, this);
            }
        }

        private void OnDestroy()
        {
            // Unregister from ComponentRegistry
            // Search NetworkPlayer using ComponentFinder (searches in current, parent, children, root, root's children)
            NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            
            if (networkPlayer != null)
            {
                ComponentRegistry.UnregisterCharacterStats(networkPlayer, this);
            }
        }

        private void Start()
        {
            currentHP = GetBaseStat(CharacterStatType.MaxHP);
            currentStamina = GetBaseStat(CharacterStatType.Stamina);
        }

        private void Update()
        {
            UpdateStatusEffects();
        }

        /// <summary>
        /// Load base stats from config (ScriptableObject or fallback fields)
        /// </summary>
        private void LoadBaseStats()
        {
            if (useConfigFile && statsConfig != null)
            {
                // Use ScriptableObject config
                return; // Values are read via GetBaseStat() at runtime
            }
            else
            {
                // Use serialized fallback fields (baseHP, baseStamina, etc.)
                // These are set in Inspector or have default values
            }
        }

        private void InitializeStats()
        {
            // Clear all runtime modifiers (status effects, equipment, zones, etc.)
            modifierStack.Clear();
        }

        /// <summary>
        /// Get base stat value (from config or fallback fields)
        /// </summary>
        private float GetBaseStat(CharacterStatType statType)
        {
            if (useConfigFile && statsConfig != null)
            {
                float value = statsConfig.GetBaseValue(statType);
                if (enableDebugLogs && statType == CharacterStatType.MoveSpeed)
                {
                    Debug.Log($"[CharacterStats] GetBaseStat({statType}) = {value} (from Config)");
                }
                return value;
            }

            // Fallback to serialized fields
            float fallbackValue = 0f;
            switch (statType)
            {
                case CharacterStatType.MaxHP:
                    fallbackValue = baseHP;
                    break;
                case CharacterStatType.Stamina:
                    fallbackValue = baseStamina;
                    break;
                case CharacterStatType.MoveSpeed:
                    fallbackValue = baseMoveSpeed;
                    break;
                case CharacterStatType.WeightCapacity:
                    fallbackValue = baseWeightCapacity;
                    break;
                case CharacterStatType.VisionRadius:
                    fallbackValue = baseVisionRadius;
                    break;
                case CharacterStatType.NoiseLevel:
                    fallbackValue = baseNoiseLevel;
                    break;
                default:
                    fallbackValue = 0f;
                    break;
            }
            
            if (enableDebugLogs && statType == CharacterStatType.MoveSpeed)
            {
                Debug.LogWarning($"[CharacterStats] GetBaseStat({statType}) = {fallbackValue} (FALLBACK - useConfigFile={useConfigFile}, hasConfig={statsConfig != null})");
            }
            
            return fallbackValue;
        }

        /// <summary>
        /// Map status effect string stat name to CharacterStatType enum
        /// </summary>
        private CharacterStatType? MapStatusEffectStatName(string statName)
        {
            // Map legacy string names to enum
            switch (statName)
            {
                case "HP":
                case "MaxHP":
                    return CharacterStatType.MaxHP;
                case "Stamina":
                    return CharacterStatType.Stamina;
                case "MoveSpeed":
                    return CharacterStatType.MoveSpeed;
                case "WeightCapacity":
                    return CharacterStatType.WeightCapacity;
                case "Vision":
                case "VisionRadius":
                    return CharacterStatType.VisionRadius;
                case "Noise":
                case "NoiseLevel":
                    return CharacterStatType.NoiseLevel;
                default:
                    // Try to parse as enum name directly
                    if (Enum.TryParse<CharacterStatType>(statName, out var parsed))
                        return parsed;
                    return null;
            }
        }

        /// <summary>
        /// Apply status effect
        /// TODO: Implement StatusEffectConfig ScriptableObject system to replace GameConfigLoader
        /// </summary>
        public void ApplyStatusEffect(string statusId, float duration)
        {
            // TODO: Load status effect config from ScriptableObject registry
            // For now, status effects are disabled until StatusEffectConfig system is implemented
            Debug.LogWarning($"[CharacterStats] ApplyStatusEffect({statusId}) - Status effect system needs StatusEffectConfig ScriptableObject implementation");
                return;
        }

        /// <summary>
        /// Remove status effect
        /// </summary>
        public void RemoveStatusEffect(string statusId)
        {
            var effect = activeStatusEffects.Find(s => s.StatusId == statusId);
            if (effect != null)
            {
                RemoveStatusEffectModifier(effect);
                activeStatusEffects.Remove(effect);
            }
        }

        private void UpdateStatusEffects()
        {
            for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = activeStatusEffects[i];
                effect.TimeRemaining -= Time.deltaTime;

                // TODO: Handle status effects when StatusEffectConfig ScriptableObject is implemented
                // Handle damage over time
                /*
                if (effect.Config.Operation == "DamageOverTime")
                {
                    float damage = effect.Config.Value * Time.deltaTime;
                    TakeDamage(damage);
                }
                */

                // Remove expired effects
                if (effect.TimeRemaining <= 0f)
                {
                    RemoveStatusEffectModifier(effect);
                    activeStatusEffects.RemoveAt(i);
                }
            }
        }

        private void ApplyStatusEffectModifier(ActiveStatusEffect effect)
        {
            // TODO: Access status effect config when StatusEffectConfig ScriptableObject is implemented
            // For now, status effect modifiers are disabled
            return;
            
            /*
            string targetStat = effect.Config.TargetStat;
            string operation = effect.Config.Operation;
            float value = effect.Config.Value;

            // Map string stat name to enum (backward compatibility)
            var statType = MapStatusEffectStatName(targetStat);
            if (statType.HasValue)
            {
                var type = operation == "Multiply" ? ModifierType.Multiply : ModifierType.Add;
                modifierStack.AddModifier(statType.Value, type, value, $"Status:{effect.StatusId}");
            }
            else
            {
                // Fallback: use string (for custom stats not in enum yet)
                var type = operation == "Multiply" ? ModifierType.Multiply : ModifierType.Add;
                var modifier = new StatModifier(targetStat, type, value, $"Status:{effect.StatusId}");
                modifierStack.AddModifier(modifier);
            }
            */
        }

        private void RemoveStatusEffectModifier(ActiveStatusEffect effect)
        {
            // TODO: Access status effect config when StatusEffectConfig ScriptableObject is implemented
            // For now, status effect modifiers are disabled
            return;
            
            /*
            string targetStat = effect.Config.TargetStat;
            var statType = MapStatusEffectStatName(targetStat);
            if (statType.HasValue)
            {
                modifierStack.RemoveModifier(statType.Value, $"Status:{effect.StatusId}");
            }
            else
            {
                modifierStack.RemoveModifier(targetStat, $"Status:{effect.StatusId}");
            }
            */
        }

        /// <summary>
        /// Get final stat value (Base × Multipliers + Additions) using enum
        /// </summary>
        public float GetFinalStat(CharacterStatType statType, float baseValue)
        {
            return modifierStack.CalculateFinalValue(statType, baseValue);
        }

        /// <summary>
        /// Get final stat value using string (backward compatibility)
        /// </summary>
        public float GetFinalStat(string statName, float baseValue)
        {
            return modifierStack.CalculateFinalValue(statName, baseValue);
        }

        // Public getters for stats (using enum internally)
        public float GetHP() => currentHP;
        public float GetMaxHP() => GetFinalStat(CharacterStatType.MaxHP, GetBaseStat(CharacterStatType.MaxHP));
        public float GetStamina() => currentStamina;
        public float GetMaxStamina() => GetFinalStat(CharacterStatType.Stamina, GetBaseStat(CharacterStatType.Stamina));
        public float GetCurrentWeight() => currentWeight;
        
        /// <summary>
        /// Final carry capacity after all modifiers (backpack, armor, status effects...).
        /// </summary>
        public float GetWeightCapacity() => GetFinalStat(CharacterStatType.WeightCapacity, GetBaseStat(CharacterStatType.WeightCapacity));
        
        public float GetVisionRadius() => GetFinalStat(CharacterStatType.VisionRadius, GetBaseStat(CharacterStatType.VisionRadius));
        public float GetNoiseLevel() => GetFinalStat(CharacterStatType.NoiseLevel, GetBaseStat(CharacterStatType.NoiseLevel));
        public float GetSpeedMultiplier()
        {
            float baseValue = GetBaseStat(CharacterStatType.MoveSpeed);
            float finalValue = GetFinalStat(CharacterStatType.MoveSpeed, baseValue);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[CharacterStats] GetSpeedMultiplier() - Base: {baseValue}, Final: {finalValue}, UseConfig: {useConfigFile}, HasConfig: {statsConfig != null}");
            }
            
            return finalValue;
        }

        // Setters
        public void SetHP(float hp)
        {
            float maxHP = GetMaxHP();
            currentHP = Mathf.Clamp(hp, 0f, maxHP);
        }
        
        public void SetStamina(float stamina)
        {
            float maxStamina = GetMaxStamina();
            currentStamina = Mathf.Clamp(stamina, 0f, maxStamina);
        }
        
        public void SetWeight(float weight) => currentWeight = weight;

        /// <summary>
        /// Take damage
        /// </summary>
        public void TakeDamage(float damage)
        {
            currentHP = Mathf.Max(0f, currentHP - damage);
        }

        /// <summary>
        /// Heal
        /// </summary>
        public void Heal(float amount)
        {
            float maxHP = GetMaxHP();
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        /// <summary>
        /// Check if character is alive
        /// </summary>
        public bool IsAlive() => currentHP > 0f;

        /// <summary>
        /// Get weight percentage (0-1)
        /// </summary>
        public float GetWeightPercentage()
        {
            float capacity = GetWeightCapacity();
            return capacity > 0f ? currentWeight / capacity : 0f;
        }

        #region External modifiers (equipment, zones, etc.)

        /// <summary>
        /// Add a modifier using enum (preferred method).
        /// </summary>
        public void AddModifier(CharacterStatType statType, ModifierType type, float value, string sourceId)
        {
            modifierStack.AddModifier(statType, type, value, sourceId);
        }

        /// <summary>
        /// Add a modifier using string (backward compatibility).
        /// </summary>
        public void AddModifier(string statName, ModifierType type, float value, string sourceId)
        {
            var statType = MapStatusEffectStatName(statName);
            if (statType.HasValue)
            {
                modifierStack.AddModifier(statType.Value, type, value, sourceId);
            }
            else
            {
                var modifier = new StatModifier(statName, type, value, sourceId);
                modifierStack.AddModifier(modifier);
            }
        }

        /// <summary>
        /// Remove a modifier using enum.
        /// </summary>
        public void RemoveModifier(CharacterStatType statType, string sourceId)
        {
            modifierStack.RemoveModifier(statType, sourceId);
        }

        /// <summary>
        /// Remove a modifier using string (backward compatibility).
        /// </summary>
        public void RemoveModifier(string statName, string sourceId)
        {
            var statType = MapStatusEffectStatName(statName);
            if (statType.HasValue)
            {
                modifierStack.RemoveModifier(statType.Value, sourceId);
            }
            else
            {
                modifierStack.RemoveModifier(statName, sourceId);
            }
        }

        /// <summary>
        /// Remove all modifiers whose SourceId starts with the given prefix (eg. "Equip:" or "Status:").
        /// </summary>
        public void RemoveAllModifiersWithSourcePrefix(string sourcePrefix)
        {
            modifierStack.RemoveAllWithSourcePrefix(sourcePrefix);
        }

        #endregion

        #region IPredictable Implementation

        /// <summary>
        /// Get current state for prediction
        /// </summary>
        public CharacterStatsState GetCurrentState()
        {
            return new CharacterStatsState
            {
                HP = currentHP,
                Stamina = currentStamina,
                CurrentWeight = currentWeight,
                MoveSpeedMultiplier = GetSpeedMultiplier(),
                VisionRadius = GetVisionRadius(),
                NoiseLevel = GetNoiseLevel()
            };
        }

        /// <summary>
        /// Apply predicted state
        /// </summary>
        public void ApplyPredictedState(CharacterStatsState state)
        {
            currentHP = state.HP;
            currentStamina = state.Stamina;
            currentWeight = state.CurrentWeight;
            // Note: Multipliers are calculated, not directly set
        }

        /// <summary>
        /// Reconcile with server state
        /// </summary>
        public void Reconcile(CharacterStatsState serverState, float threshold = 0.1f)
        {
            // Apply server state
            ApplyPredictedState(serverState);
        }

        /// <summary>
        /// Set state (IPredictable implementation)
        /// </summary>
        public void SetState(CharacterStatsState state)
        {
            ApplyPredictedState(state);
        }

        /// <summary>
        /// Check if state differs from server state
        /// </summary>
        public bool IsStateDifferent(CharacterStatsState serverState, float threshold = 0.1f)
        {
            var currentState = GetCurrentState();
            return currentState.IsStateDifferent(serverState, threshold);
        }

        #endregion
    }

    /// <summary>
    /// Active status effect data
    /// </summary>
    [System.Serializable]
    public class ActiveStatusEffect
    {
        public string StatusId;
        // TODO: Replace with StatusEffectConfig ScriptableObject when implemented
        public object Config; // Was: StatusEffectConfigData
        public float Duration;
        public float TimeRemaining;
    }
}
