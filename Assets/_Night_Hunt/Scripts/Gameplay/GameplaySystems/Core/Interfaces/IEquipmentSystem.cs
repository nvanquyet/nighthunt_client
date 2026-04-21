using System;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Contract for the server-authoritative equipment management system.
    ///
    /// RESPONSIBILITIES:
    ///   - Manages body-slot equipment (head, chest, back, legs …).
    ///   - Applies stat modifiers from equipped items via StatApplyOrchestrator.
    ///   - Handles slot assignment, unequip-to-inventory, and direct drops.
    ///
    /// NETWORK ARCHITECTURE:
    ///   - Server-authoritative: all mutations happen on the server.
    ///   - Clients receive updates via SyncDictionary.
    ///   - Owning client may send requests via [ServerRpc(RequireOwnership = true)].
    /// </summary>
    public interface IEquipmentSystem
    {
        #region Getters

        /// <summary>Returns the item equipped in the given slot, or null if empty.</summary>
        ItemInstance GetEquippedItem(EquipmentSlotType slotType);

        /// <summary>Returns all equipped items as a slot → instance dictionary.</summary>
        Dictionary<EquipmentSlotType, ItemInstance> GetAllEquippedItems();

        /// <summary>Returns true when the slot contains an item.</summary>
        bool IsSlotOccupied(EquipmentSlotType slotType);

        /// <summary>
        /// Returns true when the item with <paramref name="itemDefinitionID"/> can be
        /// placed into <paramref name="slotType"/> (type-check + slot compatibility).
        /// </summary>
        bool CanEquipInSlot(string itemDefinitionID, EquipmentSlotType slotType);

        #endregion

        #region Equip / Unequip / Drop

        /// <summary>
        /// Equip the item instance from inventory into its designated slot.
        /// If the slot is already occupied the current item is unequipped first.
        /// Owning client routes to the server automatically.
        /// </summary>
        void EquipItem(string instanceID);

        /// <summary>
        /// Unequip the item from <paramref name="slotType"/> and return it to inventory.
        /// Attachment handling (detach / keep) follows InventoryConfig.
        /// Owning client routes to the server automatically.
        /// </summary>
        void UnequipItem(EquipmentSlotType slotType);

        /// <summary>
        /// Swap the items occupying two equipment slots.
        /// If only one slot is filled the item moves to the other slot.
        /// Server-only (no client request path needed for UI drag-and-drop in current design).
        /// </summary>
        void SwapEquipment(EquipmentSlotType slot1, EquipmentSlotType slot2);

        /// <summary>
        /// Unequip the item from <paramref name="slotType"/>, detach all its attachments
        /// (returning them to inventory per config), then drop the item to the world.
        /// Owning client routes to the server automatically.
        /// </summary>
        void DropEquippedItem(EquipmentSlotType slotType);

        #endregion

        #region Events

        /// <summary>Fired when an item is placed into an equipment slot. Parameters: (slot, item).</summary>
        event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;

        /// <summary>Fired when an item is removed from an equipment slot. Parameters: (slot, item).</summary>
        event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;

        #endregion
    }
}
