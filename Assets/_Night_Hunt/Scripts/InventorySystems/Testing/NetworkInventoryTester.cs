using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Network;
using NightHunt.Inventory.Systems;

namespace NightHunt.Inventory.Testing
{
    /// <summary>
    /// Network-aware inventory tester.
    /// Tests inventory operations using NetworkSync Public API.
    /// All operations will be synchronized across network.
    /// </summary>
    public class NetworkInventoryTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryNetworkSync inventorySync;
        [SerializeField] private EquipmentNetworkSync equipmentSync;
        [SerializeField] private WeaponNetworkSync weaponSync;
        [SerializeField] private AttachmentNetworkSync attachmentSync;
        [SerializeField] private QuickSlotNetworkSync quickSlotSync;
        
        [Header("Test Items")]
        [SerializeField] private ItemDefinition testHelmet;
        [SerializeField] private ItemDefinition testVest;
        [SerializeField] private ItemDefinition testWeapon;
        [SerializeField] private ItemDefinition testGrip;
        [SerializeField] private ItemDefinition testScope;
        [SerializeField] private ItemDefinition testAmmo;
        
        [Header("Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // Track added items for testing
        private string lastAddedItemInstanceId;
        private string lastAddedWeaponInstanceId;
        private string lastAddedAttachmentInstanceId;
        
        void Start()
        {
            // Auto-find NetworkSync components if not assigned
            if (inventorySync == null)
                inventorySync = GetComponent<InventoryNetworkSync>();
            
            if (equipmentSync == null)
                equipmentSync = GetComponent<EquipmentNetworkSync>();
            
            if (weaponSync == null)
                weaponSync = GetComponent<WeaponNetworkSync>();
            
            if (attachmentSync == null)
                attachmentSync = GetComponent<AttachmentNetworkSync>();
            
            if (quickSlotSync == null)
                quickSlotSync = GetComponent<QuickSlotNetworkSync>();
            
            // Subscribe to events to track instance IDs
            InventoryEvents.OnItemAdded += OnItemAdded;
            EquipmentEvents.OnItemEquipped += OnItemEquipped;
            WeaponEvents.OnWeaponEquipped += OnWeaponEquipped;
            
            Log("Network Inventory Tester initialized. Press keys 1-9 to test operations.");
        }
        
        void OnDestroy()
        {
            InventoryEvents.OnItemAdded -= OnItemAdded;
            EquipmentEvents.OnItemEquipped -= OnItemEquipped;
            WeaponEvents.OnWeaponEquipped -= OnWeaponEquipped;
        }
        
        void Update()
        {
            // Press 1: Add helmet to inventory (via NetworkSync)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (testHelmet == null)
                {
                    LogError("testHelmet is not assigned!");
                    return;
                }
                
                if (inventorySync == null)
                {
                    LogError("InventoryNetworkSync not found!");
                    return;
                }
                
                // Use NetworkSync Public API - this will sync to all clients
                inventorySync.RequestPickup(testHelmet.ItemId, 1);
                Log($"Requested to add helmet ({testHelmet.ItemId}) via NetworkSync");
            }
            
            // Press 2: Add weapon to inventory
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (testWeapon == null)
                {
                    LogError("testWeapon is not assigned!");
                    return;
                }
                
                if (inventorySync == null)
                {
                    LogError("InventoryNetworkSync not found!");
                    return;
                }
                
                inventorySync.RequestPickup(testWeapon.ItemId, 1);
                Log($"Requested to add weapon ({testWeapon.ItemId}) via NetworkSync");
            }
            
            // Press 3: Add grip attachment to inventory
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (testGrip == null)
                {
                    LogError("testGrip is not assigned!");
                    return;
                }
                
                if (inventorySync == null)
                {
                    LogError("InventoryNetworkSync not found!");
                    return;
                }
                
                inventorySync.RequestPickup(testGrip.ItemId, 1);
                Log($"Requested to add grip ({testGrip.ItemId}) via NetworkSync");
            }
            
            // Press 4: Equip helmet from inventory (using last added item)
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                if (equipmentSync == null)
                {
                    LogError("EquipmentNetworkSync not found!");
                    return;
                }
                
                if (string.IsNullOrEmpty(lastAddedItemInstanceId))
                {
                    LogError("No item added yet! Press 1 first to add helmet.");
                    return;
                }
                
                // Use NetworkSync Public API
                equipmentSync.RequestEquipFromInventory(lastAddedItemInstanceId, EquipmentSlotType.Helmet);
                Log($"Requested to equip item ({lastAddedItemInstanceId}) to Helmet slot via NetworkSync");
            }
            
            // Press 5: Equip weapon from inventory
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                if (weaponSync == null)
                {
                    LogError("WeaponNetworkSync not found!");
                    return;
                }
                
                if (string.IsNullOrEmpty(lastAddedWeaponInstanceId))
                {
                    LogError("No weapon added yet! Press 2 first to add weapon.");
                    return;
                }
                
                // Use NetworkSync Public API
                weaponSync.RequestEquipWeaponFromInventory(lastAddedWeaponInstanceId, WeaponSlotType.Primary);
                Log($"Requested to equip weapon ({lastAddedWeaponInstanceId}) to Primary slot via NetworkSync");
            }
            
            // Press 6: Attach grip to weapon
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                if (attachmentSync == null)
                {
                    LogError("AttachmentNetworkSync not found!");
                    return;
                }
                
                if (string.IsNullOrEmpty(lastAddedWeaponInstanceId))
                {
                    LogError("No weapon equipped! Press 5 first to equip weapon.");
                    return;
                }
                
                if (string.IsNullOrEmpty(lastAddedAttachmentInstanceId))
                {
                    LogError("No attachment added! Press 3 first to add grip.");
                    return;
                }
                
                // Use NetworkSync Public API
                // Assuming grip is AttachmentSlotType.Grip
                attachmentSync.RequestAttach(lastAddedWeaponInstanceId, lastAddedAttachmentInstanceId);
                Log($"Requested to attach grip ({lastAddedAttachmentInstanceId}) to weapon ({lastAddedWeaponInstanceId}) via NetworkSync");
            }
            
            // Press 7: Assign item to quick slot
            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                if (quickSlotSync == null)
                {
                    LogError("QuickSlotNetworkSync not found!");
                    return;
                }
                
                if (string.IsNullOrEmpty(lastAddedItemInstanceId))
                {
                    LogError("No item added yet! Press 1 first to add item.");
                    return;
                }
                
                // Use NetworkSync Public API - assign to quick slot 0 (hotkey 1)
                quickSlotSync.RequestAssignQuickSlot(lastAddedItemInstanceId, 0);
                Log($"Requested to assign item ({lastAddedItemInstanceId}) to quick slot 0 via NetworkSync");
            }
            
            // Press 8: Unequip helmet
            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                if (equipmentSync == null)
                {
                    LogError("EquipmentNetworkSync not found!");
                    return;
                }
                
                // Use NetworkSync Public API
                equipmentSync.RequestUnequipToInventory(EquipmentSlotType.Helmet);
                Log($"Requested to unequip Helmet via NetworkSync");
            }
            
            // Press 9: Remove item from inventory
            if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                if (inventorySync == null)
                {
                    LogError("InventoryNetworkSync not found!");
                    return;
                }
                
                if (string.IsNullOrEmpty(lastAddedItemInstanceId))
                {
                    LogError("No item added yet! Press 1 first to add item.");
                    return;
                }
                
                // Use NetworkSync Public API
                inventorySync.RequestDrop(lastAddedItemInstanceId);
                Log($"Requested to drop item ({lastAddedItemInstanceId}) via NetworkSync");
            }
            
            // Press 0: Add multiple items at once
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                if (inventorySync == null)
                {
                    LogError("InventoryNetworkSync not found!");
                    return;
                }
                
                // Add multiple items
                if (testHelmet != null)
                    inventorySync.RequestPickup(testHelmet.ItemId, 1);
                
                if (testVest != null)
                    inventorySync.RequestPickup(testVest.ItemId, 1);
                
                if (testWeapon != null)
                    inventorySync.RequestPickup(testWeapon.ItemId, 1);
                
                if (testGrip != null)
                    inventorySync.RequestPickup(testGrip.ItemId, 1);
                
                if (testAmmo != null)
                    inventorySync.RequestPickup(testAmmo.ItemId, 10); // Stack of 10
                
                Log("Requested to add multiple items via NetworkSync");
            }
        }
        
        // === Event Handlers (to track instance IDs) ===
        
        private void OnItemAdded(ItemInstance item, int slotIndex)
        {
            if (item == null)
                return;
            
            lastAddedItemInstanceId = item.InstanceId;
            
            // Track weapon separately
            if (item.Definition != null && item.Definition.ItemType == ItemType.Weapon)
            {
                lastAddedWeaponInstanceId = item.InstanceId;
            }
            
            // Track attachments separately
            if (item.Definition != null && item.Definition.AttachmentType != AttachmentSlotType.None)
            {
                lastAddedAttachmentInstanceId = item.InstanceId;
            }
            
            Log($"Item added: {item.Definition?.DisplayName} (InstanceId: {item.InstanceId}) at slot {slotIndex}");
        }
        
        private void OnItemEquipped(ItemInstance item, EquipmentSlotType slotType)
        {
            Log($"Item equipped: {item.Definition?.DisplayName} in {slotType}");
        }
        
        private void OnWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            Log($"Weapon equipped: {weapon.Definition?.DisplayName} in {slotType}");
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetworkInventoryTester] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[NetworkInventoryTester] {message}");
        }
    }
}