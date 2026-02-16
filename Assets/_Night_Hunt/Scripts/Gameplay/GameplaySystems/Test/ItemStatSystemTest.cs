using UnityEngine;
using GameplaySystems.Inventory;
using GameplaySystems.Core;
using GameplaySystems.Core.Data;
using GameplaySystems.Stat;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Test script for ItemStatSystem
    /// Tests item stat calculations with attachments
    /// 
    /// Tests:
    /// - Base stat values from definitions
    /// - Attachment flat modifiers
    /// - Attachment percentage modifiers
    /// - Multiple attachments stacking
    /// - Cache invalidation
    /// - Stat comparison (base vs modified)
    /// </summary>
    public class ItemStatSystemTest : MonoBehaviour
    {
        [Header("Test Items")]
        [SerializeField] private string _testWeaponID = "weapon_ak47";
        [SerializeField] private string _testScopeID = "attachment_reddot";
        [SerializeField] private string _testGripID = "attachment_grip";
        [SerializeField] private string _testSuppressorID = "attachment_suppressor";
        
        [Header("Test Controls")]
        [SerializeField] private bool _runTestsOnStart = false;
        
        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private AttachmentSystem _attachmentSystem;
        
        private void Start()
        {
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
            
            if (_attachmentSystem == null)
                _attachmentSystem = GetComponent<AttachmentSystem>();
            
            if (_runTestsOnStart)
                Invoke(nameof(RunAllTests), 2f);
        }
        
        #region Automated Tests
        
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== STARTING ITEMSTATSYSTEM TESTS ===\n");
            
            Test1_BaseStatValues();
            Test2_FlatModifiers();
            Test3_PercentageModifiers();
            Test4_MultipleAttachments();
            Test5_CacheInvalidation();
            Test6_StatComparison();
            Test7_GetAllStats();
            
            Debug.Log("\n=== ALL ITEMSTATSYSTEM TESTS COMPLETED ===");
        }
        
        private void Test1_BaseStatValues()
        {
            Debug.Log("\n--- Test 1: Base Stat Values ---");
            
            // Get base stats directly from definition
            float baseDamage = ItemStatSystem.GetBaseItemStat(_testWeaponID, ItemStatType.Damage);
            float baseAccuracy = ItemStatSystem.GetBaseItemStat(_testWeaponID, ItemStatType.Accuracy);
            
            Debug.Assert(baseDamage > 0, "Weapon should have damage stat");
            Debug.Assert(baseAccuracy > 0, "Weapon should have accuracy stat");
            
            Debug.Log($"Base Damage: {baseDamage}");
            Debug.Log($"Base Accuracy: {baseAccuracy}");
            
            // Check HasStat
            bool hasDamage = ItemStatSystem.HasStat(_testWeaponID, ItemStatType.Damage);
            bool hasInvalidStat = ItemStatSystem.HasStat(_testWeaponID, (ItemStatType)999);
            
            Debug.Assert(hasDamage, "Should have damage stat");
            Debug.Assert(!hasInvalidStat, "Should not have invalid stat");
            
            Debug.Log("✓ Test 1 PASSED");
        }
        
        private void Test2_FlatModifiers()
        {
            Debug.Log("\n--- Test 2: Flat Modifiers ---");
            
            if (_inventorySystem == null)
            {
                Debug.LogWarning("InventorySystem not found, skipping test");
                return;
            }
            
            // Clear inventory
            _inventorySystem.ClearInventory();
            
            // Add weapon
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            // Get base accuracy
            float baseAccuracy = ItemStatSystem.GetBaseItemStat(_testWeaponID, ItemStatType.Accuracy);
            float currentAccuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
            
            Debug.Assert(Mathf.Approximately(baseAccuracy, currentAccuracy), 
                "Without attachments, current should equal base");
            
            Debug.Log($"Base Accuracy: {baseAccuracy}");
            Debug.Log($"Current Accuracy (no attachments): {currentAccuracy}");
            
            // Add red dot scope (should have +10 flat accuracy)
            if (ItemDatabase.HasDefinition(_testScopeID))
            {
                _inventorySystem.AddItem(_testScopeID, 1);
                var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
                
                // Attach scope to weapon
                _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
                
                // Recalculate
                float modifiedAccuracy = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                
                Debug.Log($"Modified Accuracy (with scope): {modifiedAccuracy}");
                Debug.Assert(modifiedAccuracy > baseAccuracy, "Scope should increase accuracy");
                
                float expectedIncrease = 10f; // Red dot gives +10 flat
                Debug.Assert(Mathf.Approximately(modifiedAccuracy, baseAccuracy + expectedIncrease), 
                    "Should have +10 accuracy from scope");
            }
            
            Debug.Log("✓ Test 2 PASSED");
        }
        
        private void Test3_PercentageModifiers()
        {
            Debug.Log("\n--- Test 3: Percentage Modifiers ---");
            
            if (_inventorySystem == null)
            {
                Debug.LogWarning("InventorySystem not found, skipping test");
                return;
            }
            
            _inventorySystem.ClearInventory();
            
            // Add weapon
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            float baseRecoil = ItemStatSystem.GetBaseItemStat(_testWeaponID, ItemStatType.Recoil);
            
            Debug.Log($"Base Recoil: {baseRecoil}");
            
            // Add suppressor (should have percentage reduction)
            if (ItemDatabase.HasDefinition(_testSuppressorID))
            {
                _inventorySystem.AddItem(_testSuppressorID, 1);
                var suppressor = _inventorySystem.GetItemsByDefinition(_testSuppressorID)[0];
                
                // Attach suppressor
                int barrelSlotIndex = 3; // Assuming barrel is slot 3
                _attachmentSystem.AttachItem(suppressor.InstanceID, weapon.InstanceID, barrelSlotIndex);
                
                float modifiedRecoil = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Recoil);
                
                Debug.Log($"Modified Recoil (with suppressor): {modifiedRecoil}");
                Debug.Assert(modifiedRecoil < baseRecoil, "Suppressor should reduce recoil");
                
                // Suppressor gives -30% recoil
                float expected = baseRecoil * 0.7f;
                Debug.Assert(Mathf.Approximately(modifiedRecoil, expected), 
                    "Should have -30% recoil from suppressor");
            }
            
            Debug.Log("✓ Test 3 PASSED");
        }
        
        private void Test4_MultipleAttachments()
        {
            Debug.Log("\n--- Test 4: Multiple Attachments ---");
            
            if (_inventorySystem == null || _attachmentSystem == null)
            {
                Debug.LogWarning("Required systems not found, skipping test");
                return;
            }
            
            _inventorySystem.ClearInventory();
            
            // Add weapon
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            float baseAccuracy = ItemStatSystem.GetBaseItemStat(_testWeaponID, ItemStatType.Accuracy);
            
            Debug.Log($"Base Accuracy: {baseAccuracy}");
            
            // Attach multiple items that affect accuracy
            int attachmentsAdded = 0;
            
            // Add scope (+10 flat accuracy)
            if (ItemDatabase.HasDefinition(_testScopeID))
            {
                _inventorySystem.AddItem(_testScopeID, 1);
                var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
                _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
                attachmentsAdded++;
                
                float accuracy1 = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                Debug.Log($"Accuracy with scope: {accuracy1}");
            }
            
            // Add grip (+5 flat accuracy)
            if (ItemDatabase.HasDefinition(_testGripID))
            {
                _inventorySystem.AddItem(_testGripID, 1);
                var grip = _inventorySystem.GetItemsByDefinition(_testGripID)[0];
                _attachmentSystem.AttachItem(grip.InstanceID, weapon.InstanceID, 1);
                attachmentsAdded++;
                
                float accuracy2 = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
                Debug.Log($"Accuracy with scope + grip: {accuracy2}");
                
                // Should be base + 10 (scope) + 5 (grip) = base + 15
                float expected = baseAccuracy + 15f;
                Debug.Assert(Mathf.Approximately(accuracy2, expected), 
                    "Multiple attachments should stack");
            }
            
            if (attachmentsAdded > 0)
            {
                Debug.Log($"✓ Test 4 PASSED ({attachmentsAdded} attachments tested)");
            }
            else
            {
                Debug.LogWarning("No attachment definitions found for test");
            }
        }
        
        private void Test5_CacheInvalidation()
        {
            Debug.Log("\n--- Test 5: Cache Invalidation ---");
            
            if (_inventorySystem == null)
            {
                Debug.LogWarning("InventorySystem not found, skipping test");
                return;
            }
            
            _inventorySystem.ClearInventory();
            ItemStatSystem.ClearCache();
            
            // Add weapon
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            // Calculate stat (should cache)
            float accuracy1 = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
            
            // Calculate again (should use cache)
            float accuracy2 = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
            
            Debug.Assert(accuracy1 == accuracy2, "Cached values should match");
            
            // Invalidate cache
            ItemStatSystem.InvalidateCache(weapon.InstanceID);
            
            // Calculate again (should recalculate, not use cache)
            float accuracy3 = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Accuracy);
            
            Debug.Assert(accuracy1 == accuracy3, "Recalculated value should match original");
            
            Debug.Log("✓ Test 5 PASSED");
        }
        
        private void Test6_StatComparison()
        {
            Debug.Log("\n--- Test 6: Stat Comparison ---");
            
            if (_inventorySystem == null)
            {
                Debug.LogWarning("InventorySystem not found, skipping test");
                return;
            }
            
            _inventorySystem.ClearInventory();
            
            // Add weapon
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            // Compare base vs current stats
            float baseDamage = ItemStatSystem.GetBaseItemStat(_testWeaponID, ItemStatType.Damage);
            float currentDamage = ItemStatSystem.CalculateItemStat(weapon, ItemStatType.Damage);
            float damageModifier = currentDamage - baseDamage;
            
            Debug.Log($"Damage: Base={baseDamage}, Current={currentDamage}, Modifier={damageModifier:+0.0;-0.0}");
            
            Debug.Assert(Mathf.Approximately(damageModifier, 0f), 
                "Without attachments, modifier should be 0");
            
            Debug.Log("✓ Test 6 PASSED");
        }
        
        private void Test7_GetAllStats()
        {
            Debug.Log("\n--- Test 7: Get All Stats ---");
            
            if (_inventorySystem == null)
            {
                Debug.LogWarning("InventorySystem not found, skipping test");
                return;
            }
            
            _inventorySystem.ClearInventory();
            
            // Add weapon
            _inventorySystem.AddItem(_testWeaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            // Get all stats
            var allStats = ItemStatSystem.GetAllItemStats(weapon);
            
            Debug.Assert(allStats.Count > 0, "Weapon should have stats");
            
            Debug.Log($"Weapon has {allStats.Count} stats:");
            foreach (var kvp in allStats)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value:F1}");
            }
            
            // Should have common weapon stats
            Debug.Assert(allStats.ContainsKey(ItemStatType.Damage), "Should have damage");
            Debug.Assert(allStats.ContainsKey(ItemStatType.Accuracy), "Should have accuracy");
            
            Debug.Log("✓ Test 7 PASSED");
        }
        
        #endregion
        
        #region Manual Test Buttons
        
        [ContextMenu("Test: AK-47 Stats")]
        public void TestAK47Stats()
        {
            if (_inventorySystem == null)
            {
                Debug.LogError("InventorySystem not found!");
                return;
            }
            
            _inventorySystem.ClearInventory();
            _inventorySystem.AddItem(_testWeaponID, 1);
            
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            
            ItemStatSystem.LogItemStats(weapon);
        }
        
        [ContextMenu("Test: AK-47 + Red Dot")]
        public void TestAK47WithRedDot()
        {
            if (_inventorySystem == null || _attachmentSystem == null)
            {
                Debug.LogError("Required systems not found!");
                return;
            }
            
            _inventorySystem.ClearInventory();
            _inventorySystem.AddItem(_testWeaponID, 1);
            _inventorySystem.AddItem(_testScopeID, 1);
            
            var weapon = _inventorySystem.GetItemsByDefinition(_testWeaponID)[0];
            var scope = _inventorySystem.GetItemsByDefinition(_testScopeID)[0];
            
            Debug.Log("=== BEFORE ATTACHMENT ===");
            ItemStatSystem.LogItemStats(weapon);
            
            _attachmentSystem.AttachItem(scope.InstanceID, weapon.InstanceID, 0);
            
            Debug.Log("\n=== AFTER ATTACHING RED DOT ===");
            ItemStatSystem.LogItemStats(weapon);
        }
        
        [ContextMenu("Test: Clear Cache")]
        public void TestClearCache()
        {
            ItemStatSystem.ClearCache();
            ItemStatSystem.LogCacheStats();
            Debug.Log("Cache cleared");
        }
        
        [ContextMenu("Test: Log Cache Stats")]
        public void TestLogCacheStats()
        {
            ItemStatSystem.LogCacheStats();
        }
        
        #endregion
    }
}