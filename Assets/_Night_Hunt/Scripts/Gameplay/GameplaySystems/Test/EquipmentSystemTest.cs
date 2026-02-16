using GameplaySystems.Core.Data;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using GameplaySystems.Inventory;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Stat;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Test script for EquipmentSystem with logging
    /// Tests equip/unequip operations and stat modifiers
    /// Outputs all test results to TestResults/EquipmentTest_[timestamp].txt
    /// </summary>
    public class EquipmentSystemTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private IPlayerStat _statSystem;
        
        [Header("Test Items")]
        [SerializeField] private string _testHelmetID = "armor_helmet";
        [SerializeField] private string _testVestID = "armor_vest";
        [SerializeField] private string _testBackpackID = "armor_backpack";
        
        [Header("Test Controls")]
        [SerializeField] private bool _runTestsOnStart = false;
        
        [Header("Logging")]
        [SerializeField] private bool _saveLogToFile = true;
        
        // Test result tracking
        private StringBuilder _testLog;
        private List<TestResult> _testResults;
        private System.DateTime _sessionStartTime;
        private System.DateTime _currentTestStartTime;
        private int _currentTestNumber;
        
        private class TestResult
        {
            public int TestNumber;
            public string TestName;
            public bool Passed;
            public float DurationMs;
            public string Details;
        }
        
        private TestResult _currentTest;
        
        private void Start()
        {
            if (_equipmentSystem == null)
                _equipmentSystem = GetComponent<EquipmentSystem>();
            
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
            
            if (_statSystem == null)
                _statSystem = GetComponent<IPlayerStat>();
            
            _testLog = new StringBuilder();
            _testResults = new List<TestResult>();
            _sessionStartTime = System.DateTime.Now;
            
            if (_runTestsOnStart)
                Invoke(nameof(RunAllTests), 2f);
        }
        
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            _testLog = new StringBuilder();
            _testResults = new List<TestResult>();
            _sessionStartTime = System.DateTime.Now;
            _currentTestNumber = 0;
            
            AppendLog("================================================================================");
            AppendLog("EQUIPMENT SYSTEM TEST RESULTS");
            AppendLog("================================================================================");
            AppendLog($"Test Session Started: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Unity Version: {Application.unityVersion}");
            AppendLog($"Platform: {Application.platform}");
            AppendLog("================================================================================\n");
            
            Debug.Log("=== STARTING EQUIPMENT SYSTEM TESTS ===\n");
            
            Test1_EquipItem();
            Test2_UnequipItem();
            Test3_SwapEquipment();
            Test4_StatModifiers();
            Test5_WeightModification();
            
            GenerateFinalReport();
            
            Debug.Log("\n=== ALL EQUIPMENT TESTS COMPLETED ===");
            
            if (_saveLogToFile)
            {
                SaveLogToFile();
            }
        }
        
        private void Test1_EquipItem()
        {
            StartTest("Equip Item");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                // Add helmet to inventory
                _inventorySystem.AddItem(_testHelmetID, 1);
                
                var items = _inventorySystem.GetItemsByDefinition(_testHelmetID);
                AssertNoThrow(items.Count > 0, "Should have helmet in inventory");
                
                var helmet = items[0];
                AppendLog($"Added helmet: {helmet.DefinitionID}, ID: {helmet.InstanceID}");
                
                // Equip helmet
                _equipmentSystem.EquipItem(helmet.InstanceID);
                
                // Verify equipped
                var equippedHelmet = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Head);
                AssertNoThrow(equippedHelmet != null, "Helmet should be equipped");
                AssertNoThrow(equippedHelmet.InstanceID == helmet.InstanceID, "Should be same helmet");
                
                AppendLog($"Helmet equipped to Head slot");
                
                EndTest(true, "Successfully equipped helmet");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test2_UnequipItem()
        {
            StartTest("Unequip Item");
            
            try
            {
                // Should have helmet equipped from Test1
                var equippedHelmet = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Head);
                if (equippedHelmet == null)
                {
                    AppendLog("No helmet equipped, equipping one first");
                    _inventorySystem.AddItem(_testHelmetID, 1);
                    var helmet = _inventorySystem.GetItemsByDefinition(_testHelmetID)[0];
                    _equipmentSystem.EquipItem(helmet.InstanceID);
                    equippedHelmet = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Head);
                }
                
                string helmetID = equippedHelmet.InstanceID;
                AppendLog($"Unequipping helmet: {helmetID}");
                
                // Unequip
                _equipmentSystem.UnequipItem(EquipmentSlotType.Head);
                
                // Verify unequipped
                var helmetAfter = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Head);
                AssertNoThrow(helmetAfter == null, "Helmet should be unequipped");
                
                // Should be back in inventory
                var inInventory = _inventorySystem.GetItemByInstanceID(helmetID);
                AssertNoThrow(inInventory != null, "Helmet should be in inventory");
                AssertNoThrow(inInventory.InventoryIndex >= 0, "Should have valid inventory index");
                
                AppendLog($"Helmet back in inventory at index {inInventory.InventoryIndex}");
                
                EndTest(true, "Successfully unequipped and returned to inventory");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test3_SwapEquipment()
        {
            StartTest("Swap Equipment");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                // Add two items
                _inventorySystem.AddItem(_testHelmetID, 1);
                _inventorySystem.AddItem(_testVestID, 1);
                
                var helmet = _inventorySystem.GetItemsByDefinition(_testHelmetID)[0];
                var vest = _inventorySystem.GetItemsByDefinition(_testVestID)[0];
                
                AppendLog($"Helmet: {helmet.InstanceID}, Vest: {vest.InstanceID}");
                
                // Equip both
                _equipmentSystem.EquipItem(helmet.InstanceID);
                _equipmentSystem.EquipItem(vest.InstanceID);
                
                // Verify both equipped
                var equippedHelmet = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Head);
                var equippedVest = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Chest);
                
                AssertNoThrow(equippedHelmet != null && equippedVest != null, "Both should be equipped");
                AppendLog("Both helmet and vest equipped");
                
                // Try to equip another helmet (should swap)
                _inventorySystem.AddItem(_testHelmetID, 1);
                var helmets = _inventorySystem.GetItemsByDefinition(_testHelmetID);
                
                // Find helmet not currently equipped
                ItemInstance newHelmet = null;
                foreach (var h in helmets)
                {
                    if (h.InstanceID != helmet.InstanceID)
                    {
                        newHelmet = h;
                        break;
                    }
                }
                
                if (newHelmet != null)
                {
                    AppendLog($"Equipping second helmet: {newHelmet.InstanceID}");
                    _equipmentSystem.EquipItem(newHelmet.InstanceID);
                    
                    var currentHelmet = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Head);
                    AssertNoThrow(currentHelmet.InstanceID == newHelmet.InstanceID, "Should have new helmet equipped");
                    
                    // Old helmet should be in inventory
                    var oldInInventory = _inventorySystem.GetItemByInstanceID(helmet.InstanceID);
                    AssertNoThrow(oldInInventory != null, "Old helmet should be in inventory");
                    
                    AppendLog("Successfully swapped helmets");
                }
                
                EndTest(true, "Equipment swap working correctly");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test4_StatModifiers()
        {
            StartTest("Stat Modifiers");
            
            try
            {
                if (_statSystem == null)
                {
                    EndTest(false, "No stat system available");
                    return;
                }
                
                _inventorySystem.ClearInventory();
                _equipmentSystem.UnequipItem(EquipmentSlotType.Head);
                _equipmentSystem.UnequipItem(EquipmentSlotType.Chest);
                
                float baseArmor = _statSystem.GetStat(PlayerStatType.Armor);
                AppendLog($"Base armor: {baseArmor:F1}");
                
                // Add and equip vest (should increase armor)
                _inventorySystem.AddItem(_testVestID, 1);
                var vest = _inventorySystem.GetItemsByDefinition(_testVestID)[0];
                _equipmentSystem.EquipItem(vest.InstanceID);
                
                float armorAfterEquip = _statSystem.GetStat(PlayerStatType.Armor);
                AppendLog($"Armor after equip: {armorAfterEquip:F1}");
                
                float armorIncrease = armorAfterEquip - baseArmor;
                AssertNoThrow(armorAfterEquip > baseArmor, $"Armor should increase, but went from {baseArmor:F1} to {armorAfterEquip:F1}");
                
                AppendLog($"Armor increased by: {armorIncrease:F1}");
                
                // Unequip (should restore)
                _equipmentSystem.UnequipItem(EquipmentSlotType.Chest);
                
                float armorAfterUnequip = _statSystem.GetStat(PlayerStatType.Armor);
                AppendLog($"Armor after unequip: {armorAfterUnequip:F1}");
                
                AssertNoThrow(Mathf.Approximately(armorAfterUnequip, baseArmor), 
                    $"Armor should return to base {baseArmor:F1}, but is {armorAfterUnequip:F1}");
                
                EndTest(true, $"Stat modifiers working: +{armorIncrease:F1} armor when equipped");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test5_WeightModification()
        {
            StartTest("Weight Modification");
            
            try
            {
                if (_statSystem == null)
                {
                    EndTest(false, "No stat system available");
                    return;
                }
                
                _inventorySystem.ClearInventory();
                
                float weightBefore = _statSystem.GetCurrentWeight();
                AppendLog($"Weight before: {weightBefore:F1}");
                
                // Add backpack (should modify weight when equipped)
                _inventorySystem.AddItem(_testBackpackID, 1);
                var backpack = _inventorySystem.GetItemsByDefinition(_testBackpackID)[0];
                
                float weightAfterAdd = _statSystem.GetCurrentWeight();
                AppendLog($"Weight after add to inventory: {weightAfterAdd:F1}");
                
                float weightFromItem = weightAfterAdd - weightBefore;
                AppendLog($"Backpack weight in inventory: {weightFromItem:F1}");
                
                // Equip backpack
                _equipmentSystem.EquipItem(backpack.InstanceID);
                
                float weightAfterEquip = _statSystem.GetCurrentWeight();
                AppendLog($"Weight after equip: {weightAfterEquip:F1}");
                
                float weightDifference = weightAfterEquip - weightAfterAdd;
                AppendLog($"Weight difference when equipped: {weightDifference:F1}");
                
                EndTest(true, $"Weight system working. In inventory: {weightFromItem:F1}, When equipped diff: {weightDifference:F1}");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        #region Test Logger Methods
        
        private void StartTest(string testName)
        {
            _currentTestNumber++;
            _currentTestStartTime = System.DateTime.Now;
            _currentTest = new TestResult
            {
                TestNumber = _currentTestNumber,
                TestName = testName
            };
            
            AppendLog($"\n{'=', 80}");
            AppendLog($"TEST {_currentTestNumber}: {testName}");
            AppendLog($"Started: {_currentTestStartTime:HH:mm:ss.fff}");
            AppendLog($"{'=', 80}");
            
            Debug.Log($"\n--- Test {_currentTestNumber}: {testName} ---");
        }
        
        private void EndTest(bool passed, string details = "")
        {
            var duration = (System.DateTime.Now - _currentTestStartTime).TotalMilliseconds;
            
            _currentTest.Passed = passed;
            _currentTest.DurationMs = (float)duration;
            _currentTest.Details = details;
            
            _testResults.Add(_currentTest);
            
            string status = passed ? "✓ PASSED" : "✗ FAILED";
            AppendLog($"\nResult: {status}");
            AppendLog($"Duration: {duration:F2}ms");
            
            if (!string.IsNullOrEmpty(details))
            {
                AppendLog($"Details: {details}");
            }
            
            LogEquipmentStateToTest();
            
            Debug.Log($"{status} (Duration: {duration:F2}ms)");
        }
        
        private void AssertNoThrow(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.Exception($"Assertion failed: {message}");
            }
        }
        
        private void AppendLog(string message)
        {
            _testLog.AppendLine(message);
        }
        
        private void LogEquipmentStateToTest()
        {
            AppendLog("\nEquipment State After Test:");
            var allEquipment = _equipmentSystem.GetAllEquippedItems();
            AppendLog($"  Equipped Items: {allEquipment.Count}");
            
            if (allEquipment.Count > 0)
            {
                AppendLog("  Equipment:");
                foreach (var kvp in allEquipment)
                {
                    var def = ItemDatabase.GetDefinition(kvp.Value.DefinitionID);
                    AppendLog($"    [{kvp.Key}] {def?.DisplayName ?? kvp.Value.DefinitionID}");
                }
            }
            
            if (_statSystem != null)
            {
                float armor = _statSystem.GetStat(PlayerStatType.Armor);
                float weight = _statSystem.GetCurrentWeight();
                AppendLog($"  Stats: Armor={armor:F1}, Weight={weight:F1}");
            }
        }
        
        private void GenerateFinalReport()
        {
            AppendLog("\n\n");
            AppendLog("================================================================================");
            AppendLog("FINAL TEST SUMMARY");
            AppendLog("================================================================================");
            
            int passedCount = 0;
            int failedCount = 0;
            float totalDuration = 0;
            
            foreach (var result in _testResults)
            {
                if (result.Passed) passedCount++;
                else failedCount++;
                totalDuration += result.DurationMs;
            }
            
            AppendLog($"Total Tests: {_testResults.Count}");
            AppendLog($"Passed: {passedCount}");
            AppendLog($"Failed: {failedCount}");
            AppendLog($"Success Rate: {(float)passedCount / _testResults.Count:P1}");
            AppendLog($"Total Duration: {totalDuration:F2}ms");
            AppendLog($"Average Duration: {totalDuration / _testResults.Count:F2}ms");
            
            AppendLog("\nDetailed Results:");
            AppendLog($"{"Test",-5} {"Name",-30} {"Status",-10} {"Duration",-12}");
            AppendLog(new string('-', 80));
            
            foreach (var result in _testResults)
            {
                string status = result.Passed ? "✓ PASSED" : "✗ FAILED";
                AppendLog($"{result.TestNumber,-5} {result.TestName,-30} {status,-10} {result.DurationMs:F2}ms");
            }
            
            AppendLog("\n================================================================================");
            AppendLog($"Test Session Completed: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Session Duration: {(System.DateTime.Now - _sessionStartTime).TotalSeconds:F2}s");
            AppendLog("================================================================================");
        }
        
        private void SaveLogToFile()
        {
            try
            {
                string directory = Application.dataPath + "/TestResults";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"EquipmentTest_{timestamp}.txt";
                string filepath = Path.Combine(directory, filename);
                
                File.WriteAllText(filepath, _testLog.ToString());
                
                Debug.Log($"<color=green>✓ Test results saved to: {filepath}</color>");
                
                PlayerPrefs.SetString("LastEquipmentTestLogPath", filepath);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save test log: {ex.Message}");
            }
        }
        
        [ContextMenu("Open Last Test Log")]
        private void OpenLastTestLog()
        {
            string path = PlayerPrefs.GetString("LastEquipmentTestLogPath", "");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                Application.OpenURL("file://" + path);
            }
            else
            {
                Debug.LogWarning("No test log found. Run tests first.");
            }
        }
        
        #endregion
        
        #region Manual Test Buttons
        
        [ContextMenu("Equip Test Helmet")]
        public void EquipTestHelmet()
        {
            _inventorySystem.AddItem(_testHelmetID, 1);
            var helmet = _inventorySystem.GetItemsByDefinition(_testHelmetID)[0];
            _equipmentSystem.EquipItem(helmet.InstanceID);
            Debug.Log("Equipped test helmet");
        }
        
        [ContextMenu("Equip Test Vest")]
        public void EquipTestVest()
        {
            _inventorySystem.AddItem(_testVestID, 1);
            var vest = _inventorySystem.GetItemsByDefinition(_testVestID)[0];
            _equipmentSystem.EquipItem(vest.InstanceID);
            Debug.Log("Equipped test vest");
        }
        
        [ContextMenu("Unequip All")]
        public void UnequipAll()
        {
            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                if (_equipmentSystem.IsSlotOccupied(slot))
                {
                    _equipmentSystem.UnequipItem(slot);
                }
            }
            Debug.Log("Unequipped all equipment");
        }
        
        [ContextMenu("Log Equipment State")]
        public void LogEquipmentState()
        {
            _equipmentSystem.LogEquipmentState();
        }
        
        #endregion
    }
}