using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using GameplaySystems.Core.Data;
using GameplaySystems.Inventory;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Stat;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Test script for WeaponSystem with logging
    /// Tests weapon equip/unequip, selection, and reload operations
    /// Outputs all test results to TestResults/WeaponTest_[timestamp].txt
    /// </summary>
    public class WeaponSystemTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponSystem _weaponSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private IPlayerStat _statSystem;
        
        [Header("Test Items")]
        [SerializeField] private string _testWeaponID = "weapon_ak47";
        [SerializeField] private string _testPistolID = "weapon_pistol";
        
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
            if (_weaponSystem == null)
                _weaponSystem = GetComponent<WeaponSystem>();
            
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
            AppendLog("WEAPON SYSTEM TEST RESULTS");
            AppendLog("================================================================================");
            AppendLog($"Test Session Started: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"Unity Version: {Application.unityVersion}");
            AppendLog($"Platform: {Application.platform}");
            AppendLog("================================================================================\n");
            
            Debug.Log("=== STARTING WEAPON SYSTEM TESTS ===\n");
            
            Test1_EquipWeapon();
            Test2_UnequipWeapon();
            Test3_SelectWeapon();
            Test4_HolsterWeapon();
            Test5_Reload();
            Test6_MultipleWeaponSlots();
            
            GenerateFinalReport();
            
            Debug.Log("\n=== ALL WEAPON TESTS COMPLETED ===");
            
            if (_saveLogToFile)
            {
                SaveLogToFile();
            }
        }
        
        private void Test1_EquipWeapon()
        {
            StartTest("Equip Weapon");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                
                AppendLog($"Added weapon: {weapon.DefinitionID}, ID: {weapon.InstanceID}");
                
                _weaponSystem.EquipWeapon(weapon.InstanceID);
                
                var equippedWeapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                AssertNoThrow(equippedWeapon != null, "Weapon should be equipped");
                AssertNoThrow(equippedWeapon.InstanceID == weapon.InstanceID, "Should be same weapon");
                
                AppendLog($"Weapon equipped to Primary slot");
                
                EndTest(true, "Successfully equipped weapon");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test2_UnequipWeapon()
        {
            StartTest("Unequip Weapon");
            
            try
            {
                var equippedWeapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                if (equippedWeapon == null)
                {
                    AppendLog("No weapon equipped, equipping one first");
                    _inventorySystem.AddItem(_testWeaponID, 1);
                    var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                    _weaponSystem.EquipWeapon(weapon.InstanceID);
                    equippedWeapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                }
                
                string weaponID = equippedWeapon.InstanceID;
                AppendLog($"Unequipping weapon: {weaponID}");
                
                _weaponSystem.UnequipWeapon(WeaponSlotType.Primary);
                
                var weaponAfter = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                AssertNoThrow(weaponAfter == null, "Weapon should be unequipped");
                
                var inInventory = _inventorySystem.GetItemByInstanceID(weaponID);
                AssertNoThrow(inInventory != null, "Weapon should be in inventory");
                
                AppendLog($"Weapon back in inventory at index {inInventory.InventoryIndex}");
                
                EndTest(true, "Successfully unequipped weapon");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test3_SelectWeapon()
        {
            StartTest("Select Weapon (Draw)");
            
            try
            {
                var weapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                if (weapon == null)
                {
                    _inventorySystem.AddItem(_testWeaponID, 1);
                    var w = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                    _weaponSystem.EquipWeapon(w.InstanceID);
                }
                
                AppendLog("Selecting primary weapon");
                
                _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
                
                var activeSlot = _weaponSystem.GetActiveWeaponSlot();
                AssertNoThrow(activeSlot == WeaponSlotType.Primary, 
                    $"Active slot should be Primary, but is {activeSlot}");
                
                var activeWeapon = _weaponSystem.GetActiveWeapon();
                AssertNoThrow(activeWeapon != null, "Should have active weapon");
                
                AppendLog($"Active weapon: {activeWeapon.DefinitionID}");
                
                EndTest(true, "Weapon selected successfully");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test4_HolsterWeapon()
        {
            StartTest("Holster Weapon");
            
            try
            {
                var activeWeapon = _weaponSystem.GetActiveWeapon();
                if (activeWeapon == null)
                {
                    _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
                }
                
                AppendLog("Holstering weapon");
                
                _weaponSystem.HolsterWeapon();
                
                var activeAfter = _weaponSystem.GetActiveWeapon();
                AssertNoThrow(activeAfter == null, "Should have no active weapon after holster");
                
                var activeSlot = _weaponSystem.GetActiveWeaponSlot();
                AssertNoThrow(activeSlot == WeaponSlotType.None, "Active slot should be None");
                
                EndTest(true, "Weapon holstered successfully");
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test5_Reload()
        {
            StartTest("Reload Weapon");
            
            try
            {
                var weapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                if (weapon == null)
                {
                    _inventorySystem.AddItem(_testWeaponID, 1);
                    var w = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                    _weaponSystem.EquipWeapon(w.InstanceID);
                    weapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                }
                
                var magazineBefore = _weaponSystem.GetCurrentMagazine(WeaponSlotType.Primary);
                var totalAmmoBefore = _weaponSystem.GetTotalAmmo(WeaponSlotType.Primary);
                
                AppendLog($"Before reload - Magazine: {magazineBefore}, Total Ammo: {totalAmmoBefore}");
                
                bool canReload = _weaponSystem.CanReload(WeaponSlotType.Primary);
                
                if (canReload)
                {
                    _weaponSystem.Reload(WeaponSlotType.Primary);
                    
                    var magazineAfter = _weaponSystem.GetCurrentMagazine(WeaponSlotType.Primary);
                    var totalAmmoAfter = _weaponSystem.GetTotalAmmo(WeaponSlotType.Primary);
                    
                    AppendLog($"After reload - Magazine: {magazineAfter}, Total Ammo: {totalAmmoAfter}");
                    
                    AssertNoThrow(magazineAfter >= magazineBefore, 
                        $"Magazine should be refilled or stay same. Before: {magazineBefore}, After: {magazineAfter}");
                    
                    EndTest(true, $"Reload successful. Magazine: {magazineBefore} → {magazineAfter}");
                }
                else
                {
                    AppendLog("Cannot reload (magazine full or no ammo)");
                    EndTest(true, "Reload check working correctly");
                }
            }
            catch (System.Exception ex)
            {
                EndTest(false, $"Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Test6_MultipleWeaponSlots()
        {
            StartTest("Multiple Weapon Slots");
            
            try
            {
                _inventorySystem.ClearInventory();
                
                _inventorySystem.AddItem(_testWeaponID, 1);
                _inventorySystem.AddItem(_testPistolID, 1);
                
                var rifle = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
                var pistol = _inventorySystem.GetItemsByDefinition(_testPistolID)[0];
                
                AppendLog($"Rifle: {rifle.InstanceID}, Pistol: {pistol.InstanceID}");
                
                _weaponSystem.EquipWeapon(rifle.InstanceID);
                _weaponSystem.EquipWeapon(pistol.InstanceID);
                
                var primaryWeapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
                var secondaryWeapon = _weaponSystem.GetWeapon(WeaponSlotType.Secondary);
                
                AssertNoThrow(primaryWeapon != null, "Primary weapon should be equipped");
                AssertNoThrow(secondaryWeapon != null, "Secondary weapon should be equipped");
                
                AppendLog($"Primary: {primaryWeapon.DefinitionID}");
                AppendLog($"Secondary: {secondaryWeapon.DefinitionID}");
                
                _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
                var active1 = _weaponSystem.GetActiveWeaponSlot();
                AssertNoThrow(active1 == WeaponSlotType.Primary, "Should have primary active");
                
                _weaponSystem.SelectWeapon(WeaponSlotType.Secondary);
                var active2 = _weaponSystem.GetActiveWeaponSlot();
                AssertNoThrow(active2 == WeaponSlotType.Secondary, "Should have secondary active");
                
                EndTest(true, "Multiple weapon slots working correctly");
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
            AppendLog("\nWeapon State After Test:");
            var allWeapons = _weaponSystem.GetAllWeapons();
            AppendLog($"  Equipped Weapons: {allWeapons.Count}");
            
            if (allWeapons.Count > 0)
            {
                AppendLog("  Weapons:");
                foreach (var kvp in allWeapons)
                {
                    var def = ItemDatabase.GetDefinition(kvp.Value.DefinitionID);
                    var mag = _weaponSystem.GetCurrentMagazine(kvp.Key);
                    var ammo = _weaponSystem.GetTotalAmmo(kvp.Key);
                    AppendLog($"    [{kvp.Key}] {def?.DisplayName}: Mag={mag}, Ammo={ammo}");
                }
            }
            
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            AppendLog($"  Active Slot: {activeSlot}");
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
                string filename = $"WeaponTest_{timestamp}.txt";
                string filepath = Path.Combine(directory, filename);
                
                File.WriteAllText(filepath, _testLog.ToString());
                
                Debug.Log($"<color=green>✓ Test results saved to: {filepath}</color>");
                
                PlayerPrefs.SetString("LastWeaponTestLogPath", filepath);
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
            string path = PlayerPrefs.GetString("LastWeaponTestLogPath", "");
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
        
        [ContextMenu("Equip Test Weapon")]
        public void EquipTestWeapon()
        {
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            _weaponSystem.EquipWeapon(weapon.InstanceID);
            Debug.Log("Equipped test weapon");
        }
        
        [ContextMenu("Select Primary Weapon")]
        public void SelectPrimaryWeapon()
        {
            _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
            Debug.Log("Selected primary weapon");
        }
        
        [ContextMenu("Holster Weapon")]
        public void HolsterWeapon()
        {
            _weaponSystem.HolsterWeapon();
            Debug.Log("Holstered weapon");
        }
        
        [ContextMenu("Reload Weapon")]
        public void ReloadWeapon()
        {
            _weaponSystem.Reload(WeaponSlotType.Primary);
            Debug.Log("Reloaded weapon");
        }
        
        #endregion
    }
}