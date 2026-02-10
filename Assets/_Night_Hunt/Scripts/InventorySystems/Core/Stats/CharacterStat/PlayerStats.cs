using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;
using _Night_Hunt.Scripts.InventorySystems.Core.Stats.CharacterStat;

namespace NightHunt.Inventory.Stats
{
    /// <summary>
    /// Manages character stats with modifier tracking by sourceId.
    /// Formula: FinalStat = (BaseStat × Σ Multipliers) + Σ Additions
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private PlayerStatData statData;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;
        
        
        private readonly Dictionary<CharacterStatType, float> _currentValue = new Dictionary<CharacterStatType, float>();
        private readonly Dictionary<CharacterStatType, float> _finalValue = new Dictionary<CharacterStatType, float>();
        
        public void SetPlayerStatData(PlayerStatData newStatData)
        {
            statData = newStatData;
            InitializeStats();
        }
        
        
        #region Lifecycle

        void Awake()
        {
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
            // CharacterStatsEvents.OnAddModifier += HandleAddModifier;
            // CharacterStatsEvents.OnRemoveModifier += HandleRemoveModifier;
            // CharacterStatsEvents.OnRemoveAllModifiers += HandleRemoveAllModifiers;
        }

        void UnsubscribeFromEvents()
        {
            // CharacterStatsEvents.OnAddModifier -= HandleAddModifier;
            // CharacterStatsEvents.OnRemoveModifier -= HandleRemoveModifier;
            // CharacterStatsEvents.OnRemoveAllModifiers -= HandleRemoveAllModifiers;
        }

        #endregion

        #region Initialization

        void InitializeStats()
        {
            // Initialize cached stats with base values
            foreach (var i in System.Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>())
            {
                _currentValue[i] = statData.GetValueStat(i);
                _finalValue[i] = statData.GetValueStat(i);
            }
        }
        #endregion


        // Convenience getters
        public float GetCurrentHealth() => _currentValue[CharacterStatType.Health];
        public float GetMaxHealth() => _finalValue[CharacterStatType.Health];
        public float GetCurrentStamina() => _currentValue[CharacterStatType.Stamina];
        public float GetMaxStamina() => _finalValue[CharacterStatType.Stamina];
        public float GetMoveSpeed() => _currentValue[CharacterStatType.MoveSpeed];
        public float GetWeightCapacity() => _finalValue[CharacterStatType.WeightCapacity];
        public float GetVisionRadius() => _finalValue[CharacterStatType.VisionRadius];
        
        
        public void RestoreHealthToFull()
        {
            _currentValue[CharacterStatType.Health] = _finalValue[CharacterStatType.Health];
            Log($"Restored health to full. Current Health: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void RestoreHealth(float amount)
        {
            _currentValue[CharacterStatType.Health] = Mathf.Min(_currentValue[CharacterStatType.Health] + amount, _finalValue[CharacterStatType.Health]);
            Log($"Restored {amount} health. Current Health: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void RestoreStamina(float amount)
        {
            _currentValue[CharacterStatType.Stamina] = Mathf.Min(_currentValue[CharacterStatType.Stamina] + amount, _finalValue[CharacterStatType.Stamina]);
            Log($"Restored {amount} stamina. Current Stamina: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        public void RestoreStaminaToFull()
        {
            _currentValue[CharacterStatType.Stamina] = _finalValue[CharacterStatType.Stamina];
            Log($"Restored stamina to full. Current Stamina: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        public void TakeDamage(float damage)
        {
            _currentValue[CharacterStatType.Health] = Mathf.Max(_currentValue[CharacterStatType.Health] - damage, 0);
            Log($"Took {damage} damage. Current Health: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void ConsumeStamina(float amount)
        {
            _currentValue[CharacterStatType.Stamina] = Mathf.Max(_currentValue[CharacterStatType.Stamina] - amount, 0);
            Log($"Consumed {amount} stamina. Current Stamina: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        public void SetCurrentHealth(float health)
        {
            _currentValue[CharacterStatType.Health] = Mathf.Clamp(health, 0, _finalValue[CharacterStatType.Health]);
            Log($"Set current health to {health}. Current Health: {_currentValue[CharacterStatType.Health]}");
        }
        
        public void SetCurrentStamina(float stamina)
        {
            _currentValue[CharacterStatType.Stamina] = Mathf.Clamp(stamina, 0, _finalValue[CharacterStatType.Stamina]);
            Log($"Set current stamina to {stamina}. Current Stamina: {_currentValue[CharacterStatType.Stamina]}");
        }
        
        

        #region Debug

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[CharacterStats] {message}");
        }
        #endregion

        public bool IsAlive => _currentValue[CharacterStatType.Health] > 0;
    }
}