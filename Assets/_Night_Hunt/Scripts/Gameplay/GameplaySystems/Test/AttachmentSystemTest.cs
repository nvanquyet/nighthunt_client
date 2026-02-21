using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Systems;
using System.Linq;
using NightHunt.GameplaySystems.Attachment;

namespace NightHunt.GameplaySystems.Test
{
    /// <summary>
    /// Test script for AttachmentSystem with logging
    /// Tests attachment operations and stat modifications
    /// Outputs all test results to TestResults/AttachmentTest_[timestamp].txt
    /// </summary>
    public class AttachmentSystemTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AttachmentSystem _attachmentSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        
        [Header("Test Items")]
        [SerializeField] private string _testWeaponID = "weapon_ak47";
        [SerializeField] private string _testScopeID = "attachment_reddot";
        [SerializeField] private string _testGripID = "attachment_grip";
        [SerializeField] private string _testSuppressorID = "attachment_suppressor";
        
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
            if (_attachmentSystem == null)
                _attachmentSystem = GetComponent<AttachmentSystem>();
            
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
            AppendLog("ATTACHMENT SYSTEM TEST RESULTS");
            AppendLog("================================================================================");
            AppendLog($"Test Session Started: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Unity Version: {Application.unityVersion}");
            AppendLog($"Platform: {Application.platform}");
            AppendLog("================================================================================\n");
            
            Debug.Log("=== STARTING ATTACHMENT SYSTEM TESTS ===\n");
            
            Test1_AttachItem();
            Test2_DetachItem();
            Test3_MultipleAttachments();
            Test4_ItemStatModification();
            Test5_DetachAll();
            
            GenerateFinalReport();
            
            Debug.Log("\n=== ALL ATTACHMENT TESTS COMPLETED ===");
            
            if (_saveLogToFile)
            {
                SaveLogToFile();
            }
        }
        
        private void Test1_AttachItem()
        {
            StartTest("Attach Item");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                _inventorySystem.AddItem(_testScopeID, 1);
                
                var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
                
                AppendLog($"Weapon: {weapon.InstanceID}, Scope: {scope.InstanceID}");
                
                float baseAccuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                AppendLog($"Base accuracy: {baseAccuracy:F1}");
                
                _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
                
                var attached = _attachmentSystem.GetAttachment(weapon.InstanceID, 0);
                AssertNoThrow(attached != null, "Scope should be attached");
                AssertNoThrow(attached.InstanceID == scope.InstanceID, "Should be same scope");
                
                var scopeInInventory = _inventorySystem.GetItemByInstanceID(scope.InstanceID);
                AssertNoThrow(scopeInInventory == null, "Scope should be removed from inventory");
                
                float modifiedAccuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                AppendLog($"Modified accuracy: {modifiedAccuracy:F1}");
                AssertNoThrow(modifiedAccuracy > baseAccuracy, 
                    $"Accuracy should increase. Base: {baseAccuracy:F1}, Modified: {modifiedAccuracy:F1}");
                
                EndTest(true, $"Attachment successful. Accuracy: {baseAccuracy:F1} → {modifiedAccuracy:F1}");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test2_DetachItem()
        {
            StartTest("Detach Item");
            
            try
            {
                var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                var attached = _attachmentSystem.GetAttachment(weapon.InstanceID, 0);
                
                if (attached == null)
                {
                    _inventorySystem.AddItem(_testScopeID, 1);
                    var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
                    _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
                    attached = _attachmentSystem.GetAttachment(weapon.InstanceID, 0);
                }
                
                string attachmentID = attached.InstanceID;
                AppendLog($"Detaching: {attachmentID}");
                
                float accuracyBefore = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                AppendLog($"Accuracy before detach: {accuracyBefore:F1}");
                
                _attachmentSystem.DetachItem(weapon.InstanceID, 0);
                
                var attachedAfter = _attachmentSystem.GetAttachment(weapon.InstanceID, 0);
                AssertNoThrow(attachedAfter == null, "Attachment should be detached");
                
                var inInventory = _inventorySystem.GetItemByInstanceID(attachmentID);
                AssertNoThrow(inInventory != null, "Attachment should be in inventory");
                
                float accuracyAfter = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                AppendLog($"Accuracy after detach: {accuracyAfter:F1}");
                AssertNoThrow(accuracyAfter < accuracyBefore, 
                    $"Accuracy should decrease. Before: {accuracyBefore:F1}, After: {accuracyAfter:F1}");
                
                EndTest(true, "Detachment successful, stats reverted");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test3_MultipleAttachments()
        {
            StartTest("Multiple Attachments");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                _inventorySystem.AddItem(_testScopeID, 1);
                _inventorySystem.AddItem(_testGripID, 1);
                _inventorySystem.AddItem(_testSuppressorID, 1);
                
                var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
                var grip = _inventorySystem.GetItemsByDefinition(_testGripID)[0];
                var suppressor = _inventorySystem.GetItemsByDefinition(_testSuppressorID)[0];
                
                float baseAccuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                float baseRecoil = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Recoil);
                
                AppendLog($"Base stats - Accuracy: {baseAccuracy:F1}, Recoil: {baseRecoil:F1}");
                
                _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
                _attachmentSystem.AttachItem(grip.InstanceID, weapon.InstanceID, 1);
                _attachmentSystem.AttachItem(suppressor.InstanceID, weapon.InstanceID, 3);
                
                var allAttachments = _attachmentSystem.GetAllAttachments(weapon.InstanceID).ToList();
                AssertNoThrow(allAttachments.Count == 3, $"Should have 3 attachments, but has {allAttachments.Count}");
                
                AppendLog($"Attached {allAttachments.Count} items");
                
                float modifiedAccuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                float modifiedRecoil = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Recoil);
                
                AppendLog($"Modified stats - Accuracy: {modifiedAccuracy:F1}, Recoil: {modifiedRecoil:F1}");
                
                AssertNoThrow(modifiedAccuracy > baseAccuracy, "Accuracy should increase");
                AssertNoThrow(modifiedRecoil < baseRecoil, "Recoil should decrease");
                
                EndTest(true, $"Multiple attachments working. Accuracy: {baseAccuracy:F1}→{modifiedAccuracy:F1}, Recoil: {baseRecoil:F1}→{modifiedRecoil:F1}");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test4_ItemStatModification()
        {
            StartTest("Item Stat Modification");
            
            try
            {
                var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                
                var allStats = ItemStatSystem.GetAllItemStats(weapon);
                AppendLog($"Total stats tracked: {allStats.Count}");
                
                foreach (var kvp in allStats)
                {
                    AppendLog($"  {kvp.Key}: {kvp.Value:F1}");
                }
                
                AssertNoThrow(allStats.Count > 0, "Should have stats");
                
                EndTest(true, $"Item stats calculated correctly. {allStats.Count} stats found");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test5_DetachAll()
        {
            StartTest("Detach All");
            
            try
            {
                var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                
                var attachmentsBefore = _attachmentSystem.GetAllAttachments(weapon.InstanceID).ToList();
                int countBefore = attachmentsBefore.Count;
                AppendLog($"Attachments before detach all: {countBefore}");
                
                if (countBefore == 0)
                {
                    _inventorySystem.AddItem(_testScopeID, 1);
                    var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
                    _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
                    countBefore = 1;
                }
                
                _attachmentSystem.DetachAllFromItem(weapon.InstanceID);
                
                var attachmentsAfter = _attachmentSystem.GetAllAttachments(weapon.InstanceID).ToList();
                AssertNoThrow(attachmentsAfter.Count == 0, $"Should have 0 attachments, but has {attachmentsAfter.Count}");
                
                AppendLog($"Detached {countBefore} attachments");
                
                float accuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                AppendLog($"Accuracy after detach all: {accuracy:F1}");
                
                EndTest(true, $"Detached all {countBefore} attachments successfully");
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
            AppendLog("\nAttachment State After Test:");
            
            var weapons = _inventorySystem.GetAllItems();
            int weaponCount = 0;
            
            foreach (var item in weapons)
            {
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                if (def is NightHunt.GameplaySystems.Core.Data.WeaponDefinition)
                {
                    var attachments = _attachmentSystem.GetAllAttachments(item.InstanceID).ToList();
                    if (attachments.Count > 0)
                    {
                        AppendLog($"  Weapon: {def?.DisplayName}");
                        foreach (var kvp in attachments)
                        {
                            if (kvp == null || string.IsNullOrEmpty(kvp.DefinitionID)) continue;
                            var attDef = ItemDatabase.GetDefinition(kvp.DefinitionID);
                            AppendLog($"    [Slot {kvp.DefinitionID}] {attDef?.DisplayName}");
                        }
                        weaponCount++;
                    }
                }
            }
            
            if (weaponCount == 0)
            {
                AppendLog("  No weapons with attachments");
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
                string filename = $"AttachmentTest_{timestamp}.txt";
                string filepath = Path.Combine(directory, filename);
                
                File.WriteAllText(filepath, _testLog.ToString());
                
                Debug.Log($"<color=green>✓ Test results saved to: {filepath}</color>");
                
                PlayerPrefs.SetString("LastAttachmentTestLogPath", filepath);
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
            string path = PlayerPrefs.GetString("LastAttachmentTestLogPath", "");
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
        
        [ContextMenu("Attach Test Scope")]
        public void AttachTestScope()
        {
            _inventorySystem.AddItem(_testWeaponID, 1);
            _inventorySystem.AddItem(_testScopeID, 1);
            
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
            
            _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
            Debug.Log("Attached test scope");
        }
        
        [ContextMenu("Detach All From Weapon")]
        public void DetachAllFromWeapon()
        {
            var weapons = _inventorySystem.GetItemsByDefinition(_testWeaponID);
            if (weapons.Count > 0)
            {
                _attachmentSystem.DetachAllFromItem(weapons[0].InstanceID);
                Debug.Log("Detached all attachments");
            }
        }
        
        #endregion
    }
}