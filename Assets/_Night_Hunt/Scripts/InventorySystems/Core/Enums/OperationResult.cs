namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Result codes for inventory operations.
    /// Used for validation feedback and UI messaging.
    /// </summary>
    public enum OperationResult
    {
        Success,
        
        // Inventory errors
        InventoryFull,
        SlotOccupied,
        InvalidSlotIndex,
        ItemNotFound,
        
        // Weight errors
        ExceedsWeightLimit,
        
        // Validation errors
        InvalidItemType,
        InvalidSlotType,
        IncompatibleSlot,
        
        // Equipment errors
        AlreadyEquipped,
        NotEquipped,
        RequirementsNotMet,
        
        // Attachment errors
        AttachmentSlotNotAvailable,
        AttachmentSlotOccupied,
        IncompatibleAttachment,
        
        // Stack errors
        NotStackable,
        StackLimitExceeded,
        
        // Network errors
        NotAuthorized,
        ServerValidationFailed,
        
        // General
        UnknownError
    }
}