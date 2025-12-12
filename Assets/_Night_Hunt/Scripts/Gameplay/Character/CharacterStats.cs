using System;
using System.Collections.Generic;
using NightHunt.Data.Configs;
using UnityEngine;

namespace _Night_Hunt.Scripts.Gameplay.Character
{
    /// <summary>
    /// Dynamic character stats system
    /// Formula: FinalStat = BaseStat × Multipliers + Additions
    /// All modifiers are recalculated when dirty flag is set
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        [Header("Base Stats (From Config)")]
        [SerializeField] private float baseHP = 100f;
        [SerializeField] private float baseStamina = 100f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float baseWeightCapacity = 20f;
        [SerializeField] private float baseVisionRadius = 12f;
        [SerializeField] private float baseNoiseLevel = 1f;
        [SerializeField] private float baseReviveTime = 4f;
        [SerializeField] private float baseBleedoutTime = 20f;
        
        // Runtime Stats
        private float currentHP;
        private float currentStamina;
        
        // Final Calculated Stats (cached)
        private float finalMaxHP;
        private float finalMaxStamina;
        private float finalMoveSpeed;
        private float finalWeightCapacity;
        private float finalVisionRadius;
        private float finalNoiseLevel;
        private float finalReviveTime;
        
        // Stat Modifiers
        private List<StatModifier> activeModifiers = new List<StatModifier>();
        
        // Dirty flag - recalculate when true
        private bool isDirty = true;
        
        // Events
        public event Action<float, float> OnHPChanged; // current, max
        public event Action<float, float> OnStaminaChanged; // current, max
        public event Action OnDeath;
        public event Action<StatType> OnStatModified;

        private void Awake()
        {
            InitializeStats();
        }

        private void Update()
        {
            if (isDirty)
            {
                RecalculateStats();
                isDirty = false;
            }
        }

        #region Initialization

        public void InitializeFromConfig(CharacterConfig config)
        {
            baseHP = config.BaseHP;
            baseStamina = config.BaseStamina;
            baseMoveSpeed = config.BaseMoveSpeed;
            baseWeightCapacity = config.BaseWeightCapacity;
            baseVisionRadius = config.BaseVisionRadius;
            baseNoiseLevel = config.BaseNoiseLevel;
            baseReviveTime = config.BaseReviveTime;
            baseBleedoutTime = config.BaseBleedoutTime;
            
            InitializeStats();
        }

        private void InitializeStats()
        {
            RecalculateStats();
            currentHP = finalMaxHP;
            currentStamina = finalMaxStamina;
            
            OnHPChanged?.Invoke(currentHP, finalMaxHP);
            OnStaminaChanged?.Invoke(currentStamina, finalMaxStamina);
        }

        #endregion

        #region Stat Calculation

        private void RecalculateStats()
        {
            finalMaxHP = CalculateStat(StatType.MaxHP, baseHP);
            finalMaxStamina = CalculateStat(StatType.MaxStamina, baseStamina);
            finalMoveSpeed = CalculateStat(StatType.MoveSpeed, baseMoveSpeed);
            finalWeightCapacity = CalculateStat(StatType.WeightCapacity, baseWeightCapacity);
            finalVisionRadius = CalculateStat(StatType.VisionRadius, baseVisionRadius);
            finalNoiseLevel = CalculateStat(StatType.NoiseLevel, baseNoiseLevel);
            finalReviveTime = CalculateStat(StatType.ReviveTime, baseReviveTime);
            
            // Clamp HP and Stamina if they exceed new max
            currentHP = Mathf.Min(currentHP, finalMaxHP);
            currentStamina = Mathf.Min(currentStamina, finalMaxStamina);
        }

        private float CalculateStat(StatType statType, float baseValue)
        {
            float multiplier = 1f;
            float addition = 0f;
            
            foreach (var mod in activeModifiers)
            {
                if (mod.targetStat != statType) continue;
                
                switch (mod.operationType)
                {
                    case ModifierOperation.Add:
                        addition += mod.value;
                        break;
                    
                    case ModifierOperation.Multiply:
                        multiplier *= mod.value;
                        break;
                    
                    case ModifierOperation.MultiplyAdditive:
                        multiplier += (mod.value - 1f);
                        break;
                }
            }
            
            return (baseValue * multiplier) + addition;
        }

        #endregion

        #region Modifier Management

        public void AddModifier(StatModifier modifier)
        {
            // Check if stackable
            if (!modifier.stackable)
            {
                // Remove existing modifier with same ID
                activeModifiers.RemoveAll(m => m.modifierId == modifier.modifierId);
            }
            
            activeModifiers.Add(modifier);
            isDirty = true;
            
            OnStatModified?.Invoke(modifier.targetStat);
        }

        public void RemoveModifier(string modifierId)
        {
            int removed = activeModifiers.RemoveAll(m => m.modifierId == modifierId);
            
            if (removed > 0)
            {
                isDirty = true;
            }
        }

        public void RemoveAllModifiers()
        {
            if (activeModifiers.Count > 0)
            {
                activeModifiers.Clear();
                isDirty = true;
            }
        }

        public bool HasModifier(string modifierId)
        {
            return activeModifiers.Exists(m => m.modifierId == modifierId);
        }

        #endregion

        #region HP Management

        public void TakeDamage(float amount, int attackerId = -1)
        {
            if (currentHP <= 0) return;
            
            currentHP = Mathf.Max(0, currentHP - amount);
            OnHPChanged?.Invoke(currentHP, finalMaxHP);
            
            if (currentHP <= 0)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (currentHP >= finalMaxHP) return;
            
            currentHP = Mathf.Min(finalMaxHP, currentHP + amount);
            OnHPChanged?.Invoke(currentHP, finalMaxHP);
        }

        public void SetHP(float value)
        {
            currentHP = Mathf.Clamp(value, 0, finalMaxHP);
            OnHPChanged?.Invoke(currentHP, finalMaxHP);
        }

        #endregion

        #region Stamina Management

        public void UseStamina(float amount)
        {
            if (currentStamina <= 0) return;
            
            currentStamina = Mathf.Max(0, currentStamina - amount);
            OnStaminaChanged?.Invoke(currentStamina, finalMaxStamina);
        }

        public void RegenerateStamina(float amount)
        {
            if (currentStamina >= finalMaxStamina) return;
            
            currentStamina = Mathf.Min(finalMaxStamina, currentStamina + amount);
            OnStaminaChanged?.Invoke(currentStamina, finalMaxStamina);
        }

        public void SetStamina(float value)
        {
            currentStamina = Mathf.Clamp(value, 0, finalMaxStamina);
            OnStaminaChanged?.Invoke(currentStamina, finalMaxStamina);
        }

        #endregion

        #region Getters

        public float GetCurrentHP() => currentHP;
        public float GetMaxHP() => finalMaxHP;
        public float GetHPPercent() => finalMaxHP > 0 ? currentHP / finalMaxHP : 0f;
        
        public float GetCurrentStamina() => currentStamina;
        public float GetMaxStamina() => finalMaxStamina;
        public float GetStaminaPercent() => finalMaxStamina > 0 ? currentStamina / finalMaxStamina : 0f;
        
        public float GetMoveSpeed() => finalMoveSpeed;
        public float GetWeightCapacity() => finalWeightCapacity;
        public float GetVisionRadius() => finalVisionRadius;
        public float GetNoiseLevel() => finalNoiseLevel;
        public float GetReviveTime() => finalReviveTime;
        
        public bool IsAlive() => currentHP > 0;
        public bool HasStamina() => currentStamina > 0;

        #endregion
    }
}