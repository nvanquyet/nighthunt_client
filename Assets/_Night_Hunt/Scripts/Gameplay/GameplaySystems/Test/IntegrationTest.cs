using GameplaySystems.Core.Data;
using UnityEngine;
using GameplaySystems.Inventory;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Stat;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Integration test for all gameplay systems
    /// Tests interaction between Stat, Inventory, Equipment, Weapon systems
    /// </summary>
    public class IntegrationTest : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private PlayerStatSystem _statSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private WeaponSystem _weaponSystem;
        [SerializeField] private QuickSlotSystem _quickSlotSystem;
        
        [Header("Test Items")]
        [SerializeField] private string _weaponID = "weapon_ak47";
        [SerializeField] private string _armorID = "armor_vest";
        [SerializeField] private string _backpackID = "armor_backpack";
        [SerializeField] private string _consumableID = "consumable_medkit";
        
        [Header("Test Controls")]
        [SerializeField] private bool _runTestsOnStart = false;
        
        private void Start()
        {
            ValidateReferences();
            
            if (_runTestsOnStart)
                Invoke(nameof(RunAllTests), 2f);
        }
        
        private void ValidateReferences()
        {
            if (_statSystem == null) _statSystem = GetComponent<PlayerStatSystem>();
            if (_inventorySystem == null) _inventorySystem = GetComponent<InventorySystem>();
            if (_equipmentSystem == null) _equipmentSystem = GetComponent<EquipmentSystem>();
            if (_weaponSystem == null) _weaponSystem = GetComponent<WeaponSystem>();
            if (_quickSlotSystem == null) _quickSlotSystem = GetComponent<QuickSlotSystem>();
        }
        
        [ContextMenu("Run All Integration Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== STARTING INTEGRATION TESTS ===\n");
            
            Test1_InventoryToEquipment();
            Test2_InventoryToWeapon();
            Test3_InventoryToQuickSlot();
            Test4_WeightSystemIntegration();
            Test5_FullWorkflow();
            
            Debug.Log("\n=== ALL INTEGRATION TESTS COMPLETED ===");
        }
        
        private void Test1_InventoryToEquipment()
        {
            Debug.Log("\n--- Integration Test 1: Inventory → Equipment ---");
            
            // Clear all
            _inventorySystem.ClearInventory();
            UnequipAll();
            
            // Add armor to inventory
            _inventorySystem.AddItem(_armorID, 1);
            var armor = _inventorySystem.GetItemsByDefinition(_armorID)[0];
            
            Debug.Log($"Added {_armorID} to inventory at index {armor.InventoryIndex}");
            
            // Equip from inventory
            _equipmentSystem.EquipItem(armor.InstanceID);
            
            // Verify: Item should be equipped AND no longer visible in main inventory
            var equipped = _equipmentSystem.GetEquippedItem(EquipmentSlotType.Chest);
            Debug.Assert(equipped != null, "Item should be equipped");
            Debug.Assert(equipped.InventoryIndex == -1, "Equipped item should have index -1");
            
            // Verify stats changed
            float armorValue = _statSystem.GetStat(PlayerStatType.Armor);
            Debug.Log($"Armor value after equip: {armorValue}");
            Debug.Assert(armorValue > 0, "Armor stat should be > 0");
            
            Debug.Log("✓ Integration Test 1 PASSED");
        }
        
        private void Test2_InventoryToWeapon()
        {
            Debug.Log("\n--- Integration Test 2: Inventory → Weapon ---");
            
            _inventorySystem.ClearInventory();
            UnequipAllWeapons();
            
            // Add weapon
            _inventorySystem.AddItem(_weaponID, 1);
            var weapon = _inventorySystem.GetItemsByDefinition(_weaponID)[0];
            
            Debug.Log($"Added {_weaponID} to inventory");
            
            // Equip weapon
            _weaponSystem.EquipWeapon(weapon.InstanceID);
            
            // Verify equipped
            var equippedWeapon = _weaponSystem.GetWeapon(WeaponSlotType.Primary);
            Debug.Assert(equippedWeapon != null, "Weapon should be equipped");
            Debug.Assert(equippedWeapon.InventoryIndex == -1, "Equipped weapon should have index -1");
            
            // Select weapon
            _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
            
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            Debug.Assert(activeSlot == WeaponSlotType.Primary, "Primary should be active");
            
            Debug.Log("✓ Integration Test 2 PASSED");
        }
        
        private void Test3_InventoryToQuickSlot()
        {
            Debug.Log("\n--- Integration Test 3: Inventory → QuickSlot ---");
            
            _inventorySystem.ClearInventory();
            _quickSlotSystem.ClearAllQuickSlots();
            
            // Add consumable
            _inventorySystem.AddItem(_consumableID, 10);
            var consumable = _inventorySystem.GetItemsByDefinition(_consumableID)[0];
            
            Debug.Log($"Added {_consumableID} to inventory");
            
            // Assign to quick slot 0
            _quickSlotSystem.AssignToQuickSlot(consumable.InstanceID, 0);
            
            // Verify assigned
            var slotItem = _quickSlotSystem.GetQuickSlotItem(0);
            Debug.Assert(slotItem != null, "Item should be in quick slot");
            Debug.Assert(slotItem.InstanceID == consumable.InstanceID, "Should be same item");
            
            // Item should still be in inventory
            var inInventory = _inventorySystem.GetItemByInstanceID(consumable.InstanceID);
            Debug.Assert(inInventory != null, "Item should still be in inventory");
            
            Debug.Log("✓ Integration Test 3 PASSED");
        }
        
        private void Test4_WeightSystemIntegration()
        {
            Debug.Log("\n--- Integration Test 4: Weight System Integration ---");
            
            _inventorySystem.ClearInventory();
            UnequipAll();
            
            // Initial weight
            float weight0 = _statSystem.GetCurrentWeight();
            float capacity0 = _statSystem.GetWeightCapacity();
            
            Debug.Log($"Initial: {weight0}/{capacity0}");
            
            // Add weapon (increases weight)
            _inventorySystem.AddItem(_weaponID, 1);
            float weight1 = _statSystem.GetCurrentWeight();
            Debug.Log($"After adding weapon: {weight1}");
            Debug.Assert(weight1 > weight0, "Weight should increase");
            
            // Add backpack (increases capacity)
            _inventorySystem.AddItem(_backpackID, 1);
            var backpack = _inventorySystem.GetItemsByDefinition(_backpackID)[0];
            float weight2 = _statSystem.GetCurrentWeight();
            
            _equipmentSystem.EquipItem(backpack.InstanceID);
            float capacity1 = _statSystem.GetWeightCapacity();
            Debug.Log($"After equipping backpack - Capacity: {capacity0} → {capacity1}");
            Debug.Assert(capacity1 > capacity0, "Capacity should increase");
            
            // Check movement speed penalty
            float speedMult = _statSystem.GetMovementSpeedMultiplier();
            Debug.Log($"Movement speed multiplier: {speedMult:P0}");
            
            Debug.Log("✓ Integration Test 4 PASSED");
        }
        
        private void Test5_FullWorkflow()
        {
            Debug.Log("\n--- Integration Test 5: Full Workflow ---");
            Debug.Log("Simulating player loadout setup...\n");
            
            // Clear everything
            _inventorySystem.ClearInventory();
            UnequipAll();
            UnequipAllWeapons();
            _quickSlotSystem.ClearAllQuickSlots();
            
            // Step 1: Loot items
            Debug.Log("1. Looting items...");
            _inventorySystem.AddItem(_weaponID, 1);
            _inventorySystem.AddItem(_armorID, 1);
            _inventorySystem.AddItem(_backpackID, 1);
            _inventorySystem.AddItem(_consumableID, 5);
            
            Debug.Log($"  - Inventory has {_inventorySystem.GetAllItems().Count} items");
            
            // Step 2: Equip loadout
            Debug.Log("2. Equipping loadout...");
            
            var weapon = _inventorySystem.GetItemsByDefinition(_weaponID)[0];
            _weaponSystem.EquipWeapon(weapon.InstanceID);
            Debug.Log("  - Weapon equipped");
            
            var armor = _inventorySystem.GetItemsByDefinition(_armorID)[0];
            _equipmentSystem.EquipItem(armor.InstanceID);
            Debug.Log("  - Armor equipped");
            
            var backpack = _inventorySystem.GetItemsByDefinition(_backpackID)[0];
            _equipmentSystem.EquipItem(backpack.InstanceID);
            Debug.Log("  - Backpack equipped");
            
            // Step 3: Setup quick slots
            Debug.Log("3. Setting up quick slots...");
            var consumable = _inventorySystem.GetItemsByDefinition(_consumableID)[0];
            _quickSlotSystem.AssignToQuickSlot(consumable.InstanceID, 0);
            Debug.Log("  - Medkit assigned to slot 1");
            
            // Step 4: Select weapon
            Debug.Log("4. Drawing weapon...");
            _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
            Debug.Log("  - Primary weapon drawn");
            
            // Step 5: Check final stats
            Debug.Log("\n5. Final Stats:");
            var (weight, capacity, percent) = _inventorySystem.GetWeightInfo();
            //float armor = _statSystem.GetStat(PlayerStatType.Armor);
            float speed = _statSystem.GetMovementSpeedMultiplier();
            
            Debug.Log($"  - Weight: {weight:F1}/{capacity:F1} ({percent:P0})");
            Debug.Log($"  - Armor: {armor:F0}");
            Debug.Log($"  - Speed: {speed:P0}");
            
            var activeWeapon = _weaponSystem.GetActiveWeapon();
            Debug.Log($"  - Active Weapon: {activeWeapon?.DefinitionID ?? "None"}");
            
            Debug.Log("\n✓ Integration Test 5 PASSED - Full workflow successful!");
        }
        
        #region Helper Methods
        
        private void UnequipAll()
        {
            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                if (_equipmentSystem.IsSlotOccupied(slot))
                {
                    _equipmentSystem.UnequipItem(slot);
                }
            }
        } 
        
        private void UnequipAllWeapons()
        {
            foreach (WeaponSlotType slot in System.Enum.GetValues(typeof(WeaponSlotType)))
            {
                if (_weaponSystem.IsSlotOccupied(slot))
                {
                    _weaponSystem.UnequipWeapon(slot);
                }
            }
        }
        
        #endregion
        
        #region Manual Test Scenarios
        
        [ContextMenu("Scenario: Fresh Spawn")]
        public void ScenarioFreshSpawn()
        {
            Debug.Log("=== SCENARIO: Fresh Spawn ===");
            
            _inventorySystem.ClearInventory();
            UnequipAll();
            UnequipAllWeapons();
            _quickSlotSystem.ClearAllQuickSlots();
            
            Debug.Log("Player spawned with empty inventory");
        }
        
        [ContextMenu("Scenario: Fully Equipped")]
        public void ScenarioFullyEquipped()
        {
            Debug.Log("=== SCENARIO: Fully Equipped ===");
            
            ScenarioFreshSpawn();
            
            // Add and equip full loadout
            _inventorySystem.AddItem(_weaponID, 1);
            _inventorySystem.AddItem(_armorID, 1);
            _inventorySystem.AddItem(_backpackID, 1);
            _inventorySystem.AddItem(_consumableID, 10);
            
            var weapon = _inventorySystem.GetItemsByDefinition(_weaponID)[0];
            var armor = _inventorySystem.GetItemsByDefinition(_armorID)[0];
            var backpack = _inventorySystem.GetItemsByDefinition(_backpackID)[0];
            var consumable = _inventorySystem.GetItemsByDefinition(_consumableID)[0];
            
            _weaponSystem.EquipWeapon(weapon.InstanceID);
            _weaponSystem.SelectWeapon(WeaponSlotType.Primary);
            _equipmentSystem.EquipItem(armor.InstanceID);
            _equipmentSystem.EquipItem(backpack.InstanceID);
            _quickSlotSystem.AssignToQuickSlot(consumable.InstanceID, 0);
            
            Debug.Log("Player fully equipped and ready for combat");
            
            _inventorySystem.LogInventoryState();
            _equipmentSystem.LogEquipmentState();
            _weaponSystem.LogWeaponState();
        }
        
        [ContextMenu("Scenario: Overweight")]
        public void ScenarioOverweight()
        {
            Debug.Log("=== SCENARIO: Overweight ===");
            
            ScenarioFreshSpawn();
            
            // Add many heavy items
            for (int i = 0; i < 20; i++)
            {
                _inventorySystem.AddItem(_weaponID, 1);
            }
            
            var (weight, capacity, percent) = _inventorySystem.GetWeightInfo();
            float speed = _statSystem.GetMovementSpeedMultiplier();
            
            Debug.Log($"Weight: {weight:F1}/{capacity:F1} ({percent:P0})");
            Debug.Log($"Movement Speed: {speed:P0}");
            
            if (percent > 1.5f)
            {
                Debug.LogWarning("CRITICALLY OVERWEIGHT - Cannot move!");
            }
            else if (percent > 1.0f)
            {
                Debug.LogWarning("OVERWEIGHT - Movement penalized!");
            }
        }
        
        #endregion
    }
}