using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using System.Collections.Generic;
using System.Linq;
using _Night_Hunt.Scripts.InventorySystems.Core.Interfaces;
using NightHunt.Inventory.Stats;

namespace NightHunt.Inventory.Systems
{
    /// <summary>
    /// Equipment system managing helmet, armor, backpack, boots, and future slots (rings, trinkets).
    /// Implements IEquipmentSystem interface.
    /// Applies character stat modifiers when equipped.
    /// </summary>
    public class EquipmentSystem : MonoBehaviour, IEquipmentSystem
    {
        [Header("Configuration")]
        [SerializeField] private InventoryConfig config;
        [SerializeField] private SlotLayoutConfig slotLayout;
        
        [Header("References")]
        [SerializeField] private CharacterStats characterStats;
        [SerializeField] private InventorySystem inventorySystem; // Injected by PlayerInventoryController
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // Equipment slots: SlotType → ItemInstance
        private Dictionary<EquipmentSlotType, ItemInstance> equipmentSlots = new Dictionary<EquipmentSlotType, ItemInstance>();
        
        // === Lifecycle ===
        
        void Awake()
        {
            Initialize();
        }
        
        void Initialize()
        {
            if (config == null || slotLayout == null)
            {
                LogError("InventoryConfig or SlotLayoutConfig not assigned!");
                return;
            }
            
            // Initialize slots from config
            foreach (var slotDef in slotLayout.EquipmentSlots)
            {
                equipmentSlots[slotDef.SlotType] = null;
            }
            
            Log($"Initialized equipment system with {equipmentSlots.Count} slots");
        }
        
        // === Dependency Injection ===
        
        public void SetInventorySystem(InventorySystem inventory)
        {
            inventorySystem = inventory;
        }
        
        public void SetCharacterStats(CharacterStats stats)
        {
            characterStats = stats;
        }
        
        // === IEquipmentSystem Implementation ===
        
        #region Query
        
        public ItemInstance GetEquippedItem(EquipmentSlotType slotType)
        {
            return equipmentSlots.ContainsKey(slotType) ? equipmentSlots[slotType] : null;
        }
        
        public bool IsSlotEquipped(EquipmentSlotType slotType)
        {
            return GetEquippedItem(slotType) != null;
        }
        
        public bool CanEquip(ItemInstance item, EquipmentSlotType slotType)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Check if item can be equipped in this slot
            if (!item.Definition.CanEquipInSlot(slotType))
                return false;
            
            // Check if slot exists in current config
            if (!equipmentSlots.ContainsKey(slotType))
                return false;
            
            // Check requirements (level, achievements, etc.)
            // TODO: Implement requirement checking
            
            return true;
        }
        
        public EquipmentSlotType[] GetAllSlotTypes()
        {
            return equipmentSlots.Keys.ToArray();
        }
        
        #endregion
        
        #region Equip/Unequip
        
        public OperationResult EquipItem(ItemInstance item, EquipmentSlotType slotType)
        {
            // Validate
            if (!CanEquip(item, slotType))
            {
                EquipmentEvents.InvokeEquipFailed(OperationResult.InvalidItemType, slotType, "Item cannot be equipped in this slot");
                return OperationResult.InvalidItemType;
            }
            
            // If slot occupied, unequip first
            if (IsSlotEquipped(slotType))
            {
                var swapResult = SwapEquipment(item, slotType, out ItemInstance oldItem);
                return swapResult;
            }
            
            // Equip
            equipmentSlots[slotType] = item;
            item.IsEquipped = true;
            item.EquippedLocation = SlotLocationType.Equipment;
            
            // Apply stat modifiers
            ApplyEquipmentModifiers(item);
            
            // Check if this is a backpack → expand inventory
            if (slotType == EquipmentSlotType.Backpack && inventorySystem != null)
            {
                // TODO: Get expansion amount from item definition
                int expansionAmount = 10; // Example: +10 slots
                inventorySystem.ExpandInventory(expansionAmount);
            }
            
            // Fire event
            EquipmentEvents.InvokeItemEquipped(item, slotType);
            
            Log($"Equipped {item.Definition.DisplayName} in {slotType} slot");
            return OperationResult.Success;
        }
        
        public OperationResult UnequipItem(EquipmentSlotType slotType, out ItemInstance unequippedItem)
        {
            unequippedItem = null;
            
            // Check if equipped
            if (!IsSlotEquipped(slotType))
            {
                EquipmentEvents.InvokeEquipFailed(OperationResult.NotEquipped, slotType, "Slot is empty");
                return OperationResult.NotEquipped;
            }
            
            unequippedItem = equipmentSlots[slotType];
            
            // Remove stat modifiers
            RemoveEquipmentModifiers(unequippedItem);
            
            // Clear slot
            equipmentSlots[slotType] = null;
            unequippedItem.IsEquipped = false;
            unequippedItem.EquippedLocation = SlotLocationType.Inventory;
            
            // If backpack, reduce inventory size
            if (slotType == EquipmentSlotType.Backpack && inventorySystem != null)
            {
                // TODO: Handle inventory shrinking (move overflow items?)
            }
            
            // Fire event
            EquipmentEvents.InvokeItemUnequipped(unequippedItem, slotType);
            
            Log($"Unequipped {unequippedItem.Definition.DisplayName} from {slotType} slot");
            return OperationResult.Success;
        }
        
        public OperationResult SwapEquipment(ItemInstance newItem, EquipmentSlotType slotType, out ItemInstance oldItem)
        {
            oldItem = null;
            
            // Validate new item
            if (!CanEquip(newItem, slotType))
            {
                EquipmentEvents.InvokeEquipFailed(OperationResult.InvalidItemType, slotType, "New item cannot be equipped");
                return OperationResult.InvalidItemType;
            }
            
            // Get old item
            oldItem = equipmentSlots[slotType];
            
            // If slot empty, just equip
            if (oldItem == null)
            {
                return EquipItem(newItem, slotType);
            }
            
            // Remove old modifiers
            RemoveEquipmentModifiers(oldItem);
            
            // Swap
            equipmentSlots[slotType] = newItem;
            newItem.IsEquipped = true;
            newItem.EquippedLocation = SlotLocationType.Equipment;
            oldItem.IsEquipped = false;
            oldItem.EquippedLocation = SlotLocationType.Inventory;
            
            // Apply new modifiers
            ApplyEquipmentModifiers(newItem);
            
            // Fire event
            EquipmentEvents.InvokeEquipmentSwapped(oldItem, newItem, slotType);
            
            Log($"Swapped {oldItem.Definition.DisplayName} with {newItem.Definition.DisplayName} in {slotType} slot");
            return OperationResult.Success;
        }
        
        public void UnequipAll()
        {
            var slotTypes = equipmentSlots.Keys.ToArray();
            
            foreach (var slotType in slotTypes)
            {
                if (IsSlotEquipped(slotType))
                {
                    UnequipItem(slotType, out _);
                }
            }
            
            EquipmentEvents.InvokeAllEquipmentCleared();
            
            Log("Unequipped all equipment");
        }
        
        #endregion
        
        // === Stat Modifier Management ===
        
        /// <summary>
        /// Apply character stat modifiers from equipped item and its attachments.
        /// Example: Helmet + Flashlight → VisionRadius increased
        /// </summary>
        private void ApplyEquipmentModifiers(ItemInstance item)
        {
            if (characterStats == null)
            {
                LogError("CharacterStats not assigned!");
                return;
            }
            
            // Get all modifiers from item and its attachments
            var modifiers = item.GetStatModifiers()
                .Where(m => m.Target == StatModifierTarget.Character)
                .ToList();
            
            string sourceId = item.GetModifierSourceId();
            
            foreach (var mod in modifiers)
            {
                CharacterStatsEvents.InvokeAddModifier(
                    mod.CharacterStat,
                    mod.CalculationType,
                    mod.Value,
                    sourceId
                );
                
                Log($"Applied modifier: {mod.CharacterStat} {mod.CalculationType} {mod.Value:F2} from {sourceId}");
            }
        }
        
        /// <summary>
        /// Remove character stat modifiers when item is unequipped.
        /// </summary>
        private void RemoveEquipmentModifiers(ItemInstance item)
        {
            if (characterStats == null)
                return;
            
            string sourceId = item.GetModifierSourceId();
            
            CharacterStatsEvents.InvokeRemoveModifier(sourceId);
            
            Log($"Removed modifiers from {sourceId}");
        }
        
        // === Public API - Additional ===
        
        /// <summary>
        /// Get all equipped items.
        /// </summary>
        public List<ItemInstance> GetAllEquippedItems()
        {
            return equipmentSlots.Values.Where(item => item != null).ToList();
        }
        
        /// <summary>
        /// Get total weight of equipped items.
        /// </summary>
        public float GetEquippedWeight()
        {
            float total = 0f;
            foreach (var item in equipmentSlots.Values)
            {
                if (item != null)
                {
                    total += item.GetTotalWeight();
                }
            }
            return total;
        }
        
        /// <summary>
        /// Check if specific equipment slot type exists in config.
        /// </summary>
        public bool HasSlot(EquipmentSlotType slotType)
        {
            return equipmentSlots.ContainsKey(slotType);
        }
        
        /// <summary>
        /// Handle durability decrease for equipped items.
        /// </summary>
        public void DamageEquippedItem(EquipmentSlotType slotType, float damage)
        {
            var item = GetEquippedItem(slotType);
            if (item == null)
                return;
            
            item.DecreaseDurability(damage);
            
            EquipmentEvents.InvokeEquipmentDurabilityChanged(item, slotType, item.CurrentDurability);
            
            // Auto-unequip if broken
            if (config.ItemsBreakAtZeroDurability && item.IsBroken())
            {
                Log($"{item.Definition.DisplayName} broke and was unequipped");
                UnequipItem(slotType, out _);
            }
        }
        
        // === Serialization Support ===
        
        /// <summary>
        /// Get equipment state for saving/network sync.
        /// </summary>
        public Dictionary<EquipmentSlotType, ItemInstanceData> SerializeEquipment()
        {
            var serialized = new Dictionary<EquipmentSlotType, ItemInstanceData>();
            
            foreach (var kvp in equipmentSlots)
            {
                if (kvp.Value != null)
                {
                    serialized[kvp.Key] = kvp.Value.Serialize();
                }
            }
            
            return serialized;
        }
        
        /// <summary>
        /// Load equipment state from save/network sync.
        /// </summary>
        public void DeserializeEquipment(Dictionary<EquipmentSlotType, ItemInstanceData> data)
        {
            // Clear current equipment
            UnequipAll();
            
            // Load each item
            foreach (var kvp in data)
            {
                // TODO: Resolve ItemDefinition from ItemId
                // var definition = ItemDefinitionDatabase.GetDefinition(kvp.Value.ItemId);
                // var item = ItemInstance.Deserialize(kvp.Value, definition);
                // EquipItem(item, kvp.Key);
            }
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[EquipmentSystem] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[EquipmentSystem] {message}");
        }
    }
}