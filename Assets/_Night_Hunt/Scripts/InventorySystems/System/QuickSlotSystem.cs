using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using System;
using System.Collections.Generic;

namespace NightHunt.Inventory.Systems
{
    /// <summary>
    /// Quick slot system for fast access items (hotkeys 1-8).
    /// Examples: Health potion, grenades, tools, etc.
    /// </summary>
    public class QuickSlotSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private SlotLayoutConfig slotLayout;
        
        [Header("References")]
        [SerializeField] private InventorySystem inventorySystem; // Injected
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // Quick slots: Index (0-7) → ItemInstance reference
        private Dictionary<int, ItemInstance> quickSlots = new Dictionary<int, ItemInstance>();
        
        // Events
        public static event Action<ItemInstance, int> OnQuickSlotAssigned;
        public static event Action<int> OnQuickSlotCleared;
        public static event Action<ItemInstance, int> OnQuickSlotUsed;
        public static event Action<OperationResult, int, string> OnQuickSlotOperationFailed;
        
        // === Lifecycle ===
        
        void Awake()
        {
            Initialize();
        }
        
        void Initialize()
        {
            if (slotLayout == null)
            {
                LogError("SlotLayoutConfig not assigned!");
                return;
            }
            
            // Initialize quick slots
            int slotCount = slotLayout.QuickSlotCount;
            for (int i = 0; i < slotCount; i++)
            {
                quickSlots[i] = null;
            }
            
            Log($"Initialized quick slot system with {slotCount} slots");
        }
        
        void Update()
        {
            HandleHotkeyInput();
        }
        
        // === Dependency Injection ===
        
        public void SetInventorySystem(InventorySystem inventory)
        {
            inventorySystem = inventory;
        }
        
        // === Public API ===
        
        /// <summary>
        /// Assign item to quick slot.
        /// Item remains in inventory - this is just a reference.
        /// </summary>
        public OperationResult AssignToQuickSlot(ItemInstance item, int quickSlotIndex)
        {
            // Validate
            if (!IsValidSlotIndex(quickSlotIndex))
            {
                OnQuickSlotOperationFailed?.Invoke(OperationResult.InvalidSlotIndex, quickSlotIndex, "Invalid quick slot index");
                return OperationResult.InvalidSlotIndex;
            }
            
            if (item == null)
            {
                OnQuickSlotOperationFailed?.Invoke(OperationResult.ItemNotFound, quickSlotIndex, "Item is null");
                return OperationResult.ItemNotFound;
            }
            
            // Check if item is in inventory
            if (inventorySystem != null && !inventorySystem.HasItem(item.InstanceId))
            {
                OnQuickSlotOperationFailed?.Invoke(OperationResult.ItemNotFound, quickSlotIndex, "Item not in inventory");
                return OperationResult.ItemNotFound;
            }
            
            // Assign
            quickSlots[quickSlotIndex] = item;
            
            OnQuickSlotAssigned?.Invoke(item, quickSlotIndex);
            
            Log($"Assigned {item.Definition.DisplayName} to quick slot {quickSlotIndex + 1}");
            return OperationResult.Success;
        }
        
        /// <summary>
        /// Clear quick slot.
        /// </summary>
        public void ClearQuickSlot(int quickSlotIndex)
        {
            if (!IsValidSlotIndex(quickSlotIndex))
                return;
            
            quickSlots[quickSlotIndex] = null;
            
            OnQuickSlotCleared?.Invoke(quickSlotIndex);
            
            Log($"Cleared quick slot {quickSlotIndex + 1}");
        }
        
        /// <summary>
        /// Use item in quick slot.
        /// </summary>
        public OperationResult UseQuickSlot(int quickSlotIndex)
        {
            // Validate
            if (!IsValidSlotIndex(quickSlotIndex))
            {
                OnQuickSlotOperationFailed?.Invoke(OperationResult.InvalidSlotIndex, quickSlotIndex, "Invalid slot index");
                return OperationResult.InvalidSlotIndex;
            }
            
            var item = quickSlots[quickSlotIndex];
            if (item == null)
            {
                OnQuickSlotOperationFailed?.Invoke(OperationResult.ItemNotFound, quickSlotIndex, "No item in this quick slot");
                return OperationResult.ItemNotFound;
            }
            
            // Check if item still exists in inventory
            if (inventorySystem != null && !inventorySystem.HasItem(item.InstanceId))
            {
                // Item was removed/consumed - clear slot
                ClearQuickSlot(quickSlotIndex);
                OnQuickSlotOperationFailed?.Invoke(OperationResult.ItemNotFound, quickSlotIndex, "Item no longer in inventory");
                return OperationResult.ItemNotFound;
            }
            
            // Use item based on type
            var result = UseItemByType(item);
            
            if (result == OperationResult.Success)
            {
                OnQuickSlotUsed?.Invoke(item, quickSlotIndex);
                Log($"Used quick slot {quickSlotIndex + 1}: {item.Definition.DisplayName}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Get item in quick slot.
        /// </summary>
        public ItemInstance GetQuickSlotItem(int quickSlotIndex)
        {
            if (!IsValidSlotIndex(quickSlotIndex))
                return null;
            
            return quickSlots[quickSlotIndex];
        }
        
        /// <summary>
        /// Check if slot has item assigned.
        /// </summary>
        public bool IsSlotAssigned(int quickSlotIndex)
        {
            return GetQuickSlotItem(quickSlotIndex) != null;
        }
        
        /// <summary>
        /// Get all quick slot items.
        /// </summary>
        public Dictionary<int, ItemInstance> GetAllQuickSlots()
        {
            return new Dictionary<int, ItemInstance>(quickSlots);
        }
        
        /// <summary>
        /// Get quick slot count.
        /// </summary>
        public int GetQuickSlotCount()
        {
            return quickSlots.Count;
        }
        
        // === Private Helpers ===
        
        private bool IsValidSlotIndex(int index)
        {
            return index >= 0 && index < quickSlots.Count;
        }
        
        /// <summary>
        /// Use item based on its type.
        /// </summary>
        private OperationResult UseItemByType(ItemInstance item)
        {
            switch (item.Definition.ItemType)
            {
                case ItemType.Consumable:
                    return ConsumeItem(item);
                
                // case ItemType.Weapon:
                //     // TODO: Switch to weapon (requires WeaponSystem reference)
                //     Log($"Weapon quick use not yet implemented: {item.Definition.DisplayName}");
                //     return OperationResult.Success;
                //
                // case ItemType.Equipment:
                //     // TODO: Equip item (requires EquipmentSystem reference)
                //     Log($"Equipment quick use not yet implemented: {item.Definition.DisplayName}");
                //     return OperationResult.Success;
                //
                case ItemType.Throwable:
                    // TODO: Throw item (requires combat system)
                    Log($"Throwable quick use not yet implemented: {item.Definition.DisplayName}");
                    return OperationResult.Success;
                
                default:
                    Log($"Item type {item.Definition.ItemType} cannot be quick-used");
                    return OperationResult.InvalidItemType;
            }
        }
        
        /// <summary>
        /// Consume item (health potion, food, etc.).
        /// </summary>
        private OperationResult ConsumeItem(ItemInstance item)
        {
            // Decrease stack or remove item
            if (item.Definition.IsStackable && item.StackSize > 1)
            {
                item.StackSize--;
                Log($"Consumed 1x {item.Definition.DisplayName}. Remaining: {item.StackSize}");
            }
            else
            {
                // Remove from inventory
                if (inventorySystem != null)
                {
                    inventorySystem.RemoveItem(item.InstanceId);
                }
                
                // Clear from quick slot
                int slotIndex = GetSlotIndexForItem(item);
                if (slotIndex >= 0)
                {
                    ClearQuickSlot(slotIndex);
                }
                
                Log($"Consumed last {item.Definition.DisplayName}");
            }
            
            // TODO: Apply consumable effects (healing, buffs, etc.)
            // This would integrate with CharacterStats or a separate EffectSystem
            
            return OperationResult.Success;
        }
        
        /// <summary>
        /// Find which quick slot contains this item.
        /// </summary>
        private int GetSlotIndexForItem(ItemInstance item)
        {
            foreach (var kvp in quickSlots)
            {
                if (kvp.Value == item)
                    return kvp.Key;
            }
            return -1;
        }
        
        // === Hotkey Input ===
        
        private void HandleHotkeyInput()
        {
            // Check number keys 1-8
            for (int i = 0; i < Mathf.Min(8, quickSlots.Count); i++)
            {
                // KeyCode.Alpha1 = 49, Alpha2 = 50, etc.
                KeyCode key = (KeyCode)(49 + i);
                
                if (Input.GetKeyDown(key))
                {
                    UseQuickSlot(i);
                }
            }
        }
        
        // === Auto-Cleanup ===
        
        /// <summary>
        /// Validate all quick slots and clear invalid references.
        /// Called periodically or on inventory changes.
        /// </summary>
        public void ValidateQuickSlots()
        {
            if (inventorySystem == null)
                return;
            
            var slotsToClean = new List<int>();
            
            foreach (var kvp in quickSlots)
            {
                if (kvp.Value != null && !inventorySystem.HasItem(kvp.Value.InstanceId))
                {
                    slotsToClean.Add(kvp.Key);
                }
            }
            
            foreach (int slotIndex in slotsToClean)
            {
                ClearQuickSlot(slotIndex);
                Log($"Auto-cleared quick slot {slotIndex + 1} - item no longer in inventory");
            }
        }
        
        void OnEnable()
        {
            // Subscribe to inventory events to auto-clean quick slots
            InventoryEvents.OnItemRemoved += OnInventoryItemRemoved;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnItemRemoved -= OnInventoryItemRemoved;
        }
        
        private void OnInventoryItemRemoved(ItemInstance item, int slotIndex)
        {
            // If removed item was in a quick slot, clear it
            int quickSlotIndex = GetSlotIndexForItem(item);
            if (quickSlotIndex >= 0)
            {
                ClearQuickSlot(quickSlotIndex);
            }
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[QuickSlotSystem] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[QuickSlotSystem] {message}");
        }
    }
}