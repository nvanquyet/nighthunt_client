using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Core.Bridge
{
    /// <summary>
    /// Unified interface implementation for all gameplay systems
    /// 
    /// RESPONSIBILITIES:
    /// - Single entry point for all gameplay operations
    /// - Coordinates between all gameplay systems
    /// - Re-publishes all system events for unified access
    /// - Provides convenient wrapper methods
    /// 
    /// ARCHITECTURE:
    /// - Plain C# class (NOT MonoBehaviour) - instantiated by NetworkPlayer
    /// - All systems communicate through interfaces only
    /// - Dependency Injection pattern - constructor receives all system interfaces
    /// - No direct component access
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Bridge does not handle network sync directly
    /// - All network operations delegated to individual systems
    /// - Events fire on both server and clients after sync
    /// 
    /// USAGE:
    /// var bridge = new GameplaySystemsBridge(inventory, equipment, weapon, quickSlot, statSystem, itemUse);
    /// bridge.AddItem("weapon_ak47", 1);
    /// </summary>
    public class GameplaySystemsBridge : IGameplayBridge, IDisposable
    {
        #region Private Fields
        
        private readonly IInventorySystem _inventory;
        private readonly IEquipmentSystem _equipment;
        private readonly IWeaponSystem _weapon;
        private readonly IQuickSlotSystem _quickSlot;
        private readonly IPlayerStatSystem _statSystem;
        private readonly IItemUseSystem _itemUse;
        
        #endregion
        
        #region IGameplayBridge - Properties
        
        public bool IsReady { get; private set; }
        
        public IInventorySystem Inventory => _inventory;
        public IEquipmentSystem Equipment => _equipment;
        public IWeaponSystem Weapon => _weapon;
        public IQuickSlotSystem QuickSlot => _quickSlot;
        public IPlayerStatSystem Stat => _statSystem;
        public IItemUseSystem ItemUse => _itemUse;
        
        #endregion
        
        #region Unified Events
        
        public event Action<ItemInstance> OnItemAdded;
        public event Action<ItemInstance, int> OnItemRemoved;
        public event Action<ItemInstance, ItemInstance> OnItemsSwapped;
        public event Action<ItemInstance, int, int> OnItemMoved;
        public event Action OnInventoryCleared;
        public event Action<int> OnInventorySlotCleared;
        public event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;
        public event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;
        public event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        public event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        public event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        public event Action<int, ItemInstance> OnQuickSlotAssigned;
        public event Action<int> OnQuickSlotRemoved;
        public event Action<int, ItemInstance> OnQuickSlotUsed;
        public event Action<PlayerStatType, float, float> OnStatChanged;
        public event Action<float, float> OnWeightChanged;
        public event Action<ItemInstance> OnItemUseStarted;
        public event Action<ItemInstance> OnItemUseCompleted;
        public event Action<ItemInstance> OnItemUseCancelled;
        public event Action<ItemInstance, float> OnItemUseProgress;
        
        #endregion
        
        #region Constructor (Dependency Injection)
        
        /// <summary>
        /// Create bridge with dependency injection
        /// 
        /// PARAMETERS:
        /// - inventory: Inventory system interface
        /// - equipment: Equipment system interface
        /// - weapon: Weapon system interface
        /// - quickSlot: Quick slot system interface
        /// - statSystem: Player stat system interface
        /// - itemUse: Item use system interface
        /// 
        /// NETWORK:
        /// - Call from NetworkPlayer after all systems initialized
        /// - All systems must be valid NetworkBehaviour components
        /// </summary>
        public GameplaySystemsBridge(
            IInventorySystem inventory,
            IEquipmentSystem equipment,
            IWeaponSystem weapon,
            IQuickSlotSystem quickSlot,
            IPlayerStatSystem statSystem,
            IItemUseSystem itemUse)
        {
            // Validate all dependencies
            if (inventory == null)
                Debug.LogError("[GameplaySystemsBridge] Inventory system is null!");
            if (equipment == null)
                Debug.LogError("[GameplaySystemsBridge] Equipment system is null!");
            if (weapon == null)
                Debug.LogError("[GameplaySystemsBridge] Weapon system is null!");
            if (quickSlot == null)
                Debug.LogError("[GameplaySystemsBridge] Quick slot system is null!");
            if (statSystem == null)
                Debug.LogError("[GameplaySystemsBridge] Stat system is null!");
            if (itemUse == null)
                Debug.LogWarning("[GameplaySystemsBridge] Item use system is null - item usage will not work!");
            
            // Store references
            _inventory = inventory;
            _equipment = equipment;
            _weapon = weapon;
            _quickSlot = quickSlot;
            _statSystem = statSystem;
            _itemUse = itemUse;
            
            // Wire up events
            WireEvents();
            
            IsReady = true;
            Debug.Log("[GameplaySystemsBridge] Initialized - all systems wired.");
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        /// <summary>
        /// Cleanup event subscriptions
        /// Call from NetworkPlayer.OnStopClient
        /// </summary>
        public void Dispose()
        {
            if (!IsReady)
                return;
            
            // Unsubscribe from all events
            UnwireEvents();
            
            IsReady = false;
            Debug.Log("[GameplaySystemsBridge] Disposed - all events unsubscribed.");
        }
        
        #endregion
        
        #region Event Wiring
        
        /// <summary>
        /// Wire up all system events to bridge events
        /// </summary>
        private void WireEvents()
        {
            // Inventory events
            if (_inventory != null)
            {
                _inventory.OnItemAdded += HandleItemAdded;
                _inventory.OnItemRemoved += HandleItemRemoved;
                _inventory.OnItemsSwapped += HandleItemsSwapped;
                _inventory.OnItemMoved += HandleItemMoved;
                _inventory.OnInventoryCleared += HandleInventoryCleared;
                _inventory.OnInventorySlotCleared += HandleInventorySlotCleared;
            }
            
            // Equipment events
            if (_equipment != null)
            {
                _equipment.OnItemEquipped += HandleItemEquipped;
                _equipment.OnItemUnequipped += HandleItemUnequipped;
            }
            
            // Weapon events
            if (_weapon != null)
            {
                _weapon.OnWeaponEquipped += HandleWeaponEquipped;
                _weapon.OnWeaponUnequipped += HandleWeaponUnequipped;
                _weapon.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            }
            
            // Quick slot events
            if (_quickSlot != null)
            {
                _quickSlot.OnQuickSlotAssigned += HandleQuickSlotAssigned;
                _quickSlot.OnQuickSlotRemoved += HandleQuickSlotRemoved;
                _quickSlot.OnQuickSlotUsed += HandleQuickSlotUsed;
            }
            
            // Stat system events
            if (_statSystem != null)
            {
                _statSystem.OnStatChanged += HandleStatChanged;
                _statSystem.OnWeightChanged += HandleWeightChanged;
            }
            
            // Item use events
            if (_itemUse != null)
            {
                _itemUse.OnItemUseStarted += HandleItemUseStarted;
                _itemUse.OnItemUseCompleted += HandleItemUseCompleted;
                _itemUse.OnItemUseCancelled += HandleItemUseCancelled;
                _itemUse.OnItemUseProgress += HandleItemUseProgress;
            }
        }
        
        /// <summary>
        /// Unsubscribe from all system events
        /// </summary>
        private void UnwireEvents()
        {
            // Inventory events
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= HandleItemAdded;
                _inventory.OnItemRemoved -= HandleItemRemoved;
                _inventory.OnItemsSwapped -= HandleItemsSwapped;
                _inventory.OnItemMoved -= HandleItemMoved;
                _inventory.OnInventoryCleared -= HandleInventoryCleared;
                _inventory.OnInventorySlotCleared -= HandleInventorySlotCleared;
            }
            
            // Equipment events
            if (_equipment != null)
            {
                _equipment.OnItemEquipped -= HandleItemEquipped;
                _equipment.OnItemUnequipped -= HandleItemUnequipped;
            }
            
            // Weapon events
            if (_weapon != null)
            {
                _weapon.OnWeaponEquipped -= HandleWeaponEquipped;
                _weapon.OnWeaponUnequipped -= HandleWeaponUnequipped;
                _weapon.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            }
            
            // Quick slot events
            if (_quickSlot != null)
            {
                _quickSlot.OnQuickSlotAssigned -= HandleQuickSlotAssigned;
                _quickSlot.OnQuickSlotRemoved -= HandleQuickSlotRemoved;
                _quickSlot.OnQuickSlotUsed -= HandleQuickSlotUsed;
            }
            
            // Stat system events
            if (_statSystem != null)
            {
                _statSystem.OnStatChanged -= HandleStatChanged;
                _statSystem.OnWeightChanged -= HandleWeightChanged;
            }
            
            // Item use events
            if (_itemUse != null)
            {
                _itemUse.OnItemUseStarted -= HandleItemUseStarted;
                _itemUse.OnItemUseCompleted -= HandleItemUseCompleted;
                _itemUse.OnItemUseCancelled -= HandleItemUseCancelled;
                _itemUse.OnItemUseProgress -= HandleItemUseProgress;
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleItemAdded(ItemInstance item) => OnItemAdded?.Invoke(item);
        private void HandleItemRemoved(ItemInstance item, int quantity) => OnItemRemoved?.Invoke(item, quantity);
        private void HandleItemsSwapped(ItemInstance item1, ItemInstance item2) => OnItemsSwapped?.Invoke(item1, item2);
        private void HandleItemMoved(ItemInstance item, int oldIndex, int newIndex) => OnItemMoved?.Invoke(item, oldIndex, newIndex);
        private void HandleInventoryCleared() => OnInventoryCleared?.Invoke();
        private void HandleInventorySlotCleared(int index) => OnInventorySlotCleared?.Invoke(index);
        private void HandleItemEquipped(EquipmentSlotType slot, ItemInstance item) => OnItemEquipped?.Invoke(slot, item);
        private void HandleItemUnequipped(EquipmentSlotType slot, ItemInstance item) => OnItemUnequipped?.Invoke(slot, item);
        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance weapon) => OnWeaponEquipped?.Invoke(slot, weapon);
        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance weapon) => OnWeaponUnequipped?.Invoke(slot, weapon);
        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot) => OnActiveWeaponChanged?.Invoke(oldSlot, newSlot);
        private void HandleQuickSlotAssigned(int slotIndex, ItemInstance item) => OnQuickSlotAssigned?.Invoke(slotIndex, item);
        private void HandleQuickSlotRemoved(int slotIndex) => OnQuickSlotRemoved?.Invoke(slotIndex);
        private void HandleQuickSlotUsed(int slotIndex, ItemInstance item) => OnQuickSlotUsed?.Invoke(slotIndex, item);
        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue) => OnStatChanged?.Invoke(type, oldValue, newValue);
        private void HandleWeightChanged(float current, float capacity) => OnWeightChanged?.Invoke(current, capacity);
        private void HandleItemUseStarted(ItemInstance item) => OnItemUseStarted?.Invoke(item);
        private void HandleItemUseCompleted(ItemInstance item) => OnItemUseCompleted?.Invoke(item);
        private void HandleItemUseCancelled(ItemInstance item) => OnItemUseCancelled?.Invoke(item);
        private void HandleItemUseProgress(ItemInstance item, float progress) => OnItemUseProgress?.Invoke(item, progress);
        
        #endregion
        
        #region IGameplayBridge - Inventory
        
        public void AddItem(string defID, int qty = 1)
        {
            if (!ValidateSystem(_inventory, "Inventory")) return;
            _inventory.AddItem(defID, qty);
        }

        public void RemoveItem(string instanceID, int qty = 1)
        {
            if (!ValidateSystem(_inventory, "Inventory")) return;
            _inventory.RemoveItem(instanceID, qty);
        }

        public void RemoveItemByDef(string defID, int qty = 1)
        {
            if (!ValidateSystem(_inventory, "Inventory")) return;
            _inventory.RemoveItemByDefinition(defID, qty);
        }

        public void SwapItems(string id1, string id2)
        {
            if (!ValidateSystem(_inventory, "Inventory")) return;
            _inventory.SwapItems(id1, id2);
        }

        public void DropItem(string instanceID, int qty = 1)
        {
            if (!ValidateSystem(_inventory, "Inventory")) return;
            _inventory.DropItem(instanceID, qty);
        }

        public void ClearInventory()
        {
            if (!ValidateSystem(_inventory, "Inventory")) return;
            _inventory.ClearInventory();
        }

        public IReadOnlyList<ItemInstance> GetAllItems()
            => _inventory?.GetAllItems() ?? new List<ItemInstance>();

        public ItemInstance GetItemByInstanceID(string id)
            => _inventory?.GetItemByInstanceID(id);

        public List<ItemInstance> GetItemsByDef(string defID)
            => _inventory?.GetItemsByDefinition(defID) ?? new List<ItemInstance>();

        #endregion

        #region IGameplayBridge - Equipment
        
        public void EquipItem(string instanceID)
        {
            if (!ValidateSystem(_equipment, "Equipment")) return;
            _equipment.EquipItem(instanceID);
        }

        public void UnequipItem(EquipmentSlotType slot)
        {
            if (!ValidateSystem(_equipment, "Equipment")) return;
            if (!_equipment.IsSlotOccupied(slot))
            {
                Debug.Log($"[GameplaySystemsBridge] UnequipItem: slot {slot} is already empty");
                return;
            }
            _equipment.UnequipItem(slot);
        }

        public void UnequipAll()
        {
            if (!ValidateSystem(_equipment, "Equipment")) return;
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                if (_equipment.IsSlotOccupied(slot))
                    _equipment.UnequipItem(slot);
            }
        }

        public void AddAndEquip(string defID)
        {
            AddItem(defID);
            var list = GetItemsByDef(defID);
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[GameplaySystemsBridge] AddAndEquip: {defID} not found in inventory after add");
                return;
            }
            EquipItem(list[^1].InstanceID);
        }

        public Dictionary<EquipmentSlotType, ItemInstance> GetAllEquipped()
            => _equipment?.GetAllEquippedItems() ?? new Dictionary<EquipmentSlotType, ItemInstance>();

        #endregion

        #region IGameplayBridge - Weapon
        
        public void EquipWeapon(string instanceID)
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            _weapon.EquipWeapon(instanceID);
        }

        /// <inheritdoc/>
        public void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot)
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            _weapon.EquipWeaponToSlot(instanceID, targetSlot);
        }

        public void AddAndEquipWeapon(string defID, WeaponSlotType slot)
        {
            AddItem(defID);
            var list = GetItemsByDef(defID);
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[GameplaySystemsBridge] AddAndEquipWeapon: {defID} not found");
                return;
            }
            EquipWeapon(list[^1].InstanceID);
        }

        public void UnequipWeapon(WeaponSlotType slot)
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            if (!_weapon.IsSlotOccupied(slot))
            {
                Debug.Log($"[GameplaySystemsBridge] UnequipWeapon: slot {slot} is empty");
                return;
            }
            _weapon.UnequipWeapon(slot);
        }

        public void SwapWeapons(WeaponSlotType slot1, WeaponSlotType slot2)
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            _weapon.SwapWeapons(slot1, slot2);
        }

        public void SelectWeapon(WeaponSlotType slot)
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            if (!_weapon.IsSlotOccupied(slot))
            {
                Debug.LogWarning($"[GameplaySystemsBridge] SelectWeapon: no weapon in slot {slot}");
                return;
            }
            _weapon.SelectWeapon(slot);
        }

        public void HolsterWeapon()
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            _weapon.HolsterWeapon();
        }

        public void Reload(WeaponSlotType slot)
        {
            if (!ValidateSystem(_weapon, "Weapon")) return;
            if (!_weapon.IsSlotOccupied(slot))
            {
                Debug.LogWarning($"[GameplaySystemsBridge] Reload: no weapon in slot {slot}");
                return;
            }
            _weapon.Reload(slot);
        }

        public Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons()
            => _weapon?.GetAllWeapons() ?? new Dictionary<WeaponSlotType, ItemInstance>();

        public WeaponSlotType? GetActiveSlot()
            => _weapon?.GetActiveWeaponSlot();

        #endregion

        #region IGameplayBridge - QuickSlot
        
        public void AssignToQuickSlot(string instanceID, int slotIndex)
        {
            if (!ValidateSystem(_quickSlot, "QuickSlot")) return;
            _quickSlot.AssignToQuickSlot(instanceID, slotIndex);
        }

        public void AddAndAssignQuickSlot(string defID, int slotIndex)
        {
            AddItem(defID);
            var list = GetItemsByDef(defID);
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[GameplaySystemsBridge] AddAndAssignQuickSlot: {defID} not in inventory");
                return;
            }
            AssignToQuickSlot(list[^1].InstanceID, slotIndex);
        }

        public void RemoveFromQuickSlot(int slotIndex)
        {
            if (!ValidateSystem(_quickSlot, "QuickSlot")) return;
            if (!_quickSlot.IsSlotOccupied(slotIndex))
            {
                Debug.Log($"[GameplaySystemsBridge] RemoveFromQuickSlot: QS[{slotIndex}] already empty");
                return;
            }
            _quickSlot.RemoveFromQuickSlot(slotIndex);
        }

        public void SwapQuickSlots(int slotIndex1, int slotIndex2)
        {
            if (!ValidateSystem(_quickSlot, "QuickSlot")) return;
            _quickSlot.SwapQuickSlots(slotIndex1, slotIndex2);
        }

        public void UseQuickSlot(int slotIndex)
        {
            if (!ValidateSystem(_quickSlot, "QuickSlot")) return;
            if (!_quickSlot.CanUseQuickSlot(slotIndex))
            {
                Debug.LogWarning($"[GameplaySystemsBridge] UseQuickSlot: QS[{slotIndex}] cannot be used (empty or not usable)");
                return;
            }
            _quickSlot.UseQuickSlot(slotIndex);
        }

        public void CancelItemUse()
        {
            if (_itemUse != null)
                _itemUse.CancelUse();
        }

        public void ExecuteThrow()
        {
            if (_itemUse != null)
                _itemUse.ExecuteThrow();
        }

        public ItemInstance[] GetAllQuickSlots()
            => _quickSlot?.GetAllQuickSlots() ?? Array.Empty<ItemInstance>();

        #endregion

        #region IGameplayBridge - Stats
        
        public float GetStat(PlayerStatType type)
            => _statSystem?.GetStat(type) ?? 0f;

        public float GetBaseStat(PlayerStatType type)
            => _statSystem?.GetBaseStat(type) ?? 0f;

        public float GetStatModifier(PlayerStatType type)
            => _statSystem?.GetStatModifier(type) ?? 0f;

        public Dictionary<PlayerStatType, float> GetAllStats()
            => _statSystem?.GetAllStats() ?? new Dictionary<PlayerStatType, float>();

        public float GetCurrentWeight()
            => _statSystem?.GetCurrentWeight() ?? 0f;

        public float GetWeightCapacity()
            => _statSystem?.GetWeightCapacity() ?? 0f;

        public float GetWeightPercent()
            => _statSystem?.GetWeightPercent() ?? 0f;

        public float GetMovementSpeedMultiplier()
            => _statSystem?.GetMovementSpeedMultiplier() ?? 1f;

        #endregion

        #region IGameplayBridge - Scenarios
        
        public void ScenarioFullLoadout()
        {
            ClearInventory();
            AddItem("weapon_ak47", 1);
            var weps = GetItemsByDef("weapon_ak47");
            if (weps?.Count > 0) EquipWeapon(weps[0].InstanceID);
            AddAndEquip("armor_vest");
            AddAndEquip("armor_helmet");
            AddAndEquip("armor_backpack");
            AddItem("consumable_medkit", 5);
            var meds = GetItemsByDef("consumable_medkit");
            if (meds?.Count > 0) AssignToQuickSlot(meds[0].InstanceID, 0);
            AddItem("attachment_reddot", 1);
            Debug.Log("<color=green>[GameplaySystemsBridge] Full loadout applied!</color>");
        }

        public void ScenarioOverweight()
        {
            ClearInventory();
            for (int i = 0; i < 20; i++) AddItem("weapon_ak47", 1);
            float current = GetCurrentWeight();
            float capacity = GetWeightCapacity();
            float percent = GetWeightPercent();
            Debug.Log($"<color=yellow>[GameplaySystemsBridge] Overweight: {current:F1}/{capacity:F1} ({percent:P0})</color>");
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Validate system is available
        /// </summary>
        private bool ValidateSystem(object system, string systemName)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[GameplaySystemsBridge] Bridge not ready ({systemName})");
                return false;
            }
            
            if (system == null)
            {
                Debug.LogWarning($"[GameplaySystemsBridge] {systemName} system is null");
                return false;
            }
            
            return true;
        }
        
        #endregion
    }
}
