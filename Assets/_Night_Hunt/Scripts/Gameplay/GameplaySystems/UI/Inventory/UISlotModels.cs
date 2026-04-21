using System;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    public enum UISlotType
    {
        Inventory,
        Equipment,
        Weapon,
        /// <summary>
        /// Attachment sub-slot — always a child of a Weapon or Equipment ItemSlotView.
        /// <see cref="UISlotId.ParentInstanceID"/> identifies the parent item.
        /// </summary>
        Attachment,
    }

    /// <summary>
    /// Unique identifier for a UI slot (inventory cell, equipment slot, etc.).
    /// This is what we pass around in drag & drop events.
    /// </summary>
    [Serializable]
    public struct UISlotId : IEquatable<UISlotId>
    {
        public UISlotType Type;
        public int Index;
        public EquipmentSlotType? EquipmentSlot;
        public WeaponSlotType? WeaponSlot;
        public string ParentInstanceID; // Cho attachment slots

        public static UISlotId Inventory(int index) => new UISlotId
        {
            Type = UISlotType.Inventory,
            Index = index
        };

        public static UISlotId Equipment(EquipmentSlotType slot) => new UISlotId
        {
            Type = UISlotType.Equipment,
            Index = -1,
            EquipmentSlot = slot
        };

        public static UISlotId Weapon(WeaponSlotType slot) => new UISlotId
        {
            Type = UISlotType.Weapon,
            Index = -1,
            WeaponSlot = slot
        };

        public static UISlotId DropArea() => throw new System.NotSupportedException(
            "DropArea slots are removed. WorldDrop is triggered when EndDrag lands outside any registered slot.");

        public static UISlotId Trash() => throw new System.NotSupportedException(
            "Trash slots are removed. Drop to world via drag-outside instead.");

        public static UISlotId Attachment(string parentInstanceID, int slotIndex) => new UISlotId
        {
            Type = UISlotType.Attachment,
            Index = slotIndex,
            ParentInstanceID = parentInstanceID
        };

        public bool Equals(UISlotId other)
        {
            return Type == other.Type &&
                   Index == other.Index &&
                   EquipmentSlot == other.EquipmentSlot &&
                   WeaponSlot == other.WeaponSlot &&
                   ParentInstanceID == other.ParentInstanceID;
        }

        public override bool Equals(object obj) => obj is UISlotId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Type;
                hash = (hash * 397) ^ Index;
                hash = (hash * 397) ^ (EquipmentSlot?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (WeaponSlot?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (ParentInstanceID?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Type)
            {
                case UISlotType.Inventory:  return $"Inventory[{Index}]";
                case UISlotType.Equipment:  return $"Equip[{EquipmentSlot}]";
                case UISlotType.Weapon:     return $"Weapon[{WeaponSlot}]";
                case UISlotType.Attachment: return $"Attachment[{ParentInstanceID}][{Index}]";
                default: return Type.ToString();
            }
        }
    }

    /// <summary>
    /// Visual state for a slot – this is what ItemSlotView renders.
    /// It mirrors runtime item data but is safe to mutate on client.
    /// </summary>
    [Serializable]
    public class UISlotState
    {
        public ItemInstance Item;
        public bool IsLocked;
        public bool IsHighlight;
        public bool IsValidDropTarget;

        // Visual metadata (usually derived from ItemDefinition)
        public UnityEngine.Sprite Icon;
        public UnityEngine.Color BackgroundColor;
        public int StackCount;
    }

    public enum DropActionType
    {
        None,
        Move,        // Inventory → Inventory (empty slot)
        Swap,        // Inventory ↔ Inventory, Equipment ↔ Equipment, Weapon ↔ Weapon, Attachment ↔ Attachment
        Stack,       // Inventory → Inventory (same stackable item)
        Equip,       // Inventory → Equipment slot
        EquipWeapon, // Inventory → Weapon slot
        Unequip,     // Equipment → Inventory
        UnequipWeapon, // Weapon → Inventory
        DropToWorld, // Any slot → outside panel (no target slot)
        Attach,      // Inventory → Attachment sub-slot
        Detach,      // Attachment sub-slot → Inventory
    }

    /// <summary>
    /// High-level intent for a drop operation decided by DragDropValidator.
    /// </summary>
    public struct DropAction
    {
        public DropActionType Type;
        public UISlotId Source;
        public UISlotId Target;
    }
}

