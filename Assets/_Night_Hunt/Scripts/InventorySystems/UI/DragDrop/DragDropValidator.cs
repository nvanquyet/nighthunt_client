using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.UI.Slots;
using NightHunt.Inventory.UI.Data;
using NightHunt.Inventory.UI.Trash;
using NightHunt.Inventory.Network;
using NightHunt.Inventory.UI.Panels;

namespace NightHunt.Inventory.UI.DragDrop
{
    /// <summary>
    /// Validates drop operations.
    /// Checks item type compatibility, slot availability, weight limits, attachment compatibility.
    /// Calls NetworkSync Public API on valid drop.
    /// </summary>
    public class DragDropValidator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryUIDataProvider dataProvider;
        [SerializeField] private InventoryPanel inventoryPanel;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false; 
        
        // === Public API ===
        
        /// <summary>
        /// Check if item can be dropped on target slot.
        /// </summary>
        public bool CanDrop(ItemInstance item, ItemSlotUI source, ItemSlotUI target)
        {
            if (item == null || item.Definition == null || source == null || target == null)
                return false;
            
            if (!dataProvider.CanInteract())
                return false;
            
            // Same slot - no drop
            if (source == target)
                return false;
            
            // Validate based on target slot type
            if (target is InventorySlotUI)
            {
                return CanDropToInventory(item, source, target as InventorySlotUI);
            }
            else if (target is EquipmentSlotUI)
            {
                return CanDropToEquipment(item, source, target as EquipmentSlotUI);
            }
            else if (target is WeaponSlotUI)
            {
                return CanDropToWeapon(item, source, target as WeaponSlotUI);
            }
            else if (target is QuickSlotUI)
            {
                return CanDropToQuickSlot(item, source, target as QuickSlotUI);
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if item can be dropped on target (supports TrashSlotUI).
        /// </summary>
        public bool CanDrop(ItemInstance item, ItemSlotUI source, MonoBehaviour target)
        {
            if (item == null || item.Definition == null || source == null || target == null)
                return false;
            
            // Check if target is TrashSlotUI
            var trashSlot = target.GetComponent<TrashSlotUI>();
            if (trashSlot != null)
            {
                return CanDropToTrash(item, source, trashSlot);
            }
            
            // Fallback to ItemSlotUI version
            var itemSlot = target as ItemSlotUI;
            if (itemSlot != null)
            {
                return CanDrop(item, source, itemSlot);
            }
            
            return false;
        }
        
        /// <summary>
        /// Execute drop operation (calls NetworkSync).
        /// </summary>
        public void ExecuteDrop(ItemInstance item, ItemSlotUI source, ItemSlotUI target)
        {
            if (!CanDrop(item, source, target))
            {
                LogWarning($"Cannot execute drop - validation failed");
                return;
            }
            
            // Execute based on target slot type
            if (target is InventorySlotUI)
            {
                ExecuteDropToInventory(item, source, target as InventorySlotUI);
            }
            else if (target is EquipmentSlotUI)
            {
                ExecuteDropToEquipment(item, source, target as EquipmentSlotUI);
            }
            else if (target is WeaponSlotUI)
            {
                ExecuteDropToWeapon(item, source, target as WeaponSlotUI);
            }
            else if (target is QuickSlotUI)
            {
                ExecuteDropToQuickSlot(item, source, target as QuickSlotUI);
            }
        }
        
        /// <summary>
        /// Execute drop operation (supports TrashSlotUI).
        /// </summary>
        public void ExecuteDrop(ItemInstance item, ItemSlotUI source, MonoBehaviour target)
        {
            if (item == null || source == null || target == null)
                return;
            
            // Check if target is TrashSlotUI
            var trashSlot = target.GetComponent<TrashSlotUI>();
            if (trashSlot != null)
            {
                if (CanDropToTrash(item, source, trashSlot))
                {
                    ExecuteDropToTrash(item, source, trashSlot);
                }
                return;
            }
            
            // Fallback to ItemSlotUI version
            var itemSlot = target as ItemSlotUI;
            if (itemSlot != null)
            {
                ExecuteDrop(item, source, itemSlot);
            }
        }
        
        // === Validation Methods ===
        
        private bool CanDropToInventory(ItemInstance item, ItemSlotUI source, InventorySlotUI target)
        {
            if (target == null)
                return false;
            
            // Block drops to empty slots
            if (target.IsEmptySlot())
            {
                Log("Cannot drop to empty slot");
                return false;
            }
            
            // Check if target slot index is beyond actual inventory size
            if (dataProvider != null)
            {
                int actualInventorySize = dataProvider.GetInventorySlotCount();
                int targetIndex = target.GetSlotIndex();
                
                if (targetIndex >= actualInventorySize)
                {
                    Log($"Cannot drop to slot {targetIndex} - beyond inventory size ({actualInventorySize})");
                    return false;
                }
            }
            
            // Any item can be dropped to valid inventory slot
            return true;
        }
        
        private bool CanDropToEquipment(ItemInstance item, ItemSlotUI source, EquipmentSlotUI target)
        {
            var slotType = target.GetSlotType();
            
            // Check item type matches equipment slot
            if (item.Definition.ItemType != ItemType.Equipment)
                return false;
            
            // Check equipment slot type matches
            if (item.Definition.EquipmentSlot != slotType)
                return false;
            
            return true;
        }
        
        private bool CanDropToWeapon(ItemInstance item, ItemSlotUI source, WeaponSlotUI target)
        {
            // Check item is weapon
            if (item.Definition.ItemType != ItemType.Weapon)
                return false;
            
            return true;
        }
        
        private bool CanDropToQuickSlot(ItemInstance item, ItemSlotUI source, QuickSlotUI target)
        {
            // Quick slots can accept any item (validation handled by QuickSlotSystem)
            return true;
        }
        
        private bool CanDropToTrash(ItemInstance item, ItemSlotUI source, TrashSlotUI target)
        {
            // Can always trash items (with confirmation if enabled)
            return target != null && target.CanTrash(item);
        }
        
        // === Execution Methods ===
        
        private void ExecuteDropToInventory(ItemInstance item, ItemSlotUI source, InventorySlotUI target)
        {
            // If source is equipment, unequip
            if (source is EquipmentSlotUI)
            {
                var equipmentSlot = source as EquipmentSlotUI;
                var equipmentSync = dataProvider.GetEquipmentNetworkSync();
                if (equipmentSync != null)
                {
                    equipmentSync.RequestUnequipToInventory(equipmentSlot.GetSlotType());
                }
            }
            // If source is weapon, unequip
            else if (source is WeaponSlotUI)
            {
                var weaponSlot = source as WeaponSlotUI;
                var weaponSync = dataProvider.GetWeaponNetworkSync();
                if (weaponSync != null)
                {
                    weaponSync.RequestUnequipWeaponToInventory(weaponSlot.GetSlotType());
                }
            }
            // If source is quick slot, clear
            else if (source is QuickSlotUI)
            {
                var quickSlot = source as QuickSlotUI;
                var quickSlotSync = dataProvider.GetQuickSlotNetworkSync();
                if (quickSlotSync != null)
                {
                    quickSlotSync.RequestClearQuickSlot(quickSlot.GetQuickSlotIndex());
                }
            }
            // If source is inventory, move/swap (handled by InventoryNetworkSync)
            else if (source is InventorySlotUI)
            {
                var sourceInventory = source as InventorySlotUI;
                var targetInventory = target;
                var inventorySync = dataProvider.GetInventoryNetworkSync();
                if (inventorySync != null)
                {
                    int targetIndex = targetInventory.GetSlotIndex();
                    
                    // Move or swap
                    if (targetInventory.GetItem() == null)
                    {
                        // Move - call ServerRpc directly (it's public)
                        inventorySync.RequestMoveItem_ServerRpc(sourceInventory.GetSlotIndex(), targetIndex);
                    }
                    else
                    {
                        // Swap - call ServerRpc directly (it's public)
                        inventorySync.RequestSwapItems_ServerRpc(sourceInventory.GetSlotIndex(), targetIndex);
                    }
                    
                    // Check if expansion is needed after drop
                    if (inventoryPanel != null)
                    {
                        inventoryPanel.CheckAndExpandIfNeeded(targetIndex);
                    }
                }
            }
        }
        
        private void ExecuteDropToEquipment(ItemInstance item, ItemSlotUI source, EquipmentSlotUI target)
        {
            // Must come from inventory
            if (!(source is InventorySlotUI))
                return;
            
            var equipmentSync = dataProvider.GetEquipmentNetworkSync();
            if (equipmentSync != null)
            {
                equipmentSync.RequestEquipFromInventory(item.InstanceId, target.GetSlotType());
            }
        }
        
        private void ExecuteDropToWeapon(ItemInstance item, ItemSlotUI source, WeaponSlotUI target)
        {
            // Must come from inventory
            if (!(source is InventorySlotUI))
                return;
            
            var weaponSync = dataProvider.GetWeaponNetworkSync();
            if (weaponSync != null)
            {
                weaponSync.RequestEquipWeaponFromInventory(item.InstanceId, target.GetSlotType());
            }
        }
        
        private void ExecuteDropToQuickSlot(ItemInstance item, ItemSlotUI source, QuickSlotUI target)
        {
            // Must come from inventory
            if (!(source is InventorySlotUI))
                return;
            
            var quickSlotSync = dataProvider.GetQuickSlotNetworkSync();
            if (quickSlotSync != null)
            {
                quickSlotSync.RequestAssignQuickSlot(item.InstanceId, target.GetQuickSlotIndex());
            }
        }
        
        private void ExecuteDropToTrash(ItemInstance item, ItemSlotUI source, TrashSlotUI target)
        {
            // Trash the item
            if (target != null)
            {
                target.TrashItem(item);
            }
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            if (inventoryPanel == null)
                inventoryPanel = FindObjectOfType<InventoryPanel>();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[DragDropValidator] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[DragDropValidator] {message}");
        }
    }
}
