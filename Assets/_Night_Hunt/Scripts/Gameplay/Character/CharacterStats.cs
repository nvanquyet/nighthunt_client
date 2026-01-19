using System.Collections.Generic;
using UnityEngine;
using NightHunt.Data;
using System;
using NightHunt.Gameplay.Core.Prediction;
using NightHunt.Gameplay.Character.Stats;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Manages character stats (HP, Stamina, Speed, Vision, etc.)
    /// Applies modifiers from equipment, status effects, zones, etc.
    /// Formula: FinalStat = BaseStat × Multipliers + Additions
    /// Implements IPredictable for client-side prediction
    /// </summary>
    public class CharacterStats : MonoBehaviour, IPredictable<CharacterStatsState>
    {
        [Header("Base Stats")]
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

        // Modifiers (Multipliers and Additions)
        private Dictionary<string, float> statMultipliers = new Dictionary<string, float>();
        private Dictionary<string, float> statAdditions = new Dictionary<string, float>();

        // Status effects
        private List<ActiveStatusEffect> activeStatusEffects = new List<ActiveStatusEffect>();

        // Prediction
        private PredictionManager<CharacterStatsState> predictionManager;
        private CharacterStatsSync statsSync;

        // Modifier stack
        private StatModifierStack modifierStack = new StatModifierStack();

        private void Awake()
        {
            LoadBaseStats();
            InitializeStats();
            
            // Initialize prediction
            predictionManager = new PredictionManager<CharacterStatsState>(this);
            
            // Get network sync component
            statsSync = GetComponent<CharacterStatsSync>();
        }

        private void Start()
        {
            currentHP = baseHP;
            currentStamina = baseStamina;
        }

        private void Update()
        {
            UpdateStatusEffects();
        }

        /// <summary>
        /// Load base stats from config
        /// </summary>
        private void LoadBaseStats()
        {
            var config = GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
            if (config != null)
            {
                baseHP = config.BaseHP;
                baseStamina = config.BaseStamina;
                baseMoveSpeed = config.BaseMoveSpeed;
                baseWeightCapacity = config.BaseWeightCapacity;
                baseVisionRadius = config.BaseVisionRadius;
                baseNoiseLevel = config.BaseNoiseLevel;
            }
        }

        private void InitializeStats()
        {
            // Initialize multipliers to 1.0
            statMultipliers["MoveSpeed"] = 1f;
            statMultipliers["Stamina"] = 1f;
            statMultipliers["Vision"] = 1f;
            statMultipliers["Noise"] = 1f;

            // Initialize additions to 0
            statAdditions["MoveSpeed"] = 0f;
            statAdditions["Stamina"] = 0f;
            statAdditions["Vision"] = 0f;
            statAdditions["Noise"] = 0f;
        }

        /// <summary>
        /// Apply status effect
        /// </summary>
        public void ApplyStatusEffect(string statusId, float duration)
        {
            var config = GameConfigLoader.Instance?.GetStatusEffectConfig(statusId);
            if (config == null)
            {
                Debug.LogWarning($"[CharacterStats] Status effect config not found: {statusId}");
                return;
            }

            // Check if already exists and if stackable
            var existing = activeStatusEffects.Find(s => s.StatusId == statusId);
            if (existing != null && !config.Stackable)
            {
                // Refresh duration
                existing.Duration = duration;
                return;
            }

            // Add new status effect
            var effect = new ActiveStatusEffect
            {
                StatusId = statusId,
                Config = config,
                Duration = duration,
                TimeRemaining = duration
            };

            activeStatusEffects.Add(effect);
            ApplyStatusEffectModifier(effect);
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

                // Handle damage over time
                if (effect.Config.Operation == "DamageOverTime")
                {
                    float damage = effect.Config.Value * Time.deltaTime;
                    TakeDamage(damage);
                }

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
            string targetStat = effect.Config.TargetStat;
            string operation = effect.Config.Operation;
            float value = effect.Config.Value;

            if (operation == "Multiply")
            {
                if (!statMultipliers.ContainsKey(targetStat))
                    statMultipliers[targetStat] = 1f;
                statMultipliers[targetStat] *= value;
            }
            else if (operation == "Add")
            {
                if (!statAdditions.ContainsKey(targetStat))
                    statAdditions[targetStat] = 0f;
                statAdditions[targetStat] += value;
            }
        }

        private void RemoveStatusEffectModifier(ActiveStatusEffect effect)
        {
            string targetStat = effect.Config.TargetStat;
            string operation = effect.Config.Operation;
            float value = effect.Config.Value;

            if (operation == "Multiply")
            {
                if (statMultipliers.ContainsKey(targetStat))
                    statMultipliers[targetStat] /= value;
            }
            else if (operation == "Add")
            {
                if (statAdditions.ContainsKey(targetStat))
                    statAdditions[targetStat] -= value;
            }
        }

        /// <summary>
        /// Get final stat value (Base × Multipliers + Additions)
        /// </summary>
        public float GetFinalStat(string statName, float baseValue)
        {
            float multiplier = statMultipliers.ContainsKey(statName) ? statMultipliers[statName] : 1f;
            float addition = statAdditions.ContainsKey(statName) ? statAdditions[statName] : 0f;
            return baseValue * multiplier + addition;
        }

        // Public getters for stats
        public float GetHP() => currentHP;
        public float GetMaxHP() => baseHP;
        public float GetStamina() => currentStamina;
        public float GetMaxStamina() => baseStamina;
        public float GetCurrentWeight() => currentWeight;
        public float GetWeightCapacity() => baseWeightCapacity;
        public float GetVisionRadius() => GetFinalStat("Vision", baseVisionRadius);
        public float GetNoiseLevel() => GetFinalStat("Noise", baseNoiseLevel);
        public float GetSpeedMultiplier() => statMultipliers.ContainsKey("MoveSpeed") ? statMultipliers["MoveSpeed"] : 1f;

        // Setters
        public void SetHP(float hp) => currentHP = Mathf.Clamp(hp, 0f, baseHP);
        public void SetStamina(float stamina) => currentStamina = Mathf.Clamp(stamina, 0f, baseStamina);
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
            currentHP = Mathf.Min(baseHP, currentHP + amount);
        }

        /// <summary>
        /// Check if character is alive
        /// </summary>
        public bool IsAlive() => currentHP > 0f;

        /// <summary>
        /// Get weight percentage (0-1)
        /// </summary>
        public float GetWeightPercentage() => baseWeightCapacity > 0 ? currentWeight / baseWeightCapacity : 0f;

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
        public StatusEffectConfigData Config;
        public float Duration;
        public float TimeRemaining;
    }
}

