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
                        // TODO: Có thể thêm validation chi tiết hơn về slot type compatibility
                        action.Type = DropActionType.Attach;
                        return true;
                    }
                    return false;

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
                    // Detach attachment về inventory
                    action.Type = DropActionType.Detach;
                    return true;
                    
                case UISlotType.Attachment:
                    // Swap attachments giữa 2 items
                    // Chỉ cho phép nếu cùng parent item
                    if (source.ParentInstanceID == target.ParentInstanceID)
                    {
                        action.Type = DropActionType.Swap;
                        return true;
                    }
                    return false;
                    
                default:
                    return false;
            }
        }
    }
}

