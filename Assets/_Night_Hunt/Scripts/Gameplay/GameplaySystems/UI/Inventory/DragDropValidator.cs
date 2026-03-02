using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Quyết định xem một drop operation có hợp lệ không
    /// và map sang DropAction cụ thể cho DragDropController.
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
            action = new DropAction
            {
                Source = source,
                Target = target,
                Type = DropActionType.None
            };

            if (sourceState == null || sourceState.Item == null)
                return false;

            // Drop lên chính nó thì bỏ qua.
            if (source.Equals(target))
                return false;

            switch (source.Type)
            {
                case UISlotType.Inventory:
                    return ValidateFromInventory(source, target, sourceState, targetState, ref action);
                case UISlotType.Equipment:
                    return ValidateFromEquipment(source, target, sourceState, targetState, ref action);
                case UISlotType.Weapon:
                    return ValidateFromWeapon(source, target, sourceState, targetState, ref action);
                case UISlotType.QuickSlot:
                    return ValidateFromQuickSlot(source, target, sourceState, targetState, ref action);
                case UISlotType.Attachment:
                    return ValidateFromAttachment(source, target, sourceState, targetState, ref action);
                default:
                    return false;
            }
        }

        private bool ValidateFromInventory(
            UISlotId source,
            UISlotId target,
            UISlotState sourceState,
            UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    if (targetState != null && targetState.Item != null)
                    {
                        // Check nếu có thể stack
                        var sourceDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                        var targetDef = ItemDatabase.GetDefinition(targetState.Item.DefinitionID);
                        
                        if (sourceDef != null && targetDef != null &&
                            sourceDef.ItemID == targetDef.ItemID &&
                            sourceDef.IsStackable &&
                            targetState.Item.Quantity < sourceDef.MaxStackSize)
                        {
                            // Có thể merge/stack
                            action.Type = DropActionType.Stack;
                            return true;
                        }
                        
                        // Không thể stack → swap
                        action.Type = DropActionType.Swap;
                    }
                    else
                    {
                        action.Type = DropActionType.Move;
                    }
                    return true;

                case UISlotType.Equipment:
                    action.Type = DropActionType.Equip;
                    return true;

                case UISlotType.QuickSlot:
                    action.Type = DropActionType.AssignQuickSlot;
                    return true;

                case UISlotType.DropArea:
                    action.Type = DropActionType.DropToWorld;
                    return true;

                case UISlotType.Attachment:
                    // Check xem item có phải attachment type không
                    var itemDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                    if (itemDef != null && itemDef.Type == ItemType.Attachment)
                    {
                        // Validate attachment compatibility với slot type
                        // Slot-type compatibility validation can be tightened here when slot schema is finalised.
                        action.Type = DropActionType.Attach;
                        return true;
                    }
                    return false;

                case UISlotType.Weapon:
                    // Check xem item có phải weapon type không
                    var weaponDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                    if (weaponDef != null && weaponDef.Type == ItemType.Weapon)
                    {
                        action.Type = DropActionType.EquipWeapon;
                        return true;
                    }
                    return false;

                case UISlotType.Trash:
                    action.Type = DropActionType.Trash;
                    return true;

                default:
                    return false;
            }
        }

        private bool ValidateFromEquipment(
            UISlotId source,
            UISlotId target,
            UISlotState sourceState,
            UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.Unequip;
                    return true;
                case UISlotType.Equipment:
                    action.Type = DropActionType.Swap;
                    return true;
                // BUG 4 FIX: allow drop from equipment directly to world via DropArea
                case UISlotType.DropArea:
                    action.Type = DropActionType.DropToWorld;
                    return true;
                case UISlotType.Trash:
                    action.Type = DropActionType.Trash;
                    return true;
                default:
                    return false;
            }
        }

        private bool ValidateFromQuickSlot(
            UISlotId source,
            UISlotId target,
            UISlotState sourceState,
            UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.RemoveQuickSlot;
                    return true;
                case UISlotType.QuickSlot:
                    action.Type = DropActionType.Swap;
                    return true;
                // BUG 4 FIX: drop item via QuickSlot shortcut directly to world
                case UISlotType.DropArea:
                    action.Type = DropActionType.DropToWorld;
                    return true;
                case UISlotType.Trash:
                    action.Type = DropActionType.Trash;
                    return true;
                default:
                    return false;
            }
        }

        private bool ValidateFromAttachment(
            UISlotId source,
            UISlotId target,
            UISlotState sourceState,
            UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    // Detach attachment back to inventory
                    action.Type = DropActionType.Detach;
                    return true;

                case UISlotType.Attachment:
                    // Swap attachments between two slots of the SAME parent item
                    if (source.ParentInstanceID == target.ParentInstanceID)
                    {
                        action.Type = DropActionType.Swap;
                        return true;
                    }
                    return false;

                // BUG 4 FIX: detach attachment then drop to world
                case UISlotType.DropArea:
                    action.Type = DropActionType.DropToWorld;
                    return true;

                case UISlotType.Trash:
                    action.Type = DropActionType.Trash;
                    return true;

                default:
                    return false;
            }
        }

        private bool ValidateFromWeapon(
            UISlotId source,
            UISlotId target,
            UISlotState sourceState,
            UISlotState targetState,
            ref DropAction action)
        {
            switch (target.Type)
            {
                case UISlotType.Inventory:
                    action.Type = DropActionType.UnequipWeapon;
                    return true;

                case UISlotType.Weapon:
                    // Swap weapons between two slots (Primary ↔ Secondary, etc.)
                    if (source.WeaponSlot.HasValue && target.WeaponSlot.HasValue)
                    {
                        action.Type = DropActionType.Swap;
                        return true;
                    }
                    return false;

                // BUG 4 FIX: drop weapon directly to world
                case UISlotType.DropArea:
                    action.Type = DropActionType.DropToWorld;
                    return true;

                case UISlotType.Trash:
                    action.Type = DropActionType.Trash;
                    return true;

                default:
                    return false;
            }
        }
    }
}

