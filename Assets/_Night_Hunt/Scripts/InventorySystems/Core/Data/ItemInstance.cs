using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Interfaces;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// Runtime instance of an item.
    /// Contains dynamic state that can change during gameplay.
    /// Replicated over network.
    /// REFACTORED: Implements IStatModifierProvider for stat application.
    /// </summary>
    [Serializable]
    public class ItemInstance : IStatModifierProvider
    {
        [Header("Identity")]
        [Tooltip("Server-generated unique ID for this instance")]
        public string InstanceId;

        [Tooltip("Reference to the static item definition")]
        public ItemDefinition Definition;

        [Header("Runtime State")]
        [Tooltip("Current stack count (1 for non-stackable)")]
        public int StackSize = 1;

        [Tooltip("Current durability (0-100%)")]
        [Range(0f, 100f)]
        public float CurrentDurability;

        [Tooltip("Current ammo count for weapons")]
        public int CurrentAmmo;

        [Tooltip("Is this item currently equipped?")]
        public bool IsEquipped;

        [Tooltip("Where is this item equipped? (if IsEquipped)")]
        public SlotLocationType EquippedLocation;

        [Header("Attachments")]
        [Tooltip("Items attached to this item")]
        public List<ItemInstance> AttachedItems;

        // === Constructors ===

        public ItemInstance()
        {
            AttachedItems = new List<ItemInstance>();
        }

        public ItemInstance(ItemDefinition definition, string instanceId)
        {
            Definition = definition;
            InstanceId = instanceId;
            StackSize = 1;
            CurrentDurability = definition.MaxDurability;
            CurrentAmmo = 0;
            IsEquipped = false;
            AttachedItems = new List<ItemInstance>();
        }

        // === IStatModifierProvider Implementation ===

        /// <summary>
        /// Get all stat modifiers from this item AND its attachments.
        /// Example: Helmet + Flashlight attachment = both provide modifiers
        /// </summary>
        public List<StatModifierData> GetStatModifiers()
        {
            var modifiers = new List<StatModifierData>();

            // Add this item's modifiers
            if (Definition != null && Definition.StatModifiers != null)
            {
                modifiers.AddRange(Definition.GetStatModifiersData());
            }

            // Add attachment modifiers
            foreach (var attachment in AttachedItems)
            {
                if (attachment?.Definition?.StatModifiers != null)
                {
                    modifiers.AddRange(attachment.Definition.GetStatModifiersData());
                }
            }

            return modifiers;
        }

        /// <summary>
        /// Get unique source ID for tracking modifiers.
        /// Format: "Equip:{instanceId}" or "Attach:{instanceId}"
        /// </summary>
        public string GetModifierSourceId()
        {
            return IsEquipped ? $"Equip:{InstanceId}" : $"Attach:{InstanceId}";
        }

        // === Serialization ===

        /// <summary>
        /// Serializes this item instance for network sync and world drop persistence.
        /// </summary>
        public ItemInstanceData Serialize()
        {
            return new ItemInstanceData
            {
                InstanceId = this.InstanceId,
                ItemId = this.Definition.ItemId,
                StackSize = this.StackSize,
                CurrentDurability = this.CurrentDurability,
                CurrentAmmo = this.CurrentAmmo,
                IsEquipped = this.IsEquipped,
                EquippedLocation = this.EquippedLocation,
                AttachedItemIds = this.AttachedItems.Select(a => a.InstanceId).ToArray()
            };
        }

        /// <summary>
        /// Deserializes item instance data.
        /// Note: AttachedItems must be populated separately via network sync.
        /// </summary>
        public static ItemInstance Deserialize(ItemInstanceData data, ItemDefinition definition)
        {
            return new ItemInstance
            {
                InstanceId = data.InstanceId,
                Definition = definition,
                StackSize = data.StackSize,
                CurrentDurability = data.CurrentDurability,
                CurrentAmmo = data.CurrentAmmo,
                IsEquipped = data.IsEquipped,
                EquippedLocation = data.EquippedLocation,
                AttachedItems = new List<ItemInstance>() // Populated separately
            };
        }

        // === Weight Calculation ===

        /// <summary>
        /// Gets the total weight of this item including attachments.
        /// </summary>
        public float GetTotalWeight()
        {
            if (Definition == null)
            {
                Debug.LogError($"[ItemInstance] GetTotalWeight: Definition is null for InstanceId: {InstanceId}");
                return 0f;
            }

            float weight = Definition.Weight * StackSize;

            foreach (var attachment in AttachedItems)
            {
                if (attachment?.Definition != null)
                {
                    weight += attachment.Definition.Weight;
                }
            }

            return weight;
        }

        // === Attachment System ===

        /// <summary>
        /// Checks if this item can accept the given attachment.
        /// </summary>
        public bool CanAcceptAttachment(ItemInstance attachment)
        {
            if (attachment == null || attachment.Definition == null)
                return false;

            // Check if attachment type is in allowed slots
            if (!Definition.AttachmentSlots.Contains(attachment.Definition.AttachmentType))
                return false;

            // Check if slot is already occupied
            var existingAttachment = AttachedItems.Find(a =>
                a.Definition.AttachmentType == attachment.Definition.AttachmentType);

            return existingAttachment == null;
        }

        /// <summary>
        /// Get attachment in specific slot (null if none).
        /// </summary>
        public ItemInstance GetAttachment(AttachmentSlotType slotType)
        {
            return AttachedItems.Find(a => a.Definition.AttachmentType == slotType);
        }

        /// <summary>
        /// Check if this item has an attachment in the given slot.
        /// </summary>
        public bool HasAttachment(AttachmentSlotType slotType)
        {
            return GetAttachment(slotType) != null;
        }

        /// <summary>
        /// Add attachment to this item.
        /// </summary>
        public bool AddAttachment(ItemInstance attachment)
        {
            if (!CanAcceptAttachment(attachment))
                return false;

            AttachedItems.Add(attachment);
            attachment.IsEquipped = true;
            attachment.EquippedLocation = SlotLocationType.Attachment;
            return true;
        }

        /// <summary>
        /// Remove attachment from this item.
        /// </summary>
        public bool RemoveAttachment(AttachmentSlotType slotType, out ItemInstance removedAttachment)
        {
            var attachment = GetAttachment(slotType);
            if (attachment == null)
            {
                removedAttachment = null;
                return false;
            }

            AttachedItems.Remove(attachment);
            attachment.IsEquipped = false;
            attachment.EquippedLocation = SlotLocationType.Inventory;
            removedAttachment = attachment;
            return true;
        }

        // === Durability ===

        /// <summary>
        /// Reduce durability by specified amount.
        /// </summary>
        public void DecreaseDurability(float amount)
        {
            CurrentDurability = Mathf.Max(0f, CurrentDurability - amount);
        }

        /// <summary>
        /// Check if item is broken (durability == 0).
        /// </summary>
        public bool IsBroken()
        {
            return CurrentDurability <= 0f;
        }

        /// <summary>
        /// Repair item to full durability.
        /// </summary>
        public void Repair()
        {
            CurrentDurability = Definition.MaxDurability;
        }

        // === Utility ===

        /// <summary>
        /// Clone this item instance (new InstanceId).
        /// Used for stack splitting.
        /// </summary>
        public ItemInstance Clone(string newInstanceId)
        {
            return new ItemInstance
            {
                InstanceId = newInstanceId,
                Definition = this.Definition,
                StackSize = this.StackSize,
                CurrentDurability = this.CurrentDurability,
                CurrentAmmo = this.CurrentAmmo,
                IsEquipped = false,
                AttachedItems = new List<ItemInstance>() // Attachments not cloned
            };
        }
    }

    /// <summary>
    /// Serializable data structure for network transmission and persistence.
    /// </summary>
    [Serializable]
    public struct ItemInstanceData
    {
        public string InstanceId;
        public string ItemId;
        public int StackSize;
        public float CurrentDurability;
        public int CurrentAmmo;
        public bool IsEquipped;
        public SlotLocationType EquippedLocation;
        public string[] AttachedItemIds;
    }
}