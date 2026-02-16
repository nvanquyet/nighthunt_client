using UnityEngine;
using GameplaySystems.Stat;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Configs;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Test script for PlayerStatSystem
    /// Attach to player GameObject to test stat system functionality
    /// 
    /// Tests:
    /// - Initialization from PlayerStatConfig
    /// - Adding/removing modifiers
    /// - Weight system calculations
    /// - Movement speed penalties
    /// - Events firing correctly
    /// </summary>
    public class PlayerStatSystemTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStatSystem _statSystem;
        
        [Header("Test Controls")]
        [SerializeField] private bool _runTestsOnStart = false;
        [SerializeField] private bool _subscribeToEvents = true;
        
        [Header("Manual Test Controls")]
        [SerializeField] private float _testWeightToAdd = 50f;
        [SerializeField] private float _testArmorToAdd = 30f;
        
        private void Start()
        {
            if (_statSystem == null)
            {
                _statSystem = GetComponent<PlayerStatSystem>();
            }
            
            if (_statSystem == null)
            {
                Debug.LogError("[StatTest] PlayerStatSystem not found!");
                return;
            }
            
            // Subscribe to events
            if (_subscribeToEvents)
            {
                SubscribeToEvents();
            }
            
            // Run automated tests
            if (_runTestsOnStart)
            {
                Invoke(nameof(RunAllTests), 1f); // Wait for network initialization
            }
        }
        
        private void OnDestroy()
        {
            if (_statSystem != null && _subscribeToEvents)
            {
                UnsubscribeFromEvents();
            }
        }
        
        #region Event Subscription
        
        private void SubscribeToEvents()
        {
            _statSystem.OnStatChanged += OnStatChanged;
            _statSystem.OnWeightChanged += OnWeightChanged;
            _statSystem.OnOverweightChanged += OnOverweightChanged;
            
            Debug.Log("[StatTest] Subscribed to stat events");
        }
        
        private void UnsubscribeFromEvents()
        {
            _statSystem.OnStatChanged -= OnStatChanged;
            _statSystem.OnWeightChanged -= OnWeightChanged;
            _statSystem.OnOverweightChanged -= OnOverweightChanged;
        }
        
        private void OnStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            Debug.Log($"[StatTest Event] {type}: {oldValue:F1} → {newValue:F1} (Δ{newValue - oldValue:+0.0;-0.0})");
        }
        
        private void OnWeightChanged(float current, float capacity)
        {
            float percent = capacity > 0 ? current / capacity : 0;
            Debug.Log($"[StatTest Event] Weight: {current:F1}/{capacity:F1} ({percent:P0})");
        }
        
        private void OnOverweightChanged(float weightPercent)
        {
            Debug.Log($"[StatTest Event] Overweight Status: {weightPercent:P0}");
        }
        
        #endregion
        
        #region Automated Tests
        
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== STARTING PLAYER STAT SYSTEM TESTS ===\n");
            
            Test1_BasicStatAccess();
            Test2_FlatModifiers();
            Test3_PercentageModifiers();
            Test4_MultipleModifiers();
            Test5_RemoveModifiers();
            Test6_WeightSystem();
            Test7_OverweightPenalties();
            
            Debug.Log("\n=== ALL TESTS COMPLETED ===");
        }
        
        private void Test1_BasicStatAccess()
        {
            Debug.Log("\n--- Test 1: Basic Stat Access ---");
            
            float health = _statSystem.GetStat(PlayerStatType.Health);
            float maxHealth = _statSystem.GetStat(PlayerStatType.MaxHealth);
            float movementSpeed = _statSystem.GetStat(PlayerStatType.MovementSpeed);
            
            Debug.Log($"Health: {health}/{maxHealth}");
            Debug.Log($"Movement Speed: {movementSpeed}");
            
            Debug.Assert(health > 0, "Health should be > 0");
            Debug.Assert(maxHealth > 0, "MaxHealth should be > 0");
            Debug.Assert(movementSpeed > 0, "MovementSpeed should be > 0");
            
            Debug.Log("✓ Test 1 PASSED");
        }
        
        private void Test2_FlatModifiers()
        {
            Debug.Log("\n--- Test 2: Flat Modifiers ---");
            
            float baseStat = _statSystem.GetBaseStat(PlayerStatType.Armor);
            Debug.Log($"Base Armor: {baseStat}");
            
            // Add flat modifier
            var modifier = StatModifier.CreateFlat("test_vest", 50f, 0, "Test Vest Armor");
            _statSystem.AddModifier(PlayerStatType.Armor, modifier);
            
            float newStat = _statSystem.GetStat(PlayerStatType.Armor);
            Debug.Log($"After +50 flat: {newStat}");
            
            Debug.Assert(Mathf.Approximately(newStat, baseStat + 50f), "Armor should be base + 50");
            
            Debug.Log("✓ Test 2 PASSED");
        }
        
        private void Test3_PercentageModifiers()
        {
            Debug.Log("\n--- Test 3: Percentage Modifiers ---");
            
            float baseSpeed = _statSystem.GetBaseStat(PlayerStatType.MovementSpeed);
            Debug.Log($"Base Movement Speed: {baseSpeed}");
            
            // Add -10% modifier
            var modifier = StatModifier.CreatePercentage("test_armor", -10f, 0, "Heavy Armor Penalty");
            _statSystem.AddModifier(PlayerStatType.MovementSpeed, modifier);
            
            float newSpeed = _statSystem.GetStat(PlayerStatType.MovementSpeed);
            float expected = baseSpeed * 0.9f; // -10%
            
            Debug.Log($"After -10%: {newSpeed} (expected: {expected})");
            
            Debug.Assert(Mathf.Approximately(newSpeed, expected), "Speed should be reduced by 10%");
            
            Debug.Log("✓ Test 3 PASSED");
        }
        
        private void Test4_MultipleModifiers()
        {
            Debug.Log("\n--- Test 4: Multiple Modifiers ---");
            
            // Clear previous modifiers
            _statSystem.RemoveAllModifiersFromSource("test_vest");
            _statSystem.RemoveAllModifiersFromSource("test_armor");
            
            float baseArmor = _statSystem.GetBaseStat(PlayerStatType.Armor);
            Debug.Log($"Base Armor: {baseArmor}");
            
            // Add multiple modifiers
            _statSystem.AddModifier(PlayerStatType.Armor, StatModifier.CreateFlat("vest", 30f));
            _statSystem.AddModifier(PlayerStatType.Armor, StatModifier.CreateFlat("helmet", 20f));
            _statSystem.AddModifier(PlayerStatType.Armor, StatModifier.CreatePercentage("buff", 10f));
            
            float finalArmor = _statSystem.GetStat(PlayerStatType.Armor);
            float expected = (baseArmor + 30f + 20f) * 1.1f; // Flat first, then percentage
            
            Debug.Log($"After multiple modifiers: {finalArmor} (expected: {expected})");
            
            Debug.Assert(Mathf.Approximately(finalArmor, expected), "Multiple modifiers should stack correctly");
            
            Debug.Log("✓ Test 4 PASSED");
        }
        
        private void Test5_RemoveModifiers()
        {
            Debug.Log("\n--- Test 5: Remove Modifiers ---");
            
            float beforeRemove = _statSystem.GetStat(PlayerStatType.Armor);
            Debug.Log($"Armor before remove: {beforeRemove}");
            
            // Remove one modifier
            _statSystem.RemoveModifier(PlayerStatType.Armor, "vest");
            
            float afterRemove = _statSystem.GetStat(PlayerStatType.Armor);
            Debug.Log($"Armor after removing vest: {afterRemove}");
            
            Debug.Assert(afterRemove < beforeRemove, "Armor should decrease after removing modifier");
            
            // Remove all from source
            _statSystem.RemoveAllModifiersFromSource("helmet");
            _statSystem.RemoveAllModifiersFromSource("buff");
            
            float finalArmor = _statSystem.GetStat(PlayerStatType.Armor);
            float baseArmor = _statSystem.GetBaseStat(PlayerStatType.Armor);
            
            Debug.Log($"Armor after removing all: {finalArmor} (base: {baseArmor})");
            
            Debug.Assert(Mathf.Approximately(finalArmor, baseArmor), "Should return to base after removing all modifiers");
            
            Debug.Log("✓ Test 5 PASSED");
        }
        
        private void Test6_WeightSystem()
        {
            Debug.Log("\n--- Test 6: Weight System ---");
            
            float capacity = _statSystem.GetWeightCapacity();
            float current = _statSystem.GetCurrentWeight();
            float percent = _statSystem.GetWeightPercent();
            
            Debug.Log($"Weight: {current}/{capacity} ({percent:P0})");
            
            // Add weight
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, 
                StatModifier.CreateFlat("inventory", 50f, 0, "Inventory Weight"));
            
            float newCurrent = _statSystem.GetCurrentWeight();
            float newPercent = _statSystem.GetWeightPercent();
            
            Debug.Log($"After adding 50kg: {newCurrent}/{capacity} ({newPercent:P0})");
            
            Debug.Assert(newCurrent > current, "Weight should increase");
            Debug.Assert(_statSystem.CanCarryWeight(30f), "Should be able to carry 30kg more");
            
            Debug.Log("✓ Test 6 PASSED");
        }
        
        private void Test7_OverweightPenalties()
        {
            Debug.Log("\n--- Test 7: Overweight Penalties ---");
            
            // Clear weight
            _statSystem.RemoveAllModifiersFromSource("inventory");
            
            float capacity = _statSystem.GetWeightCapacity();
            float baseSpeed = _statSystem.GetMovementSpeedMultiplier();
            
            Debug.Log($"Base speed multiplier: {baseSpeed:P0}");
            Debug.Assert(Mathf.Approximately(baseSpeed, 1f), "Should be 100% at normal weight");
            
            // Test at 100% weight
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, 
                StatModifier.CreateFlat("test", capacity, 0, "Test Weight"));
            
            float speed100 = _statSystem.GetMovementSpeedMultiplier();
            Debug.Log($"Speed at 100% weight: {speed100:P0}");
            Debug.Assert(Mathf.Approximately(speed100, 1f), "Should still be 100% at exactly capacity");
            
            // Test at 125% weight
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, 
                StatModifier.CreateFlat("test", capacity * 1.25f));
            
            float speed125 = _statSystem.GetMovementSpeedMultiplier();
            Debug.Log($"Speed at 125% weight: {speed125:P0}");
            Debug.Assert(speed125 < 1f && speed125 > 0.1f, "Should be reduced but > 10%");
            
            // Test at 150% weight (max penalty)
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, 
                StatModifier.CreateFlat("test", capacity * 1.5f));
            
            float speed150 = _statSystem.GetMovementSpeedMultiplier();
            Debug.Log($"Speed at 150% weight: {speed150:P0}");
            Debug.Assert(Mathf.Approximately(speed150, 0.1f), "Should be 10% at max overweight");
            
            // Test at 200% weight (still min)
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, 
                StatModifier.CreateFlat("test", capacity * 2f));
            
            float speed200 = _statSystem.GetMovementSpeedMultiplier();
            Debug.Log($"Speed at 200% weight: {speed200:P0}");
            Debug.Assert(Mathf.Approximately(speed200, 0.1f), "Should stay at 10% even above 150%");
            
            // Cleanup
            _statSystem.RemoveAllModifiersFromSource("test");
            
            Debug.Log("✓ Test 7 PASSED");
        }
        
        #endregion
        
        #region Manual Test Buttons
        
        [ContextMenu("Add Test Weight")]
        public void AddTestWeight()
        {
            var modifier = StatModifier.CreateFlat("manual_test", _testWeightToAdd, 0, "Manual Test Weight");
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, modifier);
            
            Debug.Log($"Added {_testWeightToAdd}kg weight. Current: {_statSystem.GetCurrentWeight():F1}kg");
        }
        
        [ContextMenu("Remove Test Weight")]
        public void RemoveTestWeight()
        {
            _statSystem.RemoveAllModifiersFromSource("manual_test");
            Debug.Log($"Removed test weight. Current: {_statSystem.GetCurrentWeight():F1}kg");
        }
        
        [ContextMenu("Add Test Armor")]
        public void AddTestArmor()
        {
            var modifier = StatModifier.CreateFlat("manual_armor", _testArmorToAdd, 0, "Manual Test Armor");
            _statSystem.AddModifier(PlayerStatType.Armor, modifier);
            
            Debug.Log($"Added {_testArmorToAdd} armor. Current: {_statSystem.GetStat(PlayerStatType.Armor):F1}");
        }
        
        [ContextMenu("Remove Test Armor")]
        public void RemoveTestArmor()
        {
            _statSystem.RemoveAllModifiersFromSource("manual_armor");
            Debug.Log($"Removed test armor. Current: {_statSystem.GetStat(PlayerStatType.Armor):F1}");
        }
        
        [ContextMenu("Log Current Stats")]
        public void LogCurrentStats()
        {
            Debug.Log("=== Current Player Stats ===");
            
            var allStats = _statSystem.GetAllStats();
            foreach (var kvp in allStats)
            {
                float baseStat = _statSystem.GetBaseStat(kvp.Key);
                float modifier = kvp.Value - baseStat;
                string modStr = modifier != 0 ? $" ({modifier:+0.0;-0.0})" : "";
                Debug.Log($"{kvp.Key}: {kvp.Value:F1}{modStr}");
            }
            
            Debug.Log($"\nWeight: {_statSystem.GetCurrentWeight():F1}/{_statSystem.GetWeightCapacity():F1} ({_statSystem.GetWeightPercent():P0})");
            Debug.Log($"Movement Speed Multiplier: {_statSystem.GetMovementSpeedMultiplier():P0}");
        }
        
        [ContextMenu("Test Weight Penalties")]
        public void TestWeightPenalties()
        {
            Debug.Log("=== Testing Weight Penalties ===");
            
            float capacity = _statSystem.GetWeightCapacity();
            float[] testPercentages = { 0f, 0.5f, 0.9f, 1.0f, 1.1f, 1.25f, 1.5f, 2.0f };
            
            foreach (float percent in testPercentages)
            {
                float weight = capacity * percent;
                _statSystem.AddModifier(PlayerStatType.CurrentWeight, 
                    StatModifier.CreateFlat("weight_test", weight));
                
                float speedMult = _statSystem.GetMovementSpeedMultiplier();
                Debug.Log($"{percent:P0} weight → {speedMult:P0} speed");
            }
            
            _statSystem.RemoveAllModifiersFromSource("weight_test");
        }
        
        #endregion
    }
}