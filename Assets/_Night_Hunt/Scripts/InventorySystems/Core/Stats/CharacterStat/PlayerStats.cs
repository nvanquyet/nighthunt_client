using System;
using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;
using _Night_Hunt.Scripts.InventorySystems.Core.Stats.CharacterStat;

namespace NightHunt.Inventory.Stats
{
    /// <summary>
    /// Manages character stats with proper modifier tracking by sourceId.
    /// Formula: FinalStat = (BaseStat + Σ FlatModifiers) × (1 + Σ PercentageModifiers)
    /// 
    /// INTEGRATION:
    /// - Subscribes to PlayerStatsManager events
    /// - Tracks modifiers per source (item instanceId)
    /// - Recalculates stats when modifiers change
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private PlayerStatData statData;
        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;
        
        // Base values from PlayerStatData
        private readonly Dictionary<CharacterStatType, float> _baseValue = new Dictionary<CharacterStatType, float>();
        
        // Current dynamic values (Health, Stamina can be < max)
        private readonly Dictionary<CharacterStatType, float> _currentValue = new Dictionary<CharacterStatType, float>();
        
        // Final calculated values (with modifiers applied)
        private readonly Dictionary<CharacterStatType, float> _finalValue = new Dictionary<CharacterStatType, float>();
        
        // Modifier tracking: [StatType][SourceId][ModifierList]
        private readonly Dictionary<CharacterStatType, Dictionary<string, List<ModifierEntry>>> _modifiers 
            = new Dictionary<CharacterStatType, Dictionary<string, List<ModifierEntry>>>();
        
        // Components
        //private PlayerStatsManager statsManager;
        
        public event Action OnStatsRecalculated;
        public event Action<CharacterStatType, float, float> OnStatChanged; // (statType, oldValue, newValue)
        
        #region Lifecycle
        
        void Awake()
        {
            //statsManager = GetComponent<PlayerStatsManager>();
            InitializeStats();
        }

        void OnEnable()
        {
            SubscribeToEvents();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        
        #endregion
        
        #region Event Subscription
        
        void SubscribeToEvents()
        {
            // if (statsManager == null) return;
            //
            // statsManager.OnModifierAdded += HandleModifierAdded;
            // statsManager.OnModifierRemoved += HandleModifierRemoved;
            // statsManager.OnAllModifiersRemovedForSource += HandleAllModifiersRemovedForSource;
        }
 
        void UnsubscribeFromEvents()
        {
            // if (statsManager == null) return;
            //
            // statsManager.OnModifierAdded -= HandleModifierAdded;
            // statsManager.OnModifierRemoved -= HandleModifierRemoved;
            // statsManager.OnAllModifiersRemovedForSource -= HandleAllModifiersRemovedForSource;
        }
        
        #endregion
        
        #region Modifier Handling
        
        private void HandleModifierAdded(StatModifierData modifier, string sourceId)
        {
            // Only handle Character stat modifiers
            if (modifier.Target != StatModifierTarget.Character)
                return;
            
            var statType = modifier.CharacterStat;
            
            // Ensure dictionaries exist
            if (!_modifiers.ContainsKey(statType))
                _modifiers[statType] = new Dictionary<string, List<ModifierEntry>>();
            
            if (!_modifiers[statType].ContainsKey(sourceId))
                _modifiers[statType][sourceId] = new List<ModifierEntry>();
            
            // Add modifier
            _modifiers[statType][sourceId].Add(new ModifierEntry
            {
                CalculationType = modifier.CalculationType,
                Value = modifier.Value
            });
            
            Log($"Added modifier: {modifier.GetDisplayString()} from source {sourceId}");
            
            // Recalculate affected stat
            RecalculateStat(statType);
        }
        
        private void HandleModifierRemoved(StatModifierData modifier, string sourceId)
        {
            if (modifier.Target != StatModifierTarget.Character)
                return;
            
            var statType = modifier.CharacterStat;
            
            if (!_modifiers.ContainsKey(statType) || !_modifiers[statType].ContainsKey(sourceId))
                return;
            
            // Find and remove the specific modifier
            var modifierList = _modifiers[statType][sourceId];
            var entry = modifierList.FirstOrDefault(m => 
                m.CalculationType == modifier.CalculationType && 
                Mathf.Approximately(m.Value, modifier.Value));
            
            if (entry != null)
            {
                modifierList.Remove(entry);
                Log($"Removed modifier: {modifier.GetDisplayString()} from source {sourceId}");
            }
            
            // Clean up empty lists
            if (modifierList.Count == 0)
                _modifiers[statType].Remove(sourceId);
            
            // Recalculate affected stat
            RecalculateStat(statType);
        }
        
        private void HandleAllModifiersRemovedForSource(string sourceId)
        {
            List<CharacterStatType> affectedStats = new List<CharacterStatType>();
            
            // Remove all modifiers from this source
            foreach (var statType in _modifiers.Keys.ToList())
            {
                if (_modifiers[statType].ContainsKey(sourceId))
                {
                    _modifiers[statType].Remove(sourceId);
                    affectedStats.Add(statType);
                }
            }
            
            Log($"Removed all modifiers from source {sourceId}");
            
            // Recalculate all affected stats
            foreach (var statType in affectedStats)
            {
                RecalculateStat(statType);
            }
        }
        
        #endregion
        
        #region Initialization
        
        void InitializeStats()
        {
            if (statData == null)
            {
                LogError("PlayerStatData is null! Cannot initialize stats.");
                return;
            }
            
            // Initialize all stats with base values
            foreach (CharacterStatType statType in System.Enum.GetValues(typeof(CharacterStatType)))
            {
                float baseValue = statData.GetValueStat(statType);
                _baseValue[statType] = baseValue;
                _currentValue[statType] = baseValue;
                _finalValue[statType] = baseValue;
                
                // Initialize modifier tracking
                _modifiers[statType] = new Dictionary<string, List<ModifierEntry>>();
            }
            
            Log("Stats initialized");
        }
        
        #endregion
        
        #region Stat Calculation
        
        /// <summary>
        /// Recalculate a specific stat with all active modifiers.
        /// Formula: FinalStat = (BaseStat + Σ FlatModifiers) × (1 + Σ PercentageModifiers)
        /// </summary>
        private void RecalculateStat(CharacterStatType statType)
        {
            float baseValue = _baseValue[statType];
            float oldFinalValue = _finalValue[statType];
            
            // Step 1: Sum all flat modifiers
            float flatSum = 0f;
            // Step 2: Sum all percentage modifiers
            float percentSum = 0f;
            
            if (_modifiers.ContainsKey(statType))
            {
                foreach (var sourceModifiers in _modifiers[statType].Values)
                {
                    foreach (var modifier in sourceModifiers)
                    {
                        if (modifier.CalculationType == ModifierCalculationType.Flat)
                        {
                            flatSum += modifier.Value;
                        }
                        else if (modifier.CalculationType == ModifierCalculationType.Percentage)
                        {
                            percentSum += modifier.Value;
                        }
                    }
                }
            }
            
            // Calculate final value
            float newFinalValue = (baseValue + flatSum) * (1f + percentSum);
            _finalValue[statType] = Mathf.Max(0f, newFinalValue); // Prevent negative stats
            
            // Adjust current value if it exceeds new max (for Health/Stamina)
            if (IsResourceStat(statType))
            {
                _currentValue[statType] = Mathf.Min(_currentValue[statType], _finalValue[statType]);
            }
            else
            {
                _currentValue[statType] = _finalValue[statType];
            }
            
            Log($"Recalculated {statType}: Base={baseValue}, Flat={flatSum:F1}, Percent={percentSum:F2} => Final={_finalValue[statType]:F1}");
            
            // Fire events
            if (!Mathf.Approximately(oldFinalValue, _finalValue[statType]))
            {
                OnStatChanged?.Invoke(statType, oldFinalValue, _finalValue[statType]);
            }
            
            OnStatsRecalculated?.Invoke();
        }
        
        /// <summary>
        /// Recalculate all stats.
        /// </summary>
        public void RecalculateAllStats()
        {
            foreach (CharacterStatType statType in System.Enum.GetValues(typeof(CharacterStatType)))
            {
                RecalculateStat(statType);
            }
        }
        
        /// <summary>
        /// Check if stat is a resource (can be < max).
        /// </summary>
        private bool IsResourceStat(CharacterStatType statType)
        {
            return statType == CharacterStatType.Health || 
                   statType == CharacterStatType.Stamina;
        }
        
        #endregion
        
        #region Public Getters
        
        // Current values (dynamic)
        public float GetCurrentHealth() => _currentValue[CharacterStatType.Health];
        public float GetCurrentStamina() => _currentValue[CharacterStatType.Stamina];
        
        // Max values (with modifiers)
        public float GetMaxHealth() => _finalValue[CharacterStatType.Health];
        public float GetMaxStamina() => _finalValue[CharacterStatType.Stamina];
        public float GetMoveSpeed() => _finalValue[CharacterStatType.MoveSpeed];
        public float GetWeightCapacity() => _finalValue[CharacterStatType.WeightCapacity];
        public float GetVisionRadius() => _finalValue[CharacterStatType.VisionRadius];
        
        /// <summary>
        /// Get final value of any stat.
        /// </summary>
        public float GetFinalStat(CharacterStatType statType)
        {
            return _finalValue.TryGetValue(statType, out float value) ? value : 0f;
        }
        
        /// <summary>
        /// Get current value of any stat.
        /// </summary>
        public float GetCurrentStat(CharacterStatType statType)
        {
            return _currentValue.TryGetValue(statType, out float value) ? value : 0f;
        }
        
        public bool IsAlive => _currentValue[CharacterStatType.Health] > 0;
        
        #endregion
        
        #region Public Setters
        
        public void SetPlayerStatData(PlayerStatData newStatData)
        {
            statData = newStatData;
            InitializeStats();
            RecalculateAllStats();
        }
        
        #endregion
        
        #region Resource Management (Health/Stamina)
        
        public void RestoreHealthToFull()
        {
            float oldValue = _currentValue[CharacterStatType.Health];
            _currentValue[CharacterStatType.Health] = _finalValue[CharacterStatType.Health];
            OnStatChanged?.Invoke(CharacterStatType.Health, oldValue, _currentValue[CharacterStatType.Health]);
            Log($"Restored health to full: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void RestoreHealth(float amount)
        {
            float oldValue = _currentValue[CharacterStatType.Health];
            _currentValue[CharacterStatType.Health] = Mathf.Min(
                _currentValue[CharacterStatType.Health] + amount, 
                _finalValue[CharacterStatType.Health]
            );
            OnStatChanged?.Invoke(CharacterStatType.Health, oldValue, _currentValue[CharacterStatType.Health]);
            Log($"Restored {amount} health. Current: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void RestoreStamina(float amount)
        {
            float oldValue = _currentValue[CharacterStatType.Stamina];
            _currentValue[CharacterStatType.Stamina] = Mathf.Min(
                _currentValue[CharacterStatType.Stamina] + amount, 
                _finalValue[CharacterStatType.Stamina]
            );
            OnStatChanged?.Invoke(CharacterStatType.Stamina, oldValue, _currentValue[CharacterStatType.Stamina]);
            Log($"Restored {amount} stamina. Current: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        public void RestoreStaminaToFull()
        {
            float oldValue = _currentValue[CharacterStatType.Stamina];
            _currentValue[CharacterStatType.Stamina] = _finalValue[CharacterStatType.Stamina];
            OnStatChanged?.Invoke(CharacterStatType.Stamina, oldValue, _currentValue[CharacterStatType.Stamina]);
            Log($"Restored stamina to full: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        public void TakeDamage(float damage)
        {
            float oldValue = _currentValue[CharacterStatType.Health];
            _currentValue[CharacterStatType.Health] = Mathf.Max(_currentValue[CharacterStatType.Health] - damage, 0);
            OnStatChanged?.Invoke(CharacterStatType.Health, oldValue, _currentValue[CharacterStatType.Health]);
            Log($"Took {damage} damage. Current Health: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void ConsumeStamina(float amount)
        {
            float oldValue = _currentValue[CharacterStatType.Stamina];
            _currentValue[CharacterStatType.Stamina] = Mathf.Max(_currentValue[CharacterStatType.Stamina] - amount, 0);
            OnStatChanged?.Invoke(CharacterStatType.Stamina, oldValue, _currentValue[CharacterStatType.Stamina]);
            Log($"Consumed {amount} stamina. Current: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        public void SetCurrentHealth(float health)
        {
            float oldValue = _currentValue[CharacterStatType.Health];
            _currentValue[CharacterStatType.Health] = Mathf.Clamp(health, 0, _finalValue[CharacterStatType.Health]);
            OnStatChanged?.Invoke(CharacterStatType.Health, oldValue, _currentValue[CharacterStatType.Health]);
            Log($"Set current health to {health}. Current: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void SetCurrentStamina(float stamina)
        {
            float oldValue = _currentValue[CharacterStatType.Stamina];
            _currentValue[CharacterStatType.Stamina] = Mathf.Clamp(stamina, 0, _finalValue[CharacterStatType.Stamina]);
            OnStatChanged?.Invoke(CharacterStatType.Stamina, oldValue, _currentValue[CharacterStatType.Stamina]);
            Log($"Set current stamina to {stamina}. Current: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Log All Stats")]
        public void LogAllStats()
        {
            UnityEngine.Debug.Log("=== Player Stats ===");
            foreach (CharacterStatType statType in System.Enum.GetValues(typeof(CharacterStatType)))
            {
                UnityEngine.Debug.Log($"{statType}: Current={_currentValue[statType]:F1}, Final={_finalValue[statType]:F1}, Base={_baseValue[statType]:F1}");
            }
        }
        
        [ContextMenu("Log All Modifiers")]
        public void LogAllModifiers()
        {
            UnityEngine.Debug.Log("=== Active Modifiers ===");
            foreach (var statType in _modifiers.Keys)
            {
                foreach (var source in _modifiers[statType].Keys)
                {
                    foreach (var modifier in _modifiers[statType][source])
                    {
                        UnityEngine.Debug.Log($"{statType} from {source}: {modifier.CalculationType} {modifier.Value:F2}");
                    }
                }
            }
        }
        
        void Log(string message)
        {
            if (enableDebugLogs)
                UnityEngine.Debug.Log($"[PlayerStats] {message}");
        }
        
        void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[PlayerStats] {message}");
        }
        
        #endregion
        
        /// <summary>
        /// Internal modifier entry for tracking.
        /// </summary>
        private class ModifierEntry
        {
            public ModifierCalculationType CalculationType;
            public float Value;
        }
    }
}