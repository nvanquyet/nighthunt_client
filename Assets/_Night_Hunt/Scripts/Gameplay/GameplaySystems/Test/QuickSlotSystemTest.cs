using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using GameplaySystems.Inventory;
using GameplaySystems.Core.Data;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Test script for QuickSlotSystem with logging
    /// Tests quickslot operations and item usage
    /// Outputs all test results to TestResults/QuickSlotTest_[timestamp].txt
    /// </summary>
    public class QuickSlotSystemTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuickSlotSystem _quickSlotSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        
        [Header("Test Items")]
        [SerializeField] private string _testConsumableID = "consumable_medkit";
        [SerializeField] private string _testConsumable2ID = "consumable_energydrink";
        
        [Header("Test Controls")]
        [SerializeField] private bool _runTestsOnStart = false;
        
        [Header("Logging")]
        [SerializeField] private bool _saveLogToFile = true;
        
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
            if (_quickSlotSystem == null)
                _quickSlotSystem = GetComponent<QuickSlotSystem>();
            
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
            
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
            AppendLog("QUICKSLOT SYSTEM TEST RESULTS");
            AppendLog("================================================================================");
            AppendLog($"Test Session Started: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Unity Version: {Application.unityVersion}");
            AppendLog($"Platform: {Application.platform}");
            AppendLog("================================================================================\n");
            
            Debug.Log("=== STARTING QUICKSLOT SYSTEM TESTS ===\n");
            
            Test1_AssignToQuickSlot();
            Test2_RemoveFromQuickSlot();
            Test3_SwapQuickSlots();
            Test4_UseQuickSlot();
            Test5_ClearAllQuickSlots();
            
            GenerateFinalReport();
            
            Debug.Log("\n=== ALL QUICKSLOT TESTS COMPLETED ===");
            
            if (_saveLogToFile)
            {
                SaveLogToFile();
            }
        }
        
        private void Test1_AssignToQuickSlot()
        {
            StartTest("Assign To QuickSlot");
            
            try
            {
                _inventorySystem.ClearInventory();
                _quickSlotSystem.ClearAllQuickSlots();
                
                _inventorySystem.AddItem(_testConsumableID, 5);
                var item = _inventorySystem.GetItemsByDefinition(_testConsumableID)[0];
                
                AppendLog($"Assigning {item.DefinitionID} (x{item.Quantity}) to slot 0");
                
                _quickSlotSystem.AssignToQuickSlot(item.InstanceID, 0);
                
                var slotItem = _quickSlotSystem.GetQuickSlotItem(0);
                AssertNoThrow(slotItem != null, "Slot 0 should have item");
                AssertNoThrow(slotItem.InstanceID == item.InstanceID, "Should be same item");
                
                var inInventory = _inventorySystem.GetItemByInstanceID(item.InstanceID);
                AssertNoThrow(inInventory != null, "Item should still be in inventory");
                
                AppendLog($"Item assigned successfully. Still in inventory: {inInventory != null}");
                
                EndTest(true, "Item assigned to quickslot successfully");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test2_RemoveFromQuickSlot()
        {
            StartTest("Remove From QuickSlot");
            
            try
            {
                var slotItem = _quickSlotSystem.GetQuickSlotItem(0);
                if (slotItem == null)
                {
                    _inventorySystem.AddItem(_testConsumableID, 1);
                    var item = _inventorySystem.GetItemsByDefinition(_testConsumableID)[0];
                    _quickSlotSystem.AssignToQuickSlot(item.InstanceID, 0);
                    slotItem = _quickSlotSystem.GetQuickSlotItem(0);
                }
                
                string itemID = slotItem.InstanceID;
                AppendLog($"Removing item from slot 0: {itemID}");
                
                _quickSlotSystem.RemoveFromQuickSlot(0);
                
                var slotAfter = _quickSlotSystem.GetQuickSlotItem(0);
                AssertNoThrow(slotAfter == null, "Slot should be empty");
                
                var inInventory = _inventorySystem.GetItemByInstanceID(itemID);
                AssertNoThrow(inInventory != null, "Item should still be in inventory");
                
                AppendLog("Quickslot cleared, item remains in inventory");
                
                EndTest(true, "Item removed from quickslot");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test3_SwapQuickSlots()
        {
            StartTest("Swap QuickSlots");
            
            try
            {
                _inventorySystem.ClearInventory();
                _quickSlotSystem.ClearAllQuickSlots();
                
                _inventorySystem.AddItem(_testConsumableID, 10);
                _inventorySystem.AddItem(_testConsumable2ID, 10);
                
                var items1 = _inventorySystem.GetItemsByDefinition(_testConsumableID);
                var items2 = _inventorySystem.GetItemsByDefinition(_testConsumable2ID);
                
                if (items1.Count > 0 && items2.Count > 0)
                {
                    var item1 = items1[0];
                    var item2 = items2[0];
                    
                    _quickSlotSystem.AssignToQuickSlot(item1.InstanceID, 0);
                    _quickSlotSystem.AssignToQuickSlot(item2.InstanceID, 1);
                    
                    AppendLog($"Slot 0: {item1.DefinitionID}, Slot 1: {item2.DefinitionID}");
                    
                    _quickSlotSystem.SwapQuickSlots(0, 1);
                    
                    var slot0After = _quickSlotSystem.GetQuickSlotItem(0);
                    var slot1After = _quickSlotSystem.GetQuickSlotItem(1);
                    
                    AssertNoThrow(slot0After.InstanceID == item2.InstanceID, "Slot 0 should have item2");
                    AssertNoThrow(slot1After.InstanceID == item1.InstanceID, "Slot 1 should have item1");
                    
                    AppendLog("Slots swapped successfully");
                    
                    EndTest(true, "QuickSlot swap working");
                }
                else
                {
                    EndTest(false, "Could not get both test items");
                }
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test4_UseQuickSlot()
        {
            StartTest("Use QuickSlot");
            
            try
            {
                _inventorySystem.ClearInventory();
                _quickSlotSystem.ClearAllQuickSlots();
                
                _inventorySystem.AddItem(_testConsumableID, 5);
                var item = _inventorySystem.GetItemsByDefinition(_testConsumableID)[0];
                _quickSlotSystem.AssignToQuickSlot(item.InstanceID, 0);
                
                int quantityBefore = item.Quantity;
                AppendLog($"Quantity before use: {quantityBefore}");
                
                bool canUse = _quickSlotSystem.CanUseQuickSlot(0);
                AppendLog($"Can use slot: {canUse}");
                
                if (canUse)
                {
                    _quickSlotSystem.UseQuickSlot(0);
                    
                    var itemAfter = _inventorySystem.GetItemByInstanceID(item.InstanceID);
                    if (itemAfter != null)
                    {
                        int quantityAfter = itemAfter.Quantity;
                        AppendLog($"Quantity after use: {quantityAfter}");
                        AssertNoThrow(quantityAfter == quantityBefore - 1, 
                            $"Quantity should be {quantityBefore - 1}, but is {quantityAfter}");
                        
                        EndTest(true, $"QuickSlot use working. Quantity: {quantityBefore} → {quantityAfter}");
                    }
                    else
                    {
                        AppendLog("Item consumed completely (was quantity 1)");
                        AssertNoThrow(quantityBefore == 1, "Item should have been quantity 1");
                        EndTest(true, "Item consumed completely");
                    }
                }
                else
                {
                    EndTest(false, "Cannot use quickslot");
                }
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test5_ClearAllQuickSlots()
        {
            StartTest("Clear All QuickSlots");
            
            try
            {
                _inventorySystem.ClearInventory();
                _inventorySystem.AddItem(_testConsumableID, 10);
                
                var items = _inventorySystem.GetItemsByDefinition(_testConsumableID);
                int slotCount = _quickSlotSystem.GetQuickSlotCount();
                
                int assignedCount = 0;
                for (int i = 0; i < System.Math.Min(items.Count, slotCount); i++)
                {
                    _quickSlotSystem.AssignToQuickSlot(items[i].InstanceID, i);
                    assignedCount++;
                }
                
                AppendLog($"Assigned {assignedCount} items to quickslots");
                
                _quickSlotSystem.ClearAllQuickSlots();
                
                for (int i = 0; i < slotCount; i++)
                {
                    var slotItem = _quickSlotSystem.GetQuickSlotItem(i);
                    AssertNoThrow(slotItem == null, $"Slot {i} should be empty");
                }
                
                AppendLog("All slots cleared");
                
                EndTest(true, "Clear all quickslots working");
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
            
            LogSystemStateToTest();
            
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
        
        private void LogSystemStateToTest()
        {
            AppendLog("\nQuickSlot State After Test:");
            int slotCount = _quickSlotSystem.GetQuickSlotCount();
            AppendLog($"  Total Slots: {slotCount}");
            
            int occupiedCount = 0;
            for (int i = 0; i < slotCount; i++)
            {
                var item = _quickSlotSystem.GetQuickSlotItem(i);
                if (item != null)
                {
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);
                    AppendLog($"    [Slot {i}] {def?.DisplayName ?? item.DefinitionID} x{item.Quantity}");
                    occupiedCount++;
                }
            }
            
            AppendLog($"  Occupied Slots: {occupiedCount}/{slotCount}");
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
                string filename = $"QuickSlotTest_{timestamp}.txt";
                string filepath = Path.Combine(directory, filename);
                
                File.WriteAllText(filepath, _testLog.ToString());
                
                Debug.Log($"<color=green>✓ Test results saved to: {filepath}</color>");
                
                PlayerPrefs.SetString("LastQuickSlotTestLogPath", filepath);
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
            string path = PlayerPrefs.GetString("LastQuickSlotTestLogPath", "");
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
        
        [ContextMenu("Assign Test Item to Slot 0")]
        public void AssignTestItem()
        {
            _inventorySystem.AddItem(_testConsumableID, 1);
            var item = _inventorySystem.GetItemsByDefinition(_testConsumableID)[0];
            _quickSlotSystem.AssignToQuickSlot(item.InstanceID, 0);
            Debug.Log("Assigned test item to slot 0");
        }
        
        [ContextMenu("Use Slot 0")]
        public void UseSlot0()
        {
            _quickSlotSystem.UseQuickSlot(0);
            Debug.Log("Used slot 0");
        }
        
        [ContextMenu("Clear All Slots")]
        public void ClearAll()
        {
            _quickSlotSystem.ClearAllQuickSlots();
            Debug.Log("Cleared all quickslots");
        }
        
        #endregion
    }
}