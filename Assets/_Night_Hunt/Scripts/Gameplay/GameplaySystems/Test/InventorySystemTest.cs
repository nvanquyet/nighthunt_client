using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using GameplaySystems.Inventory;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Data;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Test script for InventorySystem
    /// Attach to player GameObject to test inventory functionality
    /// 
    /// Tests:
    /// - Add/Remove items
    /// - Stacking behavior
    /// - Move/Swap operations
    /// - Split stack
    /// - Weight calculations
    /// - Events firing correctly
    /// 
    /// NEW: Outputs all test results to a single log file
    /// </summary>
    public class InventorySystemTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private IPlayerStat _statSystem;
        
        [Header("Test Items")]
        [SerializeField] private string _testWeaponID = "weapon_ak47";
        [SerializeField] private string _testArmorID = "armor_vest";
        [SerializeField] private string _testConsumableID = "consumable_medkit";
        
        [Header("Test Controls")]
        [SerializeField] private bool _runTestsOnStart = false;
        [SerializeField] private bool _subscribeToEvents = true;
        
        [Header("Logging")]
        [SerializeField] private bool _saveLogToFile = true;
        [SerializeField] private string _logFileName = "InventorySystemTest_Results.txt";
        
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
            public List<string> Events;
        }
        
        private TestResult _currentTest;
        
        private void Start()
        {
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
            
            if (_statSystem == null)
                _statSystem = GetComponent<IPlayerStat>();
            
            if (_inventorySystem == null)
            {
                Debug.LogError("[InventoryTest] InventorySystem not found!");
                return;
            }
            
            // Initialize test logger
            _testLog = new StringBuilder();
            _testResults = new List<TestResult>();
            _sessionStartTime = System.DateTime.Now;
            
            if (_subscribeToEvents)
                SubscribeToEvents();
            
            if (_runTestsOnStart)
                Invoke(nameof(RunAllTests), 2f); // Wait for network
        }
        
        private void OnDestroy()
        {
            if (_inventorySystem != null && _subscribeToEvents)
                UnsubscribeFromEvents();
        }
        
        #region Event Subscription
        
        private void SubscribeToEvents()
        {
            _inventorySystem.OnItemAdded += OnItemAdded;
            _inventorySystem.OnItemRemoved += OnItemRemoved;
            _inventorySystem.OnItemMoved += OnItemMoved;
            _inventorySystem.OnItemsSwapped += OnItemsSwapped;
            _inventorySystem.OnItemsStacked += OnItemsStacked;
            _inventorySystem.OnInventoryCleared += OnInventoryCleared;
            
            Debug.Log("[InventoryTest] Subscribed to events");
        }
        
        private void UnsubscribeFromEvents()
        {
            _inventorySystem.OnItemAdded -= OnItemAdded;
            _inventorySystem.OnItemRemoved -= OnItemRemoved;
            _inventorySystem.OnItemMoved -= OnItemMoved;
            _inventorySystem.OnItemsSwapped -= OnItemsSwapped;
            _inventorySystem.OnItemsStacked -= OnItemsStacked;
            _inventorySystem.OnInventoryCleared -= OnInventoryCleared;
        }
        
        private void OnItemAdded(ItemInstance item)
        {
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            string msg = $"[Event] Item Added: {def?.DisplayName} x{item.Quantity} @ index {item.InventoryIndex}";
            Debug.Log(msg);
            LogTestEvent(msg);
        }
        
        private void OnItemRemoved(ItemInstance item, int quantity)
        {
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            string msg = $"[Event] Item Removed: {def?.DisplayName} x{quantity}";
            Debug.Log(msg);
            LogTestEvent(msg);
        }
        
        private void OnItemMoved(ItemInstance item, int oldIndex, int newIndex)
        {
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            string msg = $"[Event] Item Moved: {def?.DisplayName} from {oldIndex} to {newIndex}";
            Debug.Log(msg);
            LogTestEvent(msg);
        }
        
        private void OnItemsSwapped(ItemInstance item1, ItemInstance item2)
        {
            string msg = $"[Event] Items Swapped: {item1.DefinitionID} <-> {item2.DefinitionID}";
            Debug.Log(msg);
            LogTestEvent(msg);
        }
        
        private void OnItemsStacked(ItemInstance target, ItemInstance source, int amount)
        {
            string msg = $"[Event] Items Stacked: {amount} stacked";
            Debug.Log(msg);
            LogTestEvent(msg);
        }
        
        private void OnInventoryCleared()
        {
            string msg = "[Event] Inventory Cleared";
            Debug.Log(msg);
            LogTestEvent(msg);
        }
        
        #endregion
        
        #region Automated Tests
        
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            // Initialize test session
            _testLog = new StringBuilder();
            _testResults = new List<TestResult>();
            _sessionStartTime = System.DateTime.Now;
            _currentTestNumber = 0;
            
            // Header
            AppendLog("================================================================================");
            AppendLog("INVENTORY SYSTEM TEST RESULTS");
            AppendLog("================================================================================");
            AppendLog($"Test Session Started: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Unity Version: {Application.unityVersion}");
            AppendLog($"Platform: {Application.platform}");
            AppendLog("================================================================================\n");
            
            Debug.Log("=== STARTING INVENTORY SYSTEM TESTS ===\n");
            
            // Run all tests
            Test1_AddSingleItem();
            Test2_AddMultipleItems();
            Test3_StackableItems();
            Test4_RemoveItem();
            Test5_MoveItem();
            Test6_SwapItems();
            Test7_SplitStack();
            Test8_WeightCalculation();
            Test9_ClearInventory();
            
            // Generate final report
            GenerateFinalReport();
            
            Debug.Log("\n=== ALL INVENTORY TESTS COMPLETED ===");
            
            // Save to file
            if (_saveLogToFile)
            {
                SaveLogToFile();
            }
        }
        
        private void Test1_AddSingleItem()
        {
            StartTest("Add Single Item");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                
                var items = _inventorySystem.GetAllItems();
                Debug.Assert(items.Count == 1, "Should have 1 item");
                
                var item = items[0];
                Debug.Assert(item.DefinitionID == _testWeaponID, "Item should be test weapon");
                Debug.Assert(item.Quantity == 1, "Quantity should be 1");
                
                EndTest(true, "Successfully added single item to inventory");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}");
            }
        }
        
        private void Test2_AddMultipleItems()
        {
            StartTest("Add Multiple Items");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                _inventorySystem.AddItem(_testArmorID, 1);
                _inventorySystem.AddItem(_testConsumableID, 5);
                
                var items = _inventorySystem.GetAllItems();
                AssertNoThrow(items.Count >= 3, "Should have at least 3 items");
                
                int weaponCount = _inventorySystem.GetItemCount(_testWeaponID);
                int armorCount = _inventorySystem.GetItemCount(_testArmorID);
                int consumableCount = _inventorySystem.GetItemCount(_testConsumableID);
                
                AssertNoThrow(weaponCount == 1, "Should have 1 weapon");
                AssertNoThrow(armorCount == 1, "Should have 1 armor");
                AssertNoThrow(consumableCount == 5, "Should have 5 consumables");
                
                EndTest(true, $"Added 3 different item types: weapon, armor, consumables");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test3_StackableItems()
        {
            StartTest("Stackable Items");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                // Add consumables (stackable)
                _inventorySystem.AddItem(_testConsumableID, 3);
                _inventorySystem.AddItem(_testConsumableID, 2);
                
                var items = _inventorySystem.GetItemsByDefinition(_testConsumableID);
                int totalCount = _inventorySystem.GetItemCount(_testConsumableID);
                
                AssertNoThrow(totalCount == 5, "Should have total 5 consumables");
                
                // Should be stacked into fewer items
                var def = ItemDatabase.GetDefinition(_testConsumableID);
                if (def != null && def.IsStackable && def.MaxStackSize >= 5)
                {
                    AssertNoThrow(items.Count == 1, "Should be stacked into 1 item");
                }
                
                EndTest(true, $"Stackable items working correctly. Total: {totalCount}, Stacks: {items.Count}");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test4_RemoveItem()
        {
            StartTest("Remove Item");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testConsumableID, 10);
                
                var items = _inventorySystem.GetItemsByDefinition(_testConsumableID);
                AssertNoThrow(items.Count > 0, "Should have items");
                
                var item = items[0];
                string instanceID = item.InstanceID;
                
                AppendLog($"Initial item: {item.DefinitionID} x{item.Quantity}, ID: {instanceID}");
                
                // Remove partial
                _inventorySystem.RemoveItem(instanceID, 3);
                
                var updatedItem = _inventorySystem.GetItemByInstanceID(instanceID);
                if (updatedItem != null)
                {
                    AppendLog($"After removing 3: Quantity = {updatedItem.Quantity}");
                    AssertNoThrow(updatedItem.Quantity == 7, $"Should have 7 remaining, but has {updatedItem.Quantity}");
                }
                else
                {
                    throw new System.Exception("Item not found after partial removal");
                }
                
                // Remove all
                _inventorySystem.RemoveItemByDefinition(_testConsumableID, 7);
                
                int finalCount = _inventorySystem.GetItemCount(_testConsumableID);
                AssertNoThrow(finalCount == 0, $"Should have 0 items, but has {finalCount}");
                
                EndTest(true, "Successfully removed items partially and completely");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test5_MoveItem()
        {
            StartTest("Move Item");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                
                var items = _inventorySystem.GetAllItems();
                var item = items[0];
                
                int oldIndex = item.InventoryIndex;
                int newIndex = oldIndex + 10;
                
                AppendLog($"Moving item from index {oldIndex} to {newIndex}");
                
                _inventorySystem.MoveItem(item.InstanceID, newIndex);
                
                var movedItem = _inventorySystem.GetItemByInstanceID(item.InstanceID);
                AssertNoThrow(movedItem.InventoryIndex == newIndex, $"Item should be at index {newIndex}, but at {movedItem.InventoryIndex}");
                
                EndTest(true, $"Item moved from {oldIndex} to {newIndex}");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test6_SwapItems()
        {
            StartTest("Swap Items");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                _inventorySystem.AddItem(_testArmorID, 1);
                
                var items = _inventorySystem.GetAllItems();
                AssertNoThrow(items.Count >= 2, "Should have at least 2 items");
                
                var item1 = items[0];
                var item2 = items[1];
                
                int index1 = item1.InventoryIndex;
                int index2 = item2.InventoryIndex;
                
                AppendLog($"Swapping: {item1.DefinitionID} @ {index1} <-> {item2.DefinitionID} @ {index2}");
                
                _inventorySystem.SwapItems(item1.InstanceID, item2.InstanceID);
                
                var swappedItem1 = _inventorySystem.GetItemByInstanceID(item1.InstanceID);
                var swappedItem2 = _inventorySystem.GetItemByInstanceID(item2.InstanceID);
                
                AssertNoThrow(swappedItem1.InventoryIndex == index2, "Item 1 should be at item 2's old index");
                AssertNoThrow(swappedItem2.InventoryIndex == index1, "Item 2 should be at item 1's old index");
                
                EndTest(true, "Items swapped successfully");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test7_SplitStack()
        {
            StartTest("Split Stack");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testConsumableID, 10);
                
                var items = _inventorySystem.GetItemsByDefinition(_testConsumableID);
                var item = items[0];
                
                int originalQty = item.Quantity;
                
                AppendLog($"Original stack: {originalQty}. Splitting 4 off.");
                
                _inventorySystem.SplitStack(item.InstanceID, 4);
                
                var originalItem = _inventorySystem.GetItemByInstanceID(item.InstanceID);
                AssertNoThrow(originalItem.Quantity == originalQty - 4, $"Original stack should be {originalQty - 4}, but is {originalItem.Quantity}");
                
                var allItems = _inventorySystem.GetItemsByDefinition(_testConsumableID);
                AssertNoThrow(allItems.Count >= 2, $"Should have 2 stacks now, but has {allItems.Count}");
                
                EndTest(true, $"Split stack: {originalQty} -> {originalItem.Quantity} + 4");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test8_WeightCalculation()
        {
            StartTest("Weight Calculation");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                float initialWeight = _inventorySystem.CalculateTotalWeight();
                AssertNoThrow(initialWeight == 0f, $"Initial weight should be 0, but is {initialWeight}");
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                
                float weightAfterAdd = _inventorySystem.CalculateTotalWeight();
                AssertNoThrow(weightAfterAdd > 0f, $"Weight should increase after adding item, but is {weightAfterAdd}");
                
                var (current, capacity, percent) = _inventorySystem.GetWeightInfo();
                AppendLog($"Weight: {current:F1}/{capacity:F1} ({percent:P0})");
                
                AssertNoThrow(current == weightAfterAdd, "Weight info should match calculated weight");
                
                // Check stat system integration
                if (_statSystem != null)
                {
                    float statWeight = _statSystem.GetCurrentWeight();
                    AppendLog($"Stat system weight: {statWeight:F1}");
                }
                
                EndTest(true, $"Weight system working. Current: {current:F1}/{capacity:F1}");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test9_ClearInventory()
        {
            StartTest("Clear Inventory");
            
            try
            {
                _inventorySystem.AddItem(_testWeaponID, 1);
                _inventorySystem.AddItem(_testArmorID, 1);
                
                var items = _inventorySystem.GetAllItems();
                AssertNoThrow(items.Count > 0, "Should have items before clear");
                
                int itemCountBeforeClear = items.Count;
                AppendLog($"Items before clear: {itemCountBeforeClear}");
                
                _inventorySystem.ClearInventory();
                
                var itemsAfterClear = _inventorySystem.GetAllItems();
                AssertNoThrow(itemsAfterClear.Count == 0, $"Should have 0 items after clear, but has {itemsAfterClear.Count}");
                
                float weight = _inventorySystem.CalculateTotalWeight();
                AssertNoThrow(weight == 0f, $"Weight should be 0 after clear, but is {weight}");
                
                EndTest(true, $"Cleared {itemCountBeforeClear} items successfully");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        #endregion
        
        #region Test Logger Methods
        
        private void StartTest(string testName)
        {
            _currentTestNumber++;
            _currentTestStartTime = System.DateTime.Now;
            _currentTest = new TestResult
            {
                TestNumber = _currentTestNumber,
                TestName = testName,
                Events = new List<string>()
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
            
            // Log inventory state after test
            LogInventoryStateToTest();
            
            Debug.Log($"{status} (Duration: {duration:F2}ms)");
        }
        
        private void LogTestEvent(string eventMsg)
        {
            if (_currentTest != null)
            {
                _currentTest.Events.Add(eventMsg);
            }
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
        
        private void LogInventoryStateToTest()
        {
            AppendLog("\nInventory State After Test:");
            var items = _inventorySystem.GetAllItems();
            AppendLog($"  Total Items: {items.Count}");
            
            if (items.Count > 0)
            {
                AppendLog("  Items:");
                foreach (var item in items)
                {
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);
                    AppendLog($"    [{item.InventoryIndex}] {def?.DisplayName ?? item.DefinitionID} x{item.Quantity}");
                }
            }
            
            var (weight, capacity, percent) = _inventorySystem.GetWeightInfo();
            AppendLog($"  Weight: {weight:F1}/{capacity:F1} ({percent:P0})");
            
            if (_currentTest.Events.Count > 0)
            {
                AppendLog($"\n  Events Fired ({_currentTest.Events.Count}):");
                foreach (var evt in _currentTest.Events)
                {
                    AppendLog($"    {evt}");
                }
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
            AppendLog($"{"Test",-5} {"Name",-30} {"Status",-10} {"Duration",-12} {"Events",-8}");
            AppendLog(new string('-', 80));
            
            foreach (var result in _testResults)
            {
                string status = result.Passed ? "✓ PASSED" : "✗ FAILED";
                AppendLog($"{result.TestNumber,-5} {result.TestName,-30} {status,-10} {result.DurationMs:F2}ms {result.Events.Count,-8}");
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
                string filename = $"InventoryTest_{timestamp}.txt";
                string filepath = Path.Combine(directory, filename);
                
                File.WriteAllText(filepath, _testLog.ToString());
                
                Debug.Log($"<color=green>✓ Test results saved to: {filepath}</color>");
                AppendLog($"\n\nLog file saved to: {filepath}");
                
                // Also save to PlayerPrefs for easy access
                PlayerPrefs.SetString("LastTestLogPath", filepath);
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
            string path = PlayerPrefs.GetString("LastTestLogPath", "");
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
        
        [ContextMenu("Add Test Weapon")]
        public void AddTestWeapon()
        {
            _inventorySystem.AddItem(_testWeaponID, 1);
            Debug.Log($"Added test weapon: {_testWeaponID}");
        }
        
        [ContextMenu("Add Test Armor")]
        public void AddTestArmor()
        {
            _inventorySystem.AddItem(_testArmorID, 1);
            Debug.Log($"Added test armor: {_testArmorID}");
        }
        
        [ContextMenu("Add Test Consumables (5)")]
        public void AddTestConsumables()
        {
            _inventorySystem.AddItem(_testConsumableID, 5);
            Debug.Log($"Added 5 consumables: {_testConsumableID}");
        }
        
        [ContextMenu("Clear Inventory")]
        public void ClearInventory()
        {
            _inventorySystem.ClearInventory();
            Debug.Log("Inventory cleared");
        }
        
        [ContextMenu("Log Inventory")]
        public void LogInventory()
        {
            _inventorySystem.LogInventoryState();
        }
        
        #endregion
    }
}