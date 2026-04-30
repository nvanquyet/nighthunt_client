using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Determines whether a drag-drop operation is valid and maps it to a
    /// concrete <see cref="DropAction"/> for <see cref="DragDropController"/>.
    ///
    /// WORLD-DROP:
    ///   There is no DropArea or Trash slot. When <see cref="DragDropController.EndDrag"/>
    ///   receives no registered target slot the controller itself resolves the action as
    ///   <see cref="DropActionType.DropToWorld"/> — this validator is not called in that path.
    ///
    /// ATTACHMENT SLOTS:
    ///   Attachment sub-slots are <see cref="ItemSlotView"/> children rendered inline inside
    ///   the parent weapon / equipment card. They are registered with DragDropController like
    ///   any other slot. This validator routes Inventory → Attachment as Attach, and
    ///   Attachment → Inventory as Detach.
    /// </summary>
    public class DragDropValidator
    {
        public bool CanDrop(
            UISlotId source,
            UISlotId target,
            UISlotState sourceState,
            UISlotState targetState,
            out DropAction action)
        {
            action = new DropAction { Source = source, Target = target, Type = DropActionType.None };

            if (sourceState == null || sourceState.Item == null)
                return false;

            // Drop onto itself — ignore.
            if (source.Equals(target))
                return false;

            switch (source.Type)
            {
                case UISlotType.Inventory:   return ValidateFromInventory(source, target, sourceState, targetState, ref action);
                case UISlotType.Equipment:   return ValidateFromEquipment(source, target, sourceState, targetState, ref action);
                case UISlotType.Weapon:      return ValidateFromWeapon(source, target, sourceState, targetState, ref action);
                case UISlotType.Attachment:  return ValidateFromAttachment(source, target, sourceState, targetState, ref action);
                case UISlotType.Loot:        return ValidateFromLoot(source, target, sourceState, targetState, ref action);
                default: return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private bool ValidateFromInventory(
            UISlotId source, UISlotId target,
            UISlotState sourceState, UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    if (targetState?.Item != null)
                    {
                        var srcDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                        var tgtDef = ItemDatabase.GetDefinition(targetState.Item.DefinitionID);

                        if (srcDef != null && tgtDef != null &&
                            srcDef.ItemID == tgtDef.ItemID &&
                            srcDef.IsStackable &&
                            targetState.Item.Quantity < srcDef.MaxStackSize)
                        {
                            action.Type = DropActionType.Stack;
                        }
                        else
                        {
                            action.Type = DropActionType.Swap;
                        }
                    }
                    else
                    {
                        action.Type = DropActionType.Move;
                    }
                    return true;

                case UISlotType.Equipment:
                    if (!CanEquipItemInEquipmentSlot(sourceState.Item, target.EquipmentSlot))
                        return false;

                    if (targetState?.Item != null &&
                        !CanEquipItemInEquipmentSlot(targetState.Item, GetEquipmentSlotFromDefinition(sourceState.Item)))
                        return false;

                    action.Type = targetState?.Item != null
                        ? DropActionType.Swap
                        : DropActionType.Equip;
                    return true;

                case UISlotType.Weapon:
                    var weaponDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                    if (weaponDef != null && weaponDef.Type == ItemType.Weapon
                        && target.WeaponSlot.HasValue)
                    {
                        // Validate WeaponClass is allowed in this slot (e.g. no rifle in Melee slot).
                        // CanEquipInSlot falls back gracefully when no config is present.
                        var weaponSystem = DragDropController.Instance?.WeaponSystem;
                        if (weaponSystem != null &&
                            !weaponSystem.CanEquipInSlot(sourceState.Item.DefinitionID, target.WeaponSlot.Value))
                            return false;

                        action.Type = DropActionType.EquipWeapon;
                        return true;
                    }
                    return false;

                case UISlotType.Attachment:
                    // Validate source is an attachment item — slot compatibility checked server-side.
                    var attDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                    if (attDef != null &&
                        attDef.Type == ItemType.Attachment &&
                        CanAttachToTargetSlot(attDef, target))
                    {
                        action.Type = DropActionType.Attach;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private bool ValidateFromEquipment(
            UISlotId source, UISlotId target,
            UISlotState sourceState, UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.Unequip;
                    return true;
                case UISlotType.Equipment:
                    if (!CanEquipItemInEquipmentSlot(sourceState.Item, target.EquipmentSlot))
                        return false;

                    if (targetState?.Item != null &&
                        !CanEquipItemInEquipmentSlot(targetState.Item, source.EquipmentSlot))
                        return false;

                    action.Type = DropActionType.Swap;
                    return true;
                default:
                    return false;
            }
        }

        private bool ValidateFromWeapon(
            UISlotId source, UISlotId target,
            UISlotState sourceState, UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.UnequipWeapon;
                    return true;
                case UISlotType.Weapon:
                    if (source.WeaponSlot.HasValue && target.WeaponSlot.HasValue)
                    {
                        action.Type = DropActionType.Swap;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private bool ValidateFromAttachment(
            UISlotId source, UISlotId target,
            UISlotState sourceState, UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.Detach;
                    return true;

                case UISlotType.Attachment:
                    // Swap between two attachment slots OF THE SAME parent item.
                    // Cross-item attachment swaps are not supported — route through Inventory.
                    if (source.ParentInstanceID == target.ParentInstanceID &&
                        CanAttachToTargetSlot(sourceState.Item, target) &&
                        (targetState?.Item == null || CanAttachToTargetSlot(targetState.Item, source)))
                    {
                        action.Type = DropActionType.Swap;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private bool ValidateFromLoot(
            UISlotId source, UISlotId target,
            UISlotState sourceState, UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.LootToInventory;
                    return true;
                default:
                    return false;
            }
        }

        private static bool CanEquipItemInEquipmentSlot(ItemInstance item, EquipmentSlotType? slot)
        {
            if (item == null || !slot.HasValue)
                return false;

            return ItemDatabase.GetDefinition(item.DefinitionID) is EquipmentDefinition equipmentDef &&
                   equipmentDef.EquipmentSlot == slot.Value;
        }

        private static EquipmentSlotType? GetEquipmentSlotFromDefinition(ItemInstance item)
        {
            return ItemDatabase.GetDefinition(item?.DefinitionID) is EquipmentDefinition equipmentDef
                ? equipmentDef.EquipmentSlot
                : null;
        }

        private static bool CanAttachToTargetSlot(ItemDefinition attachmentDef, UISlotId target)
        {
            if (attachmentDef == null || target.Type != UISlotType.Attachment)
                return false;

            var parent = ItemDatabase.GetInstance(target.ParentInstanceID);
            var parentDef = parent != null ? ItemDatabase.GetDefinition(parent.DefinitionID) : null;
            var slots = parentDef?.AttachmentSlots;
            if (slots == null || target.Index < 0 || target.Index >= slots.Length)
                return false;

            return attachmentDef.CanAttachToSlot(slots[target.Index]);
        }

        private static bool CanAttachToTargetSlot(ItemInstance attachment, UISlotId target)
        {
            return attachment != null &&
                   CanAttachToTargetSlot(ItemDatabase.GetDefinition(attachment.DefinitionID), target);
        }
    }
}
