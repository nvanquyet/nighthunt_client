using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Core.Bridge
{
    /// <summary>
    /// Unified interface for all gameplay systems
    /// 
    /// RESPONSIBILITIES:
    /// - Single entry point for all gameplay operations
    /// - Re-publishes all system events for unified access
    /// - Provides convenient wrapper methods for common operations
    /// 
    /// ARCHITECTURE:
    /// - All systems communicate through interfaces only
    /// - Bridge implements this interface and coordinates systems
    /// - Used by UI, Console, AI, and other external systems
    /// </summary>
    public interface IGameplayBridge
    {
        /// <summary>
        /// Whether bridge is ready and all systems initialized
        /// </summary>
        bool IsReady { get; }

        #region System Accessors
        
        /// <summary>
        /// Direct access to inventory system (read-only)
        /// Use for advanced operations not covered by wrapper methods
        /// </summary>
        IInventorySystem Inventory { get; }
        
        /// <summary>
        /// Direct access to equipment system (read-only)
        /// </summary>
        IEquipmentSystem Equipment { get; }
        
        /// <summary>
        /// Direct access to weapon system (read-only)
        /// </summary>
        IWeaponSystem Weapon { get; }
        
        /// <summary>
        /// Direct access to item selection system (read-only)
        /// </summary>
        IItemSelectionSystem ItemSelection { get; }

        /// <summary>
        /// Direct access to stat system (read-only)
        /// </summary>
        IPlayerStatSystem Stat { get; }
        
        /// <summary>
        /// Direct access to item use system (read-only)
        /// </summary>
        IItemUseSystem ItemUse { get; }

        /// <summary>
        /// Direct access to attachment system (read-only)
        /// </summary>
        IAttachmentSystem Attachment { get; }
        
        #endregion

        #region Inventory Operations
        
        void AddItem(string defID, int qty = 1);
        void RemoveItem(string instanceID, int qty = 1);
        void RemoveItemByDef(string defID, int qty = 1);
        void SwapItems(string id1, string id2);
        void DropItem(string instanceID, int qty = 1);
        void ClearInventory();
        IReadOnlyList<ItemInstance> GetAllItems();
        ItemInstance GetItemByInstanceID(string id);
        List<ItemInstance> GetItemsByDef(string defID);
        
        #endregion

        #region Equipment Operations
        
        void EquipItem(string instanceID);
        void UnequipItem(EquipmentSlotType slot);
        void UnequipAll();
        void AddAndEquip(string defID);
        Dictionary<EquipmentSlotType, ItemInstance> GetAllEquipped();
        
        #endregion

        #region Weapon Operations
        
        void EquipWeapon(string instanceID);
        /// <summary>Equip weapon from inventory into a specific slot (used by drag-and-drop).</summary>
        void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot);
        void AddAndEquipWeapon(string defID, WeaponSlotType slot);
        void UnequipWeapon(WeaponSlotType slot);
        void SwapWeapons(WeaponSlotType slot1, WeaponSlotType slot2);
        void SelectWeapon(WeaponSlotType slot);
        void HolsterWeapon();
        void Reload(WeaponSlotType slot);
        Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons();
        WeaponSlotType? GetActiveSlot();
        
        #endregion

        #region ItemSelection Operations

        void SelectItem(string instanceID);
        void DeselectItem();
        void CancelItemUse();
        void ExecuteThrow(Vector3 aimTarget);

        #endregion

        #region Stat Operations
        
        float GetStat(PlayerStatType type);
        float GetBaseStat(PlayerStatType type);
        float GetStatModifier(PlayerStatType type);
        Dictionary<PlayerStatType, float> GetAllStats();
        float GetCurrentWeight();
        float GetWeightCapacity();
        float GetWeightPercent();
        float GetMovementSpeedMultiplier();
        
        #endregion

        #region Scenarios (Debug/Testing)
        
        void ScenarioFullLoadout();
        void ScenarioOverweight();
        
        #endregion

        #region Unified Events
        
        /// <summary>
        /// All system events re-published here for unified access
        /// </summary>
        event Action<ItemInstance> OnItemAdded;
        event Action<ItemInstance, int> OnItemRemoved;
        event Action<ItemInstance, ItemInstance> OnItemsSwapped;
        event Action<ItemInstance, int, int> OnItemMoved;
        event Action OnInventoryCleared;
        /// <summary>Fired when a specific inventory slot is cleared because the item was equipped/attached.</summary>
        event Action<int> OnInventorySlotCleared;
        event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;
        event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;
        event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        event Action<ItemInstance> OnItemSelected;
        event Action OnItemDeselected;
        event Action<PlayerStatType, float, float> OnStatChanged;
        event Action<float, float> OnWeightChanged;
        event Action<ItemInstance> OnItemUseStarted;
        event Action<ItemInstance> OnItemUseCompleted;
        event Action<ItemInstance> OnItemUseCancelled;
        event Action<ItemInstance, float> OnItemUseProgress;
        event Action<string, int, ItemInstance> OnAttachmentAttached;
        event Action<string, int, ItemInstance> OnAttachmentDetached;
        
        #endregion
    }
}
